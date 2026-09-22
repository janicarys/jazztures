using System;

namespace Jazztures.Core.Ports
{
    /// <summary>
    /// One frame of hand input as the domain sees it: the left-hand pose match plus the
    /// tracking quality of both hands. The right hand's melody input is touch targets,
    /// not poses, so it contributes only its tracking quality here. Immutable value type
    /// (ADR-0007).
    /// </summary>
    public readonly struct HandPoseFrame : IEquatable<HandPoseFrame>
    {
        public HandPoseFrame(
            HandPoseCandidate leftCandidate,
            TrackingQuality leftTracking,
            TrackingQuality rightTracking,
            float leftVerticalSpeedMetresPerSecond = 0f,
            bool leftIsPinching = false,
            float leftPinchClosingRatePerSecond = 0f,
            bool leftIsTouchingTarget = false,
            float leftTouchEntrySpeedMetresPerSecond = 0f)
        {
            LeftCandidate = leftCandidate;
            LeftTracking = leftTracking;
            RightTracking = rightTracking;
            LeftVerticalSpeedMetresPerSecond = leftVerticalSpeedMetresPerSecond;
            LeftIsPinching = leftIsPinching;
            LeftPinchClosingRatePerSecond = leftPinchClosingRatePerSecond;
            LeftIsTouchingTarget = leftIsTouchingTarget;
            LeftTouchEntrySpeedMetresPerSecond = leftTouchEntrySpeedMetresPerSecond;
        }

        public HandPoseCandidate LeftCandidate { get; }

        public TrackingQuality LeftTracking { get; }

        public TrackingQuality RightTracking { get; }

        /// <summary>
        /// The left hand's signed vertical speed, metres per second — negative is downward
        /// (ADR-0025). Head/gravity-relative, not world-relative in the §3.4 lateral sense:
        /// "down" does not rotate when the user turns, so a plain vertical delta is valid
        /// without a head-forward correction. Drives <see cref="Jazztures.Core.Gesture.ChordStrikeDetector"/>,
        /// which is what actually articulates a chord — see §3.2 vs. ADR-0025.
        /// </summary>
        public float LeftVerticalSpeedMetresPerSecond { get; }

        /// <summary>
        /// The left hand's index-to-thumb self-contact this frame (Design A, ADR-0038) —
        /// the articulation commit under the continuous harmonic-field selection model,
        /// the counterpart to <see cref="LeftVerticalSpeedMetresPerSecond"/> under ADR-0025.
        /// Drives <see cref="Jazztures.Core.Gesture.ChordPinchDetector"/>. Always
        /// <see langword="false"/> when the discrete pose recognisers are the selection
        /// source instead.
        /// </summary>
        public bool LeftIsPinching { get; }

        /// <summary>
        /// Peak d(pinch strength)/dt over the adapter's sample window, per second, while the pinch
        /// is closing (ADR-0038) — 0 when not closing or not pinching. The pinch-commit
        /// counterpart of the strike path's fingertip speed: it feeds
        /// <see cref="Jazztures.Core.Melody.VelocityCurve.FromNormalized"/> to map a
        /// deliberate, fast pinch to a louder chord than a slow one.
        /// </summary>
        public float LeftPinchClosingRatePerSecond { get; }

        /// <summary>
        /// Whether the left hand's fingertip is inside the virtual
        /// <c>ChordStrikeTarget</c>'s volume this frame (ADR-0039) — a third articulation
        /// commit, alongside the strike (ADR-0025) and the pinch (ADR-0038), using the
        /// exact volume-containment test the right hand's ten melody targets already use
        /// (<see cref="Jazztures.Core.Melody.TargetVolume"/>, ADR-0016/0018) rather than a
        /// velocity threshold or the SDK's own pinch flag. Drives
        /// <see cref="Jazztures.Core.Gesture.ChordTouchDetector"/>.
        /// </summary>
        public bool LeftIsTouchingTarget { get; }

        /// <summary>
        /// The fingertip's peak speed, metres per second, over the adapter's sample window
        /// as it entered the target volume (ADR-0039) — the touch-commit counterpart of the
        /// strike path's fingertip speed and the pinch path's closing rate. Unsigned (a
        /// plain approach speed, mirroring the right hand's own entry-speed sampling,
        /// ADR-0018), not a signed derivative: containment is a boolean "inside right now",
        /// so there is no direction-reversal concern to guard against the way ADR-0035 did.
        /// Feeds <see cref="Jazztures.Core.Melody.VelocityCurve.FromSpeed"/> directly.
        /// </summary>
        public float LeftTouchEntrySpeedMetresPerSecond { get; }

        /// <summary>A frame with nothing tracked — the safe default before input arrives.</summary>
        public static HandPoseFrame Untracked =>
            new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.NotTracked, TrackingQuality.NotTracked);

        public bool Equals(HandPoseFrame other) =>
            LeftCandidate == other.LeftCandidate
            && LeftTracking == other.LeftTracking
            && RightTracking == other.RightTracking
            && LeftVerticalSpeedMetresPerSecond.Equals(other.LeftVerticalSpeedMetresPerSecond)
            && LeftIsPinching == other.LeftIsPinching
            && LeftPinchClosingRatePerSecond.Equals(other.LeftPinchClosingRatePerSecond)
            && LeftIsTouchingTarget == other.LeftIsTouchingTarget
            && LeftTouchEntrySpeedMetresPerSecond.Equals(other.LeftTouchEntrySpeedMetresPerSecond);

        public override bool Equals(object? obj) => obj is HandPoseFrame other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)LeftCandidate;
                hash = (hash * 397) ^ (int)LeftTracking;
                hash = (hash * 397) ^ (int)RightTracking;
                hash = (hash * 397) ^ LeftVerticalSpeedMetresPerSecond.GetHashCode();
                hash = (hash * 397) ^ LeftIsPinching.GetHashCode();
                hash = (hash * 397) ^ LeftPinchClosingRatePerSecond.GetHashCode();
                hash = (hash * 397) ^ LeftIsTouchingTarget.GetHashCode();
                hash = (hash * 397) ^ LeftTouchEntrySpeedMetresPerSecond.GetHashCode();
                return hash;
            }
        }

        public override string ToString() =>
            $"{LeftCandidate} (L:{LeftTracking} R:{RightTracking} vy:{LeftVerticalSpeedMetresPerSecond:0.00} " +
            $"pinch:{LeftIsPinching} pinchRate:{LeftPinchClosingRatePerSecond:0.00} " +
            $"touch:{LeftIsTouchingTarget} entrySpeed:{LeftTouchEntrySpeedMetresPerSecond:0.00})";

        public static bool operator ==(HandPoseFrame left, HandPoseFrame right) => left.Equals(right);

        public static bool operator !=(HandPoseFrame left, HandPoseFrame right) => !left.Equals(right);
    }
}
