namespace Jazztures.Config
{
    /// <summary>What crosses the palm-swing trigger line (ADR-0078). `[TUNABLE]`.</summary>
    public enum PalmSwingCrossingSource
    {
        /// <summary>The palm centre must physically cross the line in space: the whole hand moves.</summary>
        PalmPosition = 0,

        /// <summary>
        /// A ray along the fingers is cast at a vertical plane in front of the user, and the line is on that
        /// plane: it is where the fingers <i>point</i> that crosses it, so a wrist movement is enough.
        /// </summary>
        FingerRay = 1,
    }
}
