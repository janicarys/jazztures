namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Payload for <c>LessonPhaseChannel</c> (CLAUDE.md §2.3): which lesson, and which of
    /// its mode phases is now active. Raised on every phase change so the HUD, the ghost
    /// hands and the mode-gated sink can re-point. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct LessonPhaseInfo
    {
        public LessonPhaseInfo(LessonId lesson, LessonPhase phase)
        {
            Lesson = lesson;
            Phase = phase;
        }

        public LessonId Lesson { get; }

        public LessonPhase Phase { get; }

        public LearningMode Mode => Phase.Mode;

        public override string ToString() => $"{Lesson} {Phase}";
    }
}
