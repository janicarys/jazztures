using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// A short ring buffer of timestamped hand state, recorded every frame (ADR-0078). At a crossing the
    /// melody reads the state from a little <i>before</i> it, because a fast swing drags the hand's tilt (and
    /// its height) with it. Fixed size, allocation-free; the oldest sample is overwritten when full.
    /// </summary>
    public sealed class DirectionHistory
    {
        public const int DefaultCapacity = 64;

        private readonly double[] _times;
        private readonly HandSnapshot[] _snapshots;
        private int _next;
        private int _count;

        public DirectionHistory(int capacity = DefaultCapacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Must be >= 1.");
            }

            _times = new double[capacity];
            _snapshots = new HandSnapshot[capacity];
        }

        public int Count => _count;

        public void Record(double time, StepDirection direction, int zone = 0)
        {
            _times[_next] = time;
            _snapshots[_next] = new HandSnapshot(direction, zone);
            _next = (_next + 1) % _times.Length;
            if (_count < _times.Length)
            {
                _count++;
            }
        }

        /// <summary>
        /// The state in effect at <paramref name="time"/>: the latest sample at or before it. Before the oldest
        /// sample this is the oldest; after the newest, the newest; with nothing recorded, <see cref="StepDirection.Same"/>.
        /// Samples are assumed to be recorded in time order.
        /// </summary>
        public HandSnapshot At(double time)
        {
            if (_count == 0)
            {
                return new HandSnapshot(StepDirection.Same, 0);
            }

            int oldest = (_next - _count + _times.Length) % _times.Length;
            int chosen = oldest;
            for (int i = 0; i < _count; i++)
            {
                int index = (oldest + i) % _times.Length;
                if (_times[index] > time)
                {
                    break;
                }

                chosen = index;
            }

            return _snapshots[chosen];
        }

        public void Clear()
        {
            _next = 0;
            _count = 0;
        }
    }
}
