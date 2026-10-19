namespace Jazztures.Core.Menu
{
    /// <summary>What the menu is asking the app to do (ADR-0051).</summary>
    public enum MenuIntentKind
    {
        /// <summary>Run one mode of one lesson.</summary>
        StartLesson,

        /// <summary>Start free play.</summary>
        StartFreePlay,
    }
}
