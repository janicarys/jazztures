using System;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Timing
{
    public class VirtualClockTests
    {
        [Test]
        public void StartsAtZero_ByDefault()
        {
            Assert.That(new VirtualClock().Now, Is.EqualTo(0.0));
        }

        [Test]
        public void StartsAtGivenTime()
        {
            Assert.That(new VirtualClock(3.5).Now, Is.EqualTo(3.5));
        }

        [Test]
        public void Advance_MovesTimeForward()
        {
            var clock = new VirtualClock();

            clock.Advance(0.25);
            clock.Advance(0.75);

            Assert.That(clock.Now, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void Advance_RejectsNegative()
        {
            Assert.That(() => new VirtualClock().Advance(-0.1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void SetNow_JumpsForward_ButNotBackward()
        {
            var clock = new VirtualClock(1.0);

            clock.SetNow(2.0);
            Assert.That(clock.Now, Is.EqualTo(2.0));

            Assert.That(() => clock.SetNow(1.5), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
