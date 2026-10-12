using System;
using System.Globalization;
using System.IO;
using System.Threading;
using Jazztures.Core.Drills;
using Jazztures.Core.Evaluation;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Drills
{
    public class DrillReportTests
    {
        private static readonly DrillRunInfo Info =
            new DrillRunInfo("arc+touch", "Touch", "0.1+abc12345", "T01", "2026-10-08T12:00:00Z");

        // 60 BPM, no count-in, 4 bars of 4: expected onsets at 0, 1, 2, ... 15 s.
        private static TimingDrill Sixteen(string name = "Right hand, every beat") =>
            new TimingDrillBuilder()
                .Named(name)
                .WithTempo(Tempo.Bpm(60))
                .WithBars(4)
                .WithCountInBars(0)
                .WithPattern(OnsetPattern.EveryBeat)
                .Build();

        private static double[] Perfect()
        {
            var played = new double[16];
            for (int i = 0; i < played.Length; i++)
            {
                played[i] = i;
            }

            return played;
        }

        private static string Csv(DrillReport report)
        {
            var writer = new StringWriter();
            report.WriteCsv(writer, Info);
            return writer.ToString();
        }

        // --- scoring --------------------------------------------------------------------------

        [Test]
        public void APerfectRun_ScoresEveryBeatOnTime()
        {
            DrillReport report = DrillReport.Build(Sixteen(), Perfect(), OnsetWindows.Default);

            Assert.That(report.Result.OnTimeCount, Is.EqualTo(16));
            Assert.That(report.Result.MissedCount, Is.EqualTo(0));
            Assert.That(report.Result.ExtraCount, Is.EqualTo(0));
            Assert.That(report.ExtraOnsetSeconds, Is.Empty);
        }

        [Test]
        public void TheExtraNotes_AreReportedByTime_NotJustCounted()
        {
            double[] played = Perfect();
            var withDouble = new double[17];
            Array.Copy(played, withDouble, 16);
            withDouble[16] = 5.4; // a stray double in the middle of the run

            DrillReport report = DrillReport.Build(Sixteen(), withDouble, OnsetWindows.Default);

            Assert.That(report.Result.ExtraCount, Is.EqualTo(1));
            Assert.That(report.ExtraOnsetSeconds, Is.EqualTo(new[] { 5.4 }));
        }

        [Test]
        public void ANoteScoredAgainstTheDrillsOwnGrid_IncludingTheCountIn()
        {
            TimingDrill withCountIn = new TimingDrillBuilder()
                .Named("c")
                .WithTempo(Tempo.Bpm(60))
                .WithBars(1)
                .WithCountInBars(1)
                .WithPattern(OnsetPattern.EveryBeat)
                .Build();

            // The scored region starts at 4 s; a note at 4.02 s is on time, one at 1.0 s is extra.
            DrillReport report = DrillReport.Build(
                withCountIn, new[] { 1.0, 4.02, 5.0, 6.0, 7.0 }, OnsetWindows.Default);

            Assert.That(report.Result.OnTimeCount, Is.EqualTo(4));
            Assert.That(report.Result.ExtraCount, Is.EqualTo(1));
            Assert.That(report.ExtraOnsetSeconds, Is.EqualTo(new[] { 1.0 }));
        }

        // --- the one-line summary -------------------------------------------------------------

        [Test]
        public void TheSummaryLine_MatchesTheAdrExample()
        {
            // 12 on time, 3 close, 1 off, 0 missed, 1 extra, averaging 34 ms late.
            var played = new System.Collections.Generic.List<double>();
            for (int i = 0; i < 16; i++)
            {
                double deviation = i < 12 ? 0.034 : i < 15 ? 0.100 : 0.250;
                played.Add(i + deviation);
            }

            played.Add(7.5); // one extra
            DrillReport report = DrillReport.Build(Sixteen(), played, OnsetWindows.Default);

            // mean = (12*34 + 3*100 + 250)/16 = 58 ms with these numbers; assert the shape.
            Assert.That(report.SummaryLine, Does.StartWith("16 notes: 12 on time · 3 close · 1 off · 0 missed · 1 extra · avg "));
            Assert.That(report.SummaryLine, Does.EndWith(" ms late"));
        }

        [Test]
        public void TheAverage_IsSigned_LateOrEarly()
        {
            DrillReport late = DrillReport.Build(Sixteen(), ShiftedBy(0.034), OnsetWindows.Default);
            DrillReport early = DrillReport.Build(Sixteen(), ShiftedBy(-0.034), OnsetWindows.Default);

            Assert.That(late.SummaryLine, Does.EndWith("avg 34 ms late"));
            Assert.That(early.SummaryLine, Does.EndWith("avg 34 ms early"), "rushing is ordinary, and reported as early");
        }

        [Test]
        public void ADeviationUnderHalfAMillisecond_ReadsAsOnTheBeat()
        {
            DrillReport report = DrillReport.Build(Sixteen(), ShiftedBy(0.0002), OnsetWindows.Default);

            Assert.That(report.SummaryLine, Does.EndWith("avg on the beat"));
        }

        [Test]
        public void ARunWithNoMatchedNotes_HasNoAverage()
        {
            DrillReport report = DrillReport.Build(Sixteen(), Array.Empty<double>(), OnsetWindows.Default);

            Assert.That(report.SummaryLine, Is.EqualTo("16 notes: 0 on time · 0 close · 0 off · 16 missed · 0 extra"));
        }

        [Test]
        public void TheSummary_IsCultureIndependent()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                DrillReport report = DrillReport.Build(Sixteen(), ShiftedBy(0.034), OnsetWindows.Default);

                Assert.That(report.SummaryLine, Does.Contain("avg 34 ms late"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        // --- the CSV --------------------------------------------------------------------------

        [Test]
        public void TheCsv_StartsWithAHeaderOfCommentLines()
        {
            string csv = Csv(DrillReport.Build(Sixteen(), Perfect(), OnsetWindows.Default));
            string[] lines = csv.Split('\n');

            Assert.That(lines[0], Is.EqualTo("# drill,Right hand, every beat"));
            Assert.That(csv, Does.Contain("# tester,T01\n"));
            Assert.That(csv, Does.Contain("# build,0.1+abc12345\n"));
            Assert.That(csv, Does.Contain("# mechanic,arc+touch\n"));
            Assert.That(csv, Does.Contain("# harmony_commit,Touch\n"));
            Assert.That(csv, Does.Contain("# tempo_bpm,60\n"));
            Assert.That(csv, Does.Contain("# subdivision,None\n"));
            Assert.That(csv, Does.Contain("# swing,0.5\n"));
            Assert.That(csv, Does.Contain("# scored_hand,Right\n"));
            Assert.That(csv, Does.Contain("# on_time_ms,80\n"));
        }

        [Test]
        public void TheCsv_HasOneRowPerExpectedOnset_UnderAColumnHeader()
        {
            string csv = Csv(DrillReport.Build(Sixteen(), Perfect(), OnsetWindows.Default));
            string[] lines = csv.TrimEnd('\n').Split('\n');

            int header = Array.IndexOf(lines, "index,kind,expected_s,actual_s,deviation_ms,verdict");
            Assert.That(header, Is.GreaterThan(0));
            Assert.That(lines.Length - header - 1, Is.EqualTo(16));
            Assert.That(lines[header + 1], Is.EqualTo("0,onset,0.0000,0.0000,0.0,OnTime"));
        }

        [Test]
        public void AMatchedRow_CarriesTheSignedDeviationAndVerdict()
        {
            string csv = Csv(DrillReport.Build(Sixteen(), ShiftedBy(0.034), OnsetWindows.Default));

            Assert.That(csv, Does.Contain("\n0,onset,0.0000,0.0340,34.0,OnTime\n"));
        }

        [Test]
        public void AMissedOnset_IsARowWithNoActual()
        {
            double[] played = Perfect();
            double[] skipOne = new double[15];
            Array.Copy(played, 0, skipOne, 0, 3);
            Array.Copy(played, 4, skipOne, 3, 12); // beat at 3 s never played

            string csv = Csv(DrillReport.Build(Sixteen(), skipOne, OnsetWindows.Default));

            Assert.That(csv, Does.Contain("\n3,missed,3.0000,,,Missed\n"));
            Assert.That(csv, Does.Contain("\n4,onset,4.0000,4.0000,0.0,OnTime\n"), "rows stay in expected order");
        }

        [Test]
        public void AnExtraNote_IsARowWithNoExpected()
        {
            var withDouble = new double[17];
            Array.Copy(Perfect(), withDouble, 16);
            withDouble[16] = 5.4;

            string csv = Csv(DrillReport.Build(Sixteen(), withDouble, OnsetWindows.Default));

            Assert.That(csv, Does.Contain("\n16,extra,,5.4000,,Extra\n"));
        }

        [Test]
        public void TheCsv_UsesTheInvariantCulture_WhateverTheTestersLocale()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE"); // decimal comma
                string csv = Csv(DrillReport.Build(Sixteen(), ShiftedBy(0.034), OnsetWindows.Default));

                Assert.That(csv, Does.Contain("0,onset,0.0000,0.0340,34.0,OnTime"));
                Assert.That(csv, Does.Not.Contain("0,0340"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void AHeaderValueWithANewline_StaysOnOneLine()
        {
            string csv = Csv(DrillReport.Build(Sixteen("two\nlines"), Perfect(), OnsetWindows.Default));

            Assert.That(csv.Split('\n')[0], Is.EqualTo("# drill,two lines"));
        }

        [Test]
        public void TheCsv_HoldsDerivedTimingsOnly()
        {
            string csv = Csv(DrillReport.Build(Sixteen(), Perfect(), OnsetWindows.Default));

            Assert.That(csv.ToLowerInvariant(), Does.Not.Contain("joint"));
            Assert.That(csv.ToLowerInvariant(), Does.Not.Contain("position"));
        }

        [Test]
        public void BuildAndWrite_RejectNulls()
        {
            DrillReport report = DrillReport.Build(Sixteen(), Perfect(), OnsetWindows.Default);

            Assert.Throws<ArgumentNullException>(() => DrillReport.Build(null!, Perfect(), OnsetWindows.Default));
            Assert.Throws<ArgumentNullException>(() => DrillReport.Build(Sixteen(), null!, OnsetWindows.Default));
            Assert.Throws<ArgumentNullException>(() => report.WriteCsv(null!, Info));
            Assert.Throws<ArgumentNullException>(() => report.WriteCsv(new StringWriter(), null!));
        }

        private static double[] ShiftedBy(double seconds)
        {
            double[] played = Perfect();
            for (int i = 0; i < played.Length; i++)
            {
                played[i] += seconds;
            }

            return played;
        }
    }
}
