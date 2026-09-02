namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// A thing the learner can do that an authored cue may react to (ADR-0011). Coarse by
    /// design; a trigger can narrow <see cref="ChordConfirmed"/> to one function and
    /// <see cref="MelodyNotePlayed"/> to one target.
    /// </summary>
    // TODO(OPEN): richer learner signals (held N beats, played a full phrase, matched the
    // ghost within a window) as lesson authoring needs them.
    public enum LearnerAction
    {
        /// <summary>The lesson phrase has begun playing.</summary>
        PhraseStarted,

        /// <summary>The gesture interpreter confirmed a left-hand chord function.</summary>
        ChordConfirmed,

        /// <summary>A right-hand melody note fired.</summary>
        MelodyNotePlayed,

        /// <summary>The learner reached the end of an attempt (Test Yourself).</summary>
        AttemptCompleted,
    }
}
