using Jazztures.Core.Music;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Where a phrase begins (ADR-0078): a MIDI pitch (snapped to the nearest member of the active pitch set),
    /// a position in the set, or — with neither — the active chord's root nearest the middle of the range.
    /// A drill or lesson task supplies one so its pattern always has room: a down-down drill starts high, an
    /// up-up drill low.
    /// </summary>
    public readonly struct PhraseStart
    {
        private PhraseStart(int midi, int index)
        {
            Midi = midi;
            Index = index;
        }

        /// <summary>Neither pitch nor index: use the chord's root near the middle.</summary>
        public static PhraseStart ChordRoot => new PhraseStart(-1, -1);

        public static PhraseStart FromMidi(int midi) => new PhraseStart(midi, -1);

        public static PhraseStart FromIndex(int index) => new PhraseStart(-1, index);

        public static PhraseStart FromPitch(Pitch pitch) => FromMidi(pitch.Midi);

        /// <summary>The MIDI start pitch, or -1 if there is none.</summary>
        public int Midi { get; }

        /// <summary>The start position in the pitch set, or -1 if there is none.</summary>
        public int Index { get; }

        public bool HasMidi => Midi >= 0;

        public bool HasIndex => Index >= 0;
    }
}
