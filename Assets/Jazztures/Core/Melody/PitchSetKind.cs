namespace Jazztures.Core.Melody
{
    /// <summary>What the palm-swing cursor steps through. `[OPEN]` (ADR-0078): neither is decided.</summary>
    public enum PitchSetKind
    {
        /// <summary>The active chord's root, 3rd, 5th, 7th and 9th, repeated across the range.</summary>
        ChordTones = 0,

        /// <summary>The C major scale across the same range, whatever the chord.</summary>
        CMajorScale = 1,
    }
}
