using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Turns the palm's height (metres above the lane origin) into a register zone, 0 = lowest, with
    /// hysteresis (ADR-0078). The zones are equal bands centred on the origin, so for three the boundaries
    /// sit at ±half a zone height. Moving up into a zone needs the height to pass its lower boundary; moving
    /// back down needs it to fall hysteresis below that boundary. Pure, allocation-free.
    /// </summary>
    public sealed class RegisterZoneClassifier
    {
        private readonly RegisterZoneThresholds _thresholds;

        public RegisterZoneClassifier(RegisterZoneThresholds thresholds)
        {
            _thresholds = thresholds;
            Current = thresholds.MiddleZone;
        }

        public int Current { get; private set; }

        public int Update(float heightMetres)
        {
            while (Current < _thresholds.ZoneCount - 1 && heightMetres >= Boundary(Current + 1))
            {
                Current++;
            }

            while (Current > 0 && heightMetres < Boundary(Current) - _thresholds.HysteresisMetres)
            {
                Current--;
            }

            return Current;
        }

        public void Reset() => Current = _thresholds.MiddleZone;

        // The lower edge of zone `zone` (1 .. count-1).
        private float Boundary(int zone) => (zone - _thresholds.ZoneCount * 0.5f) * _thresholds.ZoneHeightMetres;
    }
}
