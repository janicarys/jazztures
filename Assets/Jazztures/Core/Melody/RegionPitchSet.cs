using System;
using Jazztures.Core.Music;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Which pitch each height region plays for the active chord (explore/swing-slices). Region <i>k</i> is the
    /// <i>k</i>-th member, ascending, of the chosen set at or above <c>lowPitch</c>: by default the chord's tones
    /// (root, 3rd, 5th, 7th, 9th) repeated upward, so a higher hand is always a higher pitch. The regions stay at the
    /// same heights; a chord change builds a new set. `[OPEN]`: chord tones or the C major scale
    /// (<see cref="PitchSetKind"/>) - not decided. Immutable; built on a chord change, never per frame.
    /// </summary>
    public sealed class RegionPitchSet
    {
        private readonly Pitch[] _pitches;

        private RegionPitchSet(Pitch[] pitches, Chord chord)
        {
            _pitches = pitches;
            Chord = chord;
        }

        public Chord Chord { get; }

        public int Count => _pitches.Length;

        public Pitch this[int region] => _pitches[region];

        public static RegionPitchSet Build(PitchSetKind kind, Chord chord, int lowPitch, int regionCount)
        {
            if (regionCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(regionCount), regionCount, "Must be >= 1.");
            }

            var pitches = new Pitch[regionCount];
            int found = 0;
            for (int midi = Math.Max(Pitch.MinMidi, lowPitch); midi <= Pitch.MaxMidi && found < regionCount; midi++)
            {
                var pitch = new Pitch(midi);
                if (PitchSet.Belongs(kind, chord, pitch))
                {
                    pitches[found++] = pitch;
                }
            }

            if (found < regionCount)
            {
                throw new ArgumentException("Not enough pitches above the low pitch to fill every region.");
            }

            return new RegionPitchSet(pitches, chord);
        }

        /// <summary>The region that plays exactly <paramref name="pitch"/>, if there is one.</summary>
        public bool TryRegionOf(Pitch pitch, out int region)
        {
            for (int i = 0; i < _pitches.Length; i++)
            {
                if (_pitches[i] == pitch)
                {
                    region = i;
                    return true;
                }
            }

            region = RegionOfNearest(pitch);
            return false;
        }

        /// <summary>The region whose pitch is nearest <paramref name="pitch"/>; the lower on a tie.</summary>
        public int RegionOfNearest(Pitch pitch)
        {
            int best = 0;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < _pitches.Length; i++)
            {
                int distance = Math.Abs(_pitches[i].Midi - pitch.Midi);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }
    }
}
