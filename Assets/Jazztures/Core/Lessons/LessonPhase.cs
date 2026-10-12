namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// One mode phase of a running lesson (CLAUDE.md §3.8/§3.9) — e.g. "phase 0:
    /// Watch and Listen". Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct LessonPhase
    {
        public LessonPhase(int index, LearningMode mode, bool isLast, ActiveHands hands = ActiveHands.Both)
        {
            Index = index;
            Mode = mode;
            IsLast = isLast;
            Hands = hands;
        }

        public int Index { get; }

        public LearningMode Mode { get; }

        public bool IsLast { get; }

        /// <summary>The hands the lesson this phase belongs to exercises (§3.9).</summary>
        public ActiveHands Hands { get; }

        /// <summary>
        /// The §3.8 behaviour this phase's mode dictates for its lesson's hands — which only
        /// matters for a right-hand lesson's Try / Test Yourself, where the system plays the
        /// chords (ADR-0043 decision 3).
        /// </summary>
        public ModePolicy Policy => ModePolicy.For(Mode, Hands);

        public override string ToString() => $"phase {Index}: {Mode}{(IsLast ? " (last)" : string.Empty)}";
    }
}
