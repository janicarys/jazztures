using System;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// A lesson's stable identifier (e.g. "L1", "L3-chord-tones"). Used in telemetry and
    /// on <c>LessonPhaseChannel</c>. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct LessonId : IEquatable<LessonId>
    {
        private readonly string? _value;

        public LessonId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Lesson id must be non-empty.", nameof(value));
            }

            _value = value;
        }

        public string Value => _value ?? throw new InvalidOperationException("Uninitialised LessonId.");

        public bool Equals(LessonId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is LessonId other && Equals(other);

        public override int GetHashCode() => _value?.GetHashCode() ?? 0;

        public override string ToString() => _value ?? "<none>";

        public static bool operator ==(LessonId left, LessonId right) => left.Equals(right);

        public static bool operator !=(LessonId left, LessonId right) => !left.Equals(right);
    }
}
