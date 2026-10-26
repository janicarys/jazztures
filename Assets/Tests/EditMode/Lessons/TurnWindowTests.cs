using System;
using Jazztures.Core.Lessons;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class TurnWindowTests
    {
        [Test]
        public void None_IsAlwaysTheSystemsTurn_AndNeverEnds()
        {
            TurnWindow none = TurnWindow.None;

            Assert.That(none.IsEnabled, Is.False);
            Assert.That(none.IsSystemTurn(0.0), Is.True);
            Assert.That(none.IsSystemTurn(1000.0), Is.True);
            Assert.That(none.SystemTurnEnd(3.0), Is.EqualTo(double.PositiveInfinity));
            Assert.That(default(TurnWindow).IsEnabled, Is.False, "default is none, so an unset timeline changes nothing");
        }

        [TestCase(0.0, true)]
        [TestCase(7.99, true)]
        [TestCase(8.0, false)]
        [TestCase(15.99, false)]
        [TestCase(16.0, true)]
        [TestCase(23.99, true)]
        [TestCase(24.0, false)]
        [TestCase(-3.0, true)]
        public void TheSystemHasTheFirstPartOfEveryRound(double beat, bool systems)
        {
            var window = new TurnWindow(roundBeats: 16.0, systemTurnBeats: 8.0);

            Assert.That(window.IsSystemTurn(beat), Is.EqualTo(systems));
        }

        [TestCase(0.0, 8.0)]
        [TestCase(5.0, 8.0)]
        [TestCase(12.0, 8.0)]
        [TestCase(16.0, 24.0)]
        [TestCase(40.0, 40.0)]
        public void TheTurnEnd_IsThatRoundsSystemTurnEnd(double beat, double end)
        {
            var window = new TurnWindow(16.0, 8.0);

            Assert.That(window.SystemTurnEnd(beat), Is.EqualTo(end));
        }

        [TestCase(0.0, 8.0)]
        [TestCase(-1.0, 8.0)]
        [TestCase(16.0, 16.0)]
        [TestCase(16.0, 20.0)]
        [TestCase(double.NaN, 8.0)]
        [TestCase(16.0, double.PositiveInfinity)]
        public void ARoundWithoutALearnersTurn_OrWithoutLength_IsRefused(double round, double system)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TurnWindow(round, system));
        }
    }
}
