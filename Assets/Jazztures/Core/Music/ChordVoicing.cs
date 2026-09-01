using System;
using System.Collections;
using System.Collections.Generic;

namespace Jazztures.Core.Music
{
    /// <summary>
    /// A chord realised as four sounding pitches — the left hand's harmony (CLAUDE.md
    /// §1.3, §3.1). Root / 3rd / 5th / 7th, in ascending pitch order for a close
    /// voicing. Produced by <see cref="Voicing"/>. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct ChordVoicing : IEquatable<ChordVoicing>, IReadOnlyList<Pitch>
    {
        /// <summary>A seventh chord always voices exactly four tones here.</summary>
        public const int ToneCount = 4;

        /// <summary>The chord this is a voicing of.</summary>
        public Chord Chord { get; }

        public Pitch Root { get; }

        public Pitch Third { get; }

        public Pitch Fifth { get; }

        public Pitch Seventh { get; }

        public ChordVoicing(Chord chord, Pitch root, Pitch third, Pitch fifth, Pitch seventh)
        {
            Chord = chord;
            Root = root;
            Third = third;
            Fifth = fifth;
            Seventh = seventh;
        }

        public int Count => ToneCount;

        public Pitch this[int index] => index switch
        {
            0 => Root,
            1 => Third,
            2 => Fifth,
            3 => Seventh,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
        };

        public Pitch Lowest
        {
            get
            {
                Pitch lowest = Root;
                for (int i = 1; i < ToneCount; i++)
                {
                    if (this[i] < lowest)
                    {
                        lowest = this[i];
                    }
                }

                return lowest;
            }
        }

        public Pitch Highest
        {
            get
            {
                Pitch highest = Root;
                for (int i = 1; i < ToneCount; i++)
                {
                    if (this[i] > highest)
                    {
                        highest = this[i];
                    }
                }

                return highest;
            }
        }

        public IEnumerator<Pitch> GetEnumerator()
        {
            yield return Root;
            yield return Third;
            yield return Fifth;
            yield return Seventh;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(ChordVoicing other) =>
            Chord == other.Chord
            && Root == other.Root
            && Third == other.Third
            && Fifth == other.Fifth
            && Seventh == other.Seventh;

        public override bool Equals(object? obj) => obj is ChordVoicing other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Chord.GetHashCode();
                hash = (hash * 397) ^ Root.GetHashCode();
                hash = (hash * 397) ^ Third.GetHashCode();
                hash = (hash * 397) ^ Fifth.GetHashCode();
                hash = (hash * 397) ^ Seventh.GetHashCode();
                return hash;
            }
        }

        public override string ToString() =>
            $"{Chord} [{Root} {Third} {Fifth} {Seventh}]";

        public static bool operator ==(ChordVoicing left, ChordVoicing right) => left.Equals(right);

        public static bool operator !=(ChordVoicing left, ChordVoicing right) => !left.Equals(right);
    }
}
