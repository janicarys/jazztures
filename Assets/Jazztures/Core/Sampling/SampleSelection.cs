using System;

namespace Jazztures.Core.Sampling
{
    /// <summary>
    /// The result of asking a <see cref="SampleLibrary"/> to play a pitch: which recorded
    /// clip to use, and the playback-rate multiplier to pitch-shift it to the target
    /// (equal temperament, <c>2^(semitones/12)</c>). Rate 1.0 means the clip is played
    /// at its recorded pitch. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct SampleSelection : IEquatable<SampleSelection>
    {
        public SampleSelection(int clipIndex, double playbackRate)
        {
            if (clipIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(clipIndex), clipIndex, null);
            }

            if (double.IsNaN(playbackRate) || double.IsInfinity(playbackRate) || playbackRate <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playbackRate), playbackRate, "Must be finite and positive.");
            }

            ClipIndex = clipIndex;
            PlaybackRate = playbackRate;
        }

        /// <summary>Index into the clip list the <see cref="SampleLibrary"/> was built from.</summary>
        public int ClipIndex { get; }

        /// <summary>Playback speed multiplier — <c>2^((targetMidi - clipRootMidi) / 12)</c>.</summary>
        public double PlaybackRate { get; }

        public bool Equals(SampleSelection other) =>
            ClipIndex == other.ClipIndex && PlaybackRate.Equals(other.PlaybackRate);

        public override bool Equals(object? obj) => obj is SampleSelection other && Equals(other);

        public override int GetHashCode() => (ClipIndex * 397) ^ PlaybackRate.GetHashCode();

        public override string ToString() => $"clip {ClipIndex} @ x{PlaybackRate:0.####}";

        public static bool operator ==(SampleSelection left, SampleSelection right) => left.Equals(right);

        public static bool operator !=(SampleSelection left, SampleSelection right) => !left.Equals(right);
    }
}
