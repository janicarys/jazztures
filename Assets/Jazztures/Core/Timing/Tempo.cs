using System;

namespace Jazztures.Core.Timing
{
    /// <summary>
    /// A tempo in beats per minute, with conversions between beats and DSP seconds.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct Tempo : IEquatable<Tempo>
    {
        /// <summary>
        /// The §3.6 default of 80 BPM. `[TUNABLE]` — a lesson asset overrides it; anything
        /// above ~100 BPM is unvalidated and gated behind a config flag. See
        /// <c>Docs/CALIBRATION.md</c>.
        /// </summary>
        public static Tempo Default => new Tempo(80.0);

        public double BeatsPerMinute { get; }

        public Tempo(double beatsPerMinute)
        {
            if (double.IsNaN(beatsPerMinute) || double.IsInfinity(beatsPerMinute) || beatsPerMinute <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(beatsPerMinute), beatsPerMinute, "Tempo must be finite and positive.");
            }

            BeatsPerMinute = beatsPerMinute;
        }

        public static Tempo Bpm(double beatsPerMinute) => new Tempo(beatsPerMinute);

        public double SecondsPerBeat => 60.0 / BeatsPerMinute;

        public double BeatsToSeconds(double beats) => beats * SecondsPerBeat;

        public double SecondsToBeats(double seconds) => seconds / SecondsPerBeat;

        public bool Equals(Tempo other) => BeatsPerMinute.Equals(other.BeatsPerMinute);

        public override bool Equals(object? obj) => obj is Tempo other && Equals(other);

        public override int GetHashCode() => BeatsPerMinute.GetHashCode();

        public override string ToString() => $"{BeatsPerMinute:0.##} BPM";

        public static bool operator ==(Tempo left, Tempo right) => left.Equals(right);

        public static bool operator !=(Tempo left, Tempo right) => !left.Equals(right);
    }
}
