using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The parameters <see cref="LazyRecenter"/> uses to decide when and how fast the
    /// body-anchored touch-target rig follows the head (ADR-0015). All `[TUNABLE]` — the
    /// Unity <c>Config/MelodyConfig.asset</c> mirrors these (in degrees / seconds) and
    /// produces one of these structs. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct LazyRecenterSettings
    {
        public LazyRecenterSettings(
            double angleThresholdRadians,
            double dwellSeconds,
            double easeSeconds)
        {
            Require(angleThresholdRadians > 0.0 && angleThresholdRadians < Math.PI, nameof(angleThresholdRadians));
            Require(dwellSeconds >= 0.0, nameof(dwellSeconds));
            Require(easeSeconds >= 0.0, nameof(easeSeconds));

            AngleThresholdRadians = angleThresholdRadians;
            DwellSeconds = dwellSeconds;
            EaseSeconds = easeSeconds;
        }

        /// <summary>Head-yaw divergence from the committed facing before a recenter begins. Default 35°.</summary>
        public double AngleThresholdRadians { get; }

        /// <summary>How long the divergence must be sustained before easing starts. Default 0.6 s.</summary>
        public double DwellSeconds { get; }

        /// <summary>
        /// Exponential time constant for the ease. Zero snaps in one frame. Default 0.5 s.
        /// </summary>
        public double EaseSeconds { get; }

        /// <summary>The ADR-0015 engineering defaults. Pilot-calibrated at M8.</summary>
        public static LazyRecenterSettings Default => FromDegrees(
            angleThresholdDegrees: 35.0,
            dwellSeconds: 0.6,
            easeSeconds: 0.5);

        /// <summary>Build from a threshold in degrees — the form the Config asset stores.</summary>
        public static LazyRecenterSettings FromDegrees(
            double angleThresholdDegrees,
            double dwellSeconds,
            double easeSeconds) =>
            new LazyRecenterSettings(angleThresholdDegrees * Math.PI / 180.0, dwellSeconds, easeSeconds);

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
