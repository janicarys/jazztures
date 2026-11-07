namespace Jazztures.Core.Melody
{
    /// <summary>How one drill event scored (ADR-0078). Logged as <c>result</c>.</summary>
    public enum DrillResult
    {
        Hit = 0,
        WrongDirection = 1,
        Miss = 2,

        /// <summary>A crossing that matched no target: a double-trigger, or a swing off the beat.</summary>
        Extra = 3,

        /// <summary>The gate was closed for a while: an interval, not a single moment.</summary>
        TrackingLost = 4,
    }
}
