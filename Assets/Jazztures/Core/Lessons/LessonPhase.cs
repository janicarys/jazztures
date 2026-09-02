namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// One mode phase of a running lesson (CLAUDE.md §3.8/§3.9) — e.g. "phase 0:
    /// Watch and Listen". Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct LessonPhase
    {
        public LessonPhase(int index, LearningMode mode, bool isLast)
        {
            Index = index;
            Mode = mode;
            IsLast = isLast;
        }

        public int Index { get; }

        public LearningMode Mode { get; }

        public bool IsLast { get; }

        /// <summary>The §3.8 behaviour this phase's mode dictates.</summary>
        public ModePolicy Policy => ModePolicy.For(Mode);

        public override string ToString() => $"phase {Index}: {Mode}{(IsLast ? " (last)" : string.Empty)}";
    }
}
