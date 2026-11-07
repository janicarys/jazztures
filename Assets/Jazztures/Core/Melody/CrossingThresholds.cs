using System;

namespace Jazztures.Core.Melody
{
    /// <summary>`[TUNABLE]` levels for <see cref="CrossingTrigger"/> (ADR-0078). Metres and seconds.</summary>
    public readonly struct CrossingThresholds
    {
        public const float DefaultDeadzoneMetres = 0.02f;
        public const double DefaultLockoutSeconds = 0.080;

        public CrossingThresholds(float deadzoneMetres, double lockoutSeconds)
        {
            if (deadzoneMetres < 0f || float.IsNaN(deadzoneMetres))
            {
                throw new ArgumentOutOfRangeException(nameof(deadzoneMetres), deadzoneMetres, "Must be >= 0.");
            }

            if (lockoutSeconds < 0.0 || double.IsNaN(lockoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(lockoutSeconds), lockoutSeconds, "Must be >= 0.");
            }

            DeadzoneMetres = deadzoneMetres;
            LockoutSeconds = lockoutSeconds;
        }

        public static CrossingThresholds Default =>
            new CrossingThresholds(DefaultDeadzoneMetres, DefaultLockoutSeconds);

        /// <summary>Total width of the dead band around the trigger line.</summary>
        public float DeadzoneMetres { get; }

        /// <summary>After a crossing, how long before another can sound.</summary>
        public double LockoutSeconds { get; }
    }
}
