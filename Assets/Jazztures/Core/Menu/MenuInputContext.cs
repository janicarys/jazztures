namespace Jazztures.Core.Menu
{
    /// <summary>
    /// What the right hand is currently for, as far as the menu recogniser is concerned
    /// (ADR-0051).
    /// </summary>
    public enum MenuInputContext
    {
        /// <summary>A menu is on screen: flick, strike and fist all mean something.</summary>
        Menu,

        /// <summary>
        /// A lesson or free play is running and the right hand is busy playing: only the fist
        /// means anything, and it must be held longer so a clenched hand mid-phrase does not
        /// throw the learner out.
        /// </summary>
        Running,
    }
}
