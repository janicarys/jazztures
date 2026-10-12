namespace Jazztures.Core.Drills
{
    /// <summary>
    /// The facts about <i>this run</i> that are not in the drill itself, written into the
    /// header of the run's CSV (ADR-0043) so a result can be traced to a build, a tester and
    /// the mechanic that was active. All plain strings — the Unity side fills them in.
    /// </summary>
    public sealed class DrillRunInfo
    {
        public DrillRunInfo(
            string mechanic,
            string harmonyCommit,
            string build,
            string testerCode,
            string startedAtUtc)
        {
            Mechanic = mechanic ?? string.Empty;
            HarmonyCommit = harmonyCommit ?? string.Empty;
            Build = build ?? string.Empty;
            TesterCode = testerCode ?? string.Empty;
            StartedAtUtc = startedAtUtc ?? string.Empty;
        }

        /// <summary>The right-hand mechanic in force, e.g. "arc+touch" or "arc+pinch".</summary>
        public string Mechanic { get; }

        /// <summary>How the left hand articulates a chord in this build (strike / pinch / touch plate).</summary>
        public string HarmonyCommit { get; }

        /// <summary>Application version and build id.</summary>
        public string Build { get; }

        /// <summary>An anonymous tester code (never a name).</summary>
        public string TesterCode { get; }

        /// <summary>ISO-8601 UTC time the run began.</summary>
        public string StartedAtUtc { get; }
    }
}
