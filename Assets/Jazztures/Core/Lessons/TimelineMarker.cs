using System;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// A named point on a <see cref="LessonTimeline"/> (e.g. "call-ends", "answer-begins").
    /// Authored cues in the <see cref="LessonScript"/> can trigger off a marker instead of
    /// a raw beat, so a caption survives the phrase being re-engraved (ADR-0011).
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct TimelineMarker
    {
        public TimelineMarker(Beat beat, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Marker name must be non-empty.", nameof(name));
            }

            Beat = beat;
            Name = name;
        }

        public Beat Beat { get; }

        public string Name { get; }

        public override string ToString() => $"\"{Name}\" @ {Beat}";
    }
}
