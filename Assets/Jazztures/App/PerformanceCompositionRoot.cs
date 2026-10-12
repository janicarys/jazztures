using Jazztures.Audio;
using Jazztures.Config;
using Jazztures.Core.Gesture;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Melody;
using Jazztures.Core.Ports;
using Jazztures.Diagnostics;
using Jazztures.Events;
using Jazztures.Input;
using Jazztures.Lessons;
using UnityEngine;

namespace Jazztures.App
{
    /// <summary>
    /// The single wiring point for the debug scene (CLAUDE.md §2.3: one
    /// <c>CompositionRoot</c> per scene, dependencies wired in <c>Awake()</c>, no
    /// singletons, no <c>FindObjectOfType</c> in the domain).
    ///
    /// <para>
    /// input → domain → presentation:
    /// hand-pose source → <see cref="GestureInterpreter"/> (selects) +
    /// <see cref="ChordStrikeDetector"/>, <see cref="ChordPinchDetector"/> or
    /// <see cref="ChordTouchDetector"/> (articulates — ADR-0025, Design A's ADR-0038, or
    /// ADR-0039; <see cref="HarmonyCommitGesture"/> picks which) →
    /// <see cref="HarmonyEngine"/> + <see cref="MelodyEngine"/> → <see cref="SamplerNoteSink"/>.
    /// The composite sink gains the OSC and telemetry sinks in later milestones.
    /// </para>
    /// </summary>
    public sealed class PerformanceCompositionRoot : MonoBehaviour
    {
        /// <summary>How a chord is articulated (Design A ADR-0038 / ADR-0039).</summary>
        public enum HarmonyCommitGesture
        {
            /// <summary>The ADR-0025 downward strike — needs a <see cref="MetaXRHandPoseSource"/>.</summary>
            Strike,

            /// <summary>An index pinch (Design A) — needs a <see cref="MetaXRHandPostureSource"/>.</summary>
            Pinch,

            /// <summary>Touching a virtual object (ADR-0039) — needs a <see cref="Jazztures.Presentation.ChordStrikeTarget"/>, works with either hand-pose source.</summary>
            Touch,
        }

        [SerializeField] private SamplerNoteSink _sampler;

        [Tooltip("Sounds a lesson's metronome (ADR-0043). Optional — a lesson that asks for a " +
                 "metronome runs it silently, with a warning, if this is empty.")]
        [SerializeField] private MetronomeVoice _metronomeVoice;

        [Tooltip("A MetaXRHandPoseSource or MetaXRHandPostureSource, or leave empty to use the desktop keyboard (Z/X/C).")]
        [SerializeField] private MonoBehaviour _handPoseSource;

        [Tooltip("A recorded .jsonl fixture. If set, it is replayed instead of the live source.")]
        [SerializeField] private TextAsset _replayFixture;

        [Tooltip("Gesture tuning for the strike path. Leave empty for the built-in defaults.")]
        [SerializeField] private GestureThresholdsConfig _gestureThresholds;

        [Tooltip("How a chord is articulated. Strike = the ADR-0025 downward motion (needs "
            + "a MetaXRHandPoseSource). Pinch = index self-contact (Design A, ADR-0038; "
            + "needs a MetaXRHandPostureSource).")]
        [SerializeField] private HarmonyCommitGesture _commitGesture = HarmonyCommitGesture.Strike;

        [Tooltip("Tuning for the continuous harmonic field and pinch commit. Required when Commit Gesture is Pinch.")]
        [SerializeField] private HarmonicFieldConfig _harmonicField;

        [Tooltip("The virtual object the left hand must touch to sound a chord. Required when Commit Gesture is Touch.")]
        [SerializeField] private Jazztures.Presentation.ChordStrikeTarget _strikeTarget;

        [Tooltip("Optional (ADR-0040). Body-anchors _strikeTarget to the left, at strike height, "
            + "instead of leaving it at a fixed world position. Lives on the same GameObject as "
            + "_strikeTarget; leave empty to keep the plate world-fixed.")]
        [SerializeField] private Jazztures.Presentation.ChordStrikeTargetAnchor _strikeTargetAnchor;

        [Tooltip("Fixed note length for melody notes, seconds. [OPEN] — pilot-calibrated at M8.")]
        [SerializeField] private double _melodySustainSeconds = MelodyEngine.DefaultSustainSeconds;

        [Tooltip("Optional: every note is also raised on this channel for presentation.")]
        [SerializeField] private NoteTriggeredChannel _noteChannel;

        [Tooltip("The touch-target binder. Lives on the target rig, not on this object, so it "
            + "must be assigned explicitly — leave empty only if the scene has no right hand.")]
        [SerializeField] private Jazztures.Presentation.TouchTargetBinder _touchTargets;

        [Tooltip("EXPLORATION (ADR-0050, branch explore/finger-bend-keys): the finger-bend melody "
            + "input — one finger bend = one chord tone. Optional; bound to the same melody engine "
            + "as the touch targets. Only one of the two should be enabled at a time.")]
        [SerializeField] private Jazztures.Input.FingerBendBinder _fingerBend;

        private IHandPoseSource _poseSource;
        private GestureInterpreter _interpreter;
        private ChordStrikeDetector _strikeDetector;
        private ChordPinchDetector _pinchDetector;
        private ChordTouchDetector _touchDetector;
        private System.Action<HandPoseFrame> _feedCommit;
        private ModeGatedNoteSink _gate;
        private HarmonyEngine _harmony;
        private MelodyEngine _melody;
        private ScriptedHarmony _scriptedHarmony;
        private KeyboardMelodyInput _melodyKeys;

        /// <summary>How the left hand articulates a chord in this build — recorded in a timing run's CSV (ADR-0043).</summary>
        public HarmonyCommitGesture CommitGesture => _commitGesture;

        private void Awake()
        {
            if (_sampler == null)
            {
                _sampler = GetComponentInChildren<SamplerNoteSink>();
            }

            if (_sampler == null)
            {
                Debug.LogError($"{nameof(PerformanceCompositionRoot)}: no {nameof(SamplerNoteSink)} assigned.", this);
                enabled = false;
                return;
            }

            var clock = new DspMusicalClock();

            // §3.8: the learning mode gates only what the learner hears. Everything —
            // sounded or not — reaches the unconditional sink (presentation, and OSC /
            // telemetry in later milestones). Default mode is Compose-on-the-Fly, so with
            // no LessonRunner present the keyboard debug path sounds everything as before.
            INoteSink unconditional = _noteChannel != null
                ? new ChannelNoteSink(_noteChannel)
                : (INoteSink)new NullNoteSink();
            _gate = new ModeGatedNoteSink(_sampler, unconditional, clock);

            _harmony = new HarmonyEngine(clock, _gate);
            _melody = new MelodyEngine(clock, _gate, _melodySustainSeconds);
            _harmony.ChordChanged += _melody.OnChordChanged;

            // ADR-0043: selects a chord without the left hand — for a right-hand lesson's
            // Try / Test Yourself and for a timing drill's fixed chord. Silent by
            // construction; the runners start and stop it.
            _scriptedHarmony = new ScriptedHarmony(clock, _harmony);

            GestureThresholds thresholds = _gestureThresholds != null
                ? _gestureThresholds.ToThresholds()
                : GestureThresholds.Default;
            _interpreter = new GestureInterpreter(clock, thresholds);
            _interpreter.ConfirmedFunctionChanged += function => _harmony.SetHeldFunction(function);

            // ADR-0025 / Design A (ADR-0038) / ADR-0039: selection (above) and
            // articulation (below) are separate events regardless of which commit gesture
            // is active — the pose/field says which chord, the strike/pinch/touch says
            // when it sounds.
            switch (_commitGesture)
            {
                case HarmonyCommitGesture.Pinch:
                {
                    PinchCommitThresholds pinchThresholds = _harmonicField != null
                        ? _harmonicField.ToPinchThresholds()
                        : PinchCommitThresholds.Default;

                    if (_harmonicField == null)
                    {
                        Debug.LogWarning(
                            $"{nameof(PerformanceCompositionRoot)}: Commit Gesture is Pinch but no "
                            + $"{nameof(HarmonicFieldConfig)} is assigned; using engineering defaults.", this);
                    }

                    _pinchDetector = new ChordPinchDetector(clock, _interpreter, pinchThresholds);
                    _pinchDetector.Articulated += velocity => _harmony.Strike(velocity);
                    _feedCommit = _pinchDetector.Feed;
                    break;
                }

                case HarmonyCommitGesture.Touch:
                {
                    if (_strikeTarget == null)
                    {
                        Debug.LogError(
                            $"{nameof(PerformanceCompositionRoot)}: Commit Gesture is Touch but no "
                            + $"{nameof(Jazztures.Presentation.ChordStrikeTarget)} is assigned — no chord will ever sound.", this);
                    }

                    _touchDetector = new ChordTouchDetector(clock, _interpreter, TouchCommitThresholds.Default);
                    _touchDetector.Articulated += velocity =>
                    {
                        _harmony.Strike(velocity);
                        if (_strikeTarget != null)
                        {
                            _strikeTarget.Flash();
                        }
                    };
                    _feedCommit = _touchDetector.Feed;
                    break;
                }

                default:
                    _strikeDetector = new ChordStrikeDetector(clock, _interpreter, thresholds);
                    _strikeDetector.Struck += velocity =>
                    {
                        DiagLog.Log($"[DIAG] STRUCK v={velocity} confirmed={_interpreter.ConfirmedFunction} t={AudioSettings.dspTime:0.000}");
                        _harmony.Strike(velocity);
                    };
                    _feedCommit = _strikeDetector.Feed;
                    break;
            }

            _poseSource = ResolvePoseSource(clock);
            _melodyKeys = new KeyboardMelodyInput();

            if (_commitGesture == HarmonyCommitGesture.Pinch
                && !(_poseSource is MetaXRHandPostureSource)
                && !(_poseSource is ReplayHandPoseSource))
            {
                Debug.LogWarning(
                    $"{nameof(PerformanceCompositionRoot)}: Commit Gesture is Pinch but the hand-pose "
                    + $"source is not a {nameof(MetaXRHandPostureSource)} — it will never report a pinch.", this);
            }

            GetComponent<DomainEventBridge>()?.Bind(_harmony, _interpreter, _poseSource);
            // `_metronomeVoice` is a Unity object: pass null explicitly when unassigned rather
            // than a fake-null reference that would pass a plain `!= null` check downstream.
            IMetronomeOutput metronomeOutput = _metronomeVoice != null ? _metronomeVoice : null;
            GetComponent<LessonRunner>()?.Bind(clock, _gate, _interpreter, metronomeOutput, _scriptedHarmony);
            GetComponent<TimingDrillRunner>()?.Bind(clock, _gate, _scriptedHarmony, metronomeOutput);
            // Assigned in the inspector, not GetComponent'd: the binder belongs on the
            // target rig. An unassigned reference means melody notes silently never fire,
            // so it is worth a warning rather than a null-check that says nothing.
            if (_fingerBend != null)
            {
                _fingerBend.Bind(_melody);
            }

            if (_touchTargets != null)
            {
                _touchTargets.Bind(_melody);
            }
            else if (_fingerBend == null)
            {
                Debug.LogWarning(
                    $"{nameof(PerformanceCompositionRoot)}: no {nameof(Jazztures.Presentation.TouchTargetBinder)} "
                    + "assigned — touch targets will light on chord change but will not sound.", this);
            }

            var probe = GetComponent<Jazztures.Diagnostics.LatencyProbe>();
            if (probe != null)
            {
                probe.Bind(_interpreter);
                _sampler.SetLatencyRecorder(probe.Recorder);
            }
        }

        private IHandPoseSource ResolvePoseSource(IMusicalClock clock)
        {
            if (_replayFixture != null)
            {
                if (HandPoseRecording.TryParseJsonl(_replayFixture.text, out HandPoseRecording recording))
                {
                    Debug.Log(
                        $"{nameof(PerformanceCompositionRoot)}: replaying '{_replayFixture.name}' " +
                        $"({recording.Count} frames, {recording.DurationSeconds:0.0}s).", this);
                    return new ReplayHandPoseSource(recording, clock);
                }

                Debug.LogError(
                    $"{nameof(PerformanceCompositionRoot)}: '{_replayFixture.name}' is not a valid recording.", this);
            }

            if (_handPoseSource is IHandPoseSource live)
            {
                return live;
            }

            if (_handPoseSource != null)
            {
                Debug.LogWarning(
                    $"{nameof(PerformanceCompositionRoot)}: '{_handPoseSource.GetType().Name}' is not an " +
                    $"{nameof(IHandPoseSource)}; falling back to the keyboard.", this);
            }

            return new KeyboardHandPoseSource();
        }

        private void Update()
        {
            if (!enabled)
            {
                return;
            }

            HandPoseFrame frame = _poseSource.CurrentFrame;
#if JAZZTURES_DIAG
            if (_commitGesture == HarmonyCommitGesture.Strike && Mathf.Abs(frame.LeftVerticalSpeedMetresPerSecond) > 0.05f)
            {
                DiagLog.Log($"[DIAG] vy={frame.LeftVerticalSpeedMetresPerSecond:0.000} candidate={frame.LeftCandidate} confirmed={_interpreter.ConfirmedFunction} phase={_interpreter.Phase} t={AudioSettings.dspTime:0.000}");
            }
#endif

            if (_commitGesture == HarmonyCommitGesture.Touch && _strikeTarget != null)
            {
                // ADR-0040: re-anchor before Sense(), same reasoning as Sense() itself —
                // called explicitly rather than left to Unity's Update()/LateUpdate() order.
                if (_strikeTargetAnchor != null)
                {
                    _strikeTargetAnchor.Reanchor(Time.deltaTime);
                }

                // Called explicitly, not left to the target's own Update() — Unity does
                // not guarantee component update order, and this frame's touch state must
                // be settled before ChordTouchDetector reads it below.
                _strikeTarget.Sense();
                frame = new HandPoseFrame(
                    frame.LeftCandidate, frame.LeftTracking, frame.RightTracking,
                    frame.LeftVerticalSpeedMetresPerSecond, frame.LeftIsPinching, frame.LeftPinchClosingRatePerSecond,
                    _strikeTarget.CurrentlyInside, _strikeTarget.CurrentEntrySpeed);
            }

            // ADR-0037: ChordStrikeDetector/ChordPinchDetector/ChordTouchDetector's
            // Feed(HandPoseFrame) overloads all internally commit before handing the frame
            // to the interpreter — see any one's doc comment. Calling through _feedCommit
            // (rather than _interpreter.Feed(frame) + the detector's own velocity/pinch/
            // touch Feed separately) makes that ordering guaranteed instead of dependent on
            // these two lines never being reordered.
            _feedCommit(frame);
            _harmony.Tick();
            _melodyKeys.Poll(_melody);
            _melody.Tick();
        }
    }
}
