using System;

namespace Jazztures.Core.Timing
{
    /// <summary>
    /// Warps positions between a straight (notated) beat grid and a swung (performed) one
    /// (CLAUDE.md §3.6). The warp is piecewise-linear within each beat: the first eighth
    /// is stretched to <see cref="SwingRatio.Value"/> of the beat, the second is
    /// compressed into what remains. Whole-beat positions are fixed points, so downbeats
    /// and the metronome are unaffected.
    ///
    /// <para>Pure and allocation-free. A <see cref="SwingRatio.Straight"/> ratio is identity.</para>
    /// </summary>
    public static class SwingQuantizer
    {
        /// <summary>
        /// Where a note notated at <paramref name="straightBeat"/> is actually played
        /// under <paramref name="ratio"/>.
        /// </summary>
        public static double Swing(double straightBeat, SwingRatio ratio)
        {
            RequireFinite(straightBeat, nameof(straightBeat));

            double whole = Math.Floor(straightBeat);
            double f = straightBeat - whole;
            double r = ratio.Value;

            double swung = f < 0.5
                ? f * 2.0 * r
                : r + (f * 2.0 - 1.0) * (1.0 - r);

            return whole + swung;
        }

        /// <summary>
        /// The inverse of <see cref="Swing"/>: the notated position a performed
        /// <paramref name="swungBeat"/> corresponds to. Used to score a user onset against
        /// the straight grid (§3.7).
        /// </summary>
        public static double Straighten(double swungBeat, SwingRatio ratio)
        {
            RequireFinite(swungBeat, nameof(swungBeat));

            double whole = Math.Floor(swungBeat);
            double g = swungBeat - whole;
            double r = ratio.Value;

            double straight = g < r
                ? g / (2.0 * r)
                : 0.5 + (g - r) / (2.0 * (1.0 - r));

            return whole + straight;
        }

        /// <summary>
        /// The swung position expressed in seconds from the grid origin, at
        /// <paramref name="tempo"/>.
        /// </summary>
        public static double SwingToSeconds(double straightBeat, SwingRatio ratio, Tempo tempo) =>
            tempo.BeatsToSeconds(Swing(straightBeat, ratio));

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, "Must be finite.");
            }
        }
    }
}
