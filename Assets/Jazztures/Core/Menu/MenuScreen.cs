namespace Jazztures.Core.Menu
{
    /// <summary>Which menu is on screen (ADR-0051). While a lesson or free play runs there is none.</summary>
    public enum MenuScreen
    {
        /// <summary>The lessons, then Free play.</summary>
        Main,

        /// <summary>One lesson's modes, its tempo card and Back.</summary>
        Modes,

        /// <summary>Shown when a mode finishes: back to modes, restart, next.</summary>
        PostRun,
    }
}
