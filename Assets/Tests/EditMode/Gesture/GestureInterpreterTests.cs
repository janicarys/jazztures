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
            Assert.That(_interpreter.TrackingUsable, Is.False);

            Feed(1, HandPoseCandidate.None);
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Idle));
            Assert.That(_interpreter.TrackingUsable, Is.True, "ChordStrikeDetector gates on this (ADR-0025)");
        }

        [Test]
        public void TrackingUsable_GoesFalseAssoonAsTrackingDrops()
        {
            Feed(3, HandPoseCandidate.None);
            Assert.That(_interpreter.TrackingUsable, Is.True);

            Feed(1, HandPoseCandidate.None, left: TrackingQuality.Low);
            Assert.That(_interpreter.TrackingUsable, Is.False);
        }

        [Test]
        public void ReachingFunction_TracksThePoseBeforeItConfirms()
        {
            Feed(3, HandPoseCandidate.None); // become usable

            Assert.That(_interpreter.ReachingFunction, Is.Null);

            _clock.Advance(0.02);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));

            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "not held long enough to confirm");
            Assert.That(_interpreter.ReachingFunction, Is.EqualTo(ChordFunction.Two), "but the intent is visible");
        }

        [Test]
        public void ConfirmsAPose_OnlyAfterHoldTimeAndFrameCount()
        {
            Feed(3, HandPoseCandidate.None); // become usable

            _clock.Advance(0.02);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Detecting));
            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "held < 150 ms");

            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "still < 150 ms");

            _clock.Advance(0.12); // total hold ~0.19 s (clear of the 150 ms default), frame 3
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

        // ADR-0025: this used to be "a flicker restarts confirmation" — the measured root
        // cause of the reported left-hand timing/fluidity problem. Real hand tracking
        // produces brief None/Ambiguous blips mid-transition; penalising every one of them
        // made confirmation time unpredictable rather than merely slow, which is why a
        // chord change couldn't reliably land on a beat. It now costs nothing, up to
        // GestureThresholds.ConfirmationMissTolerance consecutive misses.
        [Test]
        public void ABriefBlipToADifferentReading_DoesNotRestartConfirmation()
        {
            Feed(3, HandPoseCandidate.None); // become usable

            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High)); // match 1
            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High)); // match 2

            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High)); // 1-frame blip
            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "the blip itself must not confirm None");

            _clock.Advance(0.05); // total elapsed since the first Ii frame: 0.20 s, clear of the 150 ms hold
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High)); // match 3

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two),
                "the blip must not have cost a fresh confirmation window");
        }

        [Test]
        public void ABriefAmbiguousBlip_DuringConfirmation_IsAlsoTolerated()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(2, HandPoseCandidate.Ii, dt: 0.06);   // elapsed since 1st match: 0.06 s
            Feed(1, HandPoseCandidate.Ambiguous, dt: 0.06); // 0.12 s — tolerated blip
            Feed(1, HandPoseCandidate.Ii, dt: 0.06);   // 0.18 s — clear of the 150 ms hold

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));
        }

        [Test]
        public void MoreConsecutiveMissesThanTolerance_DoesRestartConfirmation_OnWhatTheHandActuallyDoesNow()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(2, HandPoseCandidate.Ii, dt: 0.05); // 2 matches toward Ii, not yet confirmed

            // A sustained switch — more consecutive V frames than the tolerance — is a real
            // transition, not noise, and must redirect confirmation to V.
            Feed(GestureThresholds.Default.ConfirmationMissTolerance + 1, HandPoseCandidate.V, dt: 0.05);
            Feed(10, HandPoseCandidate.V, dt: 0.05);

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Five));
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

        // ADR-0027: reported symptom — striking (a fast downward hand motion) while ii is
        // held could disrupt the WristUp reading for a real, non-trivial stretch (tracking
        // is High throughout — this is not the tracking-loss path above), long enough to
        // satisfy the ordinary PoseHoldSeconds/ConfirmingFrames release requirement and cut
        // the sounding chord mid-strike. Releasing now needs its own, more conservative bar.
        [Test]
        public void ABriefLossOfTheHeldPose_ShorterThanReleaseHold_DoesNotRelease()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(10, HandPoseCandidate.Ii, dt: 0.05);
            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));

            _changes.Clear();
            // A strike-length disruption: comfortably longer than the ordinary confirmation
            // window (150 ms / 3 frames) would need, but short of ReleaseHoldSeconds (400 ms).
            Feed(5, HandPoseCandidate.None, dt: 0.05); // 0.25 s, tracking still High throughout

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two),
                "a strike-length disruption must not release the chord");
            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void ASustainedLossOfTheHeldPose_LongerThanReleaseHold_StillReleases()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(10, HandPoseCandidate.Ii, dt: 0.05);
            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));

            _changes.Clear();
            Feed(20, HandPoseCandidate.None, dt: 0.05); // 1.0 s, well past ReleaseHoldSeconds

            Assert.That(_interpreter.ConfirmedFunction, Is.Null, "a genuine, sustained release must still work");
            Assert.That(_changes, Is.EqualTo(new ChordFunction?[] { null }));
        }

        [Test]
        public void SwitchingBetweenTwoConcretePoses_StaysAsFastAsBefore_OnlyReleaseIsMoreConservative()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(10, HandPoseCandidate.Ii, dt: 0.05);
            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));

            _changes.Clear();
            // Comfortably past PoseHoldSeconds (150 ms) but short of ReleaseHoldSeconds
            // (400 ms) — a pose-to-pose switch must not be held to the release bar.
            Feed(5, HandPoseCandidate.V, dt: 0.05); // 0.25 s

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Five));
        }

        [Test]
        public void APendingRelease_ToleratesMoreRecoveryFramesThanAnOrdinaryConfirmationWould()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(10, HandPoseCandidate.Ii, dt: 0.05);
            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));

            int releaseTolerance = GestureThresholds.Default.ReleaseMissTolerance;
            Assert.That(releaseTolerance, Is.GreaterThan(GestureThresholds.Default.ConfirmationMissTolerance),
                "this test only proves something if release tolerance is the larger of the two");

            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));

            // More recoveries in a row than ConfirmationMissTolerance would ever survive —
            // the pending release must still be alive: ii is neither re-confirmed nor released.
            for (int i = 0; i < releaseTolerance; i++)
            {
                _clock.Advance(0.01);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            }

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two), "still holding ii throughout");

            _clock.Advance(0.01);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two),
                "exceeding tolerance recovers cleanly onto what was already held");
            Assert.That(_interpreter.Phase, Is.EqualTo(GesturePhase.Confirmed));
        }

        // ADR-0029: reported symptom — switching poses would sometimes "incorrectly trigger
        // ii". A normal pose-to-pose switch commonly passes through one None frame first
        // (any orientation change can briefly read as unrecognised); that single frame was
        // enough to classify the whole attempt as a release-pending and hand it
        // ReleaseMissTolerance's far larger budget, so every subsequent V/I frame was
        // absorbed as a tolerated "miss" instead of being recognised as a clear switch —
        // ii stayed confirmed for up to ReleaseHoldSeconds while the learner visibly held
        // a different pose. Only None/Ambiguous/"matches what's confirmed" are weak enough
        // evidence to deserve the release budget; a different concrete pose never is.
        [Test]
        public void APoseToPoseSwitch_ThroughABriefNoneWaypoint_DoesNotInheritTheReleaseBudget()
        {
            Feed(3, HandPoseCandidate.None);
            Feed(10, HandPoseCandidate.Ii, dt: 0.05);
            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Two));

            _changes.Clear();

            // A single None frame - an ordinary waypoint of any orientation change - starts
            // a release-pending attempt...
            _clock.Advance(0.05);
            _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));

            // ...but the hand is actually heading to V. Redirecting must only cost the
            // ordinary ConfirmationMissTolerance budget, not ReleaseMissTolerance's.
            int normalTolerance = GestureThresholds.Default.ConfirmationMissTolerance;
            Assert.That(normalTolerance, Is.LessThan(GestureThresholds.Default.ReleaseMissTolerance),
                "this test only proves something if the normal budget is the smaller of the two");

            for (int i = 0; i < normalTolerance + 1; i++)
            {
                _clock.Advance(0.05);
                _interpreter.Feed(new HandPoseFrame(HandPoseCandidate.V, TrackingQuality.High, TrackingQuality.High));
            }

            // A few more clean V frames to satisfy the ordinary confirmation window from
            // the redirect point.
            Feed(5, HandPoseCandidate.V, dt: 0.05);

            Assert.That(_interpreter.ConfirmedFunction, Is.EqualTo(ChordFunction.Five),
                "switching to V must not be held hostage by the stale None release-pending");
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
