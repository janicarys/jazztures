using System;
using Jazztures.Core.Music;

namespace Jazztures.Core.Ports
{
    /// <summary>
    /// A single note-on or note-off, and the one source of truth for a sounding note
    /// (CLAUDE.md §4.2). The domain emits these; the composite <see cref="INoteSink"/>
    /// fans every one out to local audio, the OSC/DAW bridge and the telemetry log, so
    /// all three describe the same performance. Pure value — no <c>UnityEngine</c>,
    /// allocation-free (ADR-0007). Musical time is <see cref="DspTime"/> in seconds on
    /// the DSP clock, never frame time (§2.4).
    /// </summary>
    public readonly struct NoteEvent : IEquatable<NoteEvent>
    {
        public const byte MinVelocity = 0;

        public const byte MaxVelocity = 127;

        public NoteEventKind Kind { get; }

        public Pitch Pitch { get; }

        /// <summary>MIDI velocity 0..127. Meaningful for <see cref="NoteEventKind.On"/>; 0 for off.</summary>
        public byte Velocity { get; }

        /// <summary>Seconds on the DSP timeline (<c>AudioSettings.dspTime</c> domain).</summary>
        public double DspTime { get; }

        /// <summary>MIDI-style channel, used to keep harmony and melody separable downstream.</summary>
        public int Channel { get; }

        /// <summary>Which hand produced the note.</summary>
        public Handedness Source { get; }

        public NoteEvent(
            NoteEventKind kind,
            Pitch pitch,
            byte velocity,
            double dspTime,
            int channel,
            Handedness source)
        {
            if (velocity > MaxVelocity)
            {
                throw new ArgumentOutOfRangeException(nameof(velocity), velocity, "0..127.");
            }

            if (double.IsNaN(dspTime) || double.IsInfinity(dspTime))
            {
                throw new ArgumentOutOfRangeException(nameof(dspTime), dspTime, "Must be finite.");
            }

            Kind = kind;
            Pitch = pitch;
            Velocity = kind == NoteEventKind.Off ? (byte)0 : velocity;
            DspTime = dspTime;
            Channel = channel;
            Source = source;
        }

        public static NoteEvent On(
            Pitch pitch, byte velocity, double dspTime, int channel, Handedness source) =>
            new NoteEvent(NoteEventKind.On, pitch, velocity, dspTime, channel, source);

        public static NoteEvent Off(
            Pitch pitch, double dspTime, int channel, Handedness source) =>
            new NoteEvent(NoteEventKind.Off, pitch, 0, dspTime, channel, source);

        public bool Equals(NoteEvent other) =>
            Kind == other.Kind
            && Pitch == other.Pitch
            && Velocity == other.Velocity
            && DspTime.Equals(other.DspTime)
            && Channel == other.Channel
            && Source == other.Source;

        public override bool Equals(object? obj) => obj is NoteEvent other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ Pitch.GetHashCode();
                hash = (hash * 397) ^ Velocity;
                hash = (hash * 397) ^ DspTime.GetHashCode();
                hash = (hash * 397) ^ Channel;
                hash = (hash * 397) ^ (int)Source;
                return hash;
            }
        }

        public override string ToString() =>
            $"{Kind} {Pitch} v{Velocity} ch{Channel} {Source} @{DspTime:0.###}s";

        public static bool operator ==(NoteEvent left, NoteEvent right) => left.Equals(right);

        public static bool operator !=(NoteEvent left, NoteEvent right) => !left.Equals(right);
    }
}
