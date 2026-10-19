namespace Jazztures.Presentation
{
    /// <summary>
    /// How <see cref="MenuView"/> changes from one menu screen to another (ADR-0051). The motion
    /// says which way the learner went: going deeper (into a lesson, into a result) the old
    /// cards drift up and fade while the new ones rise in from below; coming back out it runs
    /// the other way.
    /// </summary>
    public enum MenuTransition
    {
        /// <summary>The same screen redrawn — a card's label changed, or the highlight moved. No fade.</summary>
        None,

        /// <summary>A step further in: old cards drift up and out, new cards rise into place.</summary>
        Deeper,

        /// <summary>A step back out: old cards sink and fade, new cards settle down into place.</summary>
        Shallower,
    }
}
