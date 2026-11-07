using System.Collections.Generic;
using System.Linq;
using Jazztures.Core.Melody;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    // Up-up at 60 BPM straight quarters, first beat at t = 10, two tasks: targets at 10..17, all Up. Window 150 ms.
    public class DrillMatcherTests
    {
        private DrillMatcher _matcher = null!;
        private List<DrillEvent> _events = null!;

        [SetUp]
        public void SetUp()
        {
            var schedule = new DrillSchedule(
                Tempo.Bpm(60), DrillRhythm.StraightQuarters, SwingRatio.Default, SwingDrill.UpUp, 10.0, tasks: 2);
            _matcher = new DrillMatcher(schedule);
            _events = new List<DrillEvent>();
            _matcher.Scored += _events.Add;
        }

        private void Cross(double time, StepDirection sampled = StepDirection.Up) =>
            _matcher.OnCrossing(time, sampled, SwingDirection.LeftToRight, 74);

        [Test]
        public void ACrossingNearATarget_InTheRightDirection_IsAHit_WithItsOffset()
        {
            Cross(10.05);

            Assert.That(_events, Has.Count.EqualTo(1));
            Assert.That(_events[0].Result, Is.EqualTo(DrillResult.Hit));
            Assert.That(_events[0].TargetIndex, Is.EqualTo(0));
            Assert.That(_events[0].OffsetSeconds, Is.EqualTo(0.05).Within(1e-9));
        }

        [Test]
        public void TheWrongDirection_IsScoredAsSuch_ButStillUsesUpTheBeat()
        {
            Cross(10.0, StepDirection.Down);
            _matcher.Advance(10.5);

            Assert.That(_events.Select(e => e.Result), Is.EqualTo(new[] { DrillResult.WrongDirection }));
        }

        [Test]
        public void ACrossingOutsideEveryWindow_IsExtra()
        {
            Cross(10.5);

            Assert.That(_events[0].Result, Is.EqualTo(DrillResult.Extra));
            Assert.That(_events[0].HasTarget, Is.False);
        }

        [Test]
        public void ACrossingBeforeAnyTarget_IsExtra()
        {
            Cross(5.0);

            Assert.That(_events[0].Result, Is.EqualTo(DrillResult.Extra));
        }

        [Test]
        public void ADoubleTrigger_ShowsUpAsAnExtra()
        {
            Cross(10.00);
            Cross(10.10);

            Assert.That(_events.Select(e => e.Result), Is.EqualTo(new[] { DrillResult.Hit, DrillResult.Extra }));
        }

        [Test]
        public void ABeatWithNoCrossing_IsAMiss_OnceItsWindowCloses_AndOnlyOnce()
        {
            _matcher.Advance(10.1);
            Assert.That(_events, Is.Empty);

            _matcher.Advance(10.2);
            _matcher.Advance(10.2);

            Assert.That(_events.Select(e => e.Result), Is.EqualTo(new[] { DrillResult.Miss }));
            Assert.That(_events[0].TargetIndex, Is.EqualTo(0));
            Assert.That(_events[0].HasCrossing, Is.False);
        }

        [Test]
        public void TheCrossingGoesToTheNearestTarget()
        {
            Cross(10.92); // 80 ms before beat 1 (t = 11), far from beat 0

            Assert.That(_events[0].TargetIndex, Is.EqualTo(1));
            Assert.That(_events[0].OffsetSeconds, Is.EqualTo(-0.08).Within(1e-9));
        }

        [Test]
        public void Finish_MakesEveryOpenTargetAMiss_AndCompletesTheRun()
        {
            Cross(10.0);
            _matcher.Finish();

            Assert.That(_events.Count(e => e.Result == DrillResult.Miss), Is.EqualTo(7));
            Assert.That(_matcher.IsComplete, Is.True);
        }

        [Test]
        public void TheSummary_CountsEachResult_AndAveragesTheOffsetOfTheMatchedOnes()
        {
            Cross(10.05);                           // hit, +50 ms
            Cross(10.90, StepDirection.Down);       // wrong direction, -100 ms
            Cross(12.5);                            // extra
            _matcher.Finish();                      // 6 misses

            DrillSummary s = _matcher.Summary;
            Assert.That((s.Hits, s.WrongDirections, s.Misses, s.Extras), Is.EqualTo((1, 1, 6, 1)));
            Assert.That(s.MeanAbsoluteOffsetSeconds, Is.EqualTo(0.075).Within(1e-9));
        }

        [Test]
        public void ATrackingLoss_IsLoggedAsAnInterval_AndNotCounted()
        {
            _matcher.OnTrackingLost(12.5, 0.35);

            Assert.That(_events[0].Result, Is.EqualTo(DrillResult.TrackingLost));
            Assert.That(_events[0].CrossingTime, Is.EqualTo(12.5));
            Assert.That(_events[0].DurationSeconds, Is.EqualTo(0.35));
            Assert.That(_matcher.Summary.Misses + _matcher.Summary.Extras, Is.Zero);
        }
    }
}
