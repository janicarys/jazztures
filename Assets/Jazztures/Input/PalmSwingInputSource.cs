using System;
using Jazztures.Config;
using Jazztures.Core.Harmony;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Diagnostics;
using UnityEngine;

namespace Jazztures.Input
{
    /// <summary>
    /// EXPLORATION (ADR-0078, branch <c>explore/melodic-movement-mapping</c>): the right hand sets the
    /// <i>direction</i> of the melody, not its pitches. Tilt the open, left-facing palm so the fingers point up,
    /// straight or down; swing it across a vertical trigger line, either way, and each crossing sounds one
    /// note that steps up, repeats or steps down. A drop-in alternative to <see cref="FingerBendBinder"/>:
    /// it is handed the same <see cref="MelodyEngine"/> and sounds through <see cref="MelodyEngine.PlayPitch"/>,
    /// so the learning-mode gate, the sampler and any recording work unchanged.
    ///
    /// <para>
    /// All decisions are in the pure <see cref="PalmSwingPipeline"/>; the hand reading is behind
    /// <see cref="IPalmSwingSampleSource"/>. This component only feeds one into the other every frame on the
    /// musical clock, forwards chord changes, and plays what comes out.
    /// </para>
    /// </summary>
    public sealed class PalmSwingInputSource : MonoBehaviour
    {
        [SerializeField] private PalmSwingSettings _settings;

        [Tooltip("A component implementing IPalmSwingSampleSource: the Meta hand reader.")]
        [SerializeField] private MonoBehaviour _sampleSource;

        [Tooltip("The chord the melody uses until the left hand has held one, and the one it keeps when the " +
                 "left hand lets go. A lesson where the left hand has not played yet must not silence the right.")]
        [SerializeField] private ChordFunction _fallbackFunction = ChordFunction.Two;

        private IMusicalClock _clock;
        private MelodyEngine _melody;
        private HarmonyEngine _harmony;
        private IPalmSwingSampleSource _source;

        /// <summary>The pure pipeline; null until <see cref="Bind"/>. The view reads its state.</summary>
        public PalmSwingPipeline Pipeline { get; private set; }

        /// <summary>The note most recently sounded, valid once <see cref="NotePlayed"/> has fired.</summary>
        public PalmSwingNote LastNote { get; private set; }

        /// <summary>Raised after each note is handed to the melody engine.</summary>
        public event Action<PalmSwingNote> NotePlayed;

        public bool GateOpen => Pipeline != null && Pipeline.GateOpen;

        /// <summary>Wire the domain up. Call once, from the composition root's <c>Awake</c>.</summary>
        public void Bind(IMusicalClock clock, MelodyEngine melody, HarmonyEngine harmony)
        {
            _clock = clock;
            _melody = melody;
            _harmony = harmony;

            _source = _sampleSource as IPalmSwingSampleSource;
            if (_source == null || _settings == null)
            {
                Debug.LogError(
                    $"{nameof(PalmSwingInputSource)}: needs a {nameof(PalmSwingSettings)} and a sample source " +
                    $"implementing {nameof(IPalmSwingSampleSource)}.", this);
                enabled = false;
                return;
            }

            Pipeline = new PalmSwingPipeline(_settings.ToParameters(), Progression.ChordFor(_fallbackFunction));
            harmony.ChordChanged += change => Pipeline.OnChordChanged(change.CurrentChord);
        }

        /// <summary>Re-centre the lane on the hand and head as they are now (debug key or menu action).</summary>
        public void Recenter() => _source?.Recenter();

        private void OnDisable()
        {
            // The lesson flow turns this off between exercises: forget the hand, keep the melody.
            Pipeline?.OnGateClosed();
        }

        private void LateUpdate()
        {
            if (Pipeline == null || _melody == null)
            {
                return;
            }

            PalmSwingSample raw = _source.Read();
            var sample = new PalmSwingSample(
                _clock.Now, raw.GateOpen, raw.PitchAngleDegrees, raw.LateralMetres, raw.HeightMetres);

            if (!Pipeline.Update(sample, out PalmSwingNote note))
            {
                return;
            }

            _melody.PlayPitch(note.Pitch, note.SpeedMetresPerSecond);
            LastNote = note;
            NotePlayed?.Invoke(note);
            DiagLog.Log(
                $"[PalmSwing] {note.Pitch} · {note.SampledDirection} · {note.Swing} · {note.SpeedMetresPerSecond:0.00} m/s" +
                (note.AtLimit ? " · at limit" : string.Empty));
        }
    }
}
