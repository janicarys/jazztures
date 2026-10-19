using Jazztures.Config;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// How a hand's floating label lights up when what it names sounds: the right hand's finger
    /// letters (<see cref="FingerNoteLabels"/>) and the left hand's chord name
    /// (<see cref="ChordNameLabel"/>) share it, so the two hands answer a note and a chord in the
    /// same visual voice.
    ///
    /// <para>
    /// A label rests a little dimmer than the configured colour, jumps to the lit colour and swells
    /// the instant it is activated, holds there, then eases back. The swell is a second channel
    /// beside the colour (§3.10: colour is never the only carrier). The look is
    /// <see cref="MelodyConfig"/>'s ("Label glow"); with no config the built-in defaults apply.
    /// </para>
    /// </summary>
    internal readonly struct LabelGlow
    {
        private static readonly Color DefaultColor = new Color(0.96f, 0.96f, 0.98f, 1f);
        private static readonly Color DefaultLit = new Color(1f, 0.82f, 0.30f, 1f);

        private readonly Color _lit;
        private readonly float _seconds;
        private readonly float _pop;

        public LabelGlow(MelodyConfig config)
        {
            Color color = config != null ? config.LabelColor : DefaultColor;
            float brightness = config != null ? config.LabelRestingBrightness : 0.72f;

            Resting = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
            _lit = config != null ? config.LabelLitColor : DefaultLit;
            _seconds = Mathf.Max(0.05f, config != null ? config.LabelLitSeconds : 0.45f);
            _pop = config != null ? config.LabelLitPop : 0.35f;
        }

        /// <summary>The colour a label rests in.</summary>
        public Color Resting { get; }

        /// <summary>Count a flash down: 1 the moment a label is activated, 0 when it has settled.</summary>
        public float Decay(float flash, float deltaSeconds) =>
            flash > 0f ? Mathf.Max(0f, flash - deltaSeconds / _seconds) : 0f;

        /// <summary>Colour and size <paramref name="label"/> for <paramref name="flash"/> (1 = just lit, 0 = resting).</summary>
        public void Apply(TextMesh label, float flash)
        {
            // Smoothstep: stays bright for the first part of the decay, then eases off, rather than
            // draining away linearly from the first frame.
            float lit = flash * flash * (3f - 2f * flash);
            label.color = Color.Lerp(Resting, _lit, lit);
            label.transform.localScale = Vector3.one * (1f + _pop * lit);
        }
    }
}
