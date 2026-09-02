using Jazztures.Core.Harmony;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// A left-hand chord-function change on a <see cref="LessonTimeline"/>, baked from the
    /// left staff / channel of a Standard MIDI File at import (ADR-0011). Immutable value
    /// type (ADR-0007).
    /// </summary>
    public readonly struct TimelineChord
    {
        public TimelineChord(Beat beat, ChordFunction function)
        {
            Beat = beat;
            Function = function;
        }

        public Beat Beat { get; }

        public ChordFunction Function { get; }

        public override string ToString() => $"{Function} @ {Beat}";
    }
}
