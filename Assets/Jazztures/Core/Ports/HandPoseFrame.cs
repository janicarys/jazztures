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
            TrackingQuality rightTracking)
        {
            LeftCandidate = leftCandidate;
            LeftTracking = leftTracking;
            RightTracking = rightTracking;
        }

        public HandPoseCandidate LeftCandidate { get; }

        public TrackingQuality LeftTracking { get; }

        public TrackingQuality RightTracking { get; }

        /// <summary>A frame with nothing tracked — the safe default before input arrives.</summary>
        public static HandPoseFrame Untracked =>
            new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.NotTracked, TrackingQuality.NotTracked);

        public bool Equals(HandPoseFrame other) =>
            LeftCandidate == other.LeftCandidate
            && LeftTracking == other.LeftTracking
            && RightTracking == other.RightTracking;

        public override bool Equals(object? obj) => obj is HandPoseFrame other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)LeftCandidate;
                hash = (hash * 397) ^ (int)LeftTracking;
                hash = (hash * 397) ^ (int)RightTracking;
                return hash;
            }
        }

        public override string ToString() =>
            $"{LeftCandidate} (L:{LeftTracking} R:{RightTracking})";

        public static bool operator ==(HandPoseFrame left, HandPoseFrame right) => left.Equals(right);

        public static bool operator !=(HandPoseFrame left, HandPoseFrame right) => !left.Equals(right);
    }
}
