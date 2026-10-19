namespace Jazztures.Core.Menu
{
    /// <summary>
    /// The four things the right hand says to the menu (ADR-0051). The menu is driven by
    /// exactly these — nothing else the hand does means anything while it is open.
    /// </summary>
    public enum MenuGesture
    {
        /// <summary>
        /// A quick sideways flick toward the learner's left — the next card. The row follows the
        /// hand, as when swiping a touch screen.
        /// </summary>
        FlickLeft,

        /// <summary>A quick sideways flick toward the learner's right — the previous card.</summary>
        FlickRight,

        /// <summary>A quick downward strike — choose the highlighted card.</summary>
        Strike,

        /// <summary>A closed fist, held — go back to the main menu from anywhere.</summary>
        Fist,
    }
}
