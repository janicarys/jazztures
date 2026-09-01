using System;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// One timestamped frame in a <see cref="HandPoseRecording"/>. Time is seconds from
    /// the start of the recording. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct HandPoseSample : IEquatable<HandPoseSample>
    {
        public HandPoseSample(double timeSeconds, HandPoseFrame frame)
        {
            if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds) || timeSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeSeconds), timeSeconds, "Must be finite and non-negative.");
            }

            TimeSeconds = timeSeconds;
            Frame = frame;
        }

        public double TimeSeconds { get; }

        public HandPoseFrame Frame { get; }

        public bool Equals(HandPoseSample other) =>
            TimeSeconds.Equals(other.TimeSeconds) && Frame.Equals(other.Frame);

        public override bool Equals(object? obj) => obj is HandPoseSample other && Equals(other);

        public override int GetHashCode() => (TimeSeconds.GetHashCode() * 397) ^ Frame.GetHashCode();

        public override string ToString() => $"{TimeSeconds:0.###}s {Frame}";

        public static bool operator ==(HandPoseSample left, HandPoseSample right) => left.Equals(right);

        public static bool operator !=(HandPoseSample left, HandPoseSample right) => !left.Equals(right);
    }
}
