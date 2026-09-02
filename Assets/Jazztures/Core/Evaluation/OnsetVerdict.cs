namespace Jazztures.Core.Evaluation
{
    /// <summary>
    /// How close a single played onset was to where it was expected (CLAUDE.md §3.7).
    /// A verdict is only assigned to an onset that was <i>matched</i> to an expected beat;
    /// unmatched notes are counted separately on the <see cref="AttemptResult"/>.
    /// </summary>
    public enum OnsetVerdict
    {
        /// <summary>Within the tight window — deviation ≤ the on-time bound.</summary>
        OnTime,

        /// <summary>Within the loose window — deviation ≤ the close bound.</summary>
        Close,

        /// <summary>Played, matched, but outside the close bound.</summary>
        Off,
    }
}
