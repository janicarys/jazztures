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
        private readonly float _quietAlpha;
        private readonly float _quietScale;
        private readonly float _restingBrightness;
        private readonly float _alpha;

        public LabelGlow(MelodyConfig config)
        {
            _quietAlpha = config != null ? config.GhostQuietAlpha : 0.3f;
            _quietScale = config != null ? config.GhostQuietScale : 0.8f;
            CueSeconds = Mathf.Max(0.01f, config != null ? config.GhostCueSeconds : 0.15f);

            Color color = config != null ? config.LabelColor : DefaultColor;
            float brightness = config != null ? config.LabelRestingBrightness : 0.72f;
            _restingBrightness = brightness;
            _alpha = color.a;

            Resting = new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
            _lit = config != null ? config.LabelLitColor : DefaultLit;
            _seconds = Mathf.Max(0.05f, config != null ? config.LabelLitSeconds : 0.45f);
            _pop = config != null ? config.LabelLitPop : 0.35f;
        }

        /// <summary>The colour a label rests in.</summary>
        public Color Resting { get; }

        /// <summary>
        /// The colour a label rests in when it carries <paramref name="hue"/> (a chord's colour, ADR-0065): that hue at
        /// the brightness every label rests at, so a coloured letter is no louder or quieter than a plain one.
        /// </summary>
        public Color RestingIn(Color hue) =>
            new Color(hue.r * _restingBrightness, hue.g * _restingBrightness, hue.b * _restingBrightness, _alpha);

        /// <summary>Count a flash down: 1 the moment a label is activated, 0 when it has settled.</summary>
        public float Decay(float flash, float deltaSeconds) =>
            flash > 0f ? Mathf.Max(0f, flash - deltaSeconds / _seconds) : 0f;

        /// <summary>Seconds a cued label takes to move between quiet and full strength.</summary>
        public float CueSeconds { get; }

        /// <summary>Colour and size <paramref name="label"/> for <paramref name="flash"/> (1 = just lit, 0 = resting).</summary>
        public void Apply(TextMesh label, float flash) => Apply(label, flash, 1f);

        /// <summary>
        /// As <see cref="Apply(TextMesh, float)"/>, for a label that is not always the one to look at
        /// (the ghost's finger letters): <paramref name="emphasis"/> 1 is the label as it normally rests, 0 is
        /// quiet - faded and a little smaller. The fade and the size are two channels (§3.10), so the cue does
        /// not rest on colour alone. A lit label is lit whatever its emphasis: it has just sounded.
        /// </summary>
        public void Apply(TextMesh label, float flash, float emphasis) => Apply(label, flash, emphasis, Resting);

        /// <summary>
        /// As above, for a label resting in a colour of its own (<see cref="RestingIn"/>). It still lights to the same
        /// colour whatever it rests in: a note that has just sounded looks the same in every chord.
        /// </summary>
        public void Apply(TextMesh label, float flash, float emphasis, Color resting)
        {
            // Smoothstep: stays bright for the first part of the decay, then eases off, rather than
            // draining away linearly from the first frame.
            float lit = flash * flash * (3f - 2f * flash);
            Color color = Color.Lerp(resting, _lit, lit);
            float strength = Mathf.Lerp(_quietAlpha, 1f, Mathf.Max(emphasis, lit));
            color.a *= strength;
            label.color = color;
            label.transform.localScale = Vector3.one
                * (Mathf.Lerp(_quietScale, 1f, emphasis) * (1f + _pop * lit));
        }
    }
}
