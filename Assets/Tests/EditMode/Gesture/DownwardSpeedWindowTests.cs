using System;
using Jazztures.Core.Gesture;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class DownwardSpeedWindowTests
    {
        private const float Dt = 0.01f; // 100 Hz keeps the arithmetic round

        [Test]
        public void TheFirstFrame_ReportsNothing_ThereIsNothingToDifferentiateAgainst()
        {
            var w = new DownwardSpeedWindow(3);

            Assert.That(w.Update(1.0f, Dt), Is.EqualTo(0f));
        }

        [Test]
        public void AHandMovingDown_ReportsItsSpeed()
        {
            var w = new DownwardSpeedWindow(3);
            w.Update(1.00f, Dt);

            float speed = w.Update(0.99f, Dt); // 1 cm in 10 ms

            Assert.That(speed, Is.EqualTo(1.0f).Within(1e-3f));
        }

        [Test]
        public void MovingUp_ReportsZero()
        {
            var w = new DownwardSpeedWindow(3);
            w.Update(1.00f, Dt);

            Assert.That(w.Update(1.02f, Dt), Is.EqualTo(0f));
        }

        [Test]
        public void MovingSideways_ReportsZero_BecauseHeightDoesNotChange()
        {
            var w = new DownwardSpeedWindow(3);
            w.Update(1.0f, Dt);

            Assert.That(w.Update(1.0f, Dt), Is.EqualTo(0f));
        }

        [Test]
        public void ADecelerationRunStillReportsItsPeak_WhileTheHandKeepsMovingDown()
        {
            var w = new DownwardSpeedWindow(3);
            w.Update(1.000f, Dt);
            w.Update(0.980f, Dt); // 2.0 m/s
            float slowing = w.Update(0.975f, Dt); // 0.5 m/s now

            Assert.That(slowing, Is.EqualTo(2.0f).Within(1e-3f),
                "a strike that decelerates on arrival must still register what it was doing a frame ago");
        }

        [Test]
        public void TheMomentTheMotionStopsGoingDown_TheOldPeakIsForgotten()
        {
            var w = new DownwardSpeedWindow(3);
            w.Update(1.000f, Dt);
            w.Update(0.980f, Dt); // fast down
            w.Update(0.975f, Dt);

            float rebound = w.Update(0.976f, Dt); // a hair upward

            Assert.That(rebound, Is.EqualTo(0f), "a finished chop must not keep striking");
        }

        [Test]
        public void ThePeakAgesOutOfTheWindow()
        {
            var w = new DownwardSpeedWindow(2);
            w.Update(1.00f, Dt);
            w.Update(0.98f, Dt);          // 2.0 m/s
            w.Update(0.9795f, Dt);        // slow, still down
            float later = w.Update(0.9790f, Dt); // the 2.0 sample has left a window of 2

            Assert.That(later, Is.LessThan(0.2f));
        }

        [Test]
        public void Reset_ForgetsThePreviousPosition()
        {
            var w = new DownwardSpeedWindow(3);
            w.Update(1.0f, Dt);
            w.Update(0.9f, Dt);

            w.Reset();

            Assert.That(w.Update(0.5f, Dt), Is.EqualTo(0f), "a big jump after a gap in tracking is not a strike");
        }

        [Test]
        public void AZeroOrNegativeDeltaTime_DoesNotDivideByZero()
        {
            var w = new DownwardSpeedWindow(3);
            w.Update(1.0f, Dt);

            Assert.That(w.Update(0.5f, 0f), Is.EqualTo(0f));
            Assert.That(w.Update(0.4f, -0.01f), Is.EqualTo(0f));
        }

        [Test]
        public void ANonPositiveWindow_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DownwardSpeedWindow(0));
        }
    }
}
