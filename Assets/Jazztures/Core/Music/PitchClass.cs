namespace Jazztures.Core.Music
{
    /// <summary>
    /// One of the twelve pitch classes, octave-independent. Value is the number of
    /// semitones above C, so <c>(int)PitchClass.C == 0</c> and it lines up with
    /// <c>Pitch.Midi % 12</c>.
    /// </summary>
    /// <remarks>
    /// Jazztures is single-key C major (CLAUDE.md §1.3), so only the naturals are ever
    /// named in lesson content — but the full chromatic set is kept because chord-tone
    /// and voicing maths needs every semitone (e.g. the B in G7, the E in Dm7's 9th).
    /// Spelling is fixed to sharps; this system never distinguishes F# from Gb.
    /// </remarks>
    public enum PitchClass
    {
        C = 0,
        CSharp = 1,
        D = 2,
        DSharp = 3,
        E = 4,
        F = 5,
        FSharp = 6,
        G = 7,
        GSharp = 8,
        A = 9,
        ASharp = 10,
        B = 11,
    }
}
