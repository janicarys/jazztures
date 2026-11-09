#nullable enable
using System;
using Jazztures.Core.Music;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// A lesson's own answer to "which pitch does each height region play" (ADR-0081). Absent, the swing-slices default applies:
    /// the active chord's tones, retuned on every chord change. Present, the regions are fixed notes that do not follow the
    /// chord (the Swing lesson's C major scale: stepwise runs that fit all three chords). Immutable.
    /// </summary>
    public sealed class LessonPitchSet
    {
        private readonly int[] _fixed;

        private LessonPitchSet(PitchSetKind kind, int lowestMidi, int regionCount, int[] fixedPitches)
        {
            Kind = kind;
            LowestMidi = lowestMidi;
            RegionCount = regionCount;
            _fixed = fixedPitches;
        }

        /// <summary>ChordTones or CMajorScale; with a fixed list, CMajorScale is only a label.</summary>
        public PitchSetKind Kind { get; }

        public int LowestMidi { get; }

        public int RegionCount { get; }

        public bool IsFixedList => _fixed.Length > 0;

        public int FixedCount => _fixed.Length;

        public int FixedPitch(int region) => _fixed[region];

        /// <summary>Regions are the C major scale ascending from <paramref name="lowestMidi"/>, whatever the chord.</summary>
        public static LessonPitchSet CMajorScale(int lowestMidi, int regionCount)
        {
            if (regionCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(regionCount), regionCount, "Must be >= 1.");
            }

            return new LessonPitchSet(PitchSetKind.CMajorScale, lowestMidi, regionCount, Array.Empty<int>());
        }

        /// <summary>Regions are exactly these pitches, bottom to top, whatever the chord.</summary>
        public static LessonPitchSet Fixed(params int[] pitches)
        {
            if (pitches == null || pitches.Length == 0)
            {
                throw new ArgumentException("A fixed pitch set needs at least one pitch.", nameof(pitches));
            }

            var copy = (int[])pitches.Clone();
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i] < Pitch.MinMidi || copy[i] > Pitch.MaxMidi)
                {
                    throw new ArgumentOutOfRangeException(nameof(pitches), copy[i], "Not a MIDI pitch.");
                }

                if (i > 0 && copy[i] <= copy[i - 1])
                {
                    throw new ArgumentException("Pitches must ascend: height is pitch.", nameof(pitches));
                }
            }

            return new LessonPitchSet(PitchSetKind.CMajorScale, copy[0], copy.Length, copy);
        }

        /// <summary>The region pitches. They do not depend on the chord, which is only needed to satisfy the set's shape.</summary>
        public RegionPitchSet Build(Chord chord) => IsFixedList
            ? RegionPitchSet.FromPitches(_fixed, chord)
            : RegionPitchSet.Build(Kind, chord, LowestMidi, RegionCount);
    }
}
