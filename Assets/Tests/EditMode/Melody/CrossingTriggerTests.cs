using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    // Defaults: dead band 2 cm total (edges at +-1 cm), lockout 80 ms.
    public class CrossingTriggerTests
    {
        private CrossingTrigger _trigger = null!;

        [SetUp]
        public void SetUp() => _trigger = new CrossingTrigger(CrossingThresholds.Default);

        [Test]
        public void LeftToRight_FiresOnce_WithThatDirection()
        {
            Assert.That(_trigger.Update(0.00, -0.05f), Is.False); // seeds the side
            Assert.That(_trigger.Update(0.05, 0.00f), Is.False);  // in the band
            Assert.That(_trigger.Update(0.10, 0.05f), Is.True);

            Assert.That(_trigger.LastDirection, Is.EqualTo(SwingDirection.LeftToRight));
            Assert.That(_trigger.LastTime, Is.EqualTo(0.10).Within(1e-9));
        }

        [Test]
        public void RightToLeft_Fires_AfterTheLockout()
        {
            _trigger.Update(0.00, -0.05f);
            _trigger.Update(0.10, 0.05f);

            Assert.That(_trigger.Update(0.30, -0.05f), Is.True);
            Assert.That(_trigger.LastDirection, Is.EqualTo(SwingDirection.RightToLeft));
        }

        [Test]
        public void HoveringOnTheLine_NeverFires()
        {
            int fired = 0;
            _trigger.Update(0.0, -0.05f);
            for (int i = 1; i <= 200; i++)
            {
                float x = i % 2 == 0 ? 0.009f : -0.009f; // jitters across the line, inside the band
                fired += _trigger.Update(i / 90.0, x) ? 1 : 0;
            }

            Assert.That(fired, Is.Zero);
        }

        [Test]
        public void SteppingIntoTheBand_AndBackOut_NeverFires()
        {
            _trigger.Update(0.0, -0.05f);
            Assert.That(_trigger.Update(0.1, 0.0f), Is.False);
            Assert.That(_trigger.Update(0.2, -0.05f), Is.False);
        }

        [Test]
        public void AHandFirstSeenOnTheFarSide_DoesNotFireByArriving()
        {
            Assert.That(_trigger.Update(0.0, 0.08f), Is.False);
            Assert.That(_trigger.Update(0.1, 0.08f), Is.False);
        }

        [Test]
        public void ACrossingInsideTheLockout_IsIgnored_ButTheNextRealOneFires()
        {
            _trigger.Update(0.00, -0.05f);
            Assert.That(_trigger.Update(1.00, 0.05f), Is.True);
            Assert.That(_trigger.Update(1.04, -0.05f), Is.False); // 40 ms later: locked out
            Assert.That(_trigger.Update(1.20, 0.05f), Is.True);   // the hand really is crossing left to right again
            Assert.That(_trigger.LastDirection, Is.EqualTo(SwingDirection.LeftToRight));
        }

        [Test]
        public void FastBackAndForthAt4Hz_FiresOncePerCrossing()
        {
            // Eight half-cycles, 125 ms apart: left, right, left, ... with a sample on the line between each.
            int fired = 0;
            for (int k = 0; k <= 8; k++)
            {
                double t = k * 0.125;
                float side = k % 2 == 0 ? -0.05f : 0.05f;
                if (k > 0)
                {
                    _trigger.Update(t - 0.0625, 0f);
                }

                fired += _trigger.Update(t, side) ? 1 : 0;
            }

            Assert.That(fired, Is.EqualTo(8)); // the first sample only seeds the side
        }

        [Test]
        public void Reset_ForgetsTheSide()
        {
            _trigger.Update(0.0, -0.05f);
            _trigger.Reset();

            Assert.That(_trigger.Update(0.5, 0.05f), Is.False);
        }

        [Test]
        public void ReportsTheLateralSpeedAtTheCrossing()
        {
            _trigger.Update(0.00, -0.05f);
            _trigger.Update(0.10, 0.05f); // 0.10 m in 0.10 s

            Assert.That(_trigger.LastSpeedMetresPerSecond, Is.EqualTo(1.0f).Within(1e-4f));
        }
    }
}
