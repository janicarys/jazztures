namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// One authored <c>trigger → action</c> entry on the lesson's cue track (ADR-0011).
    /// The music timeline carries none of this; decoupling lets a caption be retimed
    /// without re-engraving the score. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct LessonCue
    {
        public LessonCue(CueTrigger trigger, CueAction action)
        {
            Trigger = trigger;
            Action = action;
        }

        public CueTrigger Trigger { get; }

        public CueAction Action { get; }

        public override string ToString() => $"{Trigger}  ->  {Action}";
    }
}
