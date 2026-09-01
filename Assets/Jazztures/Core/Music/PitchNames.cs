namespace Jazztures.Core.Music
{
    /// <summary>
    /// The single source of truth for how a <see cref="PitchClass"/> is spelled as text.
    /// Sharps only — this system never distinguishes F♯ from G♭ (see
    /// <see cref="PitchClass"/>). Used by <see cref="Pitch.ToString"/> and
    /// <see cref="Chord.ToString"/>; presentation-layer note labels should route through
    /// here too rather than keeping their own table.
    /// </summary>
    public static class PitchNames
    {
        private static readonly string[] Sharp =
        {
            "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B",
        };

        public static string Of(PitchClass pitchClass) => Sharp[(int)pitchClass];
    }
}
