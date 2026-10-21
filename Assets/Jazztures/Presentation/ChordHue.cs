using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The colour a chord is drawn in (CLAUDE.md §3.10, ADR-0065): the left hand while it holds the pose, and the
    /// letters of that chord's notes coming down the lanes — the same colour, which is the point of both.
    ///
    /// <para>
    /// It is the <see cref="Jazztures.Config.TensionPalette"/> colour with its brightness taken to the top and its alpha
    /// dropped. The palette's values are mid-tones made for a translucent outline (sage, burnt sienna, warm purple);
    /// on a hand body and on small text over a dark sky they read as dull and hard to make out, and the palette's alpha
    /// is a strength for that outline, not for a letter. Hue and saturation are kept, so a chord is still the colour the
    /// palette chose; only how bright it is drawn changes. Colour stays a redundant channel (§3.10): the note's name, the
    /// chord's name and the sound all say the same thing.
    /// </para>
    /// </summary>
    internal static class ChordHue
    {
        /// <summary>
        /// <paramref name="paletteColor"/> at full brightness (its brightest channel brought to 1) and full alpha.
        /// A colour with no brightness to speak of has no hue to keep and comes back white.
        /// </summary>
        public static Color Of(Color paletteColor)
        {
            float peak = Mathf.Max(paletteColor.r, Mathf.Max(paletteColor.g, paletteColor.b));
            return peak < 1e-3f
                ? Color.white
                : new Color(paletteColor.r / peak, paletteColor.g / peak, paletteColor.b / peak, 1f);
        }
    }
}
