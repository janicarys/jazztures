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

        /// <summary>
        /// The harmony only — the chord changes, no melody. For a right-hand lesson in Try
        /// Yourself / Test Yourself, where the learner plays the melody over system-played
        /// chords (§3.9, ADR-0043 decision 3) and has no left hand playing them.
        /// </summary>
        HarmonyOnly,
    }
}
