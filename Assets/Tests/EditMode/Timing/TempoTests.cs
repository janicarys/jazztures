using System;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Timing
{
    public class TempoTests
    {
        [Test]
        public void Default_Is80Bpm()
        {
            Assert.That(Tempo.Default.BeatsPerMinute, Is.EqualTo(80.0));
        }

        [Test]
        public void SecondsPerBeat_At120Bpm_IsHalfASecond()
        {
            Assert.That(Tempo.Bpm(120).SecondsPerBeat, Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void ConversionsRoundTrip()
        {
            Tempo tempo = Tempo.Bpm(93.7);

            Assert.That(tempo.SecondsToBeats(tempo.BeatsToSeconds(4.0)), Is.EqualTo(4.0).Within(1e-9));
        }

        [TestCase(0.0)]
        [TestCase(-60.0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void Constructor_RejectsNonPositiveOrNonFinite(double bpm)
        {
            Assert.That(() => new Tempo(bpm), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Equality_IsByBpm()
        {
            Assert.That(Tempo.Bpm(80), Is.EqualTo(Tempo.Default));
            Assert.That(Tempo.Bpm(80) == Tempo.Default, Is.True);
            Assert.That(Tempo.Bpm(81) != Tempo.Default, Is.True);
        }
    }
}
