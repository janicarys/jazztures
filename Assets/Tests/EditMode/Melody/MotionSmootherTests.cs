using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class MotionSmootherTests
    {
        [Test]
        public void TheFirstSample_PassesStraightThrough()
        {
            Assert.That(new MotionSmoother(0.1).Update(0.0, 5f), Is.EqualTo(5f));
        }

        [Test]
        public void ATimeConstantOfZero_PassesEverySampleThrough()
        {
            var smoother = new MotionSmoother(0.0);
            smoother.Update(0.0, 0f);

            Assert.That(smoother.Update(0.01, 1f), Is.EqualTo(1f));
        }

        [Test]
        public void AStep_MovesTo63PercentAfterOneTimeConstant()
        {
            var smoother = new MotionSmoother(0.1);
            smoother.Update(0.0, 0f);

            Assert.That(smoother.Update(0.1, 1f), Is.EqualTo(0.632f).Within(0.001f));
        }

        [Test]
        public void TheResponse_DoesNotDependOnTheFrameRate()
        {
            var coarse = new MotionSmoother(0.1);
            coarse.Update(0.0, 0f);
            float oneStep = coarse.Update(0.1, 1f);

            var fine = new MotionSmoother(0.1);
            fine.Update(0.0, 0f);
            for (int i = 1; i <= 10; i++)
            {
                fine.Update(i * 0.01, 1f);
            }

            Assert.That(fine.Update(0.1000001, 1f), Is.EqualTo(oneStep).Within(0.001f));
        }

        [Test]
        public void ARepeatedTimestamp_ReturnsTheHeldValue()
        {
            var smoother = new MotionSmoother(0.1);
            smoother.Update(1.0, 0f);
            float held = smoother.Update(1.05, 1f);

            Assert.That(smoother.Update(1.05, 9f), Is.EqualTo(held));
        }

        [Test]
        public void ALongGap_PassesTheNewValueAlmostStraightThrough()
        {
            var smoother = new MotionSmoother(0.04);
            smoother.Update(0.0, 0f);

            Assert.That(smoother.Update(5.0, 1f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Reset_MakesTheNextSampleThePassThroughOne()
        {
            var smoother = new MotionSmoother(0.1);
            smoother.Update(0.0, 0f);
            smoother.Update(0.1, 1f);
            smoother.Reset();

            Assert.That(smoother.Update(0.2, 7f), Is.EqualTo(7f));
        }

        [Test]
        public void ANegativeTimeConstant_IsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MotionSmoother(-1.0));
        }
    }
}
