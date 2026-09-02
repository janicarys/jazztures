using System;

namespace Jazztures.Core.Timing
{
    /// <summary>
    /// How a pair of eighth notes divides one beat. <see cref="Value"/> is the fraction of
    /// the beat the <i>first</i> eighth occupies: 0.5 is straight (even eighths), higher
    /// values delay the off-beat "and" for a swung feel.
    ///
    /// <para>
    /// `[TUNABLE]` (CLAUDE.md §3.6). Default 0.66 (~2:1). Lessons 1–3 are straight; Lesson 4
    /// introduces swing. The value is carried on the lesson asset — see
    /// <c>Docs/CALIBRATION.md</c>. Immutable value type (ADR-0007).
    /// </para>
    /// </summary>
    public readonly struct SwingRatio : IEquatable<SwingRatio>
    {
        /// <summary>Even eighth notes — no swing.</summary>
        public static SwingRatio Straight => new SwingRatio(0.5);

        /// <summary>The §3.6 default swing, approximately a 2:1 eighth-note ratio.</summary>
        public static SwingRatio Default => new SwingRatio(0.66);

        /// <summary>Fraction of the beat taken by the first eighth of the pair, in [0.5, 1).</summary>
        public double Value { get; }

        public SwingRatio(double value)
        {
            if (double.IsNaN(value) || value < 0.5 || value >= 1.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "Swing ratio must be in [0.5, 1.0).");
            }

            Value = value;
        }

        /// <summary>True when this is (near enough) an even split — no perceptible swing.</summary>
        public bool IsStraight => Value <= 0.5 + 1e-9;

        public bool Equals(SwingRatio other) => Value.Equals(other.Value);

        public override bool Equals(object? obj) => obj is SwingRatio other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"swing {Value:0.###}";

        public static bool operator ==(SwingRatio left, SwingRatio right) => left.Equals(right);

        public static bool operator !=(SwingRatio left, SwingRatio right) => !left.Equals(right);
    }
}
