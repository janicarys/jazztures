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

            Assert.That(d.PoseHoldSeconds, Is.EqualTo(0.150));
            Assert.That(d.ConfirmingFrames, Is.EqualTo(3));
            Assert.That(d.MinInterChordSeconds, Is.EqualTo(0.100));
            Assert.That(d.HighFramesToResumeAfterLoss, Is.EqualTo(3));
            Assert.That(d.TrackingLossCueSeconds, Is.EqualTo(0.200));
            Assert.That(d.ConfirmationMissTolerance, Is.EqualTo(2));
            Assert.That(d.StrikeEnterSpeedMetresPerSecond, Is.EqualTo(0.25f));
            Assert.That(d.StrikeExitSpeedMetresPerSecond, Is.EqualTo(0.10f));
            Assert.That(d.MinInterStrikeSeconds, Is.EqualTo(0.12));
            Assert.That(d.ReleaseHoldSeconds, Is.EqualTo(0.400));
            Assert.That(d.ReleaseMissTolerance, Is.EqualTo(6));
            Assert.That(d.StrikeSettleFrames, Is.EqualTo(3));
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

        [Test]
        public void Constructor_RejectsNegativeMissTolerance()
        {
            Assert.That(
                () => new GestureThresholds(0.12, 3, 0.1, 3, 0.2, confirmationMissTolerance: -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0.10f, 0.10f, Description = "enter must exceed exit, not just match it")]
        [TestCase(0.05f, 0.10f, Description = "enter below exit inverts the Schmitt trigger")]
        public void Constructor_RejectsAStrikeEnterSpeedThatDoesNotExceedExit(float enter, float exit)
        {
            Assert.That(
                () => new GestureThresholds(
                    0.12, 3, 0.1, 3, 0.2,
                    strikeEnterSpeedMetresPerSecond: enter,
                    strikeExitSpeedMetresPerSecond: exit),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_RejectsNegativeMinInterStrikeSeconds()
        {
            Assert.That(
                () => new GestureThresholds(0.12, 3, 0.1, 3, 0.2, minInterStrikeSeconds: -0.01),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_RejectsAReleaseHoldShorterThanPoseHold()
        {
            Assert.That(
                () => new GestureThresholds(0.12, 3, 0.1, 3, 0.2, releaseHoldSeconds: 0.11),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_RejectsAReleaseMissToleranceLowerThanConfirmationMissTolerance()
        {
            Assert.That(
                () => new GestureThresholds(0.12, 3, 0.1, 3, 0.2, confirmationMissTolerance: 4, releaseMissTolerance: 3),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_RejectsANonPositiveStrikeSettleFrames()
        {
            Assert.That(
                () => new GestureThresholds(0.12, 3, 0.1, 3, 0.2, strikeSettleFrames: 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
