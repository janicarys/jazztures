namespace Jazztures.Core.Tutorial
{
    /// <summary>
    /// What the ghost hand shows during a tutorial step (ADR-0053). The ghost is a right hand held
    /// out in front of the learner, doing the thing the step asks for on a loop until the learner
    /// does it.
    /// </summary>
    public enum TutorialDemo
    {
        /// <summary>No ghost.</summary>
        None,

        /// <summary>An open hand closing into a fist and opening again.</summary>
        Fist,

        /// <summary>An open hand held with the thumb up.</summary>
        ThumbUp,

        /// <summary>The open hand flicking left, then right: a quick snap of the wrist, then a relaxed return.</summary>
        Flick,

        /// <summary>The open hand striking down, then lifting.</summary>
        Strike,
    }
}
