using System;

namespace Jazztures.Core.Melody
{
    /// <summary>`[TUNABLE]` levels for <see cref="RegisterZoneClassifier"/> (ADR-0078). Metres.</summary>
    public readonly struct RegisterZoneThresholds
    {
        public const int DefaultZoneCount = 3;
        public const float DefaultZoneHeightMetres = 0.12f;
        public const float DefaultHysteresisMetres = 0.03f;

        public RegisterZoneThresholds(int zoneCount, float zoneHeightMetres, float hysteresisMetres)
        {
            if (zoneCount is < 2 or > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(zoneCount), zoneCount, "Must be 2 or 3.");
            }

            if (zoneHeightMetres <= 0f || float.IsNaN(zoneHeightMetres))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneHeightMetres), zoneHeightMetres, "Must be > 0.");
            }

            if (hysteresisMetres < 0f || float.IsNaN(hysteresisMetres))
            {
                throw new ArgumentOutOfRangeException(nameof(hysteresisMetres), hysteresisMetres, "Must be >= 0.");
            }

            ZoneCount = zoneCount;
            ZoneHeightMetres = zoneHeightMetres;
            HysteresisMetres = hysteresisMetres;
        }

        public static RegisterZoneThresholds Default =>
            new RegisterZoneThresholds(DefaultZoneCount, DefaultZoneHeightMetres, DefaultHysteresisMetres);

        public int ZoneCount { get; }

        public float ZoneHeightMetres { get; }

        public float HysteresisMetres { get; }

        /// <summary>The zone with no octave shift: (count - 1) / 2, so 1 of 3 zones, 0 of 2.</summary>
        public int MiddleZone => (ZoneCount - 1) / 2;
    }
}
