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
            double trackingLossCueSeconds,
            int confirmationMissTolerance = 2,
            float strikeEnterSpeedMetresPerSecond = 0.25f,
            float strikeExitSpeedMetresPerSecond = 0.10f,
            double minInterStrikeSeconds = 0.12,
            double releaseHoldSeconds = 0.400,
            int releaseMissTolerance = 6,
            int strikeSettleFrames = 3)
        {
            Require(poseHoldSeconds >= 0.0, nameof(poseHoldSeconds));
            Require(confirmingFrames >= 1, nameof(confirmingFrames));
            Require(minInterChordSeconds >= 0.0, nameof(minInterChordSeconds));
            Require(highFramesToResumeAfterLoss >= 1, nameof(highFramesToResumeAfterLoss));
            Require(trackingLossCueSeconds >= 0.0, nameof(trackingLossCueSeconds));
            Require(confirmationMissTolerance >= 0, nameof(confirmationMissTolerance));
            Require(strikeExitSpeedMetresPerSecond >= 0f, nameof(strikeExitSpeedMetresPerSecond));
            Require(
                strikeEnterSpeedMetresPerSecond > strikeExitSpeedMetresPerSecond,
                nameof(strikeEnterSpeedMetresPerSecond));
            Require(minInterStrikeSeconds >= 0.0, nameof(minInterStrikeSeconds));
            Require(releaseHoldSeconds >= poseHoldSeconds, nameof(releaseHoldSeconds));
            Require(releaseMissTolerance >= confirmationMissTolerance, nameof(releaseMissTolerance));
            Require(strikeSettleFrames >= 1, nameof(strikeSettleFrames));

            PoseHoldSeconds = poseHoldSeconds;
            ConfirmingFrames = confirmingFrames;
            MinInterChordSeconds = minInterChordSeconds;
            HighFramesToResumeAfterLoss = highFramesToResumeAfterLoss;
            TrackingLossCueSeconds = trackingLossCueSeconds;
            ConfirmationMissTolerance = confirmationMissTolerance;
            StrikeEnterSpeedMetresPerSecond = strikeEnterSpeedMetresPerSecond;
            StrikeExitSpeedMetresPerSecond = strikeExitSpeedMetresPerSecond;
            MinInterStrikeSeconds = minInterStrikeSeconds;
            ReleaseHoldSeconds = releaseHoldSeconds;
            ReleaseMissTolerance = releaseMissTolerance;
            StrikeSettleFrames = strikeSettleFrames;
        }

        /// <summary>How long a pose must be held before it can confirm. Default 150 ms.</summary>
        public double PoseHoldSeconds { get; }

        /// <summary>
        /// Frames matching the pending pose needed, within <see cref="PoseHoldSeconds"/>
        /// and tolerating up to <see cref="ConfirmationMissTolerance"/> consecutive
        /// non-matching frames along the way, before it can confirm. Default 3.
        /// </summary>
        public int ConfirmingFrames { get; }

        /// <summary>Minimum gap between two confirmed chord changes (debounce). Default 100 ms.</summary>
        public double MinInterChordSeconds { get; }

        /// <summary>Consecutive High-quality frames required to resume input after a tracking loss. Default 3.</summary>
        public int HighFramesToResumeAfterLoss { get; }

        /// <summary>How long tracking must be lost before the non-modal visual cue shows. Default 200 ms.</summary>
        public double TrackingLossCueSeconds { get; }

        /// <summary>
        /// Consecutive frames that do not match the pose being confirmed — a single-frame
        /// tracking blip, an <see cref="Jazztures.Core.Ports.HandPoseCandidate.Ambiguous"/> reading mid-rotation,
        /// or a momentary read of the already-confirmed pose — that are tolerated without
        /// restarting <see cref="PoseHoldSeconds"/> / <see cref="ConfirmingFrames"/>
        /// (ADR-0025). Exceeding this many in a row means the hand has genuinely moved on.
        /// Default 2.
        /// </summary>
        public int ConfirmationMissTolerance { get; }

        /// <summary>
        /// Downward hand speed that starts a chord strike (ADR-0025;
        /// <see cref="Jazztures.Core.Gesture.ChordStrikeDetector"/>). Schmitt-trigger enter
        /// edge — must exceed <see cref="StrikeExitSpeedMetresPerSecond"/>. Default 0.25 m/s.
        /// </summary>
        public float StrikeEnterSpeedMetresPerSecond { get; }

        /// <summary>
        /// The hand must decelerate at or below this speed before another strike can arm.
        /// Schmitt-trigger exit edge, narrower than the enter edge is wide — must be less
        /// than <see cref="StrikeEnterSpeedMetresPerSecond"/>. Default 0.10 m/s.
        /// </summary>
        public float StrikeExitSpeedMetresPerSecond { get; }

        /// <summary>Minimum gap between two strikes — a retrigger cooldown. Default 120 ms.</summary>
        public double MinInterStrikeSeconds { get; }

        /// <summary>
        /// How long a pose must read <see cref="Jazztures.Core.Ports.HandPoseCandidate.None"/>
        /// before it can confirm a <b>release</b> from an already-confirmed function — always
        /// at least <see cref="PoseHoldSeconds"/> (ADR-0027). Deliberately more conservative
        /// than confirming a pose: a false release is audible (it cuts the sounding chord,
        /// §3.2) and a real strike's motion can itself disrupt the pose reading for a
        /// stretch longer than ordinary tracking noise — see <see cref="ReleaseMissTolerance"/>.
        /// A slow release is nearly unnoticeable by comparison, per §1.4's "silence is a
        /// recoverable error; a wrong chord is not." Default 400 ms.
        /// </summary>
        public double ReleaseHoldSeconds { get; }

        /// <summary>
        /// The release counterpart of <see cref="ConfirmationMissTolerance"/> — always at
        /// least as large (ADR-0027). Default 6.
        /// </summary>
        public int ReleaseMissTolerance { get; }

        /// <summary>
        /// Consecutive frames at or below <see cref="StrikeExitSpeedMetresPerSecond"/>
        /// needed before a strike re-arms (ADR-0030). A single qualifying frame is not
        /// enough: a real strike's deceleration often includes a brief rebound/settle
        /// wobble, and one frame dipping below the exit threshold mid-wobble would re-arm
        /// early, letting the wobble's own small secondary motion fire a second,
        /// unintended strike. Default 3.
        /// </summary>
        public int StrikeSettleFrames { get; }

        /// <summary>The §3.4 / §3.5 / ADR-0025 / ADR-0027 / ADR-0029 / ADR-0030 engineering defaults. Pilot-calibrated at M8.</summary>
        public static GestureThresholds Default => new GestureThresholds(
            poseHoldSeconds: 0.150,
            confirmingFrames: 3,
            minInterChordSeconds: 0.100,
            highFramesToResumeAfterLoss: 3,
            trackingLossCueSeconds: 0.200,
            confirmationMissTolerance: 2,
            strikeEnterSpeedMetresPerSecond: 0.25f,
            strikeExitSpeedMetresPerSecond: 0.10f,
            minInterStrikeSeconds: 0.12,
            releaseHoldSeconds: 0.400,
            releaseMissTolerance: 6,
            strikeSettleFrames: 3);

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
