namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// What a <see cref="LessonCue"/> does when it fires (ADR-0011). Presentation actions
    /// are consumed by the HUD / ghost hands; control actions
    /// (<see cref="WaitForInput"/>, <see cref="AdvancePhase"/>, <see cref="SetScoring"/>)
    /// are consumed by the lesson runner.
    /// </summary>
    public enum CueActionKind
    {
        /// <summary>Show authored text in a caption slot.</summary>
        ShowText,

        /// <summary>Clear a caption slot.</summary>
        HideText,

        /// <summary>Emphasise one right-hand target.</summary>
        HighlightTarget,

        /// <summary>Drop all target emphasis.</summary>
        ClearHighlights,

        /// <summary>Set the tension-arc colour band.</summary>
        SetTensionColor,

        /// <summary>Hold the phrase clock until the next qualifying learner action.</summary>
        WaitForInput,

        /// <summary>Turn deferred onset-scoring capture on or off (§3.7).</summary>
        SetScoring,

        /// <summary>Ask the lesson state machine to move to the next phase.</summary>
        AdvancePhase,
    }
}
