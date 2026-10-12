using Jazztures.Core.Drills;

namespace Jazztures.Lessons
{
    /// <summary>
    /// What a finished <see cref="TimingDrillRunner"/> run hands back: the one-line result to
    /// show after the run, the scored report (null for a metronome-only run) and where its CSV
    /// went (null if none was written, or the write failed).
    /// </summary>
    public sealed class DrillOutcome
    {
        public DrillOutcome(TimingDrill drill, DrillReport report, string summaryLine, string csvPath)
        {
            Drill = drill;
            Report = report;
            SummaryLine = summaryLine;
            CsvPath = csvPath;
        }

        public TimingDrill Drill { get; }

        /// <summary>The scored result, or null when the drill had scoring off.</summary>
        public DrillReport Report { get; }

        /// <summary>One deferred line for the screen — never shown mid-phrase (§3.7).</summary>
        public string SummaryLine { get; }

        /// <summary>The CSV written for this run, or null.</summary>
        public string CsvPath { get; }
    }
}
