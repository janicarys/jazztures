using System.Collections.Generic;
using Jazztures.Core.Gesture;
using Jazztures.Core.Harmony;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class ChordTouchDetectorTests
    {
        private VirtualClock _clock = null!;
        private GestureInterpreter _interpreter = null!;
        private ChordTouchDetector _detector = null!;
        private List<byte> _articulated = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new VirtualClock();
            _interpreter = new GestureInterpreter(_clock, GestureThresholds.Default);
            _detector = new ChordTouchDetector(_clock, _interpreter, TouchCommitThresholds.Default);
            _articulated = new List<byte>();
            _detector.Articulated += _articulated.Add;

            // Become usable and confirm ii, so an entry actually has something to articulate.
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
        public void AnEntryAboveTheVelocityGate_ArticulatesTheConfirmedChord()
        {
            _detector.Feed(true, 0.5f);

            Assert.That(_articulated, Has.Count.EqualTo(1));
        }

        [Test]
        public void AnEntryBelowTheVelocityGate_DoesNotArticulate()
        {
            _detector.Feed(true, TouchCommitThresholds.Default.EntryVelocityGateMetresPerSecond - 0.01f);

            Assert.That(_articulated, Is.Empty, "a resting hand drifting in must not articulate");
        }

        [Test]
        public void StayingInside_ArticulatesOnlyOnce()
        {
            _detector.Feed(true, 0.5f);
            _detector.Feed(true, 0.5f); // still inside — not a new rising edge

            Assert.That(_articulated, Has.Count.EqualTo(1));
        }

        [Test]
        public void LeavingAndReenteringAboveTheGate_ArticulatesAgain()
        {
            _detector.Feed(true, 0.5f);
            _detector.Feed(false, 0f);
            _clock.Advance(TouchCommitThresholds.Default.MinInterTouchSeconds + 0.01);
            _detector.Feed(true, 0.5f);

            Assert.That(_articulated, Has.Count.EqualTo(2));
        }

        [Test]
        public void ATouchWithNothingSelected_DoesNotArticulate()
        {
            var freshClock = new VirtualClock();
            var freshInterpreter = new GestureInterpreter(freshClock, GestureThresholds.Default);
            var freshDetector = new ChordTouchDetector(freshClock, freshInterpreter, TouchCommitThresholds.Default);
            var freshArticulated = new List<byte>();
            freshDetector.Articulated += freshArticulated.Add;

            for (int i = 0; i < 3; i++)
            {
                freshClock.Advance(0.02);
                freshInterpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            }

            Assume.That(freshInterpreter.ConfirmedFunction, Is.Null);

            freshDetector.Feed(true, 0.5f);

            Assert.That(freshArticulated, Is.Empty);
        }

        [Test]
        public void TwoEntriesWithinTheCooldown_OnlyTheFirstFires()
        {
            _detector.Feed(true, 0.5f);
            _detector.Feed(false, 0f);
            _clock.Advance(TouchCommitThresholds.Default.MinInterTouchSeconds / 4);
            _detector.Feed(true, 0.5f);

            Assert.That(_articulated, Has.Count.EqualTo(1), "the cooldown blocks the second entry");
        }

        [Test]
        public void TrackingLoss_SuppressesArticulation_EvenWithAnEntry()
        {
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.Low, TrackingQuality.High));
            Assume.That(_interpreter.TrackingUsable, Is.False);

            _detector.Feed(true, 0.5f);

            Assert.That(_articulated, Is.Empty, "a tracking glitch must never manufacture an articulation");
        }

        [Test]
        public void AnEntryHeldThroughATrackingDropout_DoesNotRefireOnRecovery()
        {
            _detector.Feed(true, 0.5f); // 1st articulation
            Assume.That(_articulated, Has.Count.EqualTo(1));

            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.Low, TrackingQuality.High));
            _detector.Feed(true, 0.5f); // ignored: tracking unusable, _wasInside untouched

            for (int i = 0; i < 3; i++)
            {
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            }

            Assume.That(_interpreter.TrackingUsable, Is.True);

            _detector.Feed(true, 0.5f); // still inside, not a fresh rising edge

            Assert.That(_articulated, Has.Count.EqualTo(1), "an entry held through the dropout must not re-fire on recovery");
        }

        // ADR-0037's ordering, ported a third time: a real touch, through the real
        // detector, must reach into the interpreter and cancel a release that happens to
        // be pending.
        [Test]
        public void ATouch_CancelsAReleaseThatIsPendingAtThatMoment()
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

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two), "not yet released");

            _clock.Advance(TouchCommitThresholds.Default.MinInterTouchSeconds + 0.01);
            _detector.Feed(true, 0.5f);
            Assert.That(_articulated, Has.Count.EqualTo(1));

            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two),
                "the touch must have cancelled the stale pending release");
        }

        [Test]
        public void FeedingAWholeFrame_LetsATouchClaimTheSameFrameAReleaseWouldOtherwiseConfirmOn()
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

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two), "not yet released");

            _clock.Advance(0.1);
            var frame = new HandPoseFrame(
                HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High,
                leftIsTouchingTarget: true, leftTouchEntrySpeedMetresPerSecond: 0.5f);
            _detector.Feed(frame);

            Assert.That(_articulated, Has.Count.EqualTo(1), "the touch must have fired");
            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two),
                "the touch must have claimed this frame before the pending release could confirm on it");
        }

        [Test]
        public void FeedingSeparately_InterpreterFirst_LetsTheReleaseSwallowTheTouch()
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
            var frame = new HandPoseFrame(
                HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High,
                leftIsTouchingTarget: true, leftTouchEntrySpeedMetresPerSecond: 0.5f);

            _interpreter.Feed(frame); // interpreter first: confirms the release right here
            _detector.Feed(frame.LeftIsTouchingTarget, frame.LeftTouchEntrySpeedMetresPerSecond); // too late

            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "this ordering lets the release win");
            Assert.That(_articulated, Is.Empty, "the touch had nothing left to sound");
        }

        [Test]
        public void AFasterEntry_ProducesAHigherVelocityThanASlowerOne()
        {
            _detector.Feed(true, 0.2f); // slow
            byte slowVelocity = _articulated[0];

            _clock.Advance(TouchCommitThresholds.Default.MinInterTouchSeconds + 0.01);
            _detector.Feed(false, 0f);
            _clock.Advance(TouchCommitThresholds.Default.MinInterTouchSeconds + 0.01);
            _detector.Feed(true, 1.2f); // fast

            Assert.That(_articulated, Has.Count.EqualTo(2));
            Assert.That(_articulated[1], Is.GreaterThan(slowVelocity));
        }
    }
}
