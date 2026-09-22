using System.Collections.Generic;
using Jazztures.Core.Gesture;
using Jazztures.Core.Harmony;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class ChordPinchDetectorTests
    {
        private VirtualClock _clock = null!;
        private GestureInterpreter _interpreter = null!;
        private ChordPinchDetector _detector = null!;
        private List<byte> _articulated = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new VirtualClock();
            _interpreter = new GestureInterpreter(_clock, GestureThresholds.Default);
            _detector = new ChordPinchDetector(_clock, _interpreter, PinchCommitThresholds.Default);
            _articulated = new List<byte>();
            _detector.Articulated += _articulated.Add;

            // Become usable and confirm ii, so a pinch actually has something to articulate.
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
        public void APinchRisingEdge_ArticulatesTheConfirmedChord()
        {
            _detector.Feed(true, 5f);

            Assert.That(_articulated, Has.Count.EqualTo(1));
        }

        [Test]
        public void AHeldPinch_ArticulatesOnlyOnce()
        {
            _detector.Feed(true, 5f);
            _detector.Feed(true, 5f); // still pinching — not a new rising edge

            Assert.That(_articulated, Has.Count.EqualTo(1));
        }

        [Test]
        public void ReleasingAndRePinching_ArticulatesAgain()
        {
            _detector.Feed(true, 5f);
            _detector.Feed(false, 0f);
            _clock.Advance(PinchCommitThresholds.Default.MinInterPinchSeconds + 0.01);
            _detector.Feed(true, 5f);

            Assert.That(_articulated, Has.Count.EqualTo(2));
        }

        [Test]
        public void APinchWithNothingSelected_DoesNotArticulate()
        {
            var freshClock = new VirtualClock();
            var freshInterpreter = new GestureInterpreter(freshClock, GestureThresholds.Default);
            var freshDetector = new ChordPinchDetector(freshClock, freshInterpreter, PinchCommitThresholds.Default);
            var freshArticulated = new List<byte>();
            freshDetector.Articulated += freshArticulated.Add;

            for (int i = 0; i < 3; i++)
            {
                freshClock.Advance(0.02);
                freshInterpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            }

            Assume.That(freshInterpreter.ConfirmedFunction, Is.Null);

            freshDetector.Feed(true, 5f);

            Assert.That(freshArticulated, Is.Empty);
        }

        [Test]
        public void TwoPinchesWithinTheCooldown_OnlyTheFirstFires()
        {
            _detector.Feed(true, 5f);
            _detector.Feed(false, 0f);
            _clock.Advance(PinchCommitThresholds.Default.MinInterPinchSeconds / 4);
            _detector.Feed(true, 5f);

            Assert.That(_articulated, Has.Count.EqualTo(1), "the cooldown blocks the second pinch");
        }

        [Test]
        public void TrackingLoss_SuppressesArticulation_EvenWithAPinchRisingEdge()
        {
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.Low, TrackingQuality.High));
            Assume.That(_interpreter.TrackingUsable, Is.False);

            _detector.Feed(true, 5f);

            Assert.That(_articulated, Is.Empty, "a tracking glitch must never manufacture an articulation");
        }

        // The confidence-hold guard: the Unity adapter holds the last reported pinch value
        // through a per-finger confidence blip rather than forcing it false (§6 of the
        // ADR-0038 design review) — so a pinch already held through a tracking dropout must
        // not read as a fresh rising edge on recovery.
        [Test]
        public void APinchHeldThroughATrackingDropout_DoesNotRefireOnRecovery()
        {
            _detector.Feed(true, 5f); // 1st articulation
            Assume.That(_articulated, Has.Count.EqualTo(1));

            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.Low, TrackingQuality.High));
            _detector.Feed(true, 5f); // ignored: tracking unusable, _wasPinching untouched

            for (int i = 0; i < 3; i++)
            {
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            }

            Assume.That(_interpreter.TrackingUsable, Is.True);

            _detector.Feed(true, 5f); // still pinching, not a fresh rising edge

            Assert.That(_articulated, Has.Count.EqualTo(1), "a pinch held through the dropout must not re-fire on recovery");
        }

        // ADR-0037's ordering, ported: a real pinch, through the real detector, must reach
        // into the interpreter and cancel a release that happens to be pending — see
        // GestureInterpreterTests.NotifyStruck_CancelsAPendingRelease_... for the isolated
        // interpreter-level scenario this reproduces end to end.
        [Test]
        public void APinch_CancelsAReleaseThatIsPendingAtThatMoment()
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

            _clock.Advance(PinchCommitThresholds.Default.MinInterPinchSeconds + 0.01);
            _detector.Feed(true, 5f);
            Assert.That(_articulated, Has.Count.EqualTo(1));

            // Without the pinch cancelling it, the next None match would cross
            // ReleaseHoldSeconds (elapsed since the original pending start) and release ii.
            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two),
                "the pinch must have cancelled the stale pending release");
        }

        [Test]
        public void FeedingAWholeFrame_LetsAPinchClaimTheSameFrameAReleaseWouldOtherwiseConfirmOn()
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

            // This next None frame would cross ReleaseHoldSeconds if the interpreter
            // processed it on its own — but it also carries a genuine pinch rising edge.
            // Feeding it as one frame must let the pinch claim it.
            _clock.Advance(0.1);
            var frame = new HandPoseFrame(
                HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High,
                leftIsPinching: true, leftPinchClosingRatePerSecond: 5f);
            _detector.Feed(frame);

            Assert.That(_articulated, Has.Count.EqualTo(1), "the pinch must have fired");
            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two),
                "the pinch must have claimed this frame before the pending release could confirm on it");
        }

        // The failure this ordering fixes, made explicit: feeding the same two objects
        // separately, interpreter first, lets the release win the race and the pinch finds
        // nothing left to sound. Same bug shape as ADR-0037's strike-side pair.
        [Test]
        public void FeedingSeparately_InterpreterFirst_LetsTheReleaseSwallowThePinch()
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
                leftIsPinching: true, leftPinchClosingRatePerSecond: 5f);

            _interpreter.Feed(frame); // interpreter first: confirms the release right here
            _detector.Feed(frame.LeftIsPinching, frame.LeftPinchClosingRatePerSecond); // too late

            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "this ordering lets the release win");
            Assert.That(_articulated, Is.Empty, "the pinch had nothing left to sound");
        }

        [Test]
        public void AFasterPinch_ProducesAHigherVelocityThanASlowerOne()
        {
            _detector.Feed(true, 4f); // slow
            byte slowVelocity = _articulated[0];

            _clock.Advance(PinchCommitThresholds.Default.MinInterPinchSeconds + 0.01);
            _detector.Feed(false, 0f);
            _clock.Advance(PinchCommitThresholds.Default.MinInterPinchSeconds + 0.01);
            _detector.Feed(true, 12f); // fast

            Assert.That(_articulated, Has.Count.EqualTo(2));
            Assert.That(_articulated[1], Is.GreaterThan(slowVelocity));
        }
    }
}
