using System;
using System.Collections.Generic;
using Jazztures.Core.Evaluation;
using Jazztures.Core.Gesture;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Events;
using UnityEngine;

namespace Jazztures.Lessons
{
    /// <summary>
    /// Drives one lesson through its mode phases (CLAUDE.md §3.8/§3.9). It owns the
    /// <see cref="LessonStateMachine"/> and the <see cref="LessonCuePlayer"/>, and per
    /// frame it:
    /// <list type="bullet">
    ///   <item>re-points the <see cref="ModeGatedNoteSink"/> when the phase changes;</item>
    ///   <item>plays the system demonstration from the timeline (Watch and Listen);</item>
    ///   <item>advances the cue player on the phrase clock and publishes ghost-hand frames;</item>
    ///   <item>tells the gate whether the learner's gesture is correct (Try Yourself);</item>
    ///   <item>captures melody onsets and scores the attempt at phase end (Test Yourself, §3.7).</item>
    /// </list>
    ///
    /// <para>
    /// The domain (clock, gate, interpreter) is created by the composition root and handed
    /// in through <see cref="Bind"/>. Captions and target highlights are logged as a
    /// placeholder until the HUD milestone; the ghost renderer is M6 (ADR-0012).
    /// </para>
    /// </summary>
    public sealed class LessonRunner : MonoBehaviour
    {
        [SerializeField] private LessonDefinition _lesson;

        [Header("Channels (optional)")]
        [SerializeField] private LessonPhaseChannel _phaseChannel;
        [SerializeField] private EvaluationResultChannel _evaluationChannel;
        [SerializeField] private GhostFrameChannel _ghostChannel;

        [Tooltip("Presentational cues (captions, highlights, tension colour) are raised here " +
                 "for the HUD. Control-flow cues are handled internally and never published.")]
        [SerializeField] private CueActionChannel _cueChannel;

        [Tooltip("Melody note-ons are read from here to score Test-Yourself attempts. " +
                 "Wire the same channel the composition root uses.")]
        [SerializeField] private NoteTriggeredChannel _noteChannel;

        [Header("Behaviour")]
        [Tooltip("Advance to the next phase automatically once the phrase (plus tail) has elapsed.")]
        [SerializeField] private bool _autoAdvance = true;

        [Min(0f)]
        [SerializeField] private float _phraseTailSeconds = 1.0f;

        [Tooltip("Gesture-gated modes (Gesture Learning): after the ghost has had this many " +
                 "beats to form a pose, the lesson holds there until the learner confirms it. " +
                 "Match this to GhostHandView's morph beats so the ghost settles before the wait.")]
        [Min(0f)]
        [SerializeField] private float _gateSettleBeats = 1.5f;

        [Tooltip("Log captions and highlight cues to the Console (HUD not built yet).")]
        [SerializeField] private bool _logCues = true;

        [SerializeField] private bool _startOnBind = true;

        private IMusicalClock _clock;
        private ModeGatedNoteSink _gate;
        private GestureInterpreter _interpreter;

        private LessonPlan _plan;
        private LessonTimeline _timeline;
        private LessonStateMachine _stateMachine;
        private LessonCuePlayer _cuePlayer;
        private TimelinePlayback _playback;
        private Metronome _metronome;

        private readonly List<double> _capturedOnsets = new List<double>();
        private double _phaseStartDsp;
        private double _lessonBeat;    // phrase position — gated (held) on each pose in Gesture Learning
        private double _lastRealBeat;  // clock-derived beat last frame, to measure real elapsed
        private bool _gateHeld;        // the gesture gate is holding the cursor right now
        private bool _capturing;
        private bool _waitingForInput;
        private bool _noteChannelRegistered;
        private bool _running;
        private bool _advancing;

        public bool IsRunning => _running;

        public LessonStatus Status => _stateMachine != null ? _stateMachine.Status : LessonStatus.NotStarted;

        /// <summary>Raised once when the lesson finishes its last phase (e.g. to re-show the selector).</summary>
        public event Action Completed;

        /// <summary>The lesson currently loaded, or null before <see cref="Bind"/> / <see cref="LoadLesson"/>.</summary>
        public LessonDefinition CurrentLesson => _lesson;

        /// <summary>Wire the domain in. Call once from the composition root's <c>Awake</c>.</summary>
        public void Bind(IMusicalClock clock, ModeGatedNoteSink gate, GestureInterpreter interpreter)
        {
            _clock = clock;
            _gate = gate;
            _interpreter = interpreter;

            if (_interpreter != null)
            {
                _interpreter.ConfirmedFunctionChanged += OnConfirmedFunctionChanged;
            }

            if (_startOnBind)
            {
                StartLesson();
            }
        }

        /// <summary>Bake the asset and begin on phase 0.</summary>
        public void StartLesson()
        {
            if (_lesson == null)
            {
                Debug.LogError($"{nameof(LessonRunner)}: no {nameof(LessonDefinition)} assigned.", this);
                return;
            }

            if (_clock == null || _gate == null)
            {
                Debug.LogError($"{nameof(LessonRunner)}: Bind() must run before StartLesson().", this);
                return;
            }

            _plan = _lesson.BuildPlan();
            _timeline = _lesson.BuildTimeline();
            _cuePlayer = new LessonCuePlayer(_lesson.BuildScript(), _timeline);
            _cuePlayer.ActionFired += OnCueAction;

            _stateMachine = new LessonStateMachine(_plan);
            _stateMachine.PhaseChanged += OnPhaseChanged;
            _stateMachine.Completed += OnLessonCompleted;

            if (_lesson.UseMetronome)
            {
                _metronome = new Metronome(_plan.Tempo, _timeline.BeatsPerBar);
            }

            if (_noteChannel != null && !_noteChannelRegistered)
            {
                _noteChannel.Register(OnNoteTriggered);
                _noteChannelRegistered = true;
            }

            _running = true;
            _stateMachine.Begin();
        }

        /// <summary>
        /// Swap to a different lesson and start it. Safe to call at runtime — a selector
        /// uses this. <see cref="StartLesson"/> rebuilds the plan / timeline / cue-player /
        /// state-machine from scratch, so no handlers leak; <see cref="StopLesson"/> first
        /// kills anything the previous lesson still has sounding.
        /// </summary>
        public void LoadLesson(LessonDefinition lesson)
        {
            if (lesson == null)
            {
                Debug.LogError($"{nameof(LessonRunner)}: {nameof(LoadLesson)}(null).", this);
                return;
            }

            StopLesson();
            _lesson = lesson;
            StartLesson();
        }

        /// <summary>Move to the next phase now (e.g. from a HUD button).</summary>
        public void AdvancePhase()
        {
            if (_advancing || !_running || _stateMachine == null || _stateMachine.Status != LessonStatus.InPhase)
            {
                return;
            }

            _advancing = true;
            try
            {
                EndCurrentPhase();
                _stateMachine.AdvancePhase();
            }
            finally
            {
                _advancing = false;
            }
        }

        /// <summary>Stop the lesson and release anything still sounding.</summary>
        public void StopLesson()
        {
            if (_playback != null)
            {
                _playback.Stop();
                _playback = null;
            }

            _metronome?.Stop();
            _running = false;
        }

        private void OnPhaseChanged(LessonPhase phase)
        {
            _phaseStartDsp = _clock.Now;
            _lessonBeat = 0.0;
            _lastRealBeat = 0.0;
            _waitingForInput = false;
            _capturedOnsets.Clear();
            _cuePlayer.Reset();

            _gate.SetMode(phase.Mode);
            _gate.SetGestureCorrect(false);

            ModePolicy policy = phase.Policy;
            _capturing = policy.DeferFeedback; // Test Yourself captures for end-of-attempt scoring

            if (_phaseChannel != null)
            {
                _phaseChannel.Raise(new LessonPhaseInfo(_plan.Id, phase));
            }

            // §3.9 co-design ask: the plain-language concept, shown as the lesson opens.
            // The selector is titles only; this is where the theory lands.
            if (phase.Index == 0 && _cueChannel != null && !string.IsNullOrWhiteSpace(_plan.ConceptExplanation))
            {
                _cueChannel.Raise(CueAction.ShowText(_plan.ConceptExplanation));
            }

            _playback = null;
            if (policy.SystemPlayback == SystemPlayback.Full)
            {
                _playback = new TimelinePlayback(_timeline, _clock, _gate);
                _playback.Start();
            }
            else if (policy.SystemPlayback == SystemPlayback.BackingOnly && _logCues)
            {
                // TODO(OPEN): backing track for Compose-on-the-Fly (CLAUDE.md §7).
                Debug.Log($"[{name}] {phase.Mode}: backing track not available yet — running dry.", this);
            }

            _metronome?.Start(_phaseStartDsp);

            _cuePlayer.Notify(LearnerAction.PhraseStarted);
        }

        private void OnLessonCompleted()
        {
            StopLesson();
            if (_logCues)
            {
                Debug.Log($"[{name}] lesson '{_plan.Id}' complete.", this);
            }

            Completed?.Invoke();
        }

        private void Update()
        {
            if (!_running || _stateMachine == null || _stateMachine.Status != LessonStatus.InPhase)
            {
                return;
            }

            LessonPhase phase = _stateMachine.CurrentPhase.Value;

            // The phrase position. Normally it tracks real time; in a gesture-gated mode it
            // holds on each demonstrated pose until the learner confirms that pose (§3.8).
            double realBeat = _plan.Tempo.SecondsToBeats(_clock.Now - _phaseStartDsp);
            double advanced = realBeat - _lastRealBeat;
            _lastRealBeat = realBeat;
            _lessonBeat = NextLessonBeat(phase, _lessonBeat + (advanced > 0.0 ? advanced : 0.0));

            _playback?.Tick();
            DrainMetronome();

            _cuePlayer.AdvanceTo(_lessonBeat < 0.0 ? 0.0 : _lessonBeat);
            PublishGhostFrame(phase, _lessonBeat);
            UpdateGestureCorrectness(phase, _lessonBeat);

            if (_autoAdvance && !_waitingForInput && PhraseIsFinished(phase))
            {
                AdvancePhase();
            }
        }

        /// <summary>
        /// Where the phrase clock is allowed to be this frame. In a gesture-gated mode
        /// (<see cref="ModePolicy.GateOnGesture"/>) it stops <see cref="_gateSettleBeats"/>
        /// after each chord — enough for the ghost to form the pose — and does not move on
        /// until <see cref="GestureInterpreter.ConfirmedFunction"/> matches that pose.
        /// </summary>
        private double NextLessonBeat(LessonPhase phase, double wanted)
        {
            _gateHeld = false;

            if (!phase.Policy.GateOnGesture || _interpreter == null || _timeline.Chords.Count == 0)
            {
                return wanted;
            }

            double chordBeat = 0.0;
            ChordFunction? pose = null;
            for (int i = 0; i < _timeline.Chords.Count; i++)
            {
                if (_timeline.Chords[i].Beat.Position <= wanted)
                {
                    chordBeat = _timeline.Chords[i].Beat.Position;
                    pose = _timeline.Chords[i].Function;
                }
            }

            if (pose == null || LearnerFunction(phase) == pose)
            {
                return wanted;
            }

            double holdAt = chordBeat + _gateSettleBeats;
            if (wanted <= holdAt)
            {
                return wanted; // still letting the ghost form the pose
            }

            _gateHeld = true;
            return holdAt;
        }

        private void UpdateGestureCorrectness(LessonPhase phase, double beatNow)
        {
            if (phase.Policy.UserAudio != UserAudioGate.OnlyWhenGestureCorrect)
            {
                return;
            }

            ChordFunction? expected = _timeline.ChordFunctionAt(beatNow);
            bool correct = expected.HasValue && LearnerFunction(phase) == expected;
            _gate.SetGestureCorrect(correct);
        }

        /// <summary>
        /// The learner's current pose as this phase judges it. A gesture-gated phase
        /// (Gesture Learning) uses <see cref="GestureInterpreter.ReachingFunction"/> — it
        /// accepts the pose the instant the learner clearly makes it, so the gate feels as
        /// loose as free-play chord triggering. The timed modes use the fully-confirmed
        /// function.
        /// </summary>
        private ChordFunction? LearnerFunction(LessonPhase phase)
        {
            if (_interpreter == null)
            {
                return null;
            }

            return phase.Policy.GateOnGesture
                ? _interpreter.ReachingFunction
                : _interpreter.ConfirmedFunction;
        }

        private void PublishGhostFrame(LessonPhase phase, double beatNow)
        {
            if (_ghostChannel == null)
            {
                return;
            }

            if (!phase.Policy.GhostHandsVisible)
            {
                _ghostChannel.Raise(GhostFrame.Hidden);
                return;
            }

            ChordFunction? pose = _timeline.ChordFunctionAt(beatNow);
            double poseChangedAt = 0.0;
            for (int i = 0; i < _timeline.Chords.Count; i++)
            {
                if (_timeline.Chords[i].Beat.Position <= beatNow)
                {
                    poseChangedAt = _timeline.Chords[i].Beat.Position;
                }
            }

            _ghostChannel.Raise(new GhostFrame(beatNow, pose, poseChangedAt, LitTargetAt(beatNow)));
        }

        private int LitTargetAt(double beatNow)
        {
            const double litForBeats = 0.35;
            for (int i = 0; i < _timeline.Notes.Count; i++)
            {
                TimelineNote note = _timeline.Notes[i];
                double onset = SwingQuantizer.Swing(note.Beat.Position, _timeline.Swing);
                if (beatNow >= onset && beatNow < onset + litForBeats)
                {
                    return note.TargetIndex;
                }
            }

            return -1;
        }

        private bool PhraseIsFinished(LessonPhase phase)
        {
            if (phase.Policy.SystemPlayback == SystemPlayback.Full)
            {
                return _playback == null || _playback.HasEnded;
            }

            // _lessonBeat is gated, so in a gesture-gated mode this is only true once every
            // pose (including the last) has been matched and the cursor has run out the tail.
            if (_gateHeld)
            {
                return false;
            }

            double tailBeats = _plan.Tempo.SecondsToBeats(_phraseTailSeconds);
            return _lessonBeat >= _timeline.DurationBeats + tailBeats;
        }

        private void EndCurrentPhase()
        {
            LessonPhase phase = _stateMachine.CurrentPhase.Value;

            _playback?.Stop();
            _playback = null;
            _metronome?.Stop();

            if (phase.Policy.DeferFeedback)
            {
                ScoreAttempt();
            }

            _cuePlayer.Notify(LearnerAction.AttemptCompleted);
        }

        private void ScoreAttempt()
        {
            AttemptResult result = OnsetScorer.Evaluate(
                _timeline.ExpectedOnsetSeconds(), _capturedOnsets, OnsetWindows.Default);

            if (_evaluationChannel != null)
            {
                _evaluationChannel.Raise(result);
            }

            if (_logCues)
            {
                Debug.Log($"[{name}] {result}", this);
            }
        }

        private void DrainMetronome()
        {
            if (_metronome == null)
            {
                return;
            }

            // M5: dequeue so the cursor keeps up; audio (MetronomeVoice) is not built yet.
            while (_metronome.TryDequeueClick(_clock.Now, out _))
            {
            }
        }

        private void OnConfirmedFunctionChanged(ChordFunction? function)
        {
            if (!_running || _cuePlayer == null)
            {
                return;
            }

            _waitingForInput = false;
            _cuePlayer.Notify(LearnerAction.ChordConfirmed, function);
        }

        private void OnNoteTriggered(NoteEvent note)
        {
            if (!_running || note.Kind != NoteEventKind.On || note.Source != Handedness.Right)
            {
                return;
            }

            // While the system is demonstrating, right-hand notes are the demo, not the learner.
            if (_playback != null)
            {
                return;
            }

            if (note.Channel == MidiChannel.Melody)
            {
                _waitingForInput = false;
                if (_capturing)
                {
                    _capturedOnsets.Add(note.DspTime - _phaseStartDsp);
                }

                _cuePlayer.Notify(LearnerAction.MelodyNotePlayed);
            }
        }

        private void OnCueAction(CueAction action)
        {
            switch (action.Kind)
            {
                case CueActionKind.WaitForInput:
                    _waitingForInput = true;
                    break;

                case CueActionKind.SetScoring:
                    _capturing = action.Flag;
                    break;

                case CueActionKind.AdvancePhase:
                    AdvancePhase();
                    break;

                default:
                    // Presentational: caption, highlight, tension colour. The HUD owns how
                    // these look; this only says when they fire (§2.3).
                    _cueChannel?.Raise(action);

                    if (_logCues)
                    {
                        Debug.Log($"[{name}] cue: {action}", this);
                    }

                    break;
            }
        }

        private void OnDestroy()
        {
            if (_noteChannel != null && _noteChannelRegistered)
            {
                _noteChannel.Unregister(OnNoteTriggered);
                _noteChannelRegistered = false;
            }

            if (_interpreter != null)
            {
                _interpreter.ConfirmedFunctionChanged -= OnConfirmedFunctionChanged;
            }
        }
    }
}
