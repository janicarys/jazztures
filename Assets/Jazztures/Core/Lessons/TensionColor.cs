namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// The tension-arc colour band (CLAUDE.md §3.10): cool/neutral for ii, warm/saturated
    /// for V, resolved/settled for I. A redundant reinforcing channel — it must never be
    /// the sole carrier of information, so the actual RGB values live on the Unity side.
    /// </summary>
    public enum TensionColor
    {
        /// <summary>ii — preparation.</summary>
        Cool,

        /// <summary>V — peak of tension.</summary>
        Warm,

        /// <summary>I — release.</summary>
        Resolved,
    }
}
