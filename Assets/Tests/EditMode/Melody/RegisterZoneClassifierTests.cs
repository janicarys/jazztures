using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    // Three zones of 12 cm centred on the origin: boundaries at -6 cm and +6 cm; hysteresis 3 cm.
    public class RegisterZoneClassifierTests
    {
        private RegisterZoneClassifier _zones = null!;

        [SetUp]
        public void SetUp() => _zones = new RegisterZoneClassifier(RegisterZoneThresholds.Default);

        [Test]
        public void StartsInTheMiddleZone_AndStaysThere_Near_TheOrigin()
        {
            Assert.That(_zones.Current, Is.EqualTo(1));
            Assert.That(_zones.Update(0f), Is.EqualTo(1));
            Assert.That(_zones.Update(0.05f), Is.EqualTo(1));
            Assert.That(_zones.Update(-0.05f), Is.EqualTo(1));
        }

        [Test]
        public void RisesAtTheUpperBoundary_AndFallsBackOnlyAfterTheHysteresis()
        {
            Assert.That(_zones.Update(0.06f), Is.EqualTo(2));
            Assert.That(_zones.Update(0.031f), Is.EqualTo(2));
            Assert.That(_zones.Update(0.029f), Is.EqualTo(1));
        }

        [Test]
        public void FallsBelowTheLowerBoundary_OnlyAfterTheHysteresis_AndRisesBackAtTheBoundary()
        {
            Assert.That(_zones.Update(-0.07f), Is.EqualTo(1));
            Assert.That(_zones.Update(-0.091f), Is.EqualTo(0));
            Assert.That(_zones.Update(-0.061f), Is.EqualTo(0));
            Assert.That(_zones.Update(-0.06f), Is.EqualTo(1));
        }

        [Test]
        public void HoveringAtABoundary_DoesNotChangeZone()
        {
            _zones.Update(0.062f); // into zone 2
            foreach (float h in new[] { 0.058f, 0.062f, 0.057f, 0.063f, 0.055f })
            {
                Assert.That(_zones.Update(h), Is.EqualTo(2), $"{h}");
            }
        }

        [Test]
        public void AJumpAcrossBothBoundaries_LandsInTheFarZone()
        {
            Assert.That(_zones.Update(0.3f), Is.EqualTo(2));
            Assert.That(_zones.Update(-0.3f), Is.EqualTo(0));
        }

        [Test]
        public void TwoZones_SplitAtTheOrigin()
        {
            var two = new RegisterZoneClassifier(new RegisterZoneThresholds(2, 0.12f, 0.03f));

            Assert.That(two.Current, Is.EqualTo(0));
            Assert.That(two.Update(0.01f), Is.EqualTo(1));
            Assert.That(two.Update(-0.02f), Is.EqualTo(1));
            Assert.That(two.Update(-0.031f), Is.EqualTo(0));
        }
    }
}
