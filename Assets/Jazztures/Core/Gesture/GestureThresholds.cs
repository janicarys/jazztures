using System;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// The temporal parameters <see cref="GestureInterpreter"/> uses (CLAUDE.md §3.4,
    /// §3.5). All `[TUNABLE]` — the Unity <c>Config/GestureThresholds.asset</c> mirrors
    /// these and produces one of these structs. The SDK-side curl and palm-cone values
    /// live on that same asset but are consumed by the recognisers, not here.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct GestureThresholds
    {
        public GestureThresholds(
            double poseHoldSeconds,
            int confirmingFrames,
            double minInterChordSeconds,
            int highFramesToResumeAfterLoss,
            double trackingLossCueSeconds)
        {
            Require(poseHoldSeconds >= 0.0, nameof(poseHoldSeconds));
            Require(confirmingFrames >= 1, nameof(confirmingFrames));
            Require(minInterChordSeconds >= 0.0, nameof(minInterChordSeconds));
            Require(highFramesToResumeAfterLoss >= 1, nameof(highFramesToResumeAfterLoss));
            Require(trackingLossCueSeconds >= 0.0, nameof(trackingLossCueSeconds));

            PoseHoldSeconds = poseHoldSeconds;
            ConfirmingFrames = confirmingFrames;
            MinInterChordSeconds = minInterChordSeconds;
            HighFramesToResumeAfterLoss = highFramesToResumeAfterLoss;
            TrackingLossCueSeconds = trackingLossCueSeconds;
        }

        /// <summary>How long a pose must be held before it can confirm. Default 120 ms.</summary>
        public double PoseHoldSeconds { get; }

        /// <summary>Consecutive frames matching the pose before it can confirm. Default 3.</summary>
        public int ConfirmingFrames { get; }

        /// <summary>Minimum gap between two confirmed chord changes (debounce). Default 100 ms.</summary>
        public double MinInterChordSeconds { get; }

        /// <summary>Consecutive High-quality frames required to resume input after a tracking loss. Default 3.</summary>
        public int HighFramesToResumeAfterLoss { get; }

        /// <summary>How long tracking must be lost before the non-modal visual cue shows. Default 200 ms.</summary>
        public double TrackingLossCueSeconds { get; }

        /// <summary>The §3.4 / §3.5 engineering defaults. Pilot-calibrated at M8.</summary>
        public static GestureThresholds Default => new GestureThresholds(
            poseHoldSeconds: 0.120,
            confirmingFrames: 3,
            minInterChordSeconds: 0.100,
            highFramesToResumeAfterLoss: 3,
            trackingLossCueSeconds: 0.200);

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
