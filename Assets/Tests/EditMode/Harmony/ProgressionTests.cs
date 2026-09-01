using Jazztures.Core.Harmony;
using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Harmony
{
    public class ProgressionTests
    {
        [Test]
        public void ChordFor_MapsEachFunctionToItsThesisChord()
        {
            Assert.That(Progression.ChordFor(ChordFunction.Two), Is.EqualTo(Chord.Dm7));
            Assert.That(Progression.ChordFor(ChordFunction.Five), Is.EqualTo(Chord.G7));
            Assert.That(Progression.ChordFor(ChordFunction.One), Is.EqualTo(Chord.Cmaj7));
        }

        [Test]
        public void FunctionOf_IsTheInverseForTheThreeChords()
        {
            Assert.That(Progression.FunctionOf(Chord.Dm7), Is.EqualTo(ChordFunction.Two));
            Assert.That(Progression.FunctionOf(Chord.G7), Is.EqualTo(ChordFunction.Five));
            Assert.That(Progression.FunctionOf(Chord.Cmaj7), Is.EqualTo(ChordFunction.One));
        }

        [Test]
        public void FunctionOf_ReturnsNull_ForAChordOutsideTheProgression()
        {
            Assert.That(Progression.FunctionOf(new Chord(PitchClass.A, ChordQuality.Minor7)), Is.Null);
        }

        [Test]
        public void IiViOrder_IsTwoFiveOne_ForPromptingOnly()
        {
            CollectionAssert.AreEqual(
                new[] { ChordFunction.Two, ChordFunction.Five, ChordFunction.One },
                Progression.IiViOrder);
        }
    }
}
