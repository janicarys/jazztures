namespace Jazztures.Core.Ports
{
    /// <summary>
    /// How much to trust the current hand-tracking data (CLAUDE.md §3.5). Occlusion —
    /// fingers overlapping, one hand crossing the other, a hand leaving the FOV — is the
    /// known failure mode, and the system must degrade gracefully, not glitch.
    /// </summary>
    public enum TrackingQuality
    {
        /// <summary>Hand not tracked at all.</summary>
        NotTracked = 0,

        /// <summary>Tracked but unreliable — suppress gesture transitions (§3.5.3).</summary>
        Low = 1,

        /// <summary>Tracked and usable, but not clean enough to resume after a loss.</summary>
        Medium = 2,

        /// <summary>Clean tracking.</summary>
        High = 3,
    }
}
