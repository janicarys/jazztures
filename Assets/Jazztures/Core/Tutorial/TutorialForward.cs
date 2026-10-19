using System;

namespace Jazztures.Core.Tutorial
{
    /// <summary>
    /// Which of the menu's gestures a tutorial step lets through to the real menu (ADR-0053). The
    /// tutorial runs on the real menu — a flick really moves the highlight, a strike really picks a
    /// lesson — but a stray strike while the learner is still being shown how to flick would pick one
    /// too early. A step lets through only what it has taught so far.
    /// </summary>
    [Flags]
    public enum TutorialForward
    {
        None = 0,

        /// <summary>Flicks move the highlight.</summary>
        Flick = 1,

        /// <summary>A strike chooses the highlighted card.</summary>
        Strike = 2,
    }
}
