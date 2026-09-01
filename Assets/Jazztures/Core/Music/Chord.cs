using System;

namespace Jazztures.Core.Music
{
    /// <summary>
    /// A chord as a harmonic identity: a root pitch class plus a
    /// <see cref="ChordQuality"/>. Octave-free — turning a chord into sounding pitches is
    /// <see cref="Voicing"/> (left hand) or <see cref="ChordToneSet"/> (right hand).
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct Chord : IEquatable<Chord>
    {
        public PitchClass Root { get; }

        public ChordQuality Quality { get; }

        public Chord(PitchClass root, ChordQuality quality)
        {
            Root = root;
            Quality = quality;
        }

        /// <summary>ii of C major.</summary>
        public static Chord Dm7 => new Chord(PitchClass.D, ChordQuality.Minor7);

        /// <summary>V of C major.</summary>
        public static Chord G7 => new Chord(PitchClass.G, ChordQuality.Dominant7);

        /// <summary>I of C major.</summary>
        public static Chord Cmaj7 => new Chord(PitchClass.C, ChordQuality.Major7);

        /// <summary>
        /// Semitones from the root to the given chord tone (CLAUDE.md §3.1). The 9th is
        /// a compound interval (14), not folded into an octave — the right hand's 9th
        /// target really does sit a ninth above the root.
        /// </summary>
        public int SemitoneAbove(ScaleDegree degree) => degree switch
        {
            ScaleDegree.Root => 0,
            ScaleDegree.Third => Quality == ChordQuality.Minor7 ? 3 : 4,
            ScaleDegree.Fifth => 7,
            ScaleDegree.Seventh => Quality == ChordQuality.Major7 ? 11 : 10,
            ScaleDegree.Ninth => 14,
            _ => throw new ArgumentOutOfRangeException(nameof(degree), degree, null),
        };

        /// <summary>The pitch class of the given chord tone.</summary>
        public PitchClass ClassOf(ScaleDegree degree) =>
            (PitchClass)(((int)Root + SemitoneAbove(degree)) % 12);

        public bool Equals(Chord other) => Root == other.Root && Quality == other.Quality;

        public override bool Equals(object? obj) => obj is Chord other && Equals(other);

        public override int GetHashCode() => ((int)Root * 397) ^ (int)Quality;

        public override string ToString()
        {
            string suffix = Quality switch
            {
                ChordQuality.Minor7 => "m7",
                ChordQuality.Dominant7 => "7",
                ChordQuality.Major7 => "maj7",
                _ => Quality.ToString(),
            };
            return PitchNames.Of(Root) + suffix;
        }

        public static bool operator ==(Chord left, Chord right) => left.Equals(right);

        public static bool operator !=(Chord left, Chord right) => !left.Equals(right);
    }
}
