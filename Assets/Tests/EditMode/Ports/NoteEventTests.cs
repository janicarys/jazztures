using System;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Ports
{
    public class NoteEventTests
    {
        [Test]
        public void On_CarriesEveryField()
        {
            var e = NoteEvent.On(new Pitch(72), 96, 1.25, channel: 2, Handedness.Right);

            Assert.That(e.Kind, Is.EqualTo(NoteEventKind.On));
            Assert.That(e.Pitch, Is.EqualTo(new Pitch(72)));
            Assert.That(e.Velocity, Is.EqualTo(96));
            Assert.That(e.DspTime, Is.EqualTo(1.25));
            Assert.That(e.Channel, Is.EqualTo(2));
            Assert.That(e.Source, Is.EqualTo(Handedness.Right));
        }

        [Test]
        public void Off_HasZeroVelocity_EvenIfConstructedWithOne()
        {
            var off = new NoteEvent(NoteEventKind.Off, new Pitch(60), 100, 0.0, 0, Handedness.Left);

            Assert.That(off.Velocity, Is.Zero);
        }

        [Test]
        public void Constructor_RejectsVelocityAbove127()
        {
            Assert.That(
                () => NoteEvent.On(new Pitch(60), 200, 0.0, 0, Handedness.Left),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_RejectsNonFiniteDspTime()
        {
            Assert.That(
                () => NoteEvent.On(new Pitch(60), 64, double.NaN, 0, Handedness.Left),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Equality_IsByValue()
        {
            var a = NoteEvent.On(new Pitch(67), 80, 3.0, 1, Handedness.Right);
            var b = NoteEvent.On(new Pitch(67), 80, 3.0, 1, Handedness.Right);
            var c = NoteEvent.On(new Pitch(67), 81, 3.0, 1, Handedness.Right);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a != c, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }
    }
}
