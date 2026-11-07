using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The swing-slice regions (explore/swing-slices): <c>regionCount</c> bands of <c>regionHeightMetres</c> each,
    /// stacked around the lane origin so the middle one sits on it. Region 0 is the lowest. A height maps to a region;
    /// above the top or below the bottom it clamps to the end region.
    ///
    /// <para>
    /// <see cref="Update"/> adds hysteresis: while the hand stays within <c>hysteresisMetres</c> beyond the edges of the
    /// region it is already in, it keeps that region, so a hand resting on a boundary does not flicker. Beyond that
    /// the plain mapping applies, so a fast move can skip regions. Pure and allocation-free. `[TUNABLE]`: all three numbers.
    /// </para>
    /// </summary>
    public sealed class HeightRegionLayout
    {
        public const int DefaultRegionCount = 9;
        public const float DefaultRegionHeightMetres = 0.045f;
        public const float DefaultHysteresisMetres = 0.010f;

        private readonly float _hysteresis;
        private bool _hasCurrent;
        private int _current;

        public HeightRegionLayout(int regionCount, float regionHeightMetres, float hysteresisMetres)
        {
            if (regionCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(regionCount), regionCount, "Must be >= 1.");
            }

            if (regionHeightMetres <= 0f || float.IsNaN(regionHeightMetres))
            {
                throw new ArgumentOutOfRangeException(nameof(regionHeightMetres), regionHeightMetres, "Must be > 0.");
            }

            if (hysteresisMetres < 0f || float.IsNaN(hysteresisMetres))
            {
                throw new ArgumentOutOfRangeException(nameof(hysteresisMetres), hysteresisMetres, "Must be >= 0.");
            }

            RegionCount = regionCount;
            RegionHeightMetres = regionHeightMetres;
            _hysteresis = hysteresisMetres;
        }

        public int RegionCount { get; }

        public float RegionHeightMetres { get; }

        /// <summary>The region the hand is in now (the middle one until the first <see cref="Update"/>).</summary>
        public int Current => _hasCurrent ? _current : (RegionCount - 1) / 2;

        /// <summary>Plain mapping, no hysteresis, clamped to the end regions.</summary>
        public int RegionOf(float heightMetres)
        {
            if (float.IsNaN(heightMetres))
            {
                return Current;
            }

            double index = Math.Floor(heightMetres / RegionHeightMetres + RegionCount * 0.5);
            return (int)Math.Max(0.0, Math.Min(RegionCount - 1, index));
        }

        /// <summary>Height of the middle of <paramref name="region"/> above the lane origin, metres.</summary>
        public float CentreOf(int region) => (region - (RegionCount - 1) * 0.5f) * RegionHeightMetres;

        /// <summary>Height of the lower edge of <paramref name="region"/> (0 .. count) above the lane origin, metres.</summary>
        public float EdgeOf(int boundary) => (boundary - RegionCount * 0.5f) * RegionHeightMetres;

        /// <summary>Feed the hand's height; returns the region it is in, with hysteresis.</summary>
        public int Update(float heightMetres)
        {
            if (float.IsNaN(heightMetres))
            {
                return Current;
            }

            if (_hasCurrent)
            {
                // The end regions are open-ended, so only their inner edge holds the hand.
                float lower = _current == 0 ? float.NegativeInfinity : EdgeOf(_current) - _hysteresis;
                float upper = _current == RegionCount - 1 ? float.PositiveInfinity : EdgeOf(_current + 1) + _hysteresis;
                if (heightMetres >= lower && heightMetres < upper)
                {
                    return _current;
                }
            }

            _current = RegionOf(heightMetres);
            _hasCurrent = true;
            return _current;
        }

        /// <summary>Forget the current region (the gate closed).</summary>
        public void Reset() => _hasCurrent = false;
    }
}
