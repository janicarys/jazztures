namespace Jazztures.Core.Menu
{
    /// <summary>What choosing a card does (ADR-0051).</summary>
    public enum MenuItemKind
    {
        /// <summary>Open a lesson's mode screen.</summary>
        Lesson,

        /// <summary>Start free play (§3.8 Compose-on-the-Fly) — no lesson, no modes.</summary>
        FreePlay,

        /// <summary>Start one learning mode of a lesson.</summary>
        Mode,

        /// <summary>Step the lesson's tempo to its next preset; stays on the screen.</summary>
        Tempo,

        /// <summary>Back up one level.</summary>
        Back,

        /// <summary>After a run: return to the lesson's mode screen.</summary>
        BackToModes,

        /// <summary>After a run: run the same mode again.</summary>
        Restart,

        /// <summary>After a run: the next mode of the lesson, or else the next lesson.</summary>
        Next,
    }
}
