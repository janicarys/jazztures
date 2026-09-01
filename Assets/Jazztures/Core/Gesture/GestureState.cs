using System;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// A snapshot of the gesture interpreter for the <c>GestureStateChannel</c>
    /// (CLAUDE.md §2.3): which hand, what phase, and the tracking confidence behind it.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct GestureState : IEquatable<GestureState>
    {
        public GestureState(Handedness hand, GesturePhase phase, TrackingQuality confidence)
        {
            Hand = hand;
            Phase = phase;
            Confidence = confidence;
        }

        public Handedness Hand { get; }

        public GesturePhase Phase { get; }

        public TrackingQuality Confidence { get; }

        public bool Equals(GestureState other) =>
            Hand == other.Hand && Phase == other.Phase && Confidence == other.Confidence;

        public override bool Equals(object? obj) => obj is GestureState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Hand;
                hash = (hash * 397) ^ (int)Phase;
                hash = (hash * 397) ^ (int)Confidence;
                return hash;
            }
        }

        public override string ToString() => $"{Hand} hand: {Phase} ({Confidence})";

        public static bool operator ==(GestureState left, GestureState right) => left.Equals(right);

        public static bool operator !=(GestureState left, GestureState right) => !left.Equals(right);
    }
}
