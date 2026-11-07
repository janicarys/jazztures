using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// When the melody cursor jumps back to a start pitch without playing a note (ADR-0078). Flags, so
    /// they combine; the default is <c>OnTask | OnRest</c>. `[TUNABLE]`.
    /// </summary>
    [Flags]
    public enum PhraseResetPolicy
    {
        Never = 0,

        /// <summary>No crossing for the rest time.</summary>
        OnRest = 1,

        /// <summary>At each bar line.</summary>
        OnBar = 2,

        /// <summary>When a drill task, or a Compose on the Fly answer, starts.</summary>
        OnTask = 4,
    }
}
