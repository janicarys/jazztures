using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class VelocityCurveTests
    {
        [Test]
        public void AtOrBelowMinSpeed_MapsToMinVelocity()
        {
            Assert.That(VelocityCurve.FromSpeed(VelocityCurve.MinSpeed), Is.EqualTo(VelocityCurve.MinVelocity));
            Assert.That(VelocityCurve.FromSpeed(0f), Is.EqualTo(VelocityCurve.MinVelocity));
        }

        [Test]
        public void AtOrAboveMaxSpeed_MapsToMaxVelocity()
        {
            Assert.That(VelocityCurve.FromSpeed(VelocityCurve.MaxSpeed), Is.EqualTo(VelocityCurve.MaxVelocity));
            Assert.That(VelocityCurve.FromSpeed(10f), Is.EqualTo(VelocityCurve.MaxVelocity));
        }

        [Test]
        public void Midpoint_MapsNearTheMiddleOfTheVelocityRange()
        {
            float mid = (VelocityCurve.MinSpeed + VelocityCurve.MaxSpeed) / 2f;

            Assert.That(VelocityCurve.FromSpeed(mid), Is.EqualTo(75));
        }

        [Test]
        public void NeverEmitsBelowTheAbsoluteFloor()
        {
            Assert.That(VelocityCurve.MinVelocity, Is.GreaterThanOrEqualTo(VelocityCurve.AbsoluteFloor));
            Assert.That(VelocityCurve.FromSpeed(-5f), Is.GreaterThanOrEqualTo(VelocityCurve.AbsoluteFloor));
        }

        [Test]
        public void IsMonotonicAcrossTheRange()
        {
            byte previous = 0;
            for (float s = 0f; s <= 2f; s += 0.05f)
            {
                byte v = VelocityCurve.FromSpeed(s);
                Assert.That(v, Is.GreaterThanOrEqualTo(previous));
                previous = v;
            }
        }
    }
}
