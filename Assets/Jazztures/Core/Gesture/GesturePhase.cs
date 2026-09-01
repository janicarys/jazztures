namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// What the gesture interpreter is doing right now, for the <c>GestureStateChannel</c>
    /// and presentation (CLAUDE.md §2.3). Not the same as the held chord — that is a
    /// <c>ChordFunction?</c>.
    /// </summary>
    public enum GesturePhase
    {
        /// <summary>Tracking is fine; no pose held and none being confirmed.</summary>
        Idle,

        /// <summary>A new pose is being held towards confirmation.</summary>
        Detecting,

        /// <summary>A pose has been confirmed and is the active chord function.</summary>
        Confirmed,

        /// <summary>Tracking is too poor to accept input; the current chord is sustained (§3.5).</summary>
        Suppressed,
    }
}
