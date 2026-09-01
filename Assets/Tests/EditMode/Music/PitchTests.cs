using System;
using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Music
{
    public class PitchTests
    {
        [Test]
        public void MiddleC_IsMidi60_C4()
        {
            Pitch c = Pitch.MiddleC;

            Assert.That(c.Midi, Is.EqualTo(60));
            Assert.That(c.Class, Is.EqualTo(PitchClass.C));
            Assert.That(c.Octave, Is.EqualTo(4));
        }

        [TestCase(0, PitchClass.C, -1)]
        [TestCase(60, PitchClass.C, 4)]
        [TestCase(69, PitchClass.A, 4)]
        [TestCase(71, PitchClass.B, 4)]
        [TestCase(72, PitchClass.C, 5)]
        [TestCase(127, PitchClass.G, 9)]
        public void ClassAndOctave_AreDerivedFromMidi(int midi, PitchClass expectedClass, int expectedOctave)
        {
            var pitch = new Pitch(midi);

            Assert.That(pitch.Class, Is.EqualTo(expectedClass));
            Assert.That(pitch.Octave, Is.EqualTo(expectedOctave));
        }

        [TestCase(-1)]
        [TestCase(128)]
        public void Constructor_RejectsOutOfRangeMidi(int midi)
        {
            Assert.That(() => new Pitch(midi), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Transpose_MovesBySemitones()
        {
            Assert.That(Pitch.MiddleC.Transpose(12), Is.EqualTo(new Pitch(72)));
            Assert.That(Pitch.MiddleC.Transpose(-1), Is.EqualTo(new Pitch(59)));
        }

        [Test]
        public void Transpose_OutOfRange_Throws()
        {
            Assert.That(() => new Pitch(127).Transpose(1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TryTranspose_OutOfRange_ReturnsFalse()
        {
            bool ok = new Pitch(2).TryTranspose(-5, out Pitch result);

            Assert.That(ok, Is.False);
            Assert.That(result, Is.EqualTo(default(Pitch)));
        }

        [TestCase(PitchClass.C, 72, 72)]
        [TestCase(PitchClass.D, 72, 74)]
        [TestCase(PitchClass.G, 72, 79)]
        [TestCase(PitchClass.C, 60, 60)]
        [TestCase(PitchClass.B, 60, 71)]
        public void LowestAtOrAbove_FindsFirstMatchingPitchClass(PitchClass pc, int floor, int expectedMidi)
        {
            Assert.That(Pitch.LowestAtOrAbove(pc, floor).Midi, Is.EqualTo(expectedMidi));
        }

        [Test]
        public void Equality_IsByMidi()
        {
            Assert.That(new Pitch(60), Is.EqualTo(Pitch.MiddleC));
            Assert.That(new Pitch(60) == Pitch.MiddleC, Is.True);
            Assert.That(new Pitch(61) != Pitch.MiddleC, Is.True);
        }

        [Test]
        public void Ordering_IsByMidi()
        {
            Assert.That(new Pitch(60) < new Pitch(61), Is.True);
            Assert.That(new Pitch(72) > new Pitch(60), Is.True);
            Assert.That(new Pitch(60).CompareTo(new Pitch(72)), Is.LessThan(0));
        }

        [TestCase(60, "C4")]
        [TestCase(61, "C#4")]
        [TestCase(59, "B3")]
        [TestCase(72, "C5")]
        public void ToString_IsScientificPitchNotation(int midi, string expected)
        {
            Assert.That(new Pitch(midi).ToString(), Is.EqualTo(expected));
        }
    }
}
