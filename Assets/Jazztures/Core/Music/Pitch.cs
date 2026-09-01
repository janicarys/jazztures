using System;

namespace Jazztures.Core.Music
{
    /// <summary>
    /// A single sounding pitch, identified by its MIDI note number (middle C = C4 = 60,
    /// CLAUDE.md §3.1). Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct Pitch : IEquatable<Pitch>, IComparable<Pitch>
    {
        /// <summary>Lowest representable MIDI note (C-1).</summary>
        public const int MinMidi = 0;

        /// <summary>Highest representable MIDI note (G9).</summary>
        public const int MaxMidi = 127;

        /// <summary>MIDI note number, <see cref="MinMidi"/>..<see cref="MaxMidi"/>.</summary>
        public int Midi { get; }

        public Pitch(int midi)
        {
            if (midi is < MinMidi or > MaxMidi)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(midi), midi, $"MIDI note must be {MinMidi}..{MaxMidi}.");
            }

            Midi = midi;
        }

        /// <summary>Middle C, C4, MIDI 60.</summary>
        public static Pitch MiddleC => new Pitch(60);

        /// <summary>The pitch class, ignoring octave.</summary>
        public PitchClass Class => (PitchClass)(Midi % 12);

        /// <summary>
        /// Scientific-pitch-notation octave number: C4 is octave 4, so MIDI 60..71 are
        /// octave 4 and MIDI 0..11 are octave -1.
        /// </summary>
        public int Octave => Midi / 12 - 1;

        /// <summary>
        /// The pitch <paramref name="semitones"/> away. Throws if the result leaves the
        /// MIDI range — callers doing register maths that may overflow should use
        /// <see cref="TryTranspose"/>.
        /// </summary>
        public Pitch Transpose(int semitones) => new Pitch(Midi + semitones);

        /// <summary>
        /// Non-throwing <see cref="Transpose"/>: returns false and leaves
        /// <paramref name="result"/> as <c>default</c> if the target is out of range.
        /// </summary>
        public bool TryTranspose(int semitones, out Pitch result)
        {
            int target = Midi + semitones;
            if (target is < MinMidi or > MaxMidi)
            {
                result = default;
                return false;
            }

            result = new Pitch(target);
            return true;
        }

        /// <summary>
        /// The lowest pitch at or above <paramref name="floorMidi"/> whose pitch class is
        /// <paramref name="pitchClass"/>. Throws if no such pitch fits below
        /// <see cref="MaxMidi"/>.
        /// </summary>
        public static Pitch LowestAtOrAbove(PitchClass pitchClass, int floorMidi)
        {
            if (floorMidi is < MinMidi or > MaxMidi)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(floorMidi), floorMidi, $"Floor must be {MinMidi}..{MaxMidi}.");
            }

            int offset = (((int)pitchClass - floorMidi) % 12 + 12) % 12;
            int midi = floorMidi + offset;
            if (midi > MaxMidi)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(floorMidi), floorMidi,
                    $"No {pitchClass} exists at or above MIDI {floorMidi}.");
            }

            return new Pitch(midi);
        }

        /// <summary>
        /// Parses scientific pitch notation — a letter A–G, optional accidentals
        /// (<c>#</c>/<c>b</c>, repeatable), then an octave integer that may be negative,
        /// e.g. <c>"C4"</c>, <c>"D#4"</c>, <c>"A0"</c>, <c>"C-1"</c>. Sharps and flats
        /// are accepted on input though <see cref="ToString"/> only emits sharps.
        /// Returns false (and <c>default</c>) on anything malformed or out of MIDI range.
        /// </summary>
        public static bool TryParse(string? text, out Pitch pitch)
        {
            pitch = default;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            int index = 0;
            int semitone = char.ToUpperInvariant(text![index]) switch
            {
                'C' => 0,
                'D' => 2,
                'E' => 4,
                'F' => 5,
                'G' => 7,
                'A' => 9,
                'B' => 11,
                _ => -1,
            };
            if (semitone < 0)
            {
                return false;
            }

            index++;
            while (index < text.Length && (text[index] == '#' || text[index] == 'b'
                || text[index] == '♯' || text[index] == '♭'))
            {
                semitone += text[index] == '#' || text[index] == '♯' ? 1 : -1;
                index++;
            }

            if (index >= text.Length || !int.TryParse(text.Substring(index), out int octave))
            {
                return false;
            }

            int midi = 12 * (octave + 1) + semitone;
            if (midi is < MinMidi or > MaxMidi)
            {
                return false;
            }

            pitch = new Pitch(midi);
            return true;
        }

        public int CompareTo(Pitch other) => Midi.CompareTo(other.Midi);

        public bool Equals(Pitch other) => Midi == other.Midi;

        public override bool Equals(object? obj) => obj is Pitch other && Equals(other);

        public override int GetHashCode() => Midi;

        public override string ToString() => $"{PitchNames.Of(Class)}{Octave}";

        public static bool operator ==(Pitch left, Pitch right) => left.Equals(right);

        public static bool operator !=(Pitch left, Pitch right) => !left.Equals(right);

        public static bool operator <(Pitch left, Pitch right) => left.Midi < right.Midi;

        public static bool operator >(Pitch left, Pitch right) => left.Midi > right.Midi;

        public static bool operator <=(Pitch left, Pitch right) => left.Midi <= right.Midi;

        public static bool operator >=(Pitch left, Pitch right) => left.Midi >= right.Midi;
    }
}
