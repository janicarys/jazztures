using System.Linq;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class PitchSetTests
    {
        private static int[] Midis(PitchSet set) => Enumerable.Range(0, set.Count).Select(i => set[i].Midi).ToArray();

        // Range 72..90 (an octave and a half above C5).
        [Test]
        public void ChordTones_Dm7() =>
            Assert.That(Midis(PitchSet.ChordTones(Chord.Dm7, 72, 18)), Is.EqualTo(new[] { 72, 74, 76, 77, 81, 84, 86, 88, 89 }));

        [Test]
        public void ChordTones_G7() =>
            Assert.That(Midis(PitchSet.ChordTones(Chord.G7, 72, 18)), Is.EqualTo(new[] { 74, 77, 79, 81, 83, 86, 89 }));

        [Test]
        public void ChordTones_Cmaj7() =>
            Assert.That(Midis(PitchSet.ChordTones(Chord.Cmaj7, 72, 18)), Is.EqualTo(new[] { 72, 74, 76, 79, 83, 84, 86, 88 }));

        [Test]
        public void CMajorScale_IsTheSameForEveryChord()
        {
            int[] expected = { 72, 74, 76, 77, 79, 81, 83, 84, 86, 88, 89 };

            Assert.That(Midis(PitchSet.CMajor(Chord.Dm7, 72, 18)), Is.EqualTo(expected));
            Assert.That(Midis(PitchSet.CMajor(Chord.G7, 72, 18)), Is.EqualTo(expected));
        }

        [Test]
        public void EverySetIsAscending_AndInsideTheRange()
        {
            foreach (PitchSetKind kind in new[] { PitchSetKind.ChordTones, PitchSetKind.CMajorScale })
            {
                foreach (Chord chord in new[] { Chord.Dm7, Chord.G7, Chord.Cmaj7 })
                {
                    int[] midis = Midis(PitchSet.Build(kind, chord, 72, 18));
                    Assert.That(midis, Is.Ordered.Ascending);
                    Assert.That(midis.First(), Is.GreaterThanOrEqualTo(72));
                    Assert.That(midis.Last(), Is.LessThanOrEqualTo(90));
                    Assert.That(midis.Length, Is.InRange(7, 11));
                }
            }
        }

        [Test]
        public void IndexOfNearest_PicksTheLowerOfTwoEquallyNear()
        {
            PitchSet dm7 = PitchSet.ChordTones(Chord.Dm7, 72, 18); // ... 77, 81 ...

            Assert.That(dm7[dm7.IndexOfNearest(79)].Midi, Is.EqualTo(77));
            Assert.That(dm7[dm7.IndexOfNearest(100)].Midi, Is.EqualTo(89));
            Assert.That(dm7[dm7.IndexOfNearest(0)].Midi, Is.EqualTo(72));
        }

        [Test]
        public void RootNearMiddle_IsTheRootClosestToTheCentreOfTheRange()
        {
            // Range 72..90, middle 81. Dm7's roots: D74, D86 -> 74 is 7 away, 86 is 5 away.
            PitchSet dm7 = PitchSet.ChordTones(Chord.Dm7, 72, 18);
            Assert.That(dm7[dm7.IndexOfRootNearMiddle()].Midi, Is.EqualTo(86));

            // G7: G79 only (G91 is out of range).
            PitchSet g7 = PitchSet.ChordTones(Chord.G7, 72, 18);
            Assert.That(g7[g7.IndexOfRootNearMiddle()].Midi, Is.EqualTo(79));
        }

        [Test]
        public void ARangeWithNoMember_IsRejected()
        {
            // F5 and F#5 are not chord tones of Cmaj7.
            Assert.Throws<System.ArgumentException>(() => PitchSet.ChordTones(Chord.Cmaj7, 77, 1));
        }
    }
}
