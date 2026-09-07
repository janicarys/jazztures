using System;
using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class LazyRecenterTests
    {
        private static LazyRecenter New(double initialFacing = 0.0) =>
            new LazyRecenter(LazyRecenterSettings.Default, initialFacing);

        /// <summary>Feed the same head yaw for <paramref name="frames"/> steps of <paramref name="dt"/>.</summary>
        private static void Hold(LazyRecenter recenter, double headYaw, int frames, double dt = 0.1)
        {
            for (int i = 0; i < frames; i++)
            {
                recenter.Update(headYaw, dt);
            }
        }

        private static double AngleBetween(double a, double b)
        {
            double d = (a - b) % (2.0 * Math.PI);
            if (d <= -Math.PI)
            {
                d += 2.0 * Math.PI;
            }
            else if (d > Math.PI)
            {
                d -= 2.0 * Math.PI;
            }

            return Math.Abs(d);
        }

        [Test]
        public void HoldsCommittedFacing_WhileHeadStaysInsideTheDeadZone()
        {
            LazyRecenter r = New();

            // 0.5 rad ≈ 28.6°, inside the 35° threshold.
            Hold(r, 0.5, 30);

            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Settled));
            Assert.That(r.CommittedYawRadians, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void DoesNotFollow_UntilTheDwellHasElapsed()
        {
            LazyRecenter r = New();

            r.Update(1.2, 0.1); // enter Pending
            Hold(r, 1.2, 3);    // ~0.3 s of dwell — well short of 0.6 s

            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Pending));
            Assert.That(r.CommittedYawRadians, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void StartsFollowing_OnceTheDwellPasses()
        {
            LazyRecenter r = New();

            Hold(r, 1.2, 12); // ~1.2 s — past dwell, easing under way

            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Following));
            Assert.That(r.CommittedYawRadians, Is.GreaterThan(0.0));
            Assert.That(r.CommittedYawRadians, Is.LessThan(1.2));
        }

        [Test]
        public void EasesAllTheWayToTheHead_ThenSettlesCentred()
        {
            LazyRecenter r = New();

            Hold(r, 1.2, 400, 0.05); // 20 s — dwell + a long ease

            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Settled));
            Assert.That(r.CommittedYawRadians, Is.EqualTo(1.2).Within(0.02));
        }

        [Test]
        public void CancelsThePendingRecenter_IfTheHeadReturnsBeforeTheDwell()
        {
            LazyRecenter r = New();

            r.Update(1.2, 0.1);
            r.Update(1.2, 0.1); // Pending, ~0.2 s in
            r.Update(0.3, 0.1); // back inside the dead zone

            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Settled));
            Assert.That(r.CommittedYawRadians, Is.EqualTo(0.0).Within(1e-9));

            // A fresh divergence must serve the full dwell again.
            r.Update(1.2, 0.1);
            Hold(r, 1.2, 3); // ~0.3 s
            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Pending));
        }

        [Test]
        public void TakesTheShortWayRoundThePiSeam_AndStaysSettledWithinTheDeadZone()
        {
            LazyRecenter r = New(initialFacing: 3.0); // near +π

            // Head at −3.0: the short path is +0.283 rad across the seam, not −6.0.
            Hold(r, -3.0, 5);

            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Settled));
            Assert.That(r.CommittedYawRadians, Is.EqualTo(3.0).Within(1e-9));
        }

        [Test]
        public void EasesAcrossThePiSeam_WhenTheShortPathDivergencePastTheThreshold()
        {
            LazyRecenter r = New(initialFacing: 3.0);

            Hold(r, -2.5, 400, 0.05); // short-path error ≈ 0.78 rad → diverges, eases across the seam

            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Settled));
            Assert.That(AngleBetween(r.CommittedYawRadians, -2.5), Is.LessThan(0.02));
        }

        [Test]
        public void Reset_JumpsTheFacingAndSettles()
        {
            LazyRecenter r = New();
            r.Update(2.0, 0.1);
            r.Update(2.0, 0.1); // Pending

            r.Reset(1.0);

            Assert.That(r.CommittedYawRadians, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Settled));
        }

        [Test]
        public void SnapsInOneEaseStep_WhenEaseSecondsIsZero()
        {
            var settings = LazyRecenterSettings.FromDegrees(35.0, dwellSeconds: 0.0, easeSeconds: 0.0);
            var r = new LazyRecenter(settings);

            r.Update(1.5, 0.1); // Settled → Pending
            r.Update(1.5, 0.1); // Pending → Following (dwell 0)
            r.Update(1.5, 0.1); // Following: k = 1 → snap

            Assert.That(r.CommittedYawRadians, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Settled));
        }

        [Test]
        public void TreatsANegativeDeltaAsZero()
        {
            LazyRecenter r = New();

            double committed = r.Update(-10.0, -1.0); // rewound clock

            Assert.That(committed, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(r.Phase, Is.EqualTo(RecenterPhase.Pending)); // divergence noticed, nothing eased
        }

        [Test]
        public void Settings_RejectOutOfRangeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LazyRecenterSettings(-0.1, 0.5, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LazyRecenterSettings(Math.PI, 0.5, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LazyRecenterSettings(0.5, -0.1, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LazyRecenterSettings(0.5, 0.5, -0.1));
        }

        [Test]
        public void Default_MatchesTheAdr0015EngineeringValues()
        {
            LazyRecenterSettings d = LazyRecenterSettings.Default;

            Assert.That(d.AngleThresholdRadians, Is.EqualTo(35.0 * Math.PI / 180.0).Within(1e-9));
            Assert.That(d.DwellSeconds, Is.EqualTo(0.6).Within(1e-9));
            Assert.That(d.EaseSeconds, Is.EqualTo(0.5).Within(1e-9));
        }
    }
}
