using Jazztures.Core.Harmony;
using UnityEngine;

namespace Jazztures.Config
{
    /// <summary>
    /// The tension-arc colour palette (CLAUDE.md §3.10 — "cool/neutral for ii,
    /// warm/saturated for V, resolved/settled for I"). Colour is a <b>redundant</b> channel
    /// reinforcing the harmonic function the learner also hears and sees spatially; it is
    /// never the sole carrier of information. Read by
    /// <c>Jazztures.Presentation.TensionColorDriver</c>, which eases between these over
    /// <see cref="TransitionSeconds"/> on every chord change.
    ///
    /// <para>
    /// Every value is `[TUNABLE]` — an engineering default, pilot-calibrated at M8. Mirror
    /// any change in <c>Docs/CALIBRATION.md</c>. See ADR-0017 for the deviation from
    /// §3.10's literal "cool for ii" (mustard was requested, sage/olive was the
    /// compromise — a desaturated yellow-green that still reads as preparation).
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Config/Tension Palette", fileName = "TensionPalette")]
    public sealed class TensionPalette : ScriptableObject
    {
        [Header("Base colours — no chord, then the ii-V-I arc")]
        [Tooltip("Shown when no left-hand pose is held. Cool, low-alpha: clearly inactive.")]
        [SerializeField] private Color _neutral = new Color(0.58f, 0.58f, 0.62f, 0.35f);

        [Tooltip("ii — preparation. Sage/olive: a desaturated yellow-green (ADR-0017).")]
        [SerializeField] private Color _two = new Color(0.53f, 0.60f, 0.42f, 0.80f);

        [Tooltip("V — peak of tension. Burnt sienna: warm and saturated.")]
        [SerializeField] private Color _five = new Color(0.74f, 0.33f, 0.18f, 0.85f);

        [Tooltip("I — release / resolution. Warm purple: deep and settled.")]
        [SerializeField] private Color _one = new Color(0.52f, 0.36f, 0.60f, 0.85f);

        [Header("Overlays — layered on the base colour by TouchTarget")]
        [Tooltip("Flash colour the instant a fingertip triggers a target.")]
        [SerializeField] private Color _struck = new Color(1.00f, 0.96f, 0.85f, 1.00f);

        [Tooltip("How far a target's colour is pushed toward white while its chord-change " +
                 "highlight is active, 0..1.")]
        [Range(0f, 1f)] [SerializeField] private float _highlightBoost = 0.35f;

        [Header("Transition")]
        [Tooltip("Seconds to ease from the old colour to the new one on a chord change. " +
                 "Long enough to not jar, short enough to still feel responsive.")]
        [Min(0f)] [SerializeField] private float _transitionSeconds = 0.25f;

        /// <summary>The base colour for a held function, or <see cref="Neutral"/> when nothing is held.</summary>
        public Color For(ChordFunction? function) => function switch
        {
            ChordFunction.Two => _two,
            ChordFunction.Five => _five,
            ChordFunction.One => _one,
            _ => _neutral,
        };

        public Color Neutral => _neutral;

        public Color Struck => _struck;

        public float HighlightBoost => _highlightBoost;

        public float TransitionSeconds => _transitionSeconds;
    }
}
