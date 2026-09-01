using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Music
{
    public class ScaleTests
    {
        [Test]
        public void CIonian_ContainsTheNaturals_AndExcludesTheAccidentals()
        {
            Scale c = Scale.CIonian;

            foreach (PitchClass natural in new[]
            {
                PitchClass.C, PitchClass.D, PitchClass.E, PitchClass.F,
                PitchClass.G, PitchClass.A, PitchClass.B,
            })
            {
                Assert.That(c.Contains(natural), Is.True, natural.ToString());
            }

            foreach (PitchClass accidental in new[]
            {
                PitchClass.CSharp, PitchClass.DSharp, PitchClass.FSharp,
                PitchClass.GSharp, PitchClass.ASharp,
            })
            {
                Assert.That(c.Contains(accidental), Is.False, accidental.ToString());
            }
        }

        [Test]
        public void Contains_AcceptsAPitch_IgnoringOctave()
        {
            Assert.That(Scale.CIonian.Contains(new Pitch(60)), Is.True);   // C4
            Assert.That(Scale.CIonian.Contains(new Pitch(61)), Is.False);  // C#4
            Assert.That(Scale.CIonian.Contains(new Pitch(83)), Is.True);   // B5
        }

        [Test]
        public void Major_TransposesThePattern()
        {
            Scale g = Scale.Major(PitchClass.G);

            Assert.That(g.Contains(PitchClass.FSharp), Is.True);
            Assert.That(g.Contains(PitchClass.F), Is.False);
        }

        [Test]
        public void PitchClasses_EnumeratesSevenNotes_AscendingFromC()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    PitchClass.C, PitchClass.D, PitchClass.E, PitchClass.F,
                    PitchClass.G, PitchClass.A, PitchClass.B,
                },
                Scale.CIonian.PitchClasses);
        }

        [Test]
        public void Equality_IsByTonicAndPitchContent()
        {
            Assert.That(Scale.Major(PitchClass.C), Is.EqualTo(Scale.CIonian));
            Assert.That(Scale.Major(PitchClass.C) == Scale.CIonian, Is.True);
            Assert.That(Scale.Major(PitchClass.G) != Scale.CIonian, Is.True);
        }
    }
}
