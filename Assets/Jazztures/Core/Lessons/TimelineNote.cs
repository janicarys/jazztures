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
        {
            Beat = beat;
            TargetIndex = targetIndex;
            Velocity = velocity;
        }

        public Beat Beat { get; }

        /// <summary>Slot in the active chord-tone set, 0..<see cref="Music.ChordToneSet.TargetCount"/>-1.</summary>
        public int TargetIndex { get; }

        public byte Velocity { get; }

        public override string ToString() => $"target {TargetIndex} v{Velocity} @ {Beat}";
    }
}
