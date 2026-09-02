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

        [Tooltip("Melody note-ons are read from here to score Test-Yourself attempts. " +
                 "Wire the same channel the composition root uses.")]
        [SerializeField] private NoteTriggeredChannel _noteChannel;

        [Header("Behaviour")]
        [Tooltip("Advance to the next phase automatically once the phrase (plus tail) has elapsed.")]
        [SerializeField] private bool _autoAdvance = true;

        [Min(0f)]
        [SerializeField] private float _phraseTailSeconds = 1.0f;

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
        private bool _capturing;
        private bool _waitingForInput;
        private bool _noteChannelRegistered;
        private bool _running;
        private bool _advancing;

        public bool IsRunning => _running;

        public LessonStatus Status => _stateMachine != null ? _stateMachine.Status : LessonStatus.NotStarted;

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

            _running = false;
        }

        private void OnPhaseChanged(LessonPhase phase)
        {
            _phaseStartDsp = _clock.Now;
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
        }

        private void Update()
        {
            if (!_running || _stateMachine == null || _stateMachine.Status != LessonStatus.InPhase)
            {
                return;
            }

            LessonPhase phase = _stateMachine.CurrentPhase.Value;
            double elapsed = _clock.Now - _phaseStartDsp;
            double beatNow = _plan.Tempo.SecondsToBeats(elapsed);

            _playback?.Tick();
            DrainMetronome();

            _cuePlayer.AdvanceTo(beatNow < 0.0 ? 0.0 : beatNow);
            PublishGhostFrame(phase, beatNow);
            UpdateGestureCorrectness(phase, beatNow);

            if (_autoAdvance && !_waitingForInput && PhraseIsFinished(phase, elapsed))
            {
                AdvancePhase();
            }
        }

        private void UpdateGestureCorrectness(LessonPhase phase, double beatNow)
        {
            if (phase.Policy.UserAudio != UserAudioGate.OnlyWhenGestureCorrect)
            {
                return;
            }

            ChordFunction? expected = _timeline.ChordFunctionAt(beatNow);
            bool correct = expected.HasValue
                           && _interpreter != null
                           && _interpreter.ConfirmedFunction == expected;
            _gate.SetGestureCorrect(correct);
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

        private bool PhraseIsFinished(LessonPhase phase, double elapsed)
        {
            if (phase.Policy.SystemPlayback == SystemPlayback.Full)
            {
                return _playback == null || _playback.HasEnded;
            }

            double phraseSeconds = _plan.Tempo.BeatsToSeconds(_timeline.DurationBeats);
            return elapsed >= phraseSeconds + _phraseTailSeconds;
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
