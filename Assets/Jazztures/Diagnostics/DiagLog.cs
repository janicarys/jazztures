using System.Diagnostics;

namespace Jazztures.Diagnostics
{
    /// <summary>
    /// The one door for per-frame / per-note diagnostic logging (ADR-0043, quiet build).
    ///
    /// <para>
    /// <see cref="Log"/> is <see cref="ConditionalAttribute"/>-gated on
    /// <c>JAZZTURES_DIAG</c>, so unless that scripting define is set the <i>call</i> is
    /// removed at compile time — including the string interpolation in its argument. A
    /// <c>Debug.Log($"…")</c> on the gesture or audio path allocates and does log I/O on
    /// every call (CLAUDE.md §2.4), which is jitter in exactly the timings a drill measures.
    /// The define is off for every timing run; add it under Project Settings → Player →
    /// Scripting Define Symbols to bring the logs back for debugging.
    /// </para>
    ///
    /// <para>
    /// Only for output that fires per frame or per note. One-off messages (errors, per-run
    /// summaries) stay on plain <c>UnityEngine.Debug</c>.
    /// </para>
    /// </summary>
    public static class DiagLog
    {
        /// <summary>The scripting define that turns <see cref="Log"/> on.</summary>
        public const string Define = "JAZZTURES_DIAG";

        [Conditional(Define)]
        public static void Log(string message) => UnityEngine.Debug.Log(message);
    }
}
