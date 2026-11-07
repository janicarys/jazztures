using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    // Defaults: up at >= +20, down at <= -25, hysteresis 5 (so Up is left below +15, Down above -20).
    public class DirectionClassifierTests
    {
        private DirectionClassifier _classifier = null!;

        [SetUp]
        public void SetUp() => _classifier = new DirectionClassifier(DirectionThresholds.Default);

        [Test]
        public void StartsSame_AndStaysSame_InsideTheMiddleBand()
        {
            Assert.That(_classifier.Current, Is.EqualTo(StepDirection.Same));
            foreach (float angle in new[] { 0f, 10f, 19.9f, -10f, -24.9f })
            {
                Assert.That(_classifier.Update(angle), Is.EqualTo(StepDirection.Same), $"{angle}");
            }
        }

        [Test]
        public void EntersUpAtTheThreshold_AndLeavesOnlyAfterTheHysteresis()
        {
            Assert.That(_classifier.Update(20f), Is.EqualTo(StepDirection.Up));
            Assert.That(_classifier.Update(15.1f), Is.EqualTo(StepDirection.Up));
            Assert.That(_classifier.Update(15f), Is.EqualTo(StepDirection.Up));
            Assert.That(_classifier.Update(14.9f), Is.EqualTo(StepDirection.Same));
        }

        [Test]
        public void EntersDownAtTheThreshold_AndLeavesOnlyAfterTheHysteresis()
        {
            Assert.That(_classifier.Update(-25f), Is.EqualTo(StepDirection.Down));
            Assert.That(_classifier.Update(-20.1f), Is.EqualTo(StepDirection.Down));
            Assert.That(_classifier.Update(-20f), Is.EqualTo(StepDirection.Down));
            Assert.That(_classifier.Update(-19.9f), Is.EqualTo(StepDirection.Same));
        }

        [Test]
        public void ADriftingHandNearTheUpBoundary_DoesNotFlicker()
        {
            _classifier.Update(21f);
            int changes = 0;
            StepDirection last = _classifier.Current;
            foreach (float angle in new[] { 19f, 21f, 18f, 22f, 17f, 20.5f, 16f })
            {
                StepDirection now = _classifier.Update(angle);
                if (now != last)
                {
                    changes++;
                }

                last = now;
            }

            Assert.That(changes, Is.Zero);
            Assert.That(last, Is.EqualTo(StepDirection.Up));
        }

        [Test]
        public void ADriftingHandNearTheDownBoundary_DoesNotFlicker()
        {
            _classifier.Update(-26f);
            foreach (float angle in new[] { -23f, -27f, -22f, -24f, -21f })
            {
                Assert.That(_classifier.Update(angle), Is.EqualTo(StepDirection.Down), $"{angle}");
            }
        }

        [Test]
        public void AFastTiltFromUpToDown_ReadsDownAtOnce()
        {
            _classifier.Update(30f);

            Assert.That(_classifier.Update(-30f), Is.EqualTo(StepDirection.Down));
        }

        [Test]
        public void Reset_ReturnsToSame()
        {
            _classifier.Update(30f);
            _classifier.Reset();

            Assert.That(_classifier.Current, Is.EqualTo(StepDirection.Same));
        }

        [Test]
        public void Thresholds_WithNoRoomForSame_AreRejected()
        {
            Assert.Throws<System.ArgumentException>(() => new DirectionThresholds(5f, 0f, 5f));
        }
    }
}
