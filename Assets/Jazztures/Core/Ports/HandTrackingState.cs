using System;

namespace Jazztures.Core.Ports
{
    /// <summary>
    /// The tracking quality of one hand, for the <c>TrackingQualityChannel</c>
    /// (CLAUDE.md §2.3). Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct HandTrackingState : IEquatable<HandTrackingState>
    {
        public HandTrackingState(Handedness hand, TrackingQuality quality)
        {
            Hand = hand;
            Quality = quality;
        }

        public Handedness Hand { get; }

        public TrackingQuality Quality { get; }

        public bool Equals(HandTrackingState other) => Hand == other.Hand && Quality == other.Quality;

        public override bool Equals(object? obj) => obj is HandTrackingState other && Equals(other);

        public override int GetHashCode() => ((int)Hand * 397) ^ (int)Quality;

        public override string ToString() => $"{Hand} hand: {Quality}";

        public static bool operator ==(HandTrackingState left, HandTrackingState right) => left.Equals(right);

        public static bool operator !=(HandTrackingState left, HandTrackingState right) => !left.Equals(right);
    }
}
