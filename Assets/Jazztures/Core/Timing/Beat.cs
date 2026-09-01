using System;

namespace Jazztures.Core.Timing
{
    /// <summary>
    /// A position on the beat grid, in beats from an origin. Fractional values are the
    /// norm (an eighth note is beat 0.5). Turning a beat into a wall/DSP time needs a
    /// <see cref="Tempo"/>. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct Beat : IEquatable<Beat>, IComparable<Beat>
    {
        public static Beat Zero => new Beat(0.0);

        public double Position { get; }

        public Beat(double position)
        {
            if (double.IsNaN(position) || double.IsInfinity(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "Must be finite.");
            }

            Position = position;
        }

        /// <summary>The beat containing <paramref name="seconds"/> at the given tempo.</summary>
        public static Beat FromSeconds(double seconds, Tempo tempo) =>
            new Beat(tempo.SecondsToBeats(seconds));

        public double ToSeconds(Tempo tempo) => tempo.BeatsToSeconds(Position);

        /// <summary>The whole-beat index this position falls in (floor).</summary>
        public long Index => (long)Math.Floor(Position);

        /// <summary>How far past the current whole beat this position is, in [0, 1).</summary>
        public double Fraction => Position - Math.Floor(Position);

        public int CompareTo(Beat other) => Position.CompareTo(other.Position);

        public bool Equals(Beat other) => Position.Equals(other.Position);

        public override bool Equals(object? obj) => obj is Beat other && Equals(other);

        public override int GetHashCode() => Position.GetHashCode();

        public override string ToString() => $"beat {Position:0.###}";

        public static Beat operator +(Beat beat, double beats) => new Beat(beat.Position + beats);

        public static Beat operator -(Beat beat, double beats) => new Beat(beat.Position - beats);

        public static bool operator ==(Beat left, Beat right) => left.Equals(right);

        public static bool operator !=(Beat left, Beat right) => !left.Equals(right);

        public static bool operator <(Beat left, Beat right) => left.Position < right.Position;

        public static bool operator >(Beat left, Beat right) => left.Position > right.Position;

        public static bool operator <=(Beat left, Beat right) => left.Position <= right.Position;

        public static bool operator >=(Beat left, Beat right) => left.Position >= right.Position;
    }
}
