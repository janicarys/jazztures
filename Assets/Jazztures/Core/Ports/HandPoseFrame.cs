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
            float leftVerticalSpeedMetresPerSecond = 0f)
        {
            LeftCandidate = leftCandidate;
            LeftTracking = leftTracking;
            RightTracking = rightTracking;
            LeftVerticalSpeedMetresPerSecond = leftVerticalSpeedMetresPerSecond;
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

        /// <summary>A frame with nothing tracked — the safe default before input arrives.</summary>
        public static HandPoseFrame Untracked =>
            new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.NotTracked, TrackingQuality.NotTracked);

        public bool Equals(HandPoseFrame other) =>
            LeftCandidate == other.LeftCandidate
            && LeftTracking == other.LeftTracking
            && RightTracking == other.RightTracking
            && LeftVerticalSpeedMetresPerSecond.Equals(other.LeftVerticalSpeedMetresPerSecond);

        public override bool Equals(object? obj) => obj is HandPoseFrame other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)LeftCandidate;
                hash = (hash * 397) ^ (int)LeftTracking;
                hash = (hash * 397) ^ (int)RightTracking;
                hash = (hash * 397) ^ LeftVerticalSpeedMetresPerSecond.GetHashCode();
                return hash;
            }
        }

        public override string ToString() =>
            $"{LeftCandidate} (L:{LeftTracking} R:{RightTracking} vy:{LeftVerticalSpeedMetresPerSecond:0.00})";

        public static bool operator ==(HandPoseFrame left, HandPoseFrame right) => left.Equals(right);

        public static bool operator !=(HandPoseFrame left, HandPoseFrame right) => !left.Equals(right);
    }
}
