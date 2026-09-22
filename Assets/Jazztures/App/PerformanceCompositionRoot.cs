using Jazztures.Audio;
using Jazztures.Config;
using Jazztures.Core.Gesture;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Melody;
using Jazztures.Core.Ports;
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
    /// <see cref="ChordStrikeDetector"/> or <see cref="ChordPinchDetector"/> (articulates,
    /// ADR-0025 / Design A's ADR-0038 — <see cref="HarmonyCommitGesture"/> picks which) →
    /// <see cref="HarmonyEngine"/> + <see cref="MelodyEngine"/> → <see cref="SamplerNoteSink"/>.
    /// The composite sink gains the OSC and telemetry sinks in later milestones.
    /// </para>
    /// </summary>
    public sealed class PerformanceCompositionRoot : MonoBehaviour
    {
        /// <summary>How a chord is articulated (Design A, ADR-0038).</summary>
        public enum HarmonyCommitGesture
        {
            /// <summary>The ADR-0025 downward strike — needs a <see cref="MetaXRHandPoseSource"/>.</summary>
            Strike,

            /// <summary>An index pinch (Design A) — needs a <see cref="MetaXRHandPostureSource"/>.</summary>
            Pinch,
        }

        [SerializeField] private SamplerNoteSink _sampler;

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

        [Tooltip("Fixed note length for melody notes, seconds. [OPEN] — pilot-calibrated at M8.")]
        [SerializeField] private double _melodySustainSeconds = MelodyEngine.DefaultSustainSeconds;

        [Tooltip("Optional: every note is also raised on this channel for presentation.")]
        [SerializeField] private NoteTriggeredChannel _noteChannel;

        [Tooltip("The touch-target binder. Lives on the target rig, not on this object, so it "
            + "must be assigned explicitly — leave empty only if the scene has no right hand.")]
        [SerializeField] private Jazztures.Presentation.TouchTargetBinder _touchTargets;

        private IHandPoseSource _poseSource;
        private GestureInterpreter _interpreter;
        private ChordStrikeDetector _strikeDetector;
        private ChordPinchDetector _pinchDetector;
        private System.Action<HandPoseFrame> _feedCommit;
        private ModeGatedNoteSink _gate;
        private HarmonyEngine _harmony;
        private MelodyEngine _melody;
        private KeyboardMelodyInput _melodyKeys;

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

            GestureThresholds thresholds = _gestureThresholds != null
                ? _gestureThresholds.ToThresholds()
                : GestureThresholds.Default;
            _interpreter = new GestureInterpreter(clock, thresholds);
            _interpreter.ConfirmedFunctionChanged += function => _harmony.SetHeldFunction(function);

            // ADR-0025 / Design A (ADR-0038): selection (above) and articulation (below)
            // are separate events regardless of which commit gesture is active — the
            // pose/field says which chord, the strike/pinch says when it sounds.
            if (_commitGesture == HarmonyCommitGesture.Pinch)
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
            }
            else
            {
                _strikeDetector = new ChordStrikeDetector(clock, _interpreter, thresholds);
                _strikeDetector.Struck += velocity =>
                {
                    Debug.Log($"[DIAG] STRUCK v={velocity} confirmed={_interpreter.ConfirmedFunction} t={AudioSettings.dspTime:0.000}");
                    _harmony.Strike(velocity);
                };
                _feedCommit = _strikeDetector.Feed;
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
            GetComponent<LessonRunner>()?.Bind(clock, _gate, _interpreter);
            // Assigned in the inspector, not GetComponent'd: the binder belongs on the
            // target rig. An unassigned reference means melody notes silently never fire,
            // so it is worth a warning rather than a null-check that says nothing.
            if (_touchTargets != null)
            {
                _touchTargets.Bind(_melody);
            }
            else
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
            if (_commitGesture == HarmonyCommitGesture.Strike && Mathf.Abs(frame.LeftVerticalSpeedMetresPerSecond) > 0.05f)
            {
                Debug.Log($"[DIAG] vy={frame.LeftVerticalSpeedMetresPerSecond:0.000} candidate={frame.LeftCandidate} confirmed={_interpreter.ConfirmedFunction} phase={_interpreter.Phase} t={AudioSettings.dspTime:0.000}");
            }

            // ADR-0037: both ChordStrikeDetector.Feed(HandPoseFrame) and
            // ChordPinchDetector.Feed(HandPoseFrame) internally commit before handing the
            // frame to the interpreter — see either's doc comment. Calling through
            // _feedCommit (rather than _interpreter.Feed(frame) + the detector's own
            // velocity/pinch Feed separately) makes that ordering guaranteed instead of
            // dependent on these two lines never being reordered.
            _feedCommit(frame);
            _harmony.Tick();
            _melodyKeys.Poll(_melody);
            _melody.Tick();
        }
    }
}
