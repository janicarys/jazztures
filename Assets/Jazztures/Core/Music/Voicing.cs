using System;

namespace Jazztures.Core.Music
{
    /// <summary>
    /// Turns a <see cref="Chord"/> into a <see cref="ChordVoicing"/> for the left hand.
    ///
    /// Close voicing: the root is placed in a low register, then the 3rd, 5th and 7th
    /// are each the nearest pitch of that chord tone <i>above</i> the previous one, so
    /// the four tones sit within an octave. Separating harmony (low) from melody (high)
    /// in the spectrum is the point — it lets a novice hear their own melodic line
    /// (CLAUDE.md §3.1).
    /// </summary>
    public static class Voicing
    {
        /// <summary>
        /// Lowest MIDI note the root may take, inclusive. `[TUNABLE]` (CLAUDE.md §3.1) —
        /// mirror any change in <c>Docs/CALIBRATION.md</c> and, from M8, drive it from
        /// the Config asset via <see cref="Close(Chord,int,int)"/> rather than editing
        /// this constant.
        /// </summary>
        public const int DefaultRootFloorMidi = 48;

        /// <summary>Highest MIDI note the root may take, inclusive. `[TUNABLE]` (§3.1).</summary>
        public const int DefaultRootCeilingMidi = 60;

        /// <summary>Close voicing using the §3.1 default root register (MIDI 48..60).</summary>
        public static ChordVoicing Close(Chord chord) =>
            Close(chord, DefaultRootFloorMidi, DefaultRootCeilingMidi);

        /// <summary>
        /// Close voicing with an explicit root register. The root is the lowest pitch of
        /// the chord's root class at or above <paramref name="rootFloorMidi"/>; it must
        /// land at or below <paramref name="rootCeilingMidi"/> or the register is too
        /// narrow for this chord and an exception is thrown.
        /// </summary>
        public static ChordVoicing Close(Chord chord, int rootFloorMidi, int rootCeilingMidi)
        {
            if (rootFloorMidi > rootCeilingMidi)
            {
                throw new ArgumentException(
                    $"Root register is inverted: floor {rootFloorMidi} > ceiling {rootCeilingMidi}.");
            }

            Pitch root = Pitch.LowestAtOrAbove(chord.Root, rootFloorMidi);
            if (root.Midi > rootCeilingMidi)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rootCeilingMidi), rootCeilingMidi,
                    $"No {chord.Root} root fits in MIDI {rootFloorMidi}..{rootCeilingMidi} for {chord}.");
            }

            Pitch third = NextAbove(root, chord.ClassOf(ScaleDegree.Third));
            Pitch fifth = NextAbove(third, chord.ClassOf(ScaleDegree.Fifth));
            Pitch seventh = NextAbove(fifth, chord.ClassOf(ScaleDegree.Seventh));

            return new ChordVoicing(chord, root, third, fifth, seventh);
        }

        /// <summary>
        /// The lowest pitch of class <paramref name="pitchClass"/> strictly above
        /// <paramref name="below"/>. Used to stack a close voicing.
        /// </summary>
        private static Pitch NextAbove(Pitch below, PitchClass pitchClass)
        {
            int delta = (((int)pitchClass - (below.Midi + 1)) % 12 + 12) % 12;
            return new Pitch(below.Midi + 1 + delta);
        }
    }
}
