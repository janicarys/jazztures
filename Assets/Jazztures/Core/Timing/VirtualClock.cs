using System;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Timing
{
    /// <summary>
    /// A hand-driven <see cref="IMusicalClock"/> for deterministic tests. Time only moves
    /// when the test moves it, and only forward.
    /// </summary>
    public sealed class VirtualClock : IMusicalClock
    {
        public VirtualClock(double start = 0.0)
        {
            if (double.IsNaN(start) || double.IsInfinity(start))
            {
                throw new ArgumentOutOfRangeException(nameof(start), start, "Must be finite.");
            }

            Now = start;
        }

        public double Now { get; private set; }

        /// <summary>Move time forward by <paramref name="seconds"/> (must be &gt;= 0).</summary>
        public void Advance(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seconds), seconds, "Must be finite and non-negative.");
            }

            Now += seconds;
        }

        /// <summary>Jump to an absolute time at or after the current <see cref="Now"/>.</summary>
        public void SetNow(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time) || time < Now)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(time), time, "Clock is monotonic — time must not go backwards.");
            }

            Now = time;
        }
    }
}
