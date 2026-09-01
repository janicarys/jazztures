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

            IMusicalClock clock = new DspMusicalClock();
            INoteSink sink = new CompositeNoteSink(_sampler);

            _harmony = new HarmonyEngine(clock, sink);
            _melody = new MelodyEngine(clock, sink, _melodySustainSeconds);
            _harmony.ChordChanged += _melody.OnChordChanged;

            GestureThresholds thresholds = _gestureThresholds != null
                ? _gestureThresholds.ToThresholds()
                : GestureThresholds.Default;
            _interpreter = new GestureInterpreter(clock, thresholds);
            _interpreter.ConfirmedFunctionChanged += _harmony.SetHeldFunction;

            _poseSource = _handPoseSource as IHandPoseSource ?? new KeyboardHandPoseSource();
            if (_handPoseSource != null && _poseSource == null)
            {
                Debug.LogWarning(
                    $"{nameof(PerformanceCompositionRoot)}: '{_handPoseSource.GetType().Name}' is not an " +
                    $"{nameof(IHandPoseSource)}; falling back to the keyboard.", this);
                _poseSource = new KeyboardHandPoseSource();
            }

            _melodyKeys = new KeyboardMelodyInput();
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
