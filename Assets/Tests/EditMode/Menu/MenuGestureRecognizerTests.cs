using System;
using System.Collections.Generic;
using Jazztures.Core.Menu;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Menu
{
    public class MenuGestureRecognizerTests
    {
        private const double FrameSeconds = 0.02;

        private static readonly MenuGestureThresholds T = MenuGestureThresholds.Default;

        private VirtualClock _clock = null!;
        private MenuGestureRecognizer _recognizer = null!;
        private List<MenuGesture> _seen = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new VirtualClock();
            _recognizer = new MenuGestureRecognizer(_clock, T);
            _seen = new List<MenuGesture>();
            _recognizer.Recognized += _seen.Add;

            Quiet(10); // become usable (3 High frames), see an open hand, settle, arm
            Assume.That(_seen, Is.Empty);
        }

        // --- helpers ---------------------------------------------------------------------

        private void Feed(MenuHandFrame frame)
        {
            _clock.Advance(FrameSeconds);
            _recognizer.Feed(frame);
        }

        private void Quiet(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                Feed(new MenuHandFrame(TrackingQuality.High));
            }
        }

        private void Move(float left = 0f, float right = 0f, float down = 0f, int frames = 1)
        {
            for (int i = 0; i < frames; i++)
            {
                Feed(new MenuHandFrame(TrackingQuality.High, left, right, down));
            }
        }

        private void Fist(double seconds, float amount = 1f)
        {
            int frames = (int)Math.Round(seconds / FrameSeconds);
            for (int i = 0; i < frames; i++)
            {
                Feed(new MenuHandFrame(TrackingQuality.High, fistAmount: amount));
            }
        }

        private void Wait(double seconds) => _clock.Advance(seconds);

        // --- flick -----------------------------------------------------------------------

        [Test]
        public void FastRightwardMotion_RaisesFlickRight()
        {
            Move(right: 1.2f);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.FlickRight }));
        }

        [Test]
        public void FastLeftwardMotion_RaisesFlickLeft()
        {
            Move(left: 1.2f);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.FlickLeft }));
        }

        [Test]
        public void SidewaysMotionBelowTheEnterSpeed_DoesNothing()
        {
            Move(right: T.FlickEnterSpeedMetresPerSecond - 0.05f, frames: 5);

            Assert.That(_seen, Is.Empty);
        }

        [Test]
        public void ALongFlick_FiresOnce()
        {
            Move(right: 1.2f, frames: 12);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.FlickRight }));
        }

        [Test]
        public void ADiagonalSwipe_IsNeitherAFlickNorAStrike()
        {
            Move(right: 1.0f, down: 0.8f, frames: 5);

            Assert.That(_seen, Is.Empty);
        }

        // --- strike ----------------------------------------------------------------------

        [Test]
        public void FastDownwardMotion_RaisesStrike()
        {
            Move(down: 0.9f);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.Strike }));
        }

        [Test]
        public void DownwardMotionBelowTheEnterSpeed_DoesNothing()
        {
            Move(down: T.StrikeEnterSpeedMetresPerSecond - 0.05f, frames: 5);

            Assert.That(_seen, Is.Empty);
        }

        [Test]
        public void AStrikeWithASidewaysDrift_StillCounts_WhenDownDominates()
        {
            Move(right: 0.2f, down: 0.9f);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.Strike }));
        }

        // --- settling, spacing and return strokes -----------------------------------------

        [Test]
        public void AGestureCannotFireAgain_UntilTheHandHasSettled()
        {
            Move(right: 1.2f);
            Wait(0.3);
            Move(right: 0.5f, frames: 5);  // still moving: above the exit speed
            Move(right: 1.2f);

            Assert.That(_seen, Has.Count.EqualTo(1));

            Quiet(T.SettleFrames);
            Wait(0.3);
            Move(right: 1.2f);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.FlickRight, MenuGesture.FlickRight }));
        }

        [Test]
        public void ARapidSecondGesture_InsideTheMinimumGap_IsIgnored()
        {
            Move(right: 1.2f);
            Quiet(T.SettleFrames);          // 60 ms: settled, but far inside the 250 ms gap
            Move(right: 1.2f);

            Assert.That(_seen, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheReturnStrokeAfterAFlick_IsSwallowed()
        {
            Move(right: 1.2f);
            Quiet(T.SettleFrames);
            Wait(0.25);                      // past the min gap, inside the opposite-flick lockout
            Move(left: 1.2f, frames: 3);     // the hand coming back

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.FlickRight }));
        }

        [Test]
        public void ADeliberateFlickBack_AfterTheLockout_Counts()
        {
            Move(right: 1.2f);
            Quiet(T.SettleFrames);
            Wait(T.OppositeFlickLockoutSeconds + 0.1);
            Move(left: 1.2f);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.FlickRight, MenuGesture.FlickLeft }));
        }

        [Test]
        public void ASecondFlickTheSameWay_IsNotLockedOut()
        {
            Move(right: 1.2f);
            Quiet(T.SettleFrames);
            Wait(0.3);
            Move(right: 1.2f);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.FlickRight, MenuGesture.FlickRight }));
        }

        [Test]
        public void AStrikeRightAfterAFlick_Counts_OnceSettled()
        {
            Move(right: 1.2f);
            Quiet(T.SettleFrames);
            Wait(0.3);
            Move(down: 0.9f);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.FlickRight, MenuGesture.Strike }));
        }

        // --- fist ------------------------------------------------------------------------

        [Test]
        public void AFistHeldForTheMenuHold_RaisesFist_Once()
        {
            Fist(T.FistHoldSecondsInMenu + 0.2);
            Fist(1.0); // keep holding

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.Fist }));
        }

        [Test]
        public void AFistReleasedEarly_DoesNothing_AndProgressFallsBack()
        {
            Fist(0.2);
            Assert.That(_recognizer.FistProgress, Is.GreaterThan(0f));

            Quiet(5);

            Assert.That(_seen, Is.Empty);
            Assert.That(_recognizer.FistProgress, Is.EqualTo(0f));
        }

        [Test]
        public void FistProgress_RisesTowardsOne_AsTheHoldGoesOn()
        {
            Fist(0.1);
            float early = _recognizer.FistProgress;
            Fist(0.1);
            float later = _recognizer.FistProgress;

            Assert.That(later, Is.GreaterThan(early));
            Assert.That(later, Is.LessThan(1f));
        }

        [Test]
        public void AFewOpenFramesInsideAFistHold_AreTolerated()
        {
            Fist(0.15);
            Quiet(T.FistMissToleranceFrames);
            Fist(0.3);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.Fist }));
        }

        [Test]
        public void MoreOpenFramesThanTheTolerance_CancelTheHold()
        {
            Fist(0.15);
            Quiet(T.FistMissToleranceFrames + 1);
            Fist(0.15);

            Assert.That(_seen, Is.Empty);
        }

        [Test]
        public void AFistFiresAgain_OnlyAfterTheHandHasBeenOpened()
        {
            Fist(T.FistHoldSecondsInMenu + 0.2);
            Quiet(3);
            Fist(T.FistHoldSecondsInMenu + 0.2);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.Fist, MenuGesture.Fist }));
        }

        [Test]
        public void ASemiClosedHand_BetweenTheEdges_DoesNotStartAFist()
        {
            Fist(1.0, amount: (T.FistEnterAmount + T.FistExitAmount) / 2f);

            Assert.That(_seen, Is.Empty);
        }

        [Test]
        public void WhileARunIsGoing_TheFistNeedsTheLongerHold()
        {
            _recognizer.SetContext(MenuInputContext.Running);
            Quiet(2);                                  // open hand first (the fist latch)

            Fist(T.FistHoldSecondsInMenu + 0.3);
            Assert.That(_seen, Is.Empty);

            Fist(T.FistHoldSecondsInRun);
            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.Fist }));
        }

        [Test]
        public void WhileARunIsGoing_FlicksAndStrikesAreIgnored()
        {
            _recognizer.SetContext(MenuInputContext.Running);
            Quiet(10);
            Wait(1.0);

            Move(right: 1.2f);
            Quiet(5);
            Move(down: 1.2f);

            Assert.That(_seen, Is.Empty);
        }

        [Test]
        public void AFistStillClosedWhenTheContextChanges_NeedsOpeningBeforeItCanFire()
        {
            Fist(0.1);
            _recognizer.SetContext(MenuInputContext.Running); // e.g. a lesson has just started

            Fist(T.FistHoldSecondsInRun + 0.5);                // still clenched: nothing

            Assert.That(_seen, Is.Empty);
        }

        [Test]
        public void ClosingTheHand_DoesNotAlsoStrike()
        {
            // The hand drops as the fist forms: a strike's worth of downward speed with a fist on top.
            Move(down: 0.9f);
            _seen.Clear();
            Quiet(T.SettleFrames + 5);
            Wait(0.5);

            Feed(new MenuHandFrame(TrackingQuality.High, downwardSpeed: 0.9f, fistAmount: 1f));
            Feed(new MenuHandFrame(TrackingQuality.High, downwardSpeed: 0.9f, fistAmount: 1f));

            Assert.That(_seen, Is.Empty);
        }

        // --- entry grace -----------------------------------------------------------------

        [Test]
        public void MotionDuringTheEntryGrace_IsIgnored_EvenOnceSettled()
        {
            _recognizer.SetContext(MenuInputContext.Menu);   // re-arm: the menu has just appeared

            Move(down: 0.9f, frames: 3);                       // the hand still playing targets
            Quiet(T.SettleFrames + 2);
            Move(down: 0.9f);                                  // settled, but inside the 600 ms grace

            Assert.That(_seen, Is.Empty);
        }

        [Test]
        public void AfterTheGraceAndASettle_GesturesWorkAgain()
        {
            _recognizer.SetContext(MenuInputContext.Menu);

            Quiet(5);
            Wait(T.EntryGraceSeconds + 0.1);
            Quiet(T.SettleFrames);
            Move(down: 0.9f);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.Strike }));
        }

        // --- tracking --------------------------------------------------------------------

        [Test]
        public void MotionWhileTheHandIsLostOrLow_DoesNothing()
        {
            Feed(new MenuHandFrame(TrackingQuality.NotTracked, rightwardSpeed: 1.5f));
            Feed(new MenuHandFrame(TrackingQuality.Low, rightwardSpeed: 1.5f));

            Assert.That(_seen, Is.Empty);
        }

        [Test]
        public void AfterATrackingLoss_GesturesNeedAFreshRunOfHighFramesAndASettle()
        {
            Feed(new MenuHandFrame(TrackingQuality.Low));
            Feed(new MenuHandFrame(TrackingQuality.High));
            Feed(new MenuHandFrame(TrackingQuality.High, rightwardSpeed: 1.2f));  // 2nd High frame: not usable yet
            Feed(new MenuHandFrame(TrackingQuality.High, rightwardSpeed: 1.2f));  // usable, but not settled

            Assert.That(_seen, Is.Empty);

            Quiet(T.SettleFrames + 2);
            Move(right: 1.2f);

            Assert.That(_seen, Is.EqualTo(new[] { MenuGesture.FlickRight }));
        }

        [Test]
        public void AMediumFrame_DoesNotCountTowardsResumingAfterALoss()
        {
            Feed(new MenuHandFrame(TrackingQuality.Low));
            for (int i = 0; i < 10; i++)
            {
                Feed(new MenuHandFrame(TrackingQuality.Medium, fistAmount: 0f));
            }

            Feed(new MenuHandFrame(TrackingQuality.High, rightwardSpeed: 1.2f));

            Assert.That(_seen, Is.Empty);
        }

        [Test]
        public void ATrackingLossInsideAFistHold_CancelsIt()
        {
            Fist(0.25);
            Feed(new MenuHandFrame(TrackingQuality.Low, fistAmount: 1f));
            Fist(0.3);                                 // resumes still clenched

            Assert.That(_seen, Is.Empty);
        }

        // --- thresholds ------------------------------------------------------------------

        [Test]
        public void Thresholds_RejectAnEnterEdgeNotAboveItsExitEdge()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MenuGestureThresholds(
                0.2f, 0.25f, 0.45f, 0.18f, 1.8f, 3, 0.25, 0.5, 0.6, 0.75f, 0.55f, 0.35, 1.0, 2, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MenuGestureThresholds(
                0.7f, 0.25f, 0.45f, 0.18f, 1.8f, 3, 0.25, 0.5, 0.6, 0.55f, 0.75f, 0.35, 1.0, 2, 3));
        }

        [Test]
        public void Thresholds_RejectAShorterFistHoldInARunThanInAMenu()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MenuGestureThresholds(
                0.7f, 0.25f, 0.45f, 0.18f, 1.8f, 3, 0.25, 0.5, 0.6, 0.75f, 0.55f, 0.5, 0.3, 2, 3));
        }
    }
}
