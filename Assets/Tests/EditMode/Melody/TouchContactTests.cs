using System;
using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class TouchContactTests
    {
        private static readonly TouchContactThresholds T = TouchContactThresholds.Default;

        // Speeds named by what they are against the defaults (entry 0.08, tap 0.25, settle 0.10).
        private const float Still = 0.02f;
        private const float Gentle = 0.05f;   // under the entry gate
        private const float Deliberate = 0.40f;

        private static bool Feed(ref TouchContact c, bool inside, float speed) => c.Feed(inside, speed, T);

        // --- entering (the original behaviour) ---------------------------------------------

        [Test]
        public void ADeliberateEntry_PlaysOnTheFrameItCrossesIn()
        {
            var c = new TouchContact();

            Assert.That(Feed(ref c, inside: false, Deliberate), Is.False, "still outside");
            Assert.That(Feed(ref c, inside: true, Deliberate), Is.True);
        }

        [Test]
        public void AnEntryJustAtTheGate_Plays()
        {
            var c = new TouchContact();
            Feed(ref c, false, 0f);

            Assert.That(Feed(ref c, true, T.EntrySpeed), Is.True);
        }

        [Test]
        public void AFastEntry_PlaysOnce_NotOnEveryFrameInsideTheVolume()
        {
            var c = new TouchContact();
            Feed(ref c, false, 0f);

            int plays = 0;
            for (int i = 0; i < 20; i++)
            {
                if (Feed(ref c, true, Deliberate))
                {
                    plays++;
                }
            }

            Assert.That(plays, Is.EqualTo(1), "a sweep through the volume is one note");
        }

        // --- the reported bug: a gentle arrival, then a tap ---------------------------------

        [Test]
        public void AGentleEntry_DoesNotPlay()
        {
            var c = new TouchContact();
            Feed(ref c, false, 0f);

            Assert.That(Feed(ref c, true, Gentle), Is.False);
        }

        [Test]
        public void AfterAGentleEntry_ATapInsideThe_VolumePlays()
        {
            // Put the finger on the target (it is already inside), then tap it. Entry-only
            // gave silence here: there is no outside-to-inside edge to read.
            var c = new TouchContact();
            Feed(ref c, false, 0f);
            Feed(ref c, true, Gentle);
            Feed(ref c, true, Still);
            Feed(ref c, true, Still);

            Assert.That(Feed(ref c, true, Deliberate), Is.True);
        }

        [Test]
        public void ATapAtTheTapSpeed_Plays_AndOneJustUnderItDoesNot()
        {
            var under = new TouchContact();
            Feed(ref under, false, 0f);
            Feed(ref under, true, Gentle);
            Assert.That(Feed(ref under, true, T.TapSpeed - 0.01f), Is.False);

            var at = new TouchContact();
            Feed(ref at, false, 0f);
            Feed(ref at, true, Gentle);
            Assert.That(Feed(ref at, true, T.TapSpeed), Is.True);
        }

        [Test]
        public void ARestingHand_NeverPlays_NoMatterHowLongItSitsInside()
        {
            // Tracking jitter on a still hand reaches a few tenths of a metre per second at
            // worst here; it must stay under the tap speed or the target machine-guns.
            var c = new TouchContact();
            Feed(ref c, false, 0f);
            Feed(ref c, true, Gentle);

            for (int i = 0; i < 500; i++)
            {
                float jitter = (i % 7) * 0.03f; // 0 .. 0.18 m/s, under the tap speed
                Assert.That(Feed(ref c, true, jitter), Is.False, $"frame {i}");
            }
        }

        // --- tapping repeatedly ---------------------------------------------------------------

        [Test]
        public void AfterANote_TheFingertipMustSettleBeforeTheNextTapPlays()
        {
            var c = new TouchContact();
            Feed(ref c, false, 0f);
            Assert.That(Feed(ref c, true, Deliberate), Is.True);

            Assert.That(Feed(ref c, true, Deliberate), Is.False, "still moving: the same gesture");
            Assert.That(Feed(ref c, true, Deliberate), Is.False);
        }

        [Test]
        public void RepeatedTaps_PlayEveryTime_WhenTheFingerSettlesBetween()
        {
            var c = new TouchContact();
            Feed(ref c, false, 0f);
            Feed(ref c, true, Gentle);

            int plays = 0;
            for (int tap = 0; tap < 4; tap++)
            {
                if (Feed(ref c, true, Deliberate))
                {
                    plays++;
                }

                for (int i = 0; i < T.SettleFrames; i++)
                {
                    Feed(ref c, true, Still); // between taps the finger slows right down
                }
            }

            Assert.That(plays, Is.EqualTo(4));
        }

        [Test]
        public void TooFewSettleFrames_DoNotReArm()
        {
            var c = new TouchContact();
            Feed(ref c, false, 0f);
            Feed(ref c, true, Deliberate); // plays

            for (int i = 0; i < T.SettleFrames - 1; i++)
            {
                Feed(ref c, true, Still);
            }

            Assert.That(Feed(ref c, true, Deliberate), Is.False);
        }

        [Test]
        public void ASettleStreak_IsBrokenByAMovementAboveTheSettleSpeed()
        {
            var c = new TouchContact();
            Feed(ref c, false, 0f);
            Feed(ref c, true, Deliberate); // plays

            Feed(ref c, true, Still);
            Feed(ref c, true, Still);
            Feed(ref c, true, 0.2f);       // moved again: the count restarts
            Feed(ref c, true, Still);
            Feed(ref c, true, Still);

            Assert.That(c.IsArmed, Is.False, "two calm frames after the break is not enough");
            Feed(ref c, true, Still);
            Assert.That(c.IsArmed, Is.True);
        }

        // --- leaving and coming back ---------------------------------------------------------

        [Test]
        public void LeavingTheVolume_ReArms_SoTheNextEntryPlaysImmediately()
        {
            var c = new TouchContact();
            Feed(ref c, false, 0f);
            Feed(ref c, true, Deliberate); // plays

            Feed(ref c, false, 0.5f);      // out
            Assert.That(Feed(ref c, true, Deliberate), Is.True);
        }

        [Test]
        public void ASlowEntry_AfterLeaving_StillNeedsATap()
        {
            var c = new TouchContact();
            Feed(ref c, false, 0f);
            Feed(ref c, true, Deliberate); // plays
            Feed(ref c, false, 0f);

            Assert.That(Feed(ref c, true, Gentle), Is.False);
            Assert.That(Feed(ref c, true, Deliberate), Is.True, "a tap plays it");
        }

        // --- housekeeping --------------------------------------------------------------------

        [Test]
        public void AFreshContact_StartsInsideWithoutPlaying()
        {
            // Tracking resumes with the finger already in a volume: no phantom note.
            var c = new TouchContact();

            Assert.That(Feed(ref c, true, Gentle), Is.False);
        }

        [Test]
        public void Reset_ForgetsEverything()
        {
            var c = new TouchContact();
            Feed(ref c, false, 0f);
            Feed(ref c, true, Deliberate);

            c.Reset();

            Assert.That(c.IsArmed, Is.False);
            Assert.That(Feed(ref c, true, Deliberate), Is.True, "after a reset, the next frame inside reads as a fresh entry");
        }

        [Test]
        public void ContactsInAnArray_AreIndependent()
        {
            var contacts = new TouchContact[2, 2];

            contacts[0, 0].Feed(false, 0f, T);
            contacts[0, 1].Feed(false, 0f, T);
            bool first = contacts[0, 0].Feed(true, Deliberate, T);

            Assert.That(first, Is.True);
            Assert.That(contacts[0, 1].IsArmed, Is.True, "the neighbour was not touched");
            Assert.That(contacts[0, 1].Feed(true, Gentle, T), Is.False);
        }

        // --- thresholds ----------------------------------------------------------------------

        [Test]
        public void TheDefaults_AreInternallyConsistent()
        {
            TouchContactThresholds d = TouchContactThresholds.Default;

            Assert.That(d.TapSpeed, Is.GreaterThan(d.EntrySpeed));
            Assert.That(d.SettleSpeed, Is.LessThan(d.TapSpeed), "hysteresis");
            Assert.That(d.SettleFrames, Is.GreaterThanOrEqualTo(1));
            Assert.That(d.EntrySpeed, Is.EqualTo(MelodyEngine.EntryVelocityGateMetresPerSecond),
                "the engine's gate is the entry speed");
        }

        [Test]
        public void BadThresholds_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchContactThresholds(-0.1f, 0.25f, 0.1f, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchContactThresholds(0.1f, 0.05f, 0.02f, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchContactThresholds(0.08f, 0.25f, 0.25f, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchContactThresholds(0.08f, 0.25f, 0.1f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TouchContactThresholds(float.NaN, 0.25f, 0.1f, 3));
        }
    }
}
