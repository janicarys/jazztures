using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class DirectionHistoryTests
    {
        [Test]
        public void Empty_ReadsSame()
        {
            Assert.That(new DirectionHistory().At(1.0).Direction, Is.EqualTo(StepDirection.Same));
        }

        [Test]
        public void ReturnsTheSampleInEffect50MsBack_NotTheLatest()
        {
            var history = new DirectionHistory();
            for (int i = 0; i <= 20; i++)
            {
                history.Record(i * 0.01, i < 12 ? StepDirection.Up : StepDirection.Same);
            }

            // "Now" is 0.14 (already Same); 50 ms earlier the hand was still Up.
            Assert.That(history.At(0.14 - 0.05).Direction, Is.EqualTo(StepDirection.Up));
            Assert.That(history.At(0.14).Direction, Is.EqualTo(StepDirection.Same));
        }

        [Test]
        public void BeforeTheOldestSample_ReadsTheOldest()
        {
            var history = new DirectionHistory();
            history.Record(1.00, StepDirection.Down);
            history.Record(1.01, StepDirection.Up);

            Assert.That(history.At(0.0).Direction, Is.EqualTo(StepDirection.Down));
        }

        [Test]
        public void AfterTheNewestSample_ReadsTheNewest()
        {
            var history = new DirectionHistory();
            history.Record(1.00, StepDirection.Down);
            history.Record(1.01, StepDirection.Up);

            Assert.That(history.At(50.0).Direction, Is.EqualTo(StepDirection.Up));
        }

        [Test]
        public void WhenTheBufferWraps_TheOldestSurvivingSampleIsTheEdge()
        {
            var history = new DirectionHistory(4);
            for (int i = 0; i < 10; i++)
            {
                history.Record(i, i % 2 == 0 ? StepDirection.Up : StepDirection.Down);
            }

            Assert.That(history.Count, Is.EqualTo(4));
            // Samples 6..9 survive; asking for the distant past gives sample 6 (Up), not an overwritten one.
            Assert.That(history.At(0.0).Direction, Is.EqualTo(StepDirection.Up));
            Assert.That(history.At(7.5).Direction, Is.EqualTo(StepDirection.Down));
            Assert.That(history.At(8.0).Direction, Is.EqualTo(StepDirection.Up));
        }

        [Test]
        public void CarriesTheRegisterZone_WithTheDirection()
        {
            var history = new DirectionHistory();
            history.Record(0.0, StepDirection.Same, zone: 2);

            Assert.That(history.At(0.0).Zone, Is.EqualTo(2));
        }

        [Test]
        public void Clear_Empties()
        {
            var history = new DirectionHistory();
            history.Record(0.0, StepDirection.Up);
            history.Clear();

            Assert.That(history.Count, Is.Zero);
            Assert.That(history.At(0.0).Direction, Is.EqualTo(StepDirection.Same));
        }
    }
}
