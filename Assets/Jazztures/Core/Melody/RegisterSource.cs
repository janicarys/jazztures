namespace Jazztures.Core.Melody
{
    /// <summary>Where the palm-swing melody gets its octave from (ADR-0078). `[TUNABLE]`.</summary>
    public enum RegisterSource
    {
        /// <summary>The cursor alone decides the pitch. The default.</summary>
        None = 0,

        /// <summary>The hand's height picks an octave, read between notes only.</summary>
        HandHeightZones = 1,
    }
}
