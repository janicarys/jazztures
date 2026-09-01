namespace Jazztures.Core.Harmony
{
    /// <summary>
    /// The three harmonic functions of the ii-V-I in C major (CLAUDE.md §1.3). Each is
    /// held by one static left-hand pose. This is a <b>functional</b> relationship, not
    /// an ordered sequence — the harmony engine never enforces an order, the learner may
    /// hold them in any order (§3.2).
    /// </summary>
    public enum ChordFunction
    {
        /// <summary>ii — Dm7. Preparation. Left hand: open palm facing the user's right.</summary>
        Two,

        /// <summary>V — G7. Peak of tension. Left hand: fist.</summary>
        Five,

        /// <summary>I — Cmaj7. Release / resolution. Left hand: open palm facing down.</summary>
        One,
    }
}
