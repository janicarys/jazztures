using System;
using System.Collections.Generic;
using Jazztures.Core.Menu;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Core.Tutorial;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Tutorial
{
    public class TutorialRunnerTests
    {
        private const double Dt = 0.02;

        private static readonly TutorialThresholds T = TutorialThresholds.Default;

        private VirtualClock _clock = null!;
        private List<int> _steps = null!;
        private List<int> _timedOut = null!;
        private int _completed;

        // --- frames ----------------------------------------------------------------------

        private static MenuHandFrame BothHands() =>
            new MenuHandFrame(
                TrackingQuality.High, leftTracking: TrackingQuality.High,
                viewAngleDegrees: 25f, leftViewAngleDegrees: 25f);

        private static MenuHandFrame OnlyRight() =>
            new MenuHandFrame(TrackingQuality.High, leftTracking: TrackingQuality.NotTracked, viewAngleDegrees: 25f);

        /// <summary>Both hands tracked perfectly well, but hanging at the learner's sides, outside their view.</summary>
        private static MenuHandFrame HandsAtYourSides(float angle = 85f) =>
            new MenuHandFrame(
                TrackingQuality.High, leftTracking: TrackingQuality.High,
                viewAngleDegrees: angle, leftViewAngleDegrees: angle);

        private static MenuHandFrame NoHands() =>
            new MenuHandFrame(TrackingQuality.NotTracked, leftTracking: TrackingQuality.NotTracked);

        private static MenuHandFrame OpenHand(float thumbAngle, float maxClosure = 0f) =>
            new MenuHandFrame(TrackingQuality.High, fistAmount: 0f, maxFingerClosure: maxClosure, thumbUpAngleDegrees: thumbAngle);

        // --- harness ---------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            _clock = new VirtualClock();
            _steps = new List<int>();
            _timedOut = new List<int>();
            _completed = 0;
        }

        private TutorialRunner Runner(params TutorialStep[] steps) => RunnerWith(T, steps);

        private TutorialRunner RunnerWith(TutorialThresholds thresholds, params TutorialStep[] steps)
        {
            var runner = new TutorialRunner(new TutorialPlan(steps), _clock, thresholds);
            runner.StepChanged += (index, _) => _steps.Add(index);
            runner.Completed += () => _completed++;
            runner.TimedOut += index => _timedOut.Add(index);
            runner.Start();
            return runner;
        }

        /// <summary>Hold <paramref name="frame"/> for <paramref name="seconds"/>, fed every frame.</summary>
        private void Hold(TutorialRunner runner, MenuHandFrame frame, double seconds)
        {
            int frames = (int)Math.Round(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                _clock.Advance(Dt);
                runner.Feed(frame);
            }
        }

        private void Wait(double seconds) => _clock.Advance(seconds);

        private static TutorialStep InView() => new TutorialStep("in view", TutorialGate.HandsInView);

        private static TutorialStep OutOfView() => new TutorialStep("out of view", TutorialGate.HandsOutOfView);

        private static TutorialStep Timed(double seconds) => new TutorialStep("wait", TutorialGate.AfterSeconds, seconds);

        private static TutorialStep Fist() => new TutorialStep("fist", TutorialGate.RightFist, demo: TutorialDemo.Fist);

        private static TutorialStep ThumbUp() => new TutorialStep("thumb up", TutorialGate.OpenHandThumbUp, demo: TutorialDemo.ThumbUp);

        private static TutorialStep Flick() => new TutorialStep("flick", TutorialGate.Flick);

        private static TutorialStep Strike() => new TutorialStep("strike", TutorialGate.Strike);

        // --- starting ----------------------------------------------------------------------

        [Test]
        public void Start_BeginsOnTheFirstStep()
        {
            TutorialRunner runner = Runner(InView(), OutOfView());

            Assert.That(runner.IsRunning, Is.True);
            Assert.That(runner.StepIndex, Is.EqualTo(0));
            Assert.That(_steps, Is.EqualTo(new[] { 0 }));
            Assert.That(runner.CurrentStep!.Value.Gate, Is.EqualTo(TutorialGate.HandsInView));
        }

        [Test]
        public void Start_AfterTheFirstTime_DoesNothing()
        {
            TutorialRunner runner = Runner(InView(), OutOfView());

            runner.Start();

            Assert.That(_steps, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void NothingHappensBeforeStart()
        {
            var runner = new TutorialRunner(new TutorialPlan(new[] { Fist() }), _clock, T);

            runner.Feed(BothHands());
            runner.Notify(MenuGesture.Fist);

            Assert.That(runner.IsRunning, Is.False);
            Assert.That(runner.IsComplete, Is.False);
        }

        // --- hands in view -------------------------------------------------------------------

        [Test]
        public void HandsInView_NeedsBothHands()
        {
            TutorialRunner runner = Runner(InView(), OutOfView());

            Hold(runner, OnlyRight(), 5.0);

            Assert.That(runner.StepIndex, Is.EqualTo(0));
        }

        [Test]
        public void HandsInView_AdvancesOnceHeld_ButNotBeforeTheLineHasBeenShownLongEnough()
        {
            TutorialRunner runner = Runner(InView(), OutOfView());

            Hold(runner, BothHands(), T.InViewHoldSeconds + 0.2); // held long enough, but the line is brand new
            Assert.That(runner.StepIndex, Is.EqualTo(0), "the line must be readable first");

            Hold(runner, BothHands(), T.MinStepSeconds);
            Assert.That(runner.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void HandsInView_ABriefGlimpse_DoesNotCount()
        {
            TutorialRunner runner = Runner(InView(), OutOfView());
            Wait(T.MinStepSeconds + 1.0);

            Hold(runner, BothHands(), T.InViewHoldSeconds * 0.5);
            Hold(runner, NoHands(), 0.2);
            Hold(runner, BothHands(), T.InViewHoldSeconds * 0.5);

            Assert.That(runner.StepIndex, Is.EqualTo(0), "the hold restarted when the hands left");
        }

        // --- hands out of view -----------------------------------------------------------------

        [Test]
        public void HandsOutOfView_NeedsNeitherHandTracked()
        {
            TutorialRunner runner = Runner(OutOfView(), InView());
            Wait(T.MinStepSeconds);

            Hold(runner, OnlyRight(), 5.0);
            Assert.That(runner.StepIndex, Is.EqualTo(0));

            Hold(runner, NoHands(), T.OutOfViewHoldSeconds + 0.2);
            Assert.That(runner.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void ALowQualityHand_CountsAsOutOfView()
        {
            TutorialRunner runner = Runner(OutOfView(), InView());
            Wait(T.MinStepSeconds);

            Hold(runner, new MenuHandFrame(TrackingQuality.Low, leftTracking: TrackingQuality.Low), T.OutOfViewHoldSeconds + 0.2);

            Assert.That(runner.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void HandsInView_NeedsTheHandsInFrontOfYou_NotJustTracked()
        {
            TutorialRunner runner = Runner(InView(), OutOfView());

            Hold(runner, HandsAtYourSides(), 5.0); // perfectly tracked, but at the sides

            Assert.That(runner.StepIndex, Is.EqualTo(0));
        }

        [Test]
        public void HandsOutOfView_AcceptsHandsThatAreStillTracked_ButHangingAtTheSides()
        {
            // The Quest keeps tracking hands at the learner's sides, so tracking loss cannot be the test.
            TutorialRunner runner = Runner(OutOfView(), InView());
            Wait(T.MinStepSeconds);

            Hold(runner, HandsAtYourSides(), T.OutOfViewHoldSeconds + 0.2);

            Assert.That(runner.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void AHandBetweenFrontAndSide_IsNeitherInFrontNorOutOfView()
        {
            float between = (T.InViewMaxDegrees + T.OutOfViewMinDegrees) / 2f;
            Assume.That(between > T.InViewMaxDegrees && between < T.OutOfViewMinDegrees);

            TutorialRunner outOfView = RunnerWith(NoTimeout(), OutOfView(), InView());
            Wait(T.MinStepSeconds);
            Hold(outOfView, HandsAtYourSides(between), 3.0);
            Assert.That(outOfView.StepIndex, Is.EqualTo(0), "not far enough out");

            _clock = new VirtualClock();
            TutorialRunner inView = RunnerWith(NoTimeout(), InView(), OutOfView());
            Hold(inView, HandsAtYourSides(between), 3.0);
            Assert.That(inView.StepIndex, Is.EqualTo(0), "not far enough in");
        }

        [Test]
        public void OutOfView_OnlyOneHandAtTheSide_IsNotEnough()
        {
            TutorialRunner runner = Runner(OutOfView(), InView());
            Wait(T.MinStepSeconds);

            Hold(runner, new MenuHandFrame(
                TrackingQuality.High, leftTracking: TrackingQuality.High,
                viewAngleDegrees: 85f, leftViewAngleDegrees: 25f), 3.0);

            Assert.That(runner.StepIndex, Is.EqualTo(0));
        }

        // --- the safety valve -----------------------------------------------------------------------

        private static TutorialThresholds NoTimeout() => new TutorialThresholds(
            1.5, 0.5, 0.6, 0.35f, 35f, 50f, 0.6, 0.5, postureTimeoutSeconds: 0.0);

        [Test]
        public void APostureStepNobodyCanMeet_IsPassedAfterTheTimeout_RatherThanTrapTheLearner()
        {
            TutorialRunner runner = Runner(OutOfView(), InView());

            Hold(runner, BothHands(), T.PostureTimeoutSeconds - 1.0);
            Assert.That(runner.StepIndex, Is.EqualTo(0));
            Assert.That(_timedOut, Is.Empty);

            Hold(runner, BothHands(), 1.5);
            Assert.That(runner.StepIndex, Is.EqualTo(1));
            Assert.That(_timedOut, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void TheTimeout_CanBeSwitchedOff()
        {
            TutorialRunner runner = RunnerWith(NoTimeout(), OutOfView(), InView());

            Hold(runner, BothHands(), 120.0);

            Assert.That(runner.StepIndex, Is.EqualTo(0));
            Assert.That(_timedOut, Is.Empty);
        }

        [Test]
        public void AGestureStep_NeverTimesOut_TheMenuNeedsThoseGestures()
        {
            TutorialRunner runner = Runner(Fist(), Flick());

            Hold(runner, BothHands(), 120.0);

            Assert.That(runner.StepIndex, Is.EqualTo(0));
            Assert.That(_timedOut, Is.Empty);
        }

        [Test]
        public void TheTimedExplanation_IsNotAffectedByTheTimeout()
        {
            TutorialRunner runner = Runner(Timed(5.0), Fist());

            Hold(runner, BothHands(), 5.3);

            Assert.That(runner.StepIndex, Is.EqualTo(1));
            Assert.That(_timedOut, Is.Empty);
        }

        // --- timed ---------------------------------------------------------------------------------

        [Test]
        public void ATimedStep_MovesOnAfterItsDuration_WhateverTheHandsAreDoing()
        {
            TutorialRunner runner = Runner(Timed(5.0), Fist());

            Hold(runner, NoHands(), 4.9);
            Assert.That(runner.StepIndex, Is.EqualTo(0));

            Hold(runner, NoHands(), 0.3);
            Assert.That(runner.StepIndex, Is.EqualTo(1));
        }

        // --- gestures ----------------------------------------------------------------------------

        [Test]
        public void AFistStep_IgnoresOtherGestures_AndMovesOnWithAFist()
        {
            TutorialRunner runner = Runner(Fist(), Flick());
            Wait(T.EventGraceSeconds + 0.1);

            runner.Notify(MenuGesture.FlickLeft);
            runner.Notify(MenuGesture.Strike);
            Assert.That(runner.StepIndex, Is.EqualTo(0));

            runner.Notify(MenuGesture.Fist);
            Assert.That(runner.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void AGestureRightAfterAStepBegins_IsTheTailOfTheLastMotion_AndIsIgnored()
        {
            TutorialRunner runner = Runner(Flick(), Strike());
            Wait(T.EventGraceSeconds + 0.1);
            runner.Notify(MenuGesture.FlickRight);
            Assert.That(runner.StepIndex, Is.EqualTo(1));

            Wait(T.EventGraceSeconds * 0.5);
            runner.Notify(MenuGesture.Strike);
            Assert.That(runner.StepIndex, Is.EqualTo(1), "too soon after the step began");

            Wait(T.EventGraceSeconds);
            runner.Notify(MenuGesture.Strike);
            Assert.That(runner.IsComplete, Is.True);
        }

        [Test]
        public void AFlickStep_AcceptsEitherDirection()
        {
            TutorialRunner left = Runner(Flick(), Strike());
            Wait(T.EventGraceSeconds + 0.1);
            left.Notify(MenuGesture.FlickLeft);
            Assert.That(left.StepIndex, Is.EqualTo(1));

            _clock = new VirtualClock();
            TutorialRunner right = Runner(Flick(), Strike());
            Wait(T.EventGraceSeconds + 0.1);
            right.Notify(MenuGesture.FlickRight);
            Assert.That(right.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void AStrikeStep_AcceptsOnlyAStrike()
        {
            TutorialRunner runner = Runner(Strike());
            Wait(T.EventGraceSeconds + 0.1);

            runner.Notify(MenuGesture.FlickLeft);
            runner.Notify(MenuGesture.FlickRight);
            Assert.That(runner.IsComplete, Is.False);

            runner.Notify(MenuGesture.Strike);
            Assert.That(runner.IsComplete, Is.True);
        }

        // --- a fist skips ---------------------------------------------------------------------------------

        [TestCase("hands in view")]
        [TestCase("hands out of view")]
        [TestCase("the timed explanation")]
        [TestCase("thumb up")]
        [TestCase("flick")]
        [TestCase("strike")]
        public void AFist_OnAnyStepButTheFistStep_EndsTheTutorial(string step)
        {
            TutorialStep s = step switch
            {
                "hands in view" => InView(),
                "hands out of view" => OutOfView(),
                "the timed explanation" => Timed(5.0),
                "thumb up" => ThumbUp(),
                "flick" => Flick(),
                _ => Strike(),
            };
            TutorialRunner runner = Runner(s, Strike());
            Wait(T.EventGraceSeconds + 0.1);

            runner.Notify(MenuGesture.Fist);

            Assert.That(runner.IsComplete, Is.True);
            Assert.That(_completed, Is.EqualTo(1));
        }

        [Test]
        public void AFist_OnTheFistStep_IsTheLesson_NotASkip()
        {
            TutorialRunner runner = Runner(Fist(), ThumbUp());
            Wait(T.EventGraceSeconds + 0.1);

            runner.Notify(MenuGesture.Fist);

            Assert.That(runner.IsComplete, Is.False);
            Assert.That(runner.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void AFist_RightAfterAStepBegins_IsTheTailOfTheLastMotion_AndDoesNotSkip()
        {
            // The fist that opened the menu is still closing as the next step starts.
            TutorialRunner runner = Runner(Fist(), ThumbUp());
            Wait(T.EventGraceSeconds + 0.1);
            runner.Notify(MenuGesture.Fist);
            Assert.That(runner.StepIndex, Is.EqualTo(1));

            runner.Notify(MenuGesture.Fist);

            Assert.That(runner.IsComplete, Is.False);
            Assert.That(runner.StepIndex, Is.EqualTo(1));
        }

        // --- open hand, thumb up -----------------------------------------------------------------------

        [Test]
        public void ThumbUp_NeedsAnOpenHand()
        {
            TutorialRunner runner = Runner(ThumbUp(), Flick());
            Wait(T.MinStepSeconds);

            Hold(runner, OpenHand(thumbAngle: 10f, maxClosure: 0.9f), 3.0); // thumb up, but a fist

            Assert.That(runner.StepIndex, Is.EqualTo(0));
        }

        [Test]
        public void ThumbUp_NeedsTheThumbPointingUp()
        {
            TutorialRunner runner = Runner(ThumbUp(), Flick());
            Wait(T.MinStepSeconds);

            Hold(runner, OpenHand(thumbAngle: 90f), 3.0); // an open hand, thumb forward

            Assert.That(runner.StepIndex, Is.EqualTo(0));
        }

        [Test]
        public void ThumbUp_AdvancesOnceAnOpenHandIsHeldThumbUp()
        {
            TutorialRunner runner = Runner(ThumbUp(), Flick());
            Wait(T.MinStepSeconds);

            Hold(runner, OpenHand(thumbAngle: 15f), T.ThumbUpHoldSeconds + 0.2);

            Assert.That(runner.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void ThumbUp_HasHysteresis_ASlightDriftOnceInDoesNotLoseIt()
        {
            TutorialRunner runner = Runner(ThumbUp(), Flick());
            Wait(T.MinStepSeconds);
            float betweenTheEdges = (T.ThumbUpEnterDegrees + T.ThumbUpExitDegrees) / 2f;

            // From cold, an angle between the edges is not "thumb up"...
            Hold(runner, OpenHand(betweenTheEdges), 3.0);
            Assert.That(runner.StepIndex, Is.EqualTo(0));

            // ...but once in, drifting there does not drop it.
            Hold(runner, OpenHand(10f), 0.2);
            Hold(runner, OpenHand(betweenTheEdges), T.ThumbUpHoldSeconds + 0.2);
            Assert.That(runner.StepIndex, Is.EqualTo(1));
        }

        [Test]
        public void ThumbUp_ARelaxingHandStartsTheHoldOver()
        {
            TutorialRunner runner = Runner(ThumbUp(), Flick());
            Wait(T.MinStepSeconds);

            Hold(runner, OpenHand(10f), T.ThumbUpHoldSeconds * 0.6);
            Hold(runner, OpenHand(120f), 0.1);
            Hold(runner, OpenHand(10f), T.ThumbUpHoldSeconds * 0.6);

            Assert.That(runner.StepIndex, Is.EqualTo(0));
        }

        // --- finishing ---------------------------------------------------------------------------------

        [Test]
        public void TheLastStepCompleting_RaisesCompletedOnce_AndEndsTheTutorial()
        {
            TutorialRunner runner = Runner(Strike());
            Wait(T.EventGraceSeconds + 0.1);

            runner.Notify(MenuGesture.Strike);
            runner.Notify(MenuGesture.Strike);

            Assert.That(_completed, Is.EqualTo(1));
            Assert.That(runner.IsComplete, Is.True);
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(runner.CurrentStep, Is.Null);
        }

        [Test]
        public void AfterTheEnd_FramesAndGesturesAreIgnored()
        {
            TutorialRunner runner = Runner(Strike());
            Wait(T.EventGraceSeconds + 0.1);
            runner.Notify(MenuGesture.Strike);
            _steps.Clear();

            runner.Feed(BothHands());
            runner.Notify(MenuGesture.Fist);

            Assert.That(_steps, Is.Empty);
            Assert.That(_completed, Is.EqualTo(1));
        }

        [Test]
        public void Skip_EndsTheTutorialAtOnce()
        {
            TutorialRunner runner = Runner(InView(), Fist(), Strike());

            runner.Skip();

            Assert.That(runner.IsComplete, Is.True);
            Assert.That(_completed, Is.EqualTo(1));
        }

        // --- the whole script ----------------------------------------------------------------------------------

        [Test]
        public void TheWholeScript_RunsStartToFinish_GatedByWhatTheLearnerDoes()
        {
            TutorialRunner runner = Runner(
                InView(),                       // 0 place your hands in front of you
                OutOfView(),                    // 1 move them to your side
                InView(),                       // 2 bring them back
                Timed(5.0),                     // 3 the headset can detect it
                Fist(),                         // 4 make a fist
                ThumbUp(),                      // 5 open hand, thumb up
                Flick(),                        // 6 swipe
                Strike());                      // 7 strike

            Hold(runner, BothHands(), 3.0);
            Assert.That(runner.StepIndex, Is.EqualTo(1));

            Hold(runner, NoHands(), 3.0);
            Assert.That(runner.StepIndex, Is.EqualTo(2));

            Hold(runner, BothHands(), 3.0);
            Assert.That(runner.StepIndex, Is.EqualTo(3));

            Hold(runner, BothHands(), 5.2);
            Assert.That(runner.StepIndex, Is.EqualTo(4));

            Wait(T.EventGraceSeconds + 0.1);
            runner.Notify(MenuGesture.Fist);
            Assert.That(runner.StepIndex, Is.EqualTo(5));

            Wait(T.MinStepSeconds);
            Hold(runner, OpenHand(10f), 2.5);
            Assert.That(runner.StepIndex, Is.EqualTo(6));

            Wait(T.EventGraceSeconds + 0.1);
            runner.Notify(MenuGesture.FlickRight);
            Assert.That(runner.StepIndex, Is.EqualTo(7));

            Wait(T.EventGraceSeconds + 0.1);
            runner.Notify(MenuGesture.Strike);

            Assert.That(runner.IsComplete, Is.True);
            Assert.That(_steps, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));
            Assert.That(_completed, Is.EqualTo(1));
        }

        // --- plan and thresholds -------------------------------------------------------------------------

        [Test]
        public void ATimedStepWithoutADuration_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TutorialStep("wait", TutorialGate.AfterSeconds));
        }

        [Test]
        public void AStepWithoutText_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new TutorialStep("  ", TutorialGate.Strike));
        }

        [Test]
        public void AnEmptyPlan_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => new TutorialPlan(new TutorialStep[0]));
        }

        [Test]
        public void ThumbUpThresholds_MustHaveAWiderExitEdgeThanEnterEdge()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TutorialThresholds(1.5, 0.5, 0.6, 0.35f, 50f, 35f, 0.6, 0.5));
        }

        [Test]
        public void TheOutOfViewAngle_MustBeBeyondTheInFrontAngle()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TutorialThresholds(
                1.5, 0.5, 0.6, 0.35f, 35f, 50f, 0.6, 0.5, inViewMaxDegrees: 60f, outOfViewMinDegrees: 50f));
        }

        [Test]
        public void TheDemoAndForwardingOfAStep_AreCarriedThrough()
        {
            var step = new TutorialStep("flick", TutorialGate.Flick, demo: TutorialDemo.Flick, showsMenu: true,
                forward: TutorialForward.Flick);

            Assert.That(step.Demo, Is.EqualTo(TutorialDemo.Flick));
            Assert.That(step.ShowsMenu, Is.True);
            Assert.That(step.Forward, Is.EqualTo(TutorialForward.Flick));
        }
    }
}
