using Jazztures.Core.Timing;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// A right-hand melody note on a <see cref="LessonTimeline"/>. The MIDI pitch from the
    /// melody staff is resolved at import to a <see cref="TargetIndex"/> — a slot in the
    /// chord-tone set active at that beat (ADR-0011) — so the ghost hand reaches for a
    /// fixed spatial target, not an absolute pitch (§3.1). Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct TimelineNote
    {
        public TimelineNote(Beat beat, int targetIndex, byte velocity)
            : this(beat, targetIndex, velocity, NoPitch, false)
        {
        }

        public TimelineNote(Beat beat, int targetIndex, byte velocity, int midiPitch, bool straight)
        {
            Beat = beat;
            TargetIndex = targetIndex;
            Velocity = velocity;
            MidiPitch = midiPitch;
            Straight = straight;
        }

        /// <summary><see cref="MidiPitch"/> when the note is a chord-tone slot, not a fixed pitch.</summary>
        public const int NoPitch = -1;

        public Beat Beat { get; }

        /// <summary>Slot in the active chord-tone set, 0..<see cref="Music.ChordToneSet.TargetCount"/>-1.</summary>
        public int TargetIndex { get; }

        public byte Velocity { get; }

        /// <summary>
        /// A fixed MIDI pitch (the Swing lesson's scale notes), or <see cref="NoPitch"/> to mean the chord-tone slot
        /// <see cref="TargetIndex"/>. A fixed pitch ignores the chord.
        /// </summary>
        public int MidiPitch { get; }

        public bool HasFixedPitch => MidiPitch != NoPitch;

        /// <summary>
        /// Played exactly on its notated position even in a swung lesson (Swing step 0's "straight" half). Otherwise the
        /// position is notated straight and <see cref="PlayedBeat"/> swings it.
        /// </summary>
        public bool Straight { get; }

        /// <summary>Where the note is actually played: its position, swung by <paramref name="swing"/> unless <see cref="Straight"/>.</summary>
        public double PlayedBeat(SwingRatio swing) =>
            Straight ? Beat.Position : SwingQuantizer.Swing(Beat.Position, swing);

        public override string ToString() => $"target {TargetIndex} v{Velocity} @ {Beat}";
    }
}
