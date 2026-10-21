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

        [Tooltip("Gesture-gated modes (Try Yourself, left-hand lesson): after the ghost has had " +
                 "this many beats to form a pose, the lesson holds there until the learner confirms " +
                 "it. Match this to GhostHandView's morph beats so the ghost settles before the wait.")]
        [Min(0f)]
        [SerializeField] private float _gateSettleBeats = 1.5f;

        [Header("Lead-in (ADR-0056)")]
        [Tooltip("Beats of silence, at the lesson's tempo, before each phrase starts. Nothing sounds in them - no " +
                 "chord, no melody, no metronome click - so the learner can get their hands up and read the " +
                 "caption; the ghost waits in its first pose. 0 starts at once.")]
        [Min(0f)]
        [SerializeField] private float _leadInBeats = 4f;

        [Header("Ghost strike (ADR-0052)")]
        [Tooltip("Beats after a chord's pose appears before the ghost hand strikes the pad for it - time for " +
                 "the ghost to form the shape first. Keep it at least GhostHandView's morph beats plus a " +
                 "little; the default is the 1.5-beat morph plus half a beat of grace. Lessons that score a " +
                 "pulse (L2) strike on the pulse instead and ignore this, except where the system sounds the chords " +
                 "(Watch and Listen; a right-hand lesson's Try Yourself), where the ghost always strikes the chords: " +
                 "there this is rounded up to whole beats and is also how far the system's playback trails the " +
                 "ghost, so the chord sounds as the hand lands.")]
        [Min(0f)]
        [SerializeField] private float _ghostStrikeAfterPoseBeats = 2.0f;

        [Tooltip("Beats the ghost hand takes to travel onto the pad, ending in contact.")]
        [Min(0.05f)]
        [SerializeField] private float _ghostStrikeApproachBeats = 0.5f;

        [Tooltip("Beats the ghost hand takes to lift back off the pad.")]
        [Min(0.05f)]
        [SerializeField] private float _ghostStrikeReturnBeats = 0.5f;

        [Header("Ghost finger press (ADR-0055)")]
        [Tooltip("Beats the ghost right hand's finger takes to go down onto its key, ending with the key down on " +
                 "the note's onset - so the fingertip meets the key as the note sounds. Only used when the right " +
                 "hand plays by bending fingers (ADR-0050).")]
        [Min(0.05f)]
        [SerializeField] private float _ghostPressApproachBeats = 0.25f;

        [Tooltip("Beats the ghost finger takes to lift back off its key.")]
        [Min(0.05f)]
        [SerializeField] private float _ghostPressReturnBeats = 0.35f;

        [Tooltip("Beats ahead the ghost right hand's letter for the next note is marked as 'up next' (ADR-0059). " +
                 "The finger itself only starts moving the approach beats above before its note, which is too " +
                 "little time to read the cue and reach for the finger; this is the warning that comes before " +
                 "it. 1 means the next note is marked from the moment the last one lands, for notes a beat " +
                 "apart. 0 turns the mark off. [TUNABLE] - unmeasured.")]
        [Min(0f)]
        [SerializeField] private float _ghostNextLookAheadBeats = 1.0f;

        [Tooltip("Log captions and highlight cues to the Console (HUD not built yet).")]
        [SerializeField] private bool _logCues = true;

        [SerializeField] private bool _startOnBind = true;

        // ADR-0051: the menu starts a lesson one mode at a time, at a tempo the learner may have
        // stepped. Unset (the default) the lesson runs every authored mode at its authored tempo.
        private LearningMode? _onlyMode;
        private double _tempoBpm;

        private IMusicalClock _clock;
        private ModeGatedNoteSink _gate;
        private GestureInterpreter _interpreter;

        private LessonPlan _plan;
        private LessonTimeline _timeline;
        private LessonStateMachine _stateMachine;
        private LessonCuePlayer _cuePlayer;
        private TimelinePlayback _playback;
        private Metronome _metronome;
        private IMetronomeOutput _metronomeOutput;

        private ScriptedHarmony _scriptedHarmony;
        private GhostStrikeSchedule _strikes = GhostStrikeSchedule.None;
        private GhostPressSchedule _presses = GhostPressSchedule.None;
        private double _playbackLeadBeats; // where the system sounds the chords: how many beats its playback trails the ghost
        private OnsetCapture _capture;
        private bool _melodyDemonstrating; // Watch and Listen: the system plays the melody, so the learner is not scored
        private bool _scriptStarted;
        private double _phaseStartDsp;
        // The opening explanation (the "short discussion"). Until it is over the lesson itself has not begun: no cue
        // fires, no ghost hand or lane is drawn, "phrase started" is not announced (ADR-0066).
        private readonly ConceptSlidePlayer _concept = new ConceptSlidePlayer();
        private bool _phraseAnnounced; // PhraseStarted raised for this phase (once the discussion is over)
        private double _lessonBeat;    // phrase position — gated (held) on each pose in Gesture Learning
        private double _lastRealBeat;  // clock-derived beat last frame, to measure real elapsed
        private bool _gateHeld;        // the gesture gate is holding the cursor right now
        private bool _capturing;
        private bool _waitingForInput;
        private bool _noteChannelRegistered;
        private bool _running;
        private bool _advancing;

        public bool IsRunning => _running;

        /// <summary>
        /// True while a lesson is running and its opening explanation has not finished - the words are the only thing on
        /// screen (ADR-0066). What decides whether the strike plate is up yet (ADR-0067).
        /// </summary>
        public bool InDiscussion => _running && _clock != null && !_concept.IsFinished(_clock.Now);

        public LessonStatus Status => _stateMachine != null ? _stateMachine.Status : LessonStatus.NotStarted;

        /// <summary>
        /// Which hands the loaded lesson exercises (§3.9, from <c>LessonDefinition</c>).
        /// The flow controller shows the right-hand melody surfaces only when this
        /// includes <see cref="ActiveHands.Right"/>. <see cref="ActiveHands.None"/> before
        /// a lesson is loaded.
        /// </summary>
        public ActiveHands ActiveHands => _plan != null ? _plan.Hands : ActiveHands.None;

        /// <summary>Raised once when the lesson finishes its last phase (e.g. to re-show the selector).</summary>
        public event Action Completed;

        /// <summary>The lesson currently loaded, or null before <see cref="Bind"/> / <see cref="LoadLesson"/>.</summary>
        public LessonDefinition CurrentLesson => _lesson;

        /// <summary>
        /// Wire the domain in. Call once from the composition root's <c>Awake</c>.
        /// <paramref name="metronomeOutput"/> is where a lesson's metronome becomes sound
        /// (ADR-0043); without one a lesson that asks for a metronome runs it silently.
        /// <paramref name="scriptedHarmony"/> selects a right-hand lesson's chords without the
        /// left hand (ADR-0043 decision 3); without one the melody has no chord to follow.
        /// </summary>
        public void Bind(
            IMusicalClock clock,
            ModeGatedNoteSink gate,
            GestureInterpreter interpreter,
            IMetronomeOutput metronomeOutput = null,
            ScriptedHarmony scriptedHarmony = null)
        {
            _clock = clock;
            _gate = gate;
            _interpreter = interpreter;
            _metronomeOutput = metronomeOutput;
            _scriptedHarmony = scriptedHarmony;

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

            double tempoBpm = _tempoBpm > 0.0 ? _tempoBpm : _lesson.TempoBpm;
            _plan = _onlyMode.HasValue ? _lesson.BuildPlan(_onlyMode.Value, tempoBpm) : _lesson.BuildPlan();
            _timeline = _lesson.BuildTimeline(tempoBpm);
            _cuePlayer = new LessonCuePlayer(_lesson.BuildScript(), _timeline);
            _cuePlayer.ActionFired += OnCueAction;

            _stateMachine = new LessonStateMachine(_plan);
            _stateMachine.PhaseChanged += OnPhaseChanged;
            _stateMachine.Completed += OnLessonCompleted;

            // The hand that is scored: the melody hand if the lesson has one, otherwise the
            // left hand's chord strikes (L2's pulse). OnsetCapture filters by hand and by
            // channel, so system playback on the Accompaniment channel is never the learner.
            _capture = new OnsetCapture(
                (_plan.Hands & ActiveHands.Right) != 0 ? Handedness.Right : Handedness.Left);

            _metronome = null;
            if (_lesson.UseMetronome)
            {
                _metronome = new Metronome(_plan.Tempo, _timeline.BeatsPerBar, _lesson.MetronomeSubdivision, _plan.Swing);

                if (_metronomeOutput == null)
                {
                    Debug.LogWarning(
                        $"{nameof(LessonRunner)}: lesson '{_plan.Id}' uses a metronome but no " +
                        $"{nameof(IMetronomeOutput)} was bound — it will be silent.", this);
                }
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
        public void LoadLesson(LessonDefinition lesson) => Load(lesson, null, 0.0);

        /// <summary>
        /// Run just <paramref name="mode"/> of <paramref name="lesson"/> at <paramref name="tempoBpm"/>
        /// (ADR-0051). <see cref="Completed"/> fires when that one mode ends. The lesson's metronome,
        /// timeline and onset windows all follow the tempo given here.
        /// </summary>
        public void LoadLesson(LessonDefinition lesson, LearningMode mode, double tempoBpm) =>
            Load(lesson, mode, tempoBpm);

        private void Load(LessonDefinition lesson, LearningMode? mode, double tempoBpm)
        {
            if (lesson == null)
            {
                Debug.LogError($"{nameof(LessonRunner)}: {nameof(LoadLesson)}(null).", this);
                return;
            }

            StopLesson();
            _lesson = lesson;
            _onlyMode = mode;
            _tempoBpm = tempoBpm;
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

            StopScriptedHarmony();
            StopMetronome();
            _metronome = null;
            _strikes = GhostStrikeSchedule.None;
            _presses = GhostPressSchedule.None;
            _playbackLeadBeats = 0.0;
            _ghostChannel?.Raise(GhostFrame.Hidden);

            // A stopped lesson is the free instrument again: don't leave the note gate in
            // the last phase's mode (a lesson ending on Try Yourself would keep muting the
            // learner until their gesture matched a target that is no longer there).
            _gate?.SetMode(LearningMode.ComposeOnTheFly);
            _gate?.SetGestureCorrect(true);
            _gate?.SetActiveHands(ActiveHands.Both); // ...and both hands are heard again

            _running = false;
            _concept.Stop();
        }

        private void OnPhaseChanged(LessonPhase phase)
        {
            // ADR-0043: one start time for the phrase clock, the system playback and the
            // metronome. A click can only be scheduled ahead of "now", so when the lesson has
            // an audible pulse everything is anchored a pre-roll later — otherwise beat 0's
            // click would already be late. Lessons without a pulse start immediately.
            double preRoll = _metronome != null && _metronomeOutput != null
                ? _metronomeOutput.PreRollSeconds
                : 0.0;

            // ADR-0056: the phrase starts after a silent lead-in. Everything below - the system's
            // playback, the scripted harmony, the metronome, the onset capture - is anchored to this one
            // time, so moving it is all it takes for none of them to sound before it. The phrase position
            // starts negative and counts up through the lead-in to 0.
            // The opening explanation plays first, on phase 0 only; the phrase waits for it. §3.9 co-design ask: the
            // plain-language concept, one slide at a time (TickConceptSlides) - the menu is titles only, this is where
            // the theory lands.
            bool hasConcept = phase.Index == 0 && _cueChannel != null;
            _concept.Start(hasConcept ? _plan.ConceptSlides : null, _clock.Now);
            _phraseAnnounced = false;
            double slideSeconds = hasConcept ? _plan.ConceptSeconds : 0.0;
            _phaseStartDsp = _clock.Now + slideSeconds + preRoll + _plan.Tempo.BeatsToSeconds(_leadInBeats);
            _lastRealBeat = _plan.Tempo.SecondsToBeats(_clock.Now - _phaseStartDsp);
            _lessonBeat = _lastRealBeat;
            _waitingForInput = false;
            _capture.Begin(_phaseStartDsp);
            _cuePlayer.Reset();

            _gate.SetMode(phase.Mode);
            _gate.SetGestureCorrect(false);
            _gate.SetActiveHands(_plan.Hands); // ADR-0063: a hand the lesson does not use is silent, whatever the mode

            ModePolicy policy = phase.Policy;
            _capturing = policy.DeferFeedback; // Test Yourself captures for end-of-attempt scoring

            // ADR-0052: the ghost shows the strike that sounds the chord - wherever the learner strikes
            // it (they play the left hand) or the system does (it sounds the chords itself).
            //
            // Learn the shapes / Try Yourself: the pose appears on the chord's beat and the ghost strikes
            // a couple of beats later, once it has formed the shape; the learner's own strike makes the
            // sound.
            //
            // Where the system sounds the chords - Watch and Listen, and a right-hand lesson's Try Yourself,
            // where the harmony plays and the melody is the learner's - the ghost has to land on the pad at
            // that very instant, so it needs its two beats to form the shape FIRST. The ghost keeps its usual
            // timing (pose on the chord's beat, strike after it) and the system's playback is simply
            // started that many beats later, so the chord sounds as the hand lands. Whole beats, so a
            // metronome (which keeps its own start) stays on the grid.
            _playbackLeadBeats = 0.0;
            _strikes = GhostStrikeSchedule.None;
            bool systemSoundsChords = policy.SystemPlayback == SystemPlayback.Full
                                      || policy.SystemPlayback == SystemPlayback.HarmonyOnly;
            if (policy.GhostHandsVisible && ((_plan.Hands & ActiveHands.Left) != 0 || systemSoundsChords))
            {
                double afterPose = systemSoundsChords
                    ? System.Math.Ceiling(_ghostStrikeAfterPoseBeats)
                    : _ghostStrikeAfterPoseBeats;

                if (systemSoundsChords)
                {
                    _playbackLeadBeats = afterPose;
                }

                _strikes = new GhostStrikeSchedule(
                    _timeline.StrikeBeats(afterPose, useScoredPulse: !systemSoundsChords),
                    _ghostStrikeApproachBeats,
                    _ghostStrikeReturnBeats);
            }

            // ADR-0055: where the learner plays the right hand, the ghost right hand presses a finger per
            // note, landing on the note's onset. Whether the view shows it depends on the right-hand input
            // (finger bends, ADR-0050); the runner just says what the notes ask for.
            _presses = GhostPressSchedule.None;
            if (policy.GhostHandsVisible && (_plan.Hands & ActiveHands.Right) != 0)
            {
                _presses = GhostPressSchedule.FromTimeline(
                    _timeline, _ghostPressApproachBeats, _ghostPressReturnBeats);
            }

            if (_phaseChannel != null)
            {
                _phaseChannel.Raise(new LessonPhaseInfo(_plan.Id, phase));
            }

            _playback = null;
            _melodyDemonstrating = false;
            StopScriptedHarmony();
            if (policy.SystemPlayback == SystemPlayback.Full)
            {
                _playback = new TimelinePlayback(_timeline, _clock, _gate);
                _playback.Start(_phaseStartDsp + _plan.Tempo.BeatsToSeconds(_playbackLeadBeats));
                _melodyDemonstrating = true;
            }
            else if (policy.SystemPlayback == SystemPlayback.HarmonyOnly)
            {
                // ADR-0043 decision 3: a right-hand lesson's Try / Test Yourself. The chords
                // sound (on the Accompaniment channel, so they are never the learner's
                // onsets) and the same timeline selects them for the melody to follow —
                // the chord the learner hears is the chord their notes are constrained to.
                //
                // Where the ghost is shown it strikes each chord (ADR-0052), so the harmony trails it by
                // the same lead and the chord sounds as the hand lands.
                _playback = new TimelinePlayback(_timeline, _clock, _gate, includeMelody: false);
                _playback.Start(_phaseStartDsp + _plan.Tempo.BeatsToSeconds(_playbackLeadBeats));
            }
            else if (policy.SystemPlayback == SystemPlayback.BackingOnly && _logCues)
            {
                // TODO(OPEN): backing track for Compose-on-the-Fly (CLAUDE.md §7).
                Debug.Log($"[{name}] {phase.Mode}: backing track not available yet — running dry.", this);
            }

            // ADR-0063: a right-hand lesson has no left hand, so the lesson's own chords are selected for the
            // learner in EVERY mode - not only the two where the system also plays them. Watch and Listen needs
            // it too: the melody's tones, the letters on the learner's fingers and the label on their left hand all
            // follow the selected chord, and without a script they would follow whatever the idle left hand did.
            // It selects at the instant the chords sound (the lead), so what is heard, shown and playable agree.
            if (_plan.Hands.SystemSelectsChords())
            {
                StartScriptedHarmony(_phaseStartDsp + _plan.Tempo.BeatsToSeconds(_playbackLeadBeats));
            }

            if (_metronome != null)
            {
                _metronome.Start(_phaseStartDsp);
                _metronomeOutput?.Begin(_metronome);
            }

            // "The phrase has started" is announced from Update, once the opening explanation is over (ADR-0066).
        }

        private void StartScriptedHarmony(double startDsp)
        {
            if (_scriptedHarmony == null)
            {
                Debug.LogWarning(
                    $"{nameof(LessonRunner)}: '{_plan.Id}' needs scripted harmony (a right-hand " +
                    $"lesson has no left hand to select the chord) but none was bound.", this);
                return;
            }

            _scriptedHarmony.Start(_timeline, startDsp);
            _scriptStarted = true;
        }

        private void TickConceptSlides()
        {
            if (_concept.TryTake(_clock.Now, out ConceptSlide slide))
            {
                _cueChannel.Raise(CueAction.ShowText(slide.Text, holdSeconds: slide.Seconds));
            }
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
            // Negative before the shared start time (the lead-in, ADR-0056, and the metronome's
            // pre-roll, ADR-0043): the phrase has not begun, and it counts up to 0 as that passes.
            double realBeat = _plan.Tempo.SecondsToBeats(_clock.Now - _phaseStartDsp);
            double advanced = realBeat - _lastRealBeat;
            _lastRealBeat = realBeat;
            _lessonBeat = NextLessonBeat(phase, _lessonBeat + (advanced > 0.0 ? advanced : 0.0));

            TickConceptSlides();

            _playback?.Tick();
            if (_scriptStarted)
            {
                _scriptedHarmony.Tick();
            }

            if (_concept.IsFinished(_clock.Now))
            {
                // The discussion is over: the lesson begins. Cues start (the beat is held at 0 through the lead-in, so
                // the beat-0 cues - "the first chord is..." - appear as the lead-in opens), and the interface - the ghost
                // hand, the lanes - appears with them.
                if (!_phraseAnnounced)
                {
                    _phraseAnnounced = true;
                    _cuePlayer.Notify(LearnerAction.PhraseStarted);
                }

                _cuePlayer.AdvanceTo(_lessonBeat < 0.0 ? 0.0 : _lessonBeat);
                PublishGhostFrame(phase, _lessonBeat);
            }
            else
            {
                // Still in the discussion: nothing of the lesson is on screen but the words. Without this the cue clock,
                // held at beat 0 for the whole discussion, fired "the first chord is Dm7" on the first frame, over slide
                // one, and the ghost hand stood there in its opening pose while it was explained.
                _ghostChannel?.Raise(GhostFrame.Hidden);
            }

            UpdateGestureCorrectness(phase, _lessonBeat);

            if (_autoAdvance && !_waitingForInput && PhraseIsFinished(phase))
            {
                AdvancePhase();
            }
        }

        /// <summary>
        /// Where the phrase clock is allowed to be this frame. In a gesture-gated mode
        /// (<see cref="ModePolicy.GateOnGesture"/>) on a left-hand lesson it stops
        /// <see cref="_gateSettleBeats"/> after each chord — enough for the ghost to form
        /// the pose — and does not move on until
        /// <see cref="GestureInterpreter.ConfirmedFunction"/> matches that pose. A melody
        /// lesson never gates on the chord pose (the learner is not making one).
        /// </summary>
        private double NextLessonBeat(LessonPhase phase, double wanted)
        {
            _gateHeld = false;

            if (!phase.Policy.GateOnGesture || _interpreter == null || _timeline.Chords.Count == 0
                || _plan.Hands != ActiveHands.Left)
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

            // Gate on the same confirmed function that fires the chord — so a matched pose
            // always sounds, and the gate is exactly as loose as free-play triggering.
            if (pose == null || _interpreter.ConfirmedFunction == pose)
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

            // The chord-pose gate is for a harmony lesson. In a melody lesson every note the
            // engine lets through is already a chord tone (§3.3 — the wrong ones are removed),
            // so a sounded note is always "correct": leave the reward gate open.
            if (_plan.Hands != ActiveHands.Left)
            {
                _gate.SetGestureCorrect(true);
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

            // In the lead-in (negative beat) the ghost waits in the pose the phrase opens with - ready,
            // not already striking or lighting a target: those follow the signed beat below, and none
            // of them has begun.
            double poseBeat = System.Math.Max(0.0, beatNow);
            ChordFunction? pose = _timeline.ChordFunctionAt(poseBeat);

            // Before the first chord has begun the ghost already holds that chord's shape, so it appears in
            // it rather than as a relaxed hand that morphs into it once the chord arrives.
            if (pose == null && _timeline.Chords.Count > 0)
            {
                pose = _timeline.Chords[0].Function;
                poseBeat = _timeline.Chords[0].Beat.Position;
            }

            double poseChangedAt = 0.0;
            for (int i = 0; i < _timeline.Chords.Count; i++)
            {
                if (_timeline.Chords[i].Beat.Position <= poseBeat)
                {
                    poseChangedAt = _timeline.Chords[i].Beat.Position;
                }
            }

            // The notes (lit targets, finger presses) are shown on the playback's beat, which trails the ghost's
            // by the lead; the chord they belong to is that beat's chord, not the pose's (ADR-0058).
            double melodyBeat = beatNow - _playbackLeadBeats;
            double pressProgress = _presses.ProgressAt(melodyBeat, out int pressFinger);

            // The finger to play next, announced ahead of its press (ADR-0059), and the chord its note belongs to.
            ChordFunction? nextPose = null;
            if (_presses.TryGetNext(melodyBeat, _ghostNextLookAheadBeats, out int nextFinger, out double nextOnset))
            {
                nextPose = _timeline.ChordFunctionAt(nextOnset);
            }

            _ghostChannel.Raise(new GhostFrame(
                beatNow, pose, poseChangedAt, LitTargetAt(melodyBeat),
                _strikes.ProgressAt(beatNow), _strikes.ContactsUpTo(beatNow),
                pressFinger, pressProgress, _timeline.ChordFunctionAt(System.Math.Max(0.0, melodyBeat)),
                nextFinger, nextPose, _presses, melodyBeat));
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

            // The last ghost strike lands after the last chord: don't end the phrase mid-strike. The
            // same for a finger still lifting off its last key (its schedule is on the playback's beat).
            double endBeat = System.Math.Max(
                System.Math.Max(_timeline.DurationBeats, _strikes.EndBeat),
                _presses.EndBeat + _playbackLeadBeats);
            double tailBeats = _plan.Tempo.SecondsToBeats(_phraseTailSeconds);
            return _lessonBeat >= endBeat + tailBeats;
        }

        private void EndCurrentPhase()
        {
            LessonPhase phase = _stateMachine.CurrentPhase.Value;

            _playback?.Stop();
            _playback = null;
            StopScriptedHarmony();
            StopMetronome();
            _capture.Stop();

            if (phase.Policy.DeferFeedback)
            {
                ScoreAttempt();
            }

            _cuePlayer.Notify(LearnerAction.AttemptCompleted);
        }

        private void ScoreAttempt()
        {
            AttemptResult result = OnsetScorer.Evaluate(
                _timeline.ExpectedOnsetSeconds(), _capture.OnsetsSeconds, OnsetWindows.Default);

            if (_evaluationChannel != null)
            {
                _evaluationChannel.Raise(result);
            }

            if (_logCues)
            {
                Debug.Log($"[{name}] {result}", this);
            }
        }

        /// <summary>
        /// Halt this lesson's pulse. Only touches the output when the lesson owns a metronome,
        /// so a lesson without one never silences a free-play pulse that is not its own.
        /// </summary>
        private void StopMetronome()
        {
            if (_metronome == null)
            {
                return;
            }

            _metronome.Stop();
            _metronomeOutput?.Stop();
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

        private void StopScriptedHarmony()
        {
            if (_scriptStarted)
            {
                _scriptedHarmony.Stop();
                _scriptStarted = false;
            }
        }

        private void OnNoteTriggered(NoteEvent note)
        {
            if (!_running || note.Kind != NoteEventKind.On)
            {
                return;
            }

            // Watch and Listen: the system is demonstrating the melody, so what arrives is
            // not the learner's attempt. (Harmony-only playback is not a demonstration — it
            // is on the Accompaniment channel, which OnsetCapture never takes, and the
            // learner's own melody notes must still count.)
            if (_melodyDemonstrating)
            {
                return;
            }

            if (_capturing)
            {
                _capture.Observe(note); // filters by hand, channel and one-strike-one-onset
            }

            if (note.Source == Handedness.Right && note.Channel == MidiChannel.Melody)
            {
                _waitingForInput = false;
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
