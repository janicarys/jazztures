namespace Jazztures.Core.Lessons
{
    /// <summary>Where a <see cref="LessonStateMachine"/> is in its run.</summary>
    public enum LessonStatus
    {
        /// <summary>Constructed, not yet begun.</summary>
        NotStarted,

        /// <summary>Running one of the lesson's mode phases.</summary>
        InPhase,

        /// <summary>All phases done.</summary>
        Completed,
    }
}
