using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Music
{
    public class ChordTests
    {
        [Test]
        public void ThreeChords_HaveThesisRootsAndQualities()
        {
            Assert.That(Chord.Dm7.Root, Is.EqualTo(PitchClass.D));
            Assert.That(Chord.Dm7.Quality, Is.EqualTo(ChordQuality.Minor7));

            Assert.That(Chord.G7.Root, Is.EqualTo(PitchClass.G));
            Assert.That(Chord.G7.Quality, Is.EqualTo(ChordQuality.Dominant7));

            Assert.That(Chord.Cmaj7.Root, Is.EqualTo(PitchClass.C));
            Assert.That(Chord.Cmaj7.Quality, Is.EqualTo(ChordQuality.Major7));
        }

        // CLAUDE.md §3.1 — right-hand chord tones (root, 3rd, 5th, 7th, 9th).
        [Test]
        public void Dm7_ChordToneClasses_MatchThesisTable()
        {
            AssertToneClasses(
                Chord.Dm7,
                PitchClass.D, PitchClass.F, PitchClass.A, PitchClass.C, PitchClass.E);
        }

        [Test]
        public void G7_ChordToneClasses_MatchThesisTable()
        {
            AssertToneClasses(
                Chord.G7,
                PitchClass.G, PitchClass.B, PitchClass.D, PitchClass.F, PitchClass.A);
        }

        [Test]
        public void Cmaj7_ChordToneClasses_MatchThesisTable()
        {
            AssertToneClasses(
                Chord.Cmaj7,
                PitchClass.C, PitchClass.E, PitchClass.G, PitchClass.B, PitchClass.D);
        }

        [Test]
        public void Ninth_IsACompoundInterval_Not_FoldedIntoTheOctave()
        {
            Assert.That(Chord.Cmaj7.SemitoneAbove(ScaleDegree.Ninth), Is.EqualTo(14));
        }

        [Test]
        public void Equality_IsByRootAndQuality()
        {
            Assert.That(new Chord(PitchClass.D, ChordQuality.Minor7), Is.EqualTo(Chord.Dm7));
            Assert.That(Chord.Dm7 == new Chord(PitchClass.D, ChordQuality.Minor7), Is.True);
            Assert.That(Chord.Dm7 != Chord.G7, Is.True);
        }

        [Test]
        public void ToString_IsLeadSheetShorthand()
        {
            Assert.That(Chord.Dm7.ToString(), Is.EqualTo("Dm7"));
            Assert.That(Chord.G7.ToString(), Is.EqualTo("G7"));
            Assert.That(Chord.Cmaj7.ToString(), Is.EqualTo("Cmaj7"));
        }

        private static void AssertToneClasses(
            Chord chord,
            PitchClass root,
            PitchClass third,
            PitchClass fifth,
            PitchClass seventh,
            PitchClass ninth)
        {
            Assert.That(chord.ClassOf(ScaleDegree.Root), Is.EqualTo(root));
            Assert.That(chord.ClassOf(ScaleDegree.Third), Is.EqualTo(third));
            Assert.That(chord.ClassOf(ScaleDegree.Fifth), Is.EqualTo(fifth));
            Assert.That(chord.ClassOf(ScaleDegree.Seventh), Is.EqualTo(seventh));
            Assert.That(chord.ClassOf(ScaleDegree.Ninth), Is.EqualTo(ninth));
        }
    }
}
