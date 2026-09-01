using Jazztures.Audio;
using Jazztures.Core.Harmony;
using Jazztures.Core.Melody;
using Jazztures.Core.Ports;
using Jazztures.Input;
using UnityEngine;

namespace Jazztures.App
{
    /// <summary>
    /// The single wiring point for the M2 debug scene (CLAUDE.md §2.3: one
    /// <c>CompositionRoot</c> per scene, dependencies wired in <c>Awake()</c>, no
    /// singletons, no <c>FindObjectOfType</c> in the domain).
    ///
    /// <para>
    /// Input → domain → presentation. Here: keyboard → <see cref="HarmonyEngine"/> +
    /// <see cref="MelodyEngine"/> → <see cref="SamplerNoteSink"/>. The composite sink
    /// gains the OSC and telemetry sinks in later milestones.
    /// </para>
    /// </summary>
    public sealed class PerformanceCompositionRoot : MonoBehaviour
    {
        [SerializeField] private SamplerNoteSink _sampler;

        [Tooltip("Fixed note length for keyed melody notes, seconds. [OPEN] — pilot-calibrated at M8.")]
        [SerializeField] private double _melodySustainSeconds = MelodyEngine.DefaultSustainSeconds;

        private HarmonyEngine _harmony;
        private MelodyEngine _melody;
        private KeyboardPerformanceDriver _driver;

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

            _driver = new KeyboardPerformanceDriver(_harmony, _melody);
        }

        private void Update()
        {
            _driver.Poll();
            _melody.Tick();
        }
    }
}
