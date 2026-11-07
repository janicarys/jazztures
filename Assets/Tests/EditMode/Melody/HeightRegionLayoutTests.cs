using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    // 9 regions of 4.5 cm centred on the origin: region 4 spans -2.25..+2.25 cm; the 4/5 boundary is at +2.25 cm.
    public class HeightRegionLayoutTests
    {
        private static HeightRegionLayout Make() => new HeightRegionLayout(9, 0.045f, 0.010f);

        [Test]
        public void MiddleRegion_SitsOnTheOrigin()
        {
            Assert.That(Make().RegionOf(0f), Is.EqualTo(4));
            Assert.That(Make().CentreOf(4), Is.EqualTo(0f).Within(1e-6));
        }

        [TestCase(-0.045f, 3)]
        [TestCase(0.045f, 5)]
        [TestCase(0.09f, 6)]
        [TestCase(-0.18f, 0)]
        [TestCase(0.18f, 8)]
        public void RegionOf_MapsHeightToRegion(float height, int expected) =>
            Assert.That(Make().RegionOf(height), Is.EqualTo(expected));

        [Test]
        public void AboveAndBelowTheLine_ClampToTheEndRegions()
        {
            HeightRegionLayout layout = Make();
            Assert.That(layout.RegionOf(5f), Is.EqualTo(8));
            Assert.That(layout.RegionOf(-5f), Is.EqualTo(0));
            Assert.That(layout.Update(5f), Is.EqualTo(8));
            Assert.That(layout.Update(-5f), Is.EqualTo(0));
        }

        [Test]
        public void NearABoundary_TheHandKeepsItsRegion()
        {
            HeightRegionLayout layout = Make();
            Assert.That(layout.Update(0.030f), Is.EqualTo(5));   // past the 4/5 boundary at 2.25 cm
            Assert.That(layout.Update(0.0175f), Is.EqualTo(5));  // back past it by 0.5 cm: within the 1 cm hysteresis
            Assert.That(layout.Update(0.0100f), Is.EqualTo(4));  // 1.25 cm back: leaves
        }

        [Test]
        public void Hysteresis_WorksAtTheUpperEdgeToo()
        {
            HeightRegionLayout layout = Make();
            Assert.That(layout.Update(0.0f), Is.EqualTo(4));
            Assert.That(layout.Update(0.030f), Is.EqualTo(4));   // 0.75 cm past the edge: held
            Assert.That(layout.Update(0.040f), Is.EqualTo(5));
        }

        [Test]
        public void FastMove_CanSkipRegions()
        {
            HeightRegionLayout layout = Make();
            layout.Update(-0.045f);
            Assert.That(layout.Update(0.09f), Is.EqualTo(6));
        }

        [Test]
        public void Reset_ForgetsTheRegion()
        {
            HeightRegionLayout layout = Make();
            layout.Update(0.09f);
            layout.Reset();
            Assert.That(layout.Update(0.0f), Is.EqualTo(4));
        }
    }
}
