namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// When the learner's own notes are sounded in a given <see cref="LearningMode"/>
    /// (CLAUDE.md §3.8). Recognition and telemetry are unaffected — this only gates the
    /// audible sink.
    /// </summary>
    public enum UserAudioGate
    {
        /// <summary>Always audible.</summary>
        Always,

        /// <summary>Never audible — logged only (Watch-and-Listen).</summary>
        Never,

        /// <summary>Audible only while the lesson layer says the gesture is correct (Try-Yourself).</summary>
        OnlyWhenGestureCorrect,
    }
}
