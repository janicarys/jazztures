namespace Jazztures.Core.Ports
{
    /// <summary>
    /// The one time source for everything musical (CLAUDE.md §2.4, §3.6). Seconds on the
    /// DSP timeline — the same domain as <c>AudioSettings.dspTime</c>. Never
    /// <c>Time.deltaTime</c>, never frame ordering.
    /// </summary>
    /// <remarks>
    /// Adapters: <c>DspMusicalClock</c> (device audio), <c>VirtualClock</c>
    /// (deterministic tests).
    /// </remarks>
    public interface IMusicalClock
    {
        /// <summary>The current time in seconds. Monotonic non-decreasing.</summary>
        double Now { get; }
    }
}
