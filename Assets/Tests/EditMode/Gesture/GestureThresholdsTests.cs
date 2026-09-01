using System;
using Jazztures.Core.Gesture;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class GestureThresholdsTests
    {
        [Test]
        public void Default_MatchesTheSpecValues()
        {
            GestureThresholds d = GestureThresholds.Default;

            Assert.That(d.PoseHoldSeconds, Is.EqualTo(0.120));
            Assert.That(d.ConfirmingFrames, Is.EqualTo(3));
            Assert.That(d.MinInterChordSeconds, Is.EqualTo(0.100));
            Assert.That(d.HighFramesToResumeAfterLoss, Is.EqualTo(3));
            Assert.That(d.TrackingLossCueSeconds, Is.EqualTo(0.200));
        }

        [TestCase(-0.1, 3, 0.1, 3, 0.2)]
        [TestCase(0.12, 0, 0.1, 3, 0.2)]
        [TestCase(0.12, 3, -0.1, 3, 0.2)]
        [TestCase(0.12, 3, 0.1, 0, 0.2)]
        [TestCase(0.12, 3, 0.1, 3, -0.2)]
        public void Constructor_RejectsInvalidValues(
            double hold, int frames, double debounce, int resumeFrames, double cue)
        {
            Assert.That(
                () => new GestureThresholds(hold, frames, debounce, resumeFrames, cue),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
