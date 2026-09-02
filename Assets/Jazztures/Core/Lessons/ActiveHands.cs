using System;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Which hands a lesson exercises (CLAUDE.md §3.9). Recognition always runs for both
    /// hands regardless (§3.8) — this only says which the lesson is about.
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
