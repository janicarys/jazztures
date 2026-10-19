using Jazztures.Core.Ports;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Menu
{
    public class MenuHandFrameTests
    {
        [Test]
        public void TheTutorialsExtraMembers_DefaultToUnknown()
        {
            var frame = new MenuHandFrame(TrackingQuality.High);

            Assert.That(frame.LeftTracking, Is.EqualTo(TrackingQuality.NotTracked));
            Assert.That(frame.ThumbUpAngleDegrees, Is.EqualTo(180f));
        }

        [Test]
        public void MaxFingerClosure_FallsBackToTheFistAmount_WhenNotMeasured()
        {
            var frame = new MenuHandFrame(TrackingQuality.High, fistAmount: 0.7f);

            Assert.That(frame.MaxFingerClosure, Is.EqualTo(0.7f));
        }

        [Test]
        public void MaxFingerClosure_IsKeptWhenMeasured()
        {
            var frame = new MenuHandFrame(TrackingQuality.High, fistAmount: 0.1f, maxFingerClosure: 0.9f);

            Assert.That(frame.FistAmount, Is.EqualTo(0.1f));
            Assert.That(frame.MaxFingerClosure, Is.EqualTo(0.9f));
        }

        [Test]
        public void Values_AreClampedIntoTheirRanges()
        {
            var frame = new MenuHandFrame(
                TrackingQuality.High, leftwardSpeed: -2f, downwardSpeed: float.NaN, fistAmount: 4f,
                maxFingerClosure: -1f, thumbUpAngleDegrees: 400f);

            Assert.That(frame.LeftwardSpeed, Is.EqualTo(0f));
            Assert.That(frame.DownwardSpeed, Is.EqualTo(0f));
            Assert.That(frame.FistAmount, Is.EqualTo(1f));
            Assert.That(frame.MaxFingerClosure, Is.EqualTo(0f));
            Assert.That(frame.ThumbUpAngleDegrees, Is.EqualTo(180f));
        }

        [Test]
        public void TheViewAngles_DefaultToOutOfView_AndAreClamped()
        {
            var unknown = new MenuHandFrame(TrackingQuality.High);
            Assert.That(unknown.ViewAngleDegrees, Is.EqualTo(180f));
            Assert.That(unknown.LeftViewAngleDegrees, Is.EqualTo(180f));

            var odd = new MenuHandFrame(TrackingQuality.High, viewAngleDegrees: -20f, leftViewAngleDegrees: float.NaN);
            Assert.That(odd.ViewAngleDegrees, Is.EqualTo(0f));
            Assert.That(odd.LeftViewAngleDegrees, Is.EqualTo(180f));
        }

        [Test]
        public void Equality_TakesTheNewMembersIntoAccount()
        {
            var a = new MenuHandFrame(TrackingQuality.High, leftTracking: TrackingQuality.High, thumbUpAngleDegrees: 10f);
            var b = new MenuHandFrame(TrackingQuality.High, leftTracking: TrackingQuality.High, thumbUpAngleDegrees: 10f);
            var c = new MenuHandFrame(TrackingQuality.High, leftTracking: TrackingQuality.Low, thumbUpAngleDegrees: 10f);
            var d = new MenuHandFrame(TrackingQuality.High, leftTracking: TrackingQuality.High, thumbUpAngleDegrees: 20f);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(c));
            Assert.That(a, Is.Not.EqualTo(d));
        }
    }
}
