using System;
using System.Collections.Generic;

namespace Jazztures.Core.Music
{
    /// <summary>
    /// A set of pitch classes defined by a tonic and a semitone pattern. Jazztures only
    /// needs the C major / Ionian collection today (Lesson 4 phrasing is "C Ionian",
    /// CLAUDE.md §3.9); the type takes an explicit pattern so other modes can be added
    /// without reshaping it. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct Scale : IEquatable<Scale>
    {
        /// <summary>Semitone offsets of the major scale from its tonic.</summary>
        public static readonly IReadOnlyList<int> MajorPattern = new[] { 0, 2, 4, 5, 7, 9, 11 };

        private readonly int _patternMask;

        public PitchClass Tonic { get; }

        public Scale(PitchClass tonic, IReadOnlyList<int>? semitonePattern)
        {
            if (semitonePattern == null)
            {
                throw new ArgumentNullException(nameof(semitonePattern));
            }

            Tonic = tonic;
            int mask = 0;
            foreach (int semitone in semitonePattern)
            {
                mask |= 1 << ((((int)tonic + semitone) % 12 + 12) % 12);
            }

            _patternMask = mask;
        }

        /// <summary>The major scale on <paramref name="tonic"/>.</summary>
        public static Scale Major(PitchClass tonic) => new Scale(tonic, MajorPattern);

        /// <summary>C major — the only key Jazztures uses (§1.3).</summary>
        public static Scale CIonian => Major(PitchClass.C);

        public bool Contains(PitchClass pitchClass) => (_patternMask & (1 << (int)pitchClass)) != 0;

        public bool Contains(Pitch pitch) => Contains(pitch.Class);

        /// <summary>The scale's pitch classes, ascending from C (not from the tonic).</summary>
        public IEnumerable<PitchClass> PitchClasses
        {
            get
            {
                for (int pc = 0; pc < 12; pc++)
                {
                    if ((_patternMask & (1 << pc)) != 0)
                    {
                        yield return (PitchClass)pc;
                    }
                }
            }
        }

        public bool Equals(Scale other) => Tonic == other.Tonic && _patternMask == other._patternMask;

        public override bool Equals(object? obj) => obj is Scale other && Equals(other);

        public override int GetHashCode() => ((int)Tonic * 397) ^ _patternMask;

        public override string ToString() => $"{PitchNames.Of(Tonic)} scale";

        public static bool operator ==(Scale left, Scale right) => left.Equals(right);

        public static bool operator !=(Scale left, Scale right) => !left.Equals(right);
    }
}
