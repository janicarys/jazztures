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
    /// hand-pose source → <see cref="GestureInterpreter"/> → <see cref="HarmonyEngine"/>
    /// + <see cref="MelodyEngine"/> → <see cref="SamplerNoteSink"/>. The composite sink
    /// gains the OSC and telemetry sinks in later milestones.
    /// </para>
    /// </summary>
    public sealed class PerformanceCompositionRoot : MonoBehaviour
    {
        [SerializeField] private SamplerNoteSink _sampler;

        [Tooltip("A MetaXRHandPoseSource, or leave empty to use the desktop keyboard (Z/X/C).")]
        [SerializeField] private MonoBehaviour _handPoseSource;

        [Tooltip("A recorded .jsonl fixture. If set, it is replayed instead of the live source.")]
        [SerializeField] private TextAsset _replayFixture;

        [Tooltip("Gesture tuning. Leave empty for the built-in defaults.")]
        [SerializeField] private GestureThresholdsConfig _gestureThresholds;

        [Tooltip("Fixed note length for melody notes, seconds. [OPEN] — pilot-calibrated at M8.")]
        [SerializeField] private double _melodySustainSeconds = MelodyEngine.DefaultSustainSeconds;

        [Tooltip("Optional: every note is also raised on this channel for presentation.")]
        [SerializeField] private NoteTriggeredChannel _noteChannel;

        private IHandPoseSource _poseSource;
        private GestureInterpreter _interpreter;
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
            _gate = new ModeGatedNoteSink(_sampler, unconditional);

            _harmony = new HarmonyEngine(clock, _gate);
            _melody = new MelodyEngine(clock, _gate, _melodySustainSeconds);
            _harmony.ChordChanged += _melody.OnChordChanged;

            GestureThresholds thresholds = _gestureThresholds != null
                ? _gestureThresholds.ToThresholds()
                : GestureThresholds.Default;
            _interpreter = new GestureInterpreter(clock, thresholds);
            _interpreter.ConfirmedFunctionChanged += function => _harmony.SetHeldFunction(function);

            _poseSource = ResolvePoseSource(clock);
            _melodyKeys = new KeyboardMelodyInput();

            GetComponent<DomainEventBridge>()?.Bind(_harmony, _interpreter, _poseSource);
            GetComponent<LessonRunner>()?.Bind(clock, _gate, _interpreter);

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

            _interpreter.Feed(_poseSource.CurrentFrame);
            _melodyKeys.Poll(_melody);
            _melody.Tick();
        }
    }
}
