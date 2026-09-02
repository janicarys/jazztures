using System;

namespace Jazztures.Core.Evaluation
{
    /// <summary>
    /// The timing tolerance bands for onset scoring (CLAUDE.md §3.7).
    ///
    /// <para>
    /// `[TUNABLE]` — the defaults (80 ms / 160 ms) are engineering choices to be
    /// pilot-calibrated at M8; see <c>Docs/CALIBRATION.md</c>. They must not be written
    /// into the paper as findings. Immutable value type (ADR-0007).
    /// </para>
    /// </summary>
    public readonly struct OnsetWindows : IEquatable<OnsetWindows>
    {
        /// <summary>Deviation at or below this reads as "on time".</summary>
        public const double DefaultOnTimeSeconds = 0.080;

        /// <summary>Deviation at or below this (but above on-time) reads as "close".</summary>
        public const double DefaultCloseSeconds = 0.160;

        /// <summary>
        /// A played onset further than this from every expected beat is not matched at
        /// all — it counts as an extra note, and the beat it might have been counts as
        /// missed. `[TUNABLE]`.
        /// </summary>
        public const double DefaultMatchSeconds = 0.300;

        public static OnsetWindows Default =>
            new OnsetWindows(DefaultOnTimeSeconds, DefaultCloseSeconds, DefaultMatchSeconds);

        public double OnTimeSeconds { get; }

        public double CloseSeconds { get; }

        public double MatchSeconds { get; }

        public OnsetWindows(double onTimeSeconds, double closeSeconds, double matchSeconds)
        {
            if (!(onTimeSeconds > 0.0) || double.IsInfinity(onTimeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(onTimeSeconds), onTimeSeconds, "Must be finite and positive.");
            }

            if (!(closeSeconds >= onTimeSeconds) || double.IsInfinity(closeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(closeSeconds), closeSeconds, "Must be >= onTimeSeconds.");
            }

            if (!(matchSeconds >= closeSeconds) || double.IsInfinity(matchSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(matchSeconds), matchSeconds, "Must be >= closeSeconds.");
            }

            OnTimeSeconds = onTimeSeconds;
            CloseSeconds = closeSeconds;
            MatchSeconds = matchSeconds;
        }

        public bool Equals(OnsetWindows other) =>
            OnTimeSeconds.Equals(other.OnTimeSeconds)
            && CloseSeconds.Equals(other.CloseSeconds)
            && MatchSeconds.Equals(other.MatchSeconds);

        public override bool Equals(object? obj) => obj is OnsetWindows other && Equals(other);

        public override int GetHashCode() =>
            unchecked((OnTimeSeconds.GetHashCode() * 397) ^ CloseSeconds.GetHashCode());

        public override string ToString() =>
            $"onset windows ≤{OnTimeSeconds * 1000:0}ms / ≤{CloseSeconds * 1000:0}ms";

        public static bool operator ==(OnsetWindows left, OnsetWindows right) => left.Equals(right);

        public static bool operator !=(OnsetWindows left, OnsetWindows right) => !left.Equals(right);
    }
}
