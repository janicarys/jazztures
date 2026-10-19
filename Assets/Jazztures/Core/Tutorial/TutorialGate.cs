namespace Jazztures.Core.Tutorial
{
    /// <summary>
    /// What the learner has to <i>do</i> before the tutorial moves on from a step (ADR-0053). The
    /// tutorial is gated by the learner's actual actions, not by time — except where a step is
    /// only an explanation, <see cref="AfterSeconds"/>.
    /// </summary>
    public enum TutorialGate
    {
        /// <summary>Both hands are in view, and stay there for a moment.</summary>
        HandsInView,

        /// <summary>Neither hand is in view, and stays that way for a moment.</summary>
        HandsOutOfView,

        /// <summary>Nothing to do: the step simply runs for <see cref="TutorialStep.GateSeconds"/>.</summary>
        AfterSeconds,

        /// <summary>The right hand makes the menu's fist (held, as the recogniser requires).</summary>
        RightFist,

        /// <summary>The right hand is fully open with the thumb pointing up, held for a moment.</summary>
        OpenHandThumbUp,

        /// <summary>The right hand flicks sideways, either way.</summary>
        Flick,

        /// <summary>The right hand strikes downward.</summary>
        Strike,
    }
}
