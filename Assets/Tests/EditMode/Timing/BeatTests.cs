using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Timing
{
    public class BeatTests
    {
        [Test]
        public void IndexAndFraction_SplitThePosition()
        {
            var beat = new Beat(2.75);

            Assert.That(beat.Index, Is.EqualTo(2));
            Assert.That(beat.Fraction, Is.EqualTo(0.75).Within(1e-9));
        }

        [Test]
        public void SecondsConversion_UsesTheTempo()
        {
            Tempo tempo = Tempo.Bpm(120); // 0.5 s per beat

            Assert.That(new Beat(3).ToSeconds(tempo), Is.EqualTo(1.5).Within(1e-9));
            Assert.That(Beat.FromSeconds(1.5, tempo).Position, Is.EqualTo(3.0).Within(1e-9));
        }

        [Test]
        public void ArithmeticAndOrdering()
        {
            Assert.That((new Beat(1.0) + 0.5).Position, Is.EqualTo(1.5).Within(1e-9));
            Assert.That((new Beat(2.0) - 0.5).Position, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(new Beat(1.0) < new Beat(1.5), Is.True);
            Assert.That(Beat.Zero.CompareTo(new Beat(1.0)), Is.LessThan(0));
        }

        [Test]
        public void Equality_IsByPosition()
        {
            Assert.That(new Beat(1.5), Is.EqualTo(new Beat(1.5)));
            Assert.That(new Beat(1.5) == new Beat(1.5), Is.True);
        }
    }
}
