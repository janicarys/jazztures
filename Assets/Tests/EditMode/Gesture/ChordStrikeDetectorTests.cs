using System.Collections.Generic;
using Jazztures.Core.Gesture;
using Jazztures.Core.Harmony;
using Jazztures.Core.Melody;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class ChordStrikeDetectorTests
    {
        private VirtualClock _clock = null!;
        private GestureInterpreter _interpreter = null!;
        private ChordStrikeDetector _detector = null!;
        private List<byte> _strikes = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new VirtualClock();
            _interpreter = new GestureInterpreter(_clock, GestureThresholds.Default);
            _detector = new ChordStrikeDetector(_clock, _interpreter, GestureThresholds.Default);
            _strikes = new List<byte>();
            _detector.Struck += _strikes.Add;

            // Become usable and confirm ii, so a struck function actually exists.
            for (int i = 0; i < 3; i++)
            {
                _clock.Advance(0.02);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            }

            for (int i = 0; i < 10; i++)
            {
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            }

            Assume.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));
        }

        [Test]
        public void SlowDownwardMotion_BelowTheEnterThreshold_DoesNotStrike()
        {
            _detector.Feed(-(GestureThresholds.Default.StrikeEnterSpeedMetresPerSecond - 0.01f));

            Assert.That(_strikes, Is.Empty);
        }

        [Test]
        public void UpwardMotion_NeverStrikes_RegardlessOfSpeed()
        {
            _detector.Feed(5.0f); // fast, but upward

            Assert.That(_strikes, Is.Empty);
        }

        [Test]
        public void FastDownwardMotion_CrossingTheEnterThreshold_Strikes()
        {
            _detector.Feed(-1.0f);

            Assert.That(_strikes, Has.Count.EqualTo(1));
            Assert.That(_strikes[0], Is.EqualTo(VelocityCurve.FromSpeed(1.0f)));
        }

        [Test]
        public void WithNothingSelected_DownwardMotion_DoesNotStrike()
        {
            var freshClock = new VirtualClock();
            var freshInterpreter = new GestureInterpreter(freshClock, GestureThresholds.Default);
            var freshDetector = new ChordStrikeDetector(freshClock, freshInterpreter, GestureThresholds.Default);
            var freshStrikes = new List<byte>();
            freshDetector.Struck += freshStrikes.Add;

            for (int i = 0; i < 3; i++)
            {
                freshClock.Advance(0.02);
                freshInterpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            }

            Assume.That(freshInterpreter.ConfirmedFunction, Is.Null);

            freshDetector.Feed(-1.0f);

            Assert.That(freshStrikes, Is.Empty);
        }

        [Test]
        public void SettlingBelowTheExitThreshold_ThenStrikingAgain_ReArmsAndFiresTwice()
        {
            _detector.Feed(-1.0f); // first strike
            Assert.That(_strikes, Has.Count.EqualTo(1));

            _clock.Advance(GestureThresholds.Default.MinInterStrikeSeconds + 0.01);
            SettleBelowExit();
            _detector.Feed(-1.0f); // strike again

            Assert.That(_strikes, Has.Count.EqualTo(2));
        }

        // ADR-0030: reported symptom — a single strike sometimes fired twice. A real
        // strike's own deceleration often rebounds slightly; one frame below the exit
        // speed mid-rebound used to re-arm immediately, letting the rebound's small
        // secondary motion fire a second, unintended strike.
        [Test]
        public void ARebound_ThatNeverSettlesForEnoughConsecutiveFrames_DoesNotReArm()
        {
            _detector.Feed(-1.0f); // first strike
            Assert.That(_strikes, Has.Count.EqualTo(1));

            int settleFrames = GestureThresholds.Default.StrikeSettleFrames;
            Assert.That(settleFrames, Is.GreaterThan(1),
                "this test only proves something if more than one settled frame is required");

            _clock.Advance(GestureThresholds.Default.MinInterStrikeSeconds + 0.01);

            // A rebound: dips below exit, then a small secondary downward motion interrupts
            // the settle before it ever reaches StrikeSettleFrames consecutive frames.
            for (int i = 0; i < settleFrames - 1; i++)
            {
                _detector.Feed(-(GestureThresholds.Default.StrikeExitSpeedMetresPerSecond - 0.01f));
            }

            _detector.Feed(-(GestureThresholds.Default.StrikeEnterSpeedMetresPerSecond - 0.01f)); // interrupts the settle
            _detector.Feed(-1.0f); // the rebound's own secondary motion — must not fire

            Assert.That(_strikes, Has.Count.EqualTo(1), "an interrupted settle must not have re-armed");
        }

        [Test]
        public void OneSettledFrame_IsNotEnoughToReArm_WhenMoreThanOneIsRequired()
        {
            _detector.Feed(-1.0f);
            Assert.That(_strikes, Has.Count.EqualTo(1));

            Assert.That(GestureThresholds.Default.StrikeSettleFrames, Is.GreaterThan(1),
                "this test only proves something if more than one settled frame is required");

            _clock.Advance(GestureThresholds.Default.MinInterStrikeSeconds + 0.01);
            _detector.Feed(-(GestureThresholds.Default.StrikeExitSpeedMetresPerSecond - 0.01f)); // only one
            _detector.Feed(-1.0f);

            Assert.That(_strikes, Has.Count.EqualTo(1), "not yet re-armed after a single settled frame");
        }

        [Test]
        public void StayingAboveTheEnterThreshold_DoesNotRepeatedlyStrike_UntilItReArms()
        {
            _detector.Feed(-1.0f);
            _clock.Advance(1.0); // well past the cooldown
            _detector.Feed(-1.0f); // still above enter, never dropped below exit

            Assert.That(_strikes, Has.Count.EqualTo(1), "must decelerate below the exit threshold to re-arm");
        }

        [Test]
        public void TwoStrikesWithinTheCooldown_OnlyTheFirstFires()
        {
            _detector.Feed(-1.0f);
            _clock.Advance(GestureThresholds.Default.MinInterStrikeSeconds / 4);
            SettleBelowExit(); // fully re-arms — Feed() calls alone don't advance the clock
            _detector.Feed(-1.0f);

            Assert.That(_strikes, Has.Count.EqualTo(1), "the cooldown blocks the second strike even though it re-armed");
        }

        private void SettleBelowExit()
        {
            for (int i = 0; i < GestureThresholds.Default.StrikeSettleFrames; i++)
            {
                _detector.Feed(-(GestureThresholds.Default.StrikeExitSpeedMetresPerSecond - 0.01f));
            }
        }

        // ADR-0037: feeding one HandPoseFrame through Feed(HandPoseFrame) must let a
        // genuine strike claim the frame before that same frame's own candidate can
        // confirm a pending release out from under it — the failure NotifyStruck
        // (ADR-0034) cannot prevent on its own, since it only cancels a release once a
        // strike has already fired.
        [Test]
        public void FeedingAWholeFrame_LetsAStrikeClaimTheSameFrameAReleaseWouldOtherwiseConfirmOn()
        {
            // SetUp already confirmed ii (Two).

            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            for (int i = 0; i < 3; i++)
            {
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            }

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two), "not yet released");

            // This next None frame would cross ReleaseHoldSeconds if the interpreter
            // processed it on its own — but it also carries a genuine strike-speed
            // downward velocity. Feeding it as one frame must let the strike claim it.
            _clock.Advance(0.1);
            var frame = new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High, -1.0f);
            _detector.Feed(frame);

            Assert.That(_strikes, Has.Count.EqualTo(1), "the strike must have fired");
            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two),
                "the strike must have claimed this frame before the pending release could confirm on it");
        }

        // The failure ADR-0037 fixes, made explicit: feeding the same two objects
        // separately, interpreter first, lets the release win the race and the strike
        // finds nothing left to sound. This is exactly the bug reported on device — a
        // pose reads correctly but striking it produces no sound.
        [Test]
        public void FeedingSeparately_InterpreterFirst_LetsTheReleaseSwallowTheStrike()
        {
            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            for (int i = 0; i < 3; i++)
            {
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            }

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));

            _clock.Advance(0.1);
            var frame = new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High, -1.0f);

            _interpreter.Feed(frame); // interpreter first: confirms the release right here
            _detector.Feed(frame.LeftVerticalSpeedMetresPerSecond); // too late: nothing to strike

            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "this ordering lets the release win");
            Assert.That(_strikes, Is.Empty, "the strike had nothing left to sound");
        }

        // ADR-0034: a real strike, through the real detector, must reach into the
        // interpreter and cancel a release that happens to be pending — see
        // GestureInterpreterTests.NotifyStruck_CancelsAPendingRelease_... for the isolated
        // interpreter-level scenario this reproduces end to end.
        [Test]
        public void AStrike_CancelsAReleaseThatIsPendingAtThatMoment()
        {
            // SetUp already confirmed ii (Two).

            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            for (int i = 0; i < 3; i++)
            {
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            }

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two), "not yet released");

            _clock.Advance(GestureThresholds.Default.MinInterStrikeSeconds + 0.01);
            _detector.Feed(-1.0f);
            Assert.That(_strikes, Has.Count.EqualTo(1));

            // Without the strike cancelling it, the next None match would cross
            // ReleaseHoldSeconds (elapsed since the original pending start) and release ii.
            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two),
                "the strike must have cancelled the stale pending release");
        }

        [Test]
        public void TrackingLoss_SuppressesTheStrike_EvenWithFastDownwardMotion()
        {
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.Low, TrackingQuality.High));
            Assume.That(_interpreter.TrackingUsable, Is.False);

            _detector.Feed(-5.0f);

            Assert.That(_strikes, Is.Empty, "a tracking glitch must never manufacture a strike (ADR-0025 / §3.5)");
        }

        // ADR-0031: reported symptom (by code review, not yet on device) — a tracking blip
        // landing mid-strike used to force an immediate re-arm, exactly like this test used
        // to assert. Fast motion (a strike) is exactly what degrades optical tracking, so
        // the blip is realistic; forcing a re-arm reopened ADR-0030's double-fire through a
        // different door — the strike never physically stopped, so the first good frame
        // back was often still fast enough to cross the enter threshold again.
        [Test]
        public void TrackingLossMidStrike_DoesNotReArm_UntilTheHandActuallySettles()
        {
            _detector.Feed(-1.0f); // first strike
            Assert.That(_strikes, Has.Count.EqualTo(1));

            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.Low, TrackingQuality.High));
            _detector.Feed(-5.0f); // ignored: tracking unusable

            // Regain tracking (3 High frames); ii is still held (sustained through the loss).
            for (int i = 0; i < 3; i++)
            {
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            }

            Assume.That(_interpreter.TrackingUsable, Is.True);
            _clock.Advance(GestureThresholds.Default.MinInterStrikeSeconds + 0.01);

            // The strike never physically stopped — the hand is still fast on the first
            // good frame back. Without a genuine settle, this must not re-fire.
            _detector.Feed(-1.0f);
            Assert.That(_strikes, Has.Count.EqualTo(1), "a tracking blip mid-strike must not force a re-arm");

            // Once the hand genuinely settles, a fresh strike still works normally.
            SettleBelowExit();
            _detector.Feed(-1.0f);
            Assert.That(_strikes, Has.Count.EqualTo(2), "a genuine settle after the loss still re-arms normally");
        }

        // The flip side of the fix above: a blip that happens while the detector was
        // already armed (no strike in flight) must not cost the next strike anything —
        // only a strike-in-progress's arm/settle state is being protected.
        [Test]
        public void TrackingLossWhileAlreadyArmed_DoesNotBlockTheNextStrike()
        {
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.Low, TrackingQuality.High));
            _detector.Feed(-5.0f); // ignored: tracking unusable

            for (int i = 0; i < 3; i++)
            {
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            }

            Assume.That(_interpreter.TrackingUsable, Is.True);

            _detector.Feed(-1.0f);
            Assert.That(_strikes, Has.Count.EqualTo(1), "a blip while already armed must not block the next strike");
        }
    }
}
