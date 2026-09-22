using System;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// The parameters <see cref="ChordPinchDetector"/> uses (Design A, ADR-0038). Kept
    /// separate from <see cref="GestureThresholds"/> — the strike path's own threshold set
    /// — rather than added to it, so the ADR-0025/-0027/-0030 strike/release parameters
    /// stay untouched regardless of which articulation mechanism is active. All
    /// `[TUNABLE]`; the Unity <c>Config/HarmonicFieldConfig.asset</c> mirrors these.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct PinchCommitThresholds
    {
        public PinchCommitThresholds(
            double minInterPinchSeconds,
            float pinchRateAtMinVelocityPerSecond,
            float pinchRateAtMaxVelocityPerSecond)
        {
            Require(minInterPinchSeconds >= 0.0, nameof(minInterPinchSeconds));
            Require(pinchRateAtMinVelocityPerSecond >= 0f, nameof(pinchRateAtMinVelocityPerSecond));
            Require(
                pinchRateAtMaxVelocityPerSecond > pinchRateAtMinVelocityPerSecond,
                nameof(pinchRateAtMaxVelocityPerSecond));

            MinInterPinchSeconds = minInterPinchSeconds;
            PinchRateAtMinVelocityPerSecond = pinchRateAtMinVelocityPerSecond;
            PinchRateAtMaxVelocityPerSecond = pinchRateAtMaxVelocityPerSecond;
        }

        /// <summary>
        /// Minimum gap between two pinch articulations — a retrigger cooldown, the pinch
        /// counterpart of <see cref="GestureThresholds.MinInterStrikeSeconds"/>. Default
        /// 120 ms (the same value, inherited rather than re-guessed).
        /// </summary>
        public double MinInterPinchSeconds { get; }

        /// <summary>Pinch-closing rate (per second) mapping to <see cref="Melody.VelocityCurve.MinVelocity"/>.</summary>
        public float PinchRateAtMinVelocityPerSecond { get; }

        /// <summary>Pinch-closing rate (per second) mapping to <see cref="Melody.VelocityCurve.MaxVelocity"/>.</summary>
        public float PinchRateAtMaxVelocityPerSecond { get; }

        /// <summary>
        /// The ADR-0038 defaults. <see cref="MinInterPinchSeconds"/> is inherited from
        /// <see cref="GestureThresholds.MinInterStrikeSeconds"/>'s value; the two rate
        /// bounds are pure engineering guesses — nobody has measured how fast a pinch
        /// closes on this tracker. Pilot-calibrate at M8.
        /// </summary>
        public static PinchCommitThresholds Default => new PinchCommitThresholds(
            minInterPinchSeconds: 0.12,
            pinchRateAtMinVelocityPerSecond: 3.0f,
            pinchRateAtMaxVelocityPerSecond: 14.0f);

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
