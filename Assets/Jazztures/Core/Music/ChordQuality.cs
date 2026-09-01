namespace Jazztures.Core.Music
{
    /// <summary>
    /// The chord qualities Jazztures uses. The ii-V-I in C major needs exactly these
    /// three (CLAUDE.md §3.1): Dm7, G7, Cmaj7. Do not extend this without a thesis
    /// reason — the vocabulary is deliberately tiny (§1.5, cognitive load).
    /// </summary>
    public enum ChordQuality
    {
        /// <summary>Minor seventh — root, ♭3, 5, ♭7 (e.g. Dm7).</summary>
        Minor7,

        /// <summary>Dominant seventh — root, 3, 5, ♭7 (e.g. G7).</summary>
        Dominant7,

        /// <summary>Major seventh — root, 3, 5, 7 (e.g. Cmaj7).</summary>
        Major7,
    }
}
