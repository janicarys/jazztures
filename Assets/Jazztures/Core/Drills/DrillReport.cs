using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Jazztures.Core.Evaluation;

namespace Jazztures.Core.Drills
{
    /// <summary>
    /// The outcome of one scored <see cref="TimingDrill"/> run (ADR-0043): the
    /// <see cref="AttemptResult"/> from <see cref="OnsetScorer"/>, plus the two lists the
    /// aggregate hides — which expected onsets got no note, and which played notes matched
    /// nothing — and the two ways to put it in front of a person: a one-line summary for the
    /// screen after the run (§3.7: never mid-phrase) and a CSV for analysis.
    ///
    /// <para>
    /// <b>Derived timings only.</b> The CSV holds onset times and verdicts — no joint
    /// positions, nothing that identifies a person (CLAUDE.md §4.1 data-handling note).
    /// </para>
    /// </summary>
    public sealed class DrillReport
    {
        private readonly double[] _expected;

        private DrillReport(
            TimingDrill drill,
            OnsetWindows windows,
            double[] expected,
            AttemptResult result,
            double[] extra)
        {
            Drill = drill;
            Windows = windows;
            _expected = expected;
            Result = result;
            ExtraOnsetSeconds = extra;
        }

        public TimingDrill Drill { get; }

        public OnsetWindows Windows { get; }

        public AttemptResult Result { get; }

        /// <summary>Played notes that matched no expected onset, in time order (seconds from the shared start).</summary>
        public IReadOnlyList<double> ExtraOnsetSeconds { get; }

        /// <summary>The one-line summary for the screen — see <see cref="FormatSummary"/>.</summary>
        public string SummaryLine => FormatSummary(Result);

        /// <summary>
        /// Score the learner's <paramref name="actualOnsetSeconds"/> (from the shared start)
        /// against <paramref name="drill"/>'s expected onsets.
        /// </summary>
        public static DrillReport Build(
            TimingDrill drill, IReadOnlyList<double> actualOnsetSeconds, OnsetWindows windows)
        {
            if (drill == null)
            {
                throw new ArgumentNullException(nameof(drill));
            }

            if (actualOnsetSeconds == null)
            {
                throw new ArgumentNullException(nameof(actualOnsetSeconds));
            }

            double[] expected = drill.ExpectedOnsetSeconds();
            Array.Sort(expected);

            var actual = new double[actualOnsetSeconds.Count];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = actualOnsetSeconds[i];
            }

            Array.Sort(actual);

            AttemptResult result = OnsetScorer.Evaluate(expected, actual, windows);

            // The scorer reports counts; the CSV wants the notes themselves. Every matched
            // onset carries the exact actual time it claimed, so the extras are what is left.
            var remaining = new List<double>(actual);
            foreach (ScoredOnset matched in result.Onsets)
            {
                remaining.Remove(matched.ActualSeconds);
            }

            return new DrillReport(drill, windows, expected, result, remaining.ToArray());
        }

        /// <summary>
        /// "16 notes: 12 on time · 3 close · 1 off · 0 missed · 1 extra · avg 34 ms late".
        /// The average is the mean <i>signed</i> error: late is positive. A negative mean is
        /// ordinary — people tap ahead of a click (Repp, 2005) — not a fault.
        /// </summary>
        public static string FormatSummary(AttemptResult result)
        {
            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0} notes: {1} on time · {2} close · {3} off · {4} missed · {5} extra",
                result.ExpectedCount, result.OnTimeCount, result.CloseCount, result.OffCount,
                result.MissedCount, result.ExtraCount);

            if (result.MatchedCount == 0)
            {
                return line;
            }

            double meanMs = result.MeanSignedDeviationSeconds * 1000.0;
            long rounded = (long)Math.Round(Math.Abs(meanMs), MidpointRounding.AwayFromZero);
            if (rounded == 0)
            {
                return line + " · avg on the beat";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} · avg {1} ms {2}",
                line, rounded, meanMs > 0.0 ? "late" : "early");
        }

        /// <summary>
        /// Write the run as CSV: <c>#</c>-prefixed <c>key,value</c> header lines (read them
        /// with <c>comment='#'</c>), then one row per expected onset, missed or played, and one
        /// per extra note. Times are seconds from the shared start — the start of the
        /// count-in — and use the invariant culture, so a tester's locale cannot change the
        /// decimal separator.
        /// </summary>
        public void WriteCsv(TextWriter writer, DrillRunInfo info)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            Header(writer, "drill", Drill.Name);
            Header(writer, "started_utc", info.StartedAtUtc);
            Header(writer, "tester", info.TesterCode);
            Header(writer, "build", info.Build);
            Header(writer, "mechanic", info.Mechanic);
            Header(writer, "harmony_commit", info.HarmonyCommit);
            Header(writer, "tempo_bpm", Number(Drill.Tempo.BeatsPerMinute, "0.##"));
            Header(writer, "beats_per_bar", Drill.BeatsPerBar.ToString(CultureInfo.InvariantCulture));
            Header(writer, "subdivision", Drill.Subdivision.ToString());
            Header(writer, "swing", Number(Drill.Swing.Value, "0.###"));
            Header(writer, "scored_hand", Drill.ScoredHand.ToString());
            Header(writer, "pattern", Drill.Pattern.ToString());
            Header(writer, "count_in_bars", Drill.CountInBars.ToString(CultureInfo.InvariantCulture));
            Header(writer, "scored_bars", Drill.Bars.ToString(CultureInfo.InvariantCulture));
            Header(writer, "scored_from_s", Number(Drill.CountInSeconds, "0.0000"));
            Header(writer, "harmony", Drill.HarmonyKind.ToString());
            Header(writer, "harmony_audible", Drill.HarmonyAudible ? "true" : "false");
            Header(writer, "on_time_ms", Number(Windows.OnTimeSeconds * 1000.0, "0.#"));
            Header(writer, "close_ms", Number(Windows.CloseSeconds * 1000.0, "0.#"));
            Header(writer, "match_ms", Number(Windows.MatchSeconds * 1000.0, "0.#"));

            writer.Write("index,kind,expected_s,actual_s,deviation_ms,verdict\n");

            int index = 0;
            int matchedCursor = 0;
            IReadOnlyList<ScoredOnset> matched = Result.Onsets;
            foreach (double expected in _expected)
            {
                // ScoredOnsets come back in expected order, one per matched expected onset, so
                // an expected onset is matched exactly when the next scored one is its own.
                if (matchedCursor < matched.Count && matched[matchedCursor].ExpectedSeconds == expected)
                {
                    ScoredOnset onset = matched[matchedCursor++];
                    Row(writer, index++, "onset", expected, onset.ActualSeconds, onset.DeviationSeconds * 1000.0,
                        onset.Verdict.ToString());
                }
                else
                {
                    Row(writer, index++, "missed", expected, null, null, "Missed");
                }
            }

            foreach (double extra in ExtraOnsetSeconds)
            {
                Row(writer, index++, "extra", null, extra, null, "Extra");
            }
        }

        private static void Header(TextWriter writer, string key, string value)
        {
            writer.Write("# ");
            writer.Write(key);
            writer.Write(',');
            writer.Write(Clean(value));
            writer.Write('\n');
        }

        private static void Row(
            TextWriter writer, int index, string kind, double? expected, double? actual, double? deviationMs,
            string verdict)
        {
            writer.Write(index.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(kind);
            writer.Write(',');
            writer.Write(expected.HasValue ? Number(expected.Value, "0.0000") : string.Empty);
            writer.Write(',');
            writer.Write(actual.HasValue ? Number(actual.Value, "0.0000") : string.Empty);
            writer.Write(',');
            writer.Write(deviationMs.HasValue ? Number(deviationMs.Value, "0.0") : string.Empty);
            writer.Write(',');
            writer.Write(verdict);
            writer.Write('\n');
        }

        private static string Number(double value, string format) =>
            value.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>A header value is free text; keep it on one line.</summary>
        private static string Clean(string value) =>
            (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
    }
}
