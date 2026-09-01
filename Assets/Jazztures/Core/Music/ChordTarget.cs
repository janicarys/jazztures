using System;

namespace Jazztures.Core.Music
{
    /// <summary>
    /// One of the ten right-hand touch targets (CLAUDE.md §3.1). The identity is
    /// <see cref="Index"/> + <see cref="Degree"/> + <see cref="OctaveOffset"/> — that is
    /// <b>stable</b> across chord changes. Only <see cref="Pitch"/> is re-computed when
    /// the left hand changes chord: "re-pitched, never re-arranged". Immutable value
    /// type (ADR-0007).
    /// </summary>
    public readonly struct ChordTarget : IEquatable<ChordTarget>
    {
        /// <summary>Stable slot, 0..9. See <see cref="ChordToneSet"/> for the layout.</summary>
        public int Index { get; }

        /// <summary>Which chord tone this slot always represents.</summary>
        public ScaleDegree Degree { get; }

        /// <summary>0 for the lower octave, 1 for the upper. Both slots share a <see cref="Degree"/>.</summary>
        public int OctaveOffset { get; }

        /// <summary>The sounding pitch for the currently active chord.</summary>
        public Pitch Pitch { get; }

        public ChordTarget(int index, ScaleDegree degree, int octaveOffset, Pitch pitch)
        {
            Index = index;
            Degree = degree;
            OctaveOffset = octaveOffset;
            Pitch = pitch;
        }

        public bool Equals(ChordTarget other) =>
            Index == other.Index
            && Degree == other.Degree
            && OctaveOffset == other.OctaveOffset
            && Pitch == other.Pitch;

        public override bool Equals(object? obj) => obj is ChordTarget other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Index;
                hash = (hash * 397) ^ (int)Degree;
                hash = (hash * 397) ^ OctaveOffset;
                hash = (hash * 397) ^ Pitch.GetHashCode();
                return hash;
            }
        }

        public override string ToString() =>
            $"#{Index} {Degree}{(OctaveOffset == 0 ? "" : "+8")} = {Pitch}";

        public static bool operator ==(ChordTarget left, ChordTarget right) => left.Equals(right);

        public static bool operator !=(ChordTarget left, ChordTarget right) => !left.Equals(right);
    }
}
