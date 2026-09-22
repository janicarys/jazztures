using Jazztures.Core.Gesture;
using Jazztures.Core.Ports;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class HarmonicFieldTests
    {
        private HarmonicField _field = null!;

        [SetUp]
        public void SetUp()
        {
            _field = new HarmonicField(HarmonicFieldThresholds.Default);
        }

        [Test]
        public void HandAtTheIiLandmark_SelectsIi()
        {
            var d = HarmonicFieldThresholds.Default;
            Assert.That(_field.Update(d.IiHeight, d.IiOpenness), Is.EqualTo(HandPoseCandidate.Ii));
        }

        [Test]
        public void HandAtTheVLandmark_SelectsV()
        {
            var d = HarmonicFieldThresholds.Default;
            Assert.That(_field.Update(d.VHeight, d.VOpenness), Is.EqualTo(HandPoseCandidate.V));
        }

        [Test]
        public void HandAtTheILandmark_SelectsI()
        {
            var d = HarmonicFieldThresholds.Default;
            Assert.That(_field.Update(d.IHeight, d.IOpenness), Is.EqualTo(HandPoseCandidate.I));
        }

        [Test]
        public void HandBelowTheFloor_WithNothingLatched_SelectsNothing()
        {
            var d = HarmonicFieldThresholds.Default;
            Assert.That(_field.Update(d.FloorHeight - 0.5f, 0.5f), Is.EqualTo(HandPoseCandidate.None));
        }

        // ADR-0034-class regression guard: the whole point of the latch is that being
        // nearer to a different landmark is not, by itself, enough to switch — and being
        // outside every landmark's lock radius (the "gap" between them) must never read as
        // a release, unlike the old discrete/velocity system where ordinary pose noise in
        // that gap could confirm a release nobody intended.
        [Test]
        public void HandBetweenTwoLandmarks_HoldsWhateverWasLatched_RatherThanReleasing()
        {
            var d = HarmonicFieldThresholds.Default;
            _field.Update(d.VHeight, d.VOpenness);
            Assume.That(_field.Current, Is.EqualTo(HandPoseCandidate.V));

            // Nearer to ii than to V now, but not within ii's LockRadius yet.
            HandPoseCandidate result = _field.Update(0.7f, 0.6f);

            Assert.That(result, Is.Not.EqualTo(HandPoseCandidate.None), "the gap between landmarks must never release");
            Assert.That(result, Is.EqualTo(HandPoseCandidate.V), "not yet close enough to ii to switch");
        }

        [Test]
        public void LeavingALatchedLandmark_NeedsToExceedTheUnlockRadius_NotMerelyEnteringTheOthersLockRadius()
        {
            Assert.That(HarmonicFieldThresholds.Default.UnlockRadius, Is.GreaterThan(HarmonicFieldThresholds.Default.LockRadius),
                "this test only proves something if unlock is the larger of the two");

            // A 1D field (openness fixed at 0 for both real landmarks) with room between
            // LockRadius and UnlockRadius, so the two conditions can be pulled apart.
            var thresholds = new HarmonicFieldThresholds(
                iiHeight: 0f, iiOpenness: 0f,
                vHeight: 1f, vOpenness: 0f,
                iHeight: -100f, iOpenness: 0f, // far away, never nearest
                floorHeight: -1000f, floorReleaseHeight: -2000f,
                lockRadius: 0.3f, unlockRadius: 0.8f);
            var field = new HarmonicField(thresholds);

            field.Update(0f, 0f);
            Assume.That(field.Current, Is.EqualTo(HandPoseCandidate.Ii));

            // Distance to V = 0.25 (within LockRadius 0.3); distance to ii = 0.75 (NOT yet
            // past UnlockRadius 0.8). Must not switch.
            Assert.That(field.Update(0.75f, 0f), Is.EqualTo(HandPoseCandidate.Ii),
                "V is close enough to lock, but ii hasn't been left far enough behind yet");

            // Distance to V = 0.15 (within LockRadius); distance to ii = 0.85 (past
            // UnlockRadius 0.8 now). Must switch.
            Assert.That(field.Update(0.85f, 0f), Is.EqualTo(HandPoseCandidate.V),
                "now ii has been left far enough behind for V to take over");
        }

        [Test]
        public void DriftingJustPastTheFloor_WhileLatched_DoesNotRelease_UntilTheReleaseFloor()
        {
            var d = HarmonicFieldThresholds.Default;
            Assert.That(d.FloorReleaseHeight, Is.LessThan(d.FloorHeight),
                "this test only proves something if the release floor is the lower of the two");

            _field.Update(d.IHeight, d.IOpenness);
            Assume.That(_field.Current, Is.EqualTo(HandPoseCandidate.I));

            // Below FloorHeight but not yet below FloorReleaseHeight.
            float justBelowFloor = (d.FloorHeight + d.FloorReleaseHeight) / 2f;
            Assert.That(_field.Update(justBelowFloor, d.IOpenness), Is.EqualTo(HandPoseCandidate.I),
                "a latched landmark must not release at the ordinary floor, only the release floor");

            Assert.That(_field.Update(d.FloorReleaseHeight - 0.01f, d.IOpenness), Is.EqualTo(HandPoseCandidate.None),
                "past the release floor, it does release");
        }

        [Test]
        public void ATravelFromIiToI_AtConstantOpenness_PassesThroughNoReleaseState()
        {
            var d = HarmonicFieldThresholds.Default;
            _field.Update(d.IiHeight, d.IiOpenness);
            Assume.That(_field.Current, Is.EqualTo(HandPoseCandidate.Ii));

            for (float h = d.IiHeight; h >= d.IHeight; h -= 0.01f)
            {
                HandPoseCandidate result = _field.Update(h, d.IiOpenness);
                Assert.That(result, Is.Not.EqualTo(HandPoseCandidate.None),
                    $"height {h} must not read as a release mid-travel");
            }

            Assert.That(_field.Current, Is.EqualTo(HandPoseCandidate.I), "should have arrived at I by the end of the travel");
        }

        [Test]
        public void TheSameInputTwice_YieldsTheSameOutput()
        {
            var d = HarmonicFieldThresholds.Default;
            HandPoseCandidate first = _field.Update(d.VHeight, d.VOpenness);
            HandPoseCandidate second = _field.Update(d.VHeight, d.VOpenness);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Reset_UnlatchesImmediately_RegardlessOfPosition()
        {
            var d = HarmonicFieldThresholds.Default;
            _field.Update(d.IiHeight, d.IiOpenness);
            Assume.That(_field.Current, Is.EqualTo(HandPoseCandidate.Ii));

            _field.Reset();

            Assert.That(_field.Current, Is.EqualTo(HandPoseCandidate.None));
        }

        [Test]
        public void NoInput_EverProducesAmbiguous()
        {
            for (float h = -1f; h <= 2f; h += 0.1f)
            {
                for (float o = -0.5f; o <= 1.5f; o += 0.1f)
                {
                    Assert.That(_field.Update(h, o), Is.Not.EqualTo(HandPoseCandidate.Ambiguous));
                }
            }
        }
    }
}
