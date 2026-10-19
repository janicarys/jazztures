namespace Jazztures.Presentation
{
    /// <summary>
    /// Where <see cref="GhostHandView"/> puts the ghost's left hand (ADR-0012, ADR-0052).
    /// </summary>
    public enum GhostAnchor
    {
        /// <summary>
        /// Superimposed on the learner's own wrist (ADR-0012): the fingers show the difference between
        /// the learner's hand and the target. A strike then runs from wherever the learner's hand is to
        /// the pad - at an angle, unless the hand happens to be directly above it.
        /// </summary>
        Wrist,

        /// <summary>
        /// Hovering above the strike pad, ready to strike, wherever the learner's hand is (ADR-0052). A
        /// strike is a straight drop onto the pad, and the ghost shows the learner where to hold their
        /// hand as well as what shape to make. Needs a pad; without one the ghost falls back to
        /// <see cref="Wrist"/>.
        /// </summary>
        StrikePad,
    }
}
