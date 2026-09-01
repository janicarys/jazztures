namespace Jazztures.Core.Lessons
{
    /// <summary>What the system itself plays in a given <see cref="LearningMode"/> (CLAUDE.md §3.8).</summary>
    public enum SystemPlayback
    {
        /// <summary>Nothing.</summary>
        None,

        /// <summary>The full demonstration — harmony and melody.</summary>
        Full,

        /// <summary>Backing accompaniment only; the learner supplies harmony and melody.</summary>
        BackingOnly,
    }
}
