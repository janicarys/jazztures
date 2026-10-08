namespace Jazztures.Core.Timing
{
    /// <summary>
    /// What, if anything, the <see cref="Metronome"/> clicks between the quarter-note beats
    /// (ADR-0043). The quarter pulse itself is never swung (§3.6) — only the off-beat click moves.
    /// </summary>
    public enum MetronomeSubdivision
    {
        /// <summary>Quarter-note pulse only.</summary>
        None = 0,

        /// <summary>An extra click halfway between beats (even eighths).</summary>
        Eighths = 1,

        /// <summary>
        /// An extra click on the swung "and" — at the metronome's swing ratio through the
        /// beat, via <see cref="SwingQuantizer"/>.
        /// </summary>
        SwungEighths = 2,
    }
}
