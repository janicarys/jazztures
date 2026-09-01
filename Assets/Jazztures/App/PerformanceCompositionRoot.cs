using Jazztures.Audio;
using Jazztures.Config;
using Jazztures.Core.Gesture;
using Jazztures.Core.Harmony;
using Jazztures.Core.Melody;
using Jazztures.Core.Ports;
using Jazztures.Input;
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

        private IHandPoseSource _poseSource;
        private GestureInterpreter _interpreter;
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
            INoteSink sink = new CompositeNoteSink(_sampler);

            _harmony = new HarmonyEngine(clock, sink);
            _melody = new MelodyEngine(clock, sink, _melodySustainSeconds);
            _harmony.ChordChanged += _melody.OnChordChanged;

            GestureThresholds thresholds = _gestureThresholds != null
                ? _gestureThresholds.ToThresholds()
                : GestureThresholds.Default;
            _interpreter = new GestureInterpreter(clock, thresholds);
            _interpreter.ConfirmedFunctionChanged += function => _harmony.SetHeldFunction(function);

            _poseSource = ResolvePoseSource(clock);
            _melodyKeys = new KeyboardMelodyInput();
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
