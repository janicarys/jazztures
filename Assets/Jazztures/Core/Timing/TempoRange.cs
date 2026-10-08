using System;

namespace Jazztures.Core.Timing
{
    /// <summary>
    /// The tempo range the system stands behind (CLAUDE.md §3.6). The literature this work
    /// leans on evaluates open-air tracking stability up to about 70 BPM; anything above
    /// ~100 BPM is unvalidated and stays behind an explicit opt-in.
    ///
    /// <para>`[TUNABLE]` bounds — an engineering default, not a finding.</para>
    /// </summary>
    public static class TempoRange
    {
        /// <summary>Slowest tempo offered. `[TUNABLE]`.</summary>
        public const double MinBpm = 40.0;

        /// <summary>Fastest tempo accepted without <c>allowUnvalidated</c>. `[TUNABLE]`.</summary>
        public const double ValidatedMaxBpm = 100.0;

        /// <summary>
        /// Hard ceiling even with the opt-in — a typo guard for the Inspector, not a claim
        /// that anything above <see cref="ValidatedMaxBpm"/> has been validated.
        /// </summary>
        public const double AbsoluteMaxBpm = 240.0;

        /// <summary>
        /// Clamp <paramref name="bpm"/> into the supported range. The upper bound is
        /// <see cref="ValidatedMaxBpm"/> unless <paramref name="allowUnvalidated"/> lifts it
        /// to <see cref="AbsoluteMaxBpm"/>.
        /// </summary>
        public static double Clamp(double bpm, bool allowUnvalidated)
        {
            if (double.IsNaN(bpm))
            {
                throw new ArgumentOutOfRangeException(nameof(bpm), bpm, "Must be a number.");
            }

            double max = allowUnvalidated ? AbsoluteMaxBpm : ValidatedMaxBpm;
            return Math.Min(Math.Max(bpm, MinBpm), max);
        }

        /// <summary><see cref="Clamp"/> as a <see cref="Tempo"/>.</summary>
        public static Tempo ClampToTempo(double bpm, bool allowUnvalidated) =>
            Tempo.Bpm(Clamp(bpm, allowUnvalidated));

        /// <summary>True when <paramref name="bpm"/> is inside the validated range.</summary>
        public static bool IsValidated(double bpm) => bpm >= MinBpm && bpm <= ValidatedMaxBpm;
    }
}
