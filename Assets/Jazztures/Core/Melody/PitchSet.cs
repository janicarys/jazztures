using System;
using System.Collections.Generic;
using Jazztures.Core.Music;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The pitches the palm-swing cursor steps through: an ascending list across a fixed range (ADR-0078).
    /// The range does not move with the chord, so a chord change re-pitches the positions but the melody
    /// stays in the same register. Immutable once built; built on a chord change, never per frame.
    /// `[OPEN]`: whether chord tones or the C major scale is the right set is undecided.
    /// </summary>
    public sealed class PitchSet
    {
        private readonly Pitch[] _pitches;

        private PitchSet(Pitch[] pitches, Chord chord, int lowestMidi, int highestMidi)
        {
            _pitches = pitches;
            Chord = chord;
            LowestMidi = lowestMidi;
            HighestMidi = highestMidi;
        }

        /// <summary>The chord this set was built for (the root the cursor resets to).</summary>
        public Chord Chord { get; }

        /// <summary>Bottom and top of the range (inclusive), whether or not a pitch sits exactly there.</summary>
        public int LowestMidi { get; }

        public int HighestMidi { get; }

        public int Count => _pitches.Length;

        public Pitch this[int index] => _pitches[index];

        public static PitchSet Build(PitchSetKind kind, Chord chord, int lowestMidi, int spanSemitones)
        {
            if (spanSemitones < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(spanSemitones), spanSemitones, "Must be >= 1.");
            }

            int highest = Math.Min(Pitch.MaxMidi, lowestMidi + spanSemitones);
            var list = new List<Pitch>();
            for (int midi = Math.Max(Pitch.MinMidi, lowestMidi); midi <= highest; midi++)
            {
                var pitch = new Pitch(midi);
                if (Belongs(kind, chord, pitch))
                {
                    list.Add(pitch);
                }
            }

            if (list.Count == 0)
            {
                throw new ArgumentException("The range holds no pitch of the set.");
            }

            return new PitchSet(list.ToArray(), chord, lowestMidi, highest);
        }

        public static PitchSet ChordTones(Chord chord, int lowestMidi, int spanSemitones) =>
            Build(PitchSetKind.ChordTones, chord, lowestMidi, spanSemitones);

        public static PitchSet CMajor(Chord chord, int lowestMidi, int spanSemitones) =>
            Build(PitchSetKind.CMajorScale, chord, lowestMidi, spanSemitones);

        /// <summary>
        /// The index of the member nearest <paramref name="midi"/>; of two equally near, the lower one.
        /// </summary>
        public int IndexOfNearest(int midi)
        {
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < _pitches.Length; i++)
            {
                int distance = Math.Abs(_pitches[i].Midi - midi);
                if (distance < bestDistance) // strictly: ascending order means the lower one wins a tie
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public int IndexOfNearest(Pitch pitch) => IndexOfNearest(pitch.Midi);

        /// <summary>The chord's root nearest the middle of the range (the lower one on a tie).</summary>
        public int IndexOfRootNearMiddle()
        {
            double middle = (LowestMidi + HighestMidi) * 0.5;
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < _pitches.Length; i++)
            {
                if (_pitches[i].Class != Chord.Root)
                {
                    continue;
                }

                double distance = Math.Abs(_pitches[i].Midi - middle);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            // A range too narrow to hold the root: fall back to the middle position.
            return best >= 0 ? best : _pitches.Length / 2;
        }

        internal static bool Belongs(PitchSetKind kind, Chord chord, Pitch pitch)
        {
            if (kind == PitchSetKind.CMajorScale)
            {
                return Scale.CIonian.Contains(pitch);
            }

            foreach (ScaleDegree degree in ChordToneSet.Degrees)
            {
                if (chord.ClassOf(degree) == pitch.Class)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
