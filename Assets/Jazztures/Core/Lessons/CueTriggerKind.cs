namespace Jazztures.Core.Lessons
{
    /// <summary>What makes a <see cref="LessonCue"/> fire (ADR-0011).</summary>
    public enum CueTriggerKind
    {
        /// <summary>A position on the beat grid is reached.</summary>
        AtBeat,

        /// <summary>A named <see cref="TimelineMarker"/> is passed.</summary>
        AtMarker,

        /// <summary>The learner does something — see <see cref="LearnerAction"/>.</summary>
        OnLearnerAction,
    }
}
