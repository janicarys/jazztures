namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Where <see cref="LazyRecenter"/> is in its follow cycle (ADR-0015).
    /// </summary>
    public enum RecenterPhase
    {
        /// <summary>The committed facing is holding; head yaw is within the dead zone.</summary>
        Settled = 0,

        /// <summary>Head yaw is past the threshold, waiting out the dwell before following.</summary>
        Pending = 1,

        /// <summary>Easing the committed facing toward the head yaw.</summary>
        Following = 2,
    }
}
