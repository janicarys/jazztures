using System.Globalization;
using System.Text;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The sandbox's CSV, one row per event (ADR-0078). Fixed columns; invariant culture so a file written on a
    /// headset reads the same everywhere. <c>tracking_lost_ms</c> is a trailing column added to the specified set:
    /// a dropout is an interval, and its duration has nowhere else to go. Raw joint positions are never written
    /// (CLAUDE.md §4.1) — only classifications, timestamps and note events.
    /// </summary>
    public static class DrillCsv
    {
        public const string Header =
            "timestamp_audio,bpm,rhythm,drill,target_beat_time,target_direction," +
            "crossing_time,swing_dir,sampled_direction,pitch,offset_ms," +
            "result,start_pitch,reset_event,register_zone,tracking_lost_ms";

        /// <param name="resetEvent">The reset that happened since the previous row: none, task, bar or rest.</param>
        /// <param name="registerZone">The register zone, or -1 when height zones are off (written empty).</param>
        public static string Row(
            DrillSchedule schedule, DrillEvent e, int startPitchMidi, PhraseResetReason resetEvent, int registerZone)
        {
            double timestamp = e.HasCrossing ? e.CrossingTime : e.TargetTime;
            bool note = e.PitchMidi >= 0;
            var sb = new StringBuilder(160);
            Add(sb, Number(timestamp, "0.000"));
            Add(sb, schedule.BeatsPerMinute.ToString("0.##", CultureInfo.InvariantCulture));
            Add(sb, schedule.Rhythm == DrillRhythm.StraightQuarters ? "straight" : "swung");
            Add(sb, schedule.Drill.Name);
            Add(sb, e.HasTarget ? Number(e.TargetTime, "0.000") : string.Empty);
            Add(sb, e.HasTarget ? Letter(e.TargetDirection) : string.Empty);
            Add(sb, e.HasCrossing ? Number(e.CrossingTime, "0.000") : string.Empty);
            Add(sb, e.Result is DrillResult.Miss or DrillResult.TrackingLost
                ? string.Empty
                : e.Swing == SwingDirection.LeftToRight ? "LR" : "RL");
            Add(sb, note ? Letter(e.SampledDirection) : string.Empty);
            Add(sb, note ? e.PitchMidi.ToString(CultureInfo.InvariantCulture) : string.Empty);
            Add(sb, double.IsNaN(e.OffsetSeconds) ? string.Empty : Number(e.OffsetSeconds * 1000.0, "0.0"));
            Add(sb, ResultName(e.Result));
            Add(sb, startPitchMidi.ToString(CultureInfo.InvariantCulture));
            Add(sb, ResetName(resetEvent));
            Add(sb, registerZone >= 0 ? registerZone.ToString(CultureInfo.InvariantCulture) : string.Empty);
            sb.Append(e.Result == DrillResult.TrackingLost ? Number(e.DurationSeconds * 1000.0, "0") : string.Empty);
            return sb.ToString();
        }

        public static string ResultName(DrillResult result) => result switch
        {
            DrillResult.Hit => "hit",
            DrillResult.WrongDirection => "wrong_direction",
            DrillResult.Miss => "miss",
            DrillResult.Extra => "extra",
            _ => "tracking_lost",
        };

        public static string ResetName(PhraseResetReason reason) => reason switch
        {
            PhraseResetReason.Task => "task",
            PhraseResetReason.Bar => "bar",
            PhraseResetReason.Rest => "rest",
            _ => "none",
        };

        private static string Letter(StepDirection d) => d switch
        {
            StepDirection.Up => "U",
            StepDirection.Down => "D",
            _ => "S",
        };

        private static string Number(double value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

        private static void Add(StringBuilder sb, string field) => sb.Append(field).Append(',');
    }
}
