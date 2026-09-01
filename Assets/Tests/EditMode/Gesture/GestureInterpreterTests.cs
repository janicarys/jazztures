using System.Collections.Generic;
using Jazztures.Core.Gesture;
using Jazztures.Core.Harmony;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class GestureInterpreterTests
    {
        private VirtualClock _clock = null!;
        private GestureInterpreter _interpreter = null!;
        private List<ChordFunction?> _changes = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new VirtualClock();
            _interpreter = new GestureInterpreter(_clock, GestureThresholds.Default);
            _changes = new List<ChordFunction?>();
            _interpreter.ConfirmedFunctionChanged += f => _changes.Add(f);
        }

        /// <summary>Feed <paramref name="count"/> frames, advancing the clock by <paramref name="dt"/> before each.</summary>
        private void Feed(int count, HandPoseCandidate candidate, double dt = 0.02,
            TrackingQuality left = TrackingQuality.High)
        {
            for (int i = 0; i < count; i++)
            {
                _clock.Advance(dt);
                _interpreter.Feed(new HandPoseFrame(candidate, left, TrackingQuality.High));
            }
        }

        [Test]
        public void StartsSuppressed_WithNothingConfirmed()
        {
            Assert.That(_interpreter.ConfirmedFunction, Is.Null);
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Suppressed));
            Assert.That(_interpreter.TrackingCueActive, Is.False);
        }

        [Test]
        public void RequiresThreeHighFrames_BeforeAcceptingAnyInput()
        {
            Feed(2, HandPoseCandidate.None);
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Suppressed));

            Feed(1, HandPoseCandidate.None);
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Idle));
        }

        [Test]
        public void ConfirmsAPose_OnlyAfterHoldTimeAndFrameCount()
        {
            Feed(3, HandPoseCandidate.None); // become usable

            _clock.Advance(0.02);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Detecting));
            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "held < 120 ms");

            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "still < 120 ms");

            _clock.Advance(0.08); // total hold ~0.13 s, frame 3
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Confirmed));
            Assert.That(_changes, Is.EqualTo(new ChordFunction?[] { ChordFunction.Two }));
        }

        [Test]
        public void TwoFramesFarApart_DoNotConfirm_FrameCountIsAlsoRequired()
        {
            Feed(3, HandPoseCandidate.None);

            _clock.Advance(0.02);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.V, TrackingQuality.High, TrackingQuality.High));
            _clock.Advance(1.0);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.V, TrackingQuality.High, TrackingQuality.High));

            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "held long enough but only 2 frames");
        }

        [Test]
        public void AFlickerToADifferentCandidate_RestartsConfirmation()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(2, HandPoseCandidate.Ii);
            Feed(1, HandPoseCandidate.None); // flicker resets the in-progress confirmation
            Feed(10, HandPoseCandidate.Ii, dt: 0.05); // long, unbroken hold after the flicker

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));
        }

        [Test]
        public void Ambiguous_HoldsThePreviousChord_AndEmitsNothing()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(10, HandPoseCandidate.V, dt: 0.05); // confirm V
            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Five));

            _changes.Clear();
            Feed(10, HandPoseCandidate.Ambiguous, dt: 0.05);

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Five));
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Confirmed));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void Debounce_DelaysTheNextConfirmation()
        {
            var thresholds = new GestureThresholds(
                poseHoldSeconds: 0.02,
                confirmingFrames: 1,
                minInterChordSeconds: 0.20,
                highFramesToResumeAfterLoss: 1,
                trackingLossCueSeconds: 0.2);
            var interpreter = new GestureInterpreter(_clock, thresholds);

            _clock.Advance(0.02);
            interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            _clock.Advance(0.05);
            interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            Assert.That(interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));

            // Immediately switch to V — held long enough, but within the debounce window.
            _clock.Advance(0.05);
            interpreter.Feed(new HandPoseFrame(HandPoseCandidate.V, TrackingQuality.High, TrackingQuality.High));
            _clock.Advance(0.05);
            interpreter.Feed(new HandPoseFrame(HandPoseCandidate.V, TrackingQuality.High, TrackingQuality.High));
            Assert.That(interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two), "still debouncing");

            _clock.Advance(0.20);
            interpreter.Feed(new HandPoseFrame(HandPoseCandidate.V, TrackingQuality.High, TrackingQuality.High));
            Assert.That(interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Five));
        }

        [Test]
        public void TrackingLoss_SustainsTheChord_NeverReleasesIt()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(10, HandPoseCandidate.Ii, dt: 0.05); // confirm ii
            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));

            _changes.Clear();
            Feed(20, HandPoseCandidate.None, dt: 0.05, left: TrackingQuality.Low);

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two), "sustained, not released");
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Suppressed));
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void AfterTrackingReturns_ASustainedReleasePoseStillReleases()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(10, HandPoseCandidate.Ii, dt: 0.05);
            Feed(10, HandPoseCandidate.Ii, dt: 0.05, left: TrackingQuality.Low); // lose tracking
            Feed(3, HandPoseCandidate.None); // 3 High frames to resume
            Feed(10, HandPoseCandidate.None, dt: 0.05); // hold "no pose"

            Assert.That(_interpreter.ConfirmedFunction, Is.Null);
            Assert.That(_changes, Does.Contain(null));
        }

        [Test]
        public void ResumeCounter_ResetsOnANonHighFrame()
        {
            Feed(2, HandPoseCandidate.None, left: TrackingQuality.High);
            Feed(1, HandPoseCandidate.None, left: TrackingQuality.Medium); // resets the counter
            Feed(2, HandPoseCandidate.None, left: TrackingQuality.High);
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Suppressed), "only 2 High since the Medium");

            Feed(1, HandPoseCandidate.None, left: TrackingQuality.High);
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Idle));
        }

        [Test]
        public void TrackingCue_ArmsAfterTheGracePeriod_AndOnlyOnceTrackingWasEverGood()
        {
            // Never-tracked: no cue no matter how long.
            Feed(50, HandPoseCandidate.None, dt: 0.05, left: TrackingQuality.NotTracked);
            Assert.That(_interpreter.TrackingCueActive, Is.False);

            Feed(3, HandPoseCandidate.None); // become usable
            _changes.Clear();

            // Lose tracking; the cue arms only after the 200 ms grace period.
            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.Low, TrackingQuality.High));
            Assert.That(_interpreter.TrackingCueActive, Is.False, "just lost");

            _clock.Advance(0.10);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.Low, TrackingQuality.High));
            Assert.That(_interpreter.TrackingCueActive, Is.False, "0.10 s < grace");

            _clock.Advance(0.15);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.Low, TrackingQuality.High));
            Assert.That(_interpreter.TrackingCueActive, Is.True, "0.25 s >= grace");

            Feed(3, HandPoseCandidate.None); // recover
            Assert.That(_interpreter.TrackingCueActive, Is.False);
        }

        [Test]
        public void PhaseChanged_FiresOnEachTransition_NotOnNoOps()
        {
            var phases = new List<GesturePhase>();
            _interpreter.PhaseChanged += phases.Add;

            Feed(3, HandPoseCandidate.None);            // Suppressed -> Idle
            Feed(10, HandPoseCandidate.Ii, dt: 0.05);   // Idle -> Detecting -> Confirmed
            Feed(5, HandPoseCandidate.Ii, dt: 0.05);    // no change while held

            Assert.That(phases, Is.EqualTo(new[]
            {
                GesturePhase.Idle, GesturePhase.Detecting, GesturePhase.Confirmed,
            }));
        }

        [Test]
        public void ConfirmedFunctionChanged_FiresOncePerActualChange()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(10, HandPoseCandidate.Ii, dt: 0.05);
            Feed(10, HandPoseCandidate.Ii, dt: 0.05); // keep holding — no repeat event
            Feed(10, HandPoseCandidate.V, dt: 0.05);
            Feed(10, HandPoseCandidate.None, dt: 0.05);

            Assert.That(_changes, Is.EqualTo(new ChordFunction?[]
            {
                ChordFunction.Two, ChordFunction.Five, null,
            }));
        }
    }
}
