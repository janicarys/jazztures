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

        [Test]
        public void TrackingLoss_SuppressesTheStrike_EvenWithFastDownwardMotion()
        {
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.Low, TrackingQuality.High));
            Assume.That(_interpreter.TrackingUsable, Is.False);

            _detector.Feed(-5.0f);

            Assert.That(_strikes, Is.Empty, "a tracking glitch must never manufacture a strike (ADR-0025 / §3.5)");
        }

        [Test]
        public void AfterTrackingLoss_TheFirstGoodFrameCanStillStrike_ButOnlyOnceReArmed()
        {
            _detector.Feed(-1.0f); // arm state consumed — 1st strike
            Assume.That(_strikes, Has.Count.EqualTo(1));

            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.Low, TrackingQuality.High));
            _detector.Feed(-5.0f); // ignored: tracking unusable

            // Regain tracking (3 High frames) and confirm ii is still held (sustained through the loss).
            for (int i = 0; i < 3; i++)
            {
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            }

            Assume.That(_interpreter.TrackingUsable, Is.True);
            _clock.Advance(GestureThresholds.Default.MinInterStrikeSeconds + 0.01);

            _detector.Feed(-1.0f); // 2nd strike — tracking loss forced a re-arm

            Assert.That(_strikes, Has.Count.EqualTo(2), "tracking loss forces a re-arm, so this is a fresh strike");
        }
    }
}
