using System.Globalization;
using System.Linq;
using System.Threading;
using Jazztures.Core.Melody;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class DrillCsvTests
    {
        private static DrillSchedule Schedule() => new DrillSchedule(
            Tempo.Bpm(60), DrillRhythm.StraightQuarters, SwingRatio.Default, SwingDrill.UpUp, 10.0, tasks: 2);

        [Test]
        public void Header_HasSixteenColumns_InTheSpecifiedOrder()
        {
            string[] columns = DrillCsv.Header.Split(',');

            Assert.That(columns, Has.Length.EqualTo(16));
            Assert.That(columns.Take(12), Is.EqualTo(new[]
            {
                "timestamp_audio", "bpm", "rhythm", "drill", "target_beat_time", "target_direction",
                "crossing_time", "swing_dir", "sampled_direction", "pitch", "offset_ms", "result",
            }));
            Assert.That(columns.Skip(12).Take(3), Is.EqualTo(new[] { "start_pitch", "reset_event", "register_zone" }));
        }

        [Test]
        public void AHitRow_CarriesEveryField()
        {
            var e = new DrillEvent(
                DrillResult.Hit, 0, 10.0, StepDirection.Up, 10.05, SwingDirection.LeftToRight,
                StepDirection.Up, 74, 0.05, 0.0);

            string row = DrillCsv.Row(Schedule(), e, 72, PhraseResetReason.Task, registerZone: -1);

            Assert.That(row, Is.EqualTo("10.050,60,straight,UU,10.000,U,10.050,LR,U,74,50.0,hit,72,task,,"));
            Assert.That(row.Split(','), Has.Length.EqualTo(16));
        }

        [Test]
        public void AMissRow_HasNoCrossingOrNote_AndIsStampedWithTheTargetTime()
        {
            var e = new DrillEvent(
                DrillResult.Miss, 2, 12.0, StepDirection.Up, double.NaN, SwingDirection.LeftToRight,
                StepDirection.Same, -1, double.NaN, 0.0);

            string row = DrillCsv.Row(Schedule(), e, 72, PhraseResetReason.None, -1);

            Assert.That(row, Is.EqualTo("12.000,60,straight,UU,12.000,U,,,,,,miss,72,none,,"));
        }

        [Test]
        public void AnExtraRow_HasACrossingButNoTarget()
        {
            var e = new DrillEvent(
                DrillResult.Extra, -1, double.NaN, StepDirection.Same, 10.1, SwingDirection.RightToLeft,
                StepDirection.Down, 70, double.NaN, 0.0);

            string row = DrillCsv.Row(Schedule(), e, 72, PhraseResetReason.None, -1);

            Assert.That(row, Is.EqualTo("10.100,60,straight,UU,,,10.100,RL,D,70,,extra,72,none,,"));
        }

        [Test]
        public void ATrackingLossRow_GivesTheIntervalStartAndItsDurationInMilliseconds()
        {
            var e = new DrillEvent(
                DrillResult.TrackingLost, -1, double.NaN, StepDirection.Same, 12.5, SwingDirection.LeftToRight,
                StepDirection.Same, -1, double.NaN, 0.35);

            string row = DrillCsv.Row(Schedule(), e, 72, PhraseResetReason.None, -1);

            Assert.That(row, Is.EqualTo("12.500,60,straight,UU,,,12.500,,,,,tracking_lost,72,none,,350"));
        }

        [Test]
        public void TheRegisterZone_IsWrittenOnlyWhenHeightZonesAreOn()
        {
            var e = new DrillEvent(
                DrillResult.Hit, 0, 10.0, StepDirection.Up, 10.0, SwingDirection.LeftToRight,
                StepDirection.Up, 74, 0.0, 0.0);

            Assert.That(DrillCsv.Row(Schedule(), e, 72, PhraseResetReason.None, 2), Does.Contain(",none,2,"));
            Assert.That(DrillCsv.Row(Schedule(), e, 72, PhraseResetReason.None, -1), Does.Contain(",none,,"));
        }

        [Test]
        public void NumbersAreWrittenWithTheInvariantCulture()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var e = new DrillEvent(
                    DrillResult.Hit, 0, 10.0, StepDirection.Up, 10.05, SwingDirection.LeftToRight,
                    StepDirection.Up, 74, 0.05, 0.0);

                Assert.That(DrillCsv.Row(Schedule(), e, 72, PhraseResetReason.None, -1), Does.StartWith("10.050,"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Test]
        public void SwungRhythm_IsNamedSwung()
        {
            var swung = new DrillSchedule(
                Tempo.Bpm(70), DrillRhythm.SwungEighths, SwingRatio.Default, SwingDrill.DownDown, 0.0, tasks: 1);
            var e = new DrillEvent(
                DrillResult.Hit, 0, 0.0, StepDirection.Down, 0.0, SwingDirection.RightToLeft,
                StepDirection.Down, 86, 0.0, 0.0);

            Assert.That(DrillCsv.Row(swung, e, 88, PhraseResetReason.None, -1), Does.Contain(",70,swung,DD,"));
        }
    }
}
