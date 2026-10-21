using System;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Which hands a lesson exercises (CLAUDE.md §3.9). Recognition always runs for both
    /// hands regardless (§3.8) — this says which the lesson is about: the hand it does not use is
    /// not heard (<see cref="ModeGatedNoteSink.SetActiveHands"/>), carries no label, and, for the left, does
    /// not select the chords (<see cref="ActiveHandsExtensions.SystemSelectsChords"/>; ADR-0063).
    /// </summary>
    [Flags]
    public enum ActiveHands
    {
        None = 0,

        /// <summary>Left hand — harmony poses.</summary>
        Left = 1,

        /// <summary>Right hand — melody targets.</summary>
        Right = 2,

        Both = Left | Right,
    }
}
