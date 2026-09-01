using System;
using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Music
{
    public class ChordToneSetTests
    {
        [Test]
        public void Set_HasTenTargets_FiveDegreesAcrossTwoOctaves()
        {
            ChordToneSet set = ChordToneSet.For(Chord.Cmaj7);

            Assert.That(set.Count, Is.EqualTo(10));
            Assert.That(ChordToneSet.TargetCount, Is.EqualTo(10));
            Assert.That(ChordToneSet.DegreesPerOctave, Is.EqualTo(5));
            Assert.That(ChordToneSet.OctaveCount, Is.EqualTo(2));
        }

        // The whole point of §3.1: a slot's degree is fixed; only its pitch is re-computed.
        [Test]
        public void SlotDegrees_AreStableAcrossEveryChord()
        {
            foreach (Chord chord in new[] { Chord.Dm7, Chord.G7, Chord.Cmaj7 })
            {
                ChordToneSet set = ChordToneSet.For(chord);
                for (int i = 0; i < ChordToneSet.TargetCount; i++)
                {
                    Assert.That(set[i].Index, Is.EqualTo(i));
                    Assert.That(set[i].Degree, Is.EqualTo(ChordToneSet.DegreeAt(i)), $"{chord} slot {i}");
                }
            }
        }

        [Test]
        public void Slot2_IsAlwaysTheFifth_EvenAsItsPitchChanges()
        {
            Assert.That(ChordToneSet.DegreeAt(2), Is.EqualTo(ScaleDegree.Fifth));

            Assert.That(ChordToneSet.For(Chord.Dm7)[2].Pitch.Class, Is.EqualTo(PitchClass.A));
            Assert.That(ChordToneSet.For(Chord.G7)[2].Pitch.Class, Is.EqualTo(PitchClass.D));
            Assert.That(ChordToneSet.For(Chord.Cmaj7)[2].Pitch.Class, Is.EqualTo(PitchClass.G));
        }

        [TestCase("Dm7", new[] { 74, 77, 81, 84, 88, 86, 89, 93, 96, 100 })]
        [TestCase("G7", new[] { 79, 83, 86, 89, 93, 91, 95, 98, 101, 105 })]
        [TestCase("Cmaj7", new[] { 72, 76, 79, 83, 86, 84, 88, 91, 95, 98 })]
        public void ExactTargetPitches_ForDefaultFloor72(string chordName, int[] expectedMidi)
        {
            ChordToneSet set = ChordToneSet.For(ChordByName(chordName));

            int[] actual = new int[10];
            for (int i = 0; i < 10; i++)
            {
                actual[i] = set[i].Pitch.Midi;
            }

            CollectionAssert.AreEqual(expectedMidi, actual);
        }

        [Test]
        public void LowestTarget_IsAtOrAboveTheFloor()
        {
            foreach (Chord chord in new[] { Chord.Dm7, Chord.G7, Chord.Cmaj7 })
            {
                ChordToneSet set = ChordToneSet.For(chord);
                foreach (ChordTarget target in set)
                {
                    Assert.That(
                        target.Pitch.Midi,
                        Is.GreaterThanOrEqualTo(ChordToneSet.DefaultLowestTargetFloorMidi),
                        $"{chord} {target}");
                }
            }
        }

        [Test]
        public void Targets_AreNotSortedByPitch()
        {
            // Dm7: the lower-octave 9th (E6, 88) sits above the upper-octave root (D6, 86).
            ChordToneSet set = ChordToneSet.For(Chord.Dm7);

            Assert.That(set[4].Degree, Is.EqualTo(ScaleDegree.Ninth));
            Assert.That(set[5].Degree, Is.EqualTo(ScaleDegree.Root));
            Assert.That(set[4].Pitch.Midi, Is.GreaterThan(set[5].Pitch.Midi));
        }

        [Test]
        public void UpperOctaveTarget_IsTwelveSemitonesAboveItsLowerTwin()
        {
            ChordToneSet set = ChordToneSet.For(Chord.G7);

            for (int d = 0; d < ChordToneSet.DegreesPerOctave; d++)
            {
                Assert.That(
                    set[d + ChordToneSet.DegreesPerOctave].Pitch.Midi - set[d].Pitch.Midi,
                    Is.EqualTo(12));
            }
        }

        [Test]
        public void ForDegree_ReturnsTheMatchingSlot()
        {
            ChordToneSet set = ChordToneSet.For(Chord.Cmaj7);

            ChordTarget lowerFifth = set.ForDegree(ScaleDegree.Fifth, 0);
            ChordTarget upperFifth = set.ForDegree(ScaleDegree.Fifth, 1);

            Assert.That(lowerFifth.Index, Is.EqualTo(2));
            Assert.That(upperFifth.Index, Is.EqualTo(7));
            Assert.That(lowerFifth.Pitch.Class, Is.EqualTo(PitchClass.G));
        }

        [Test]
        public void CustomFloor_MovesEveryTarget()
        {
            ChordToneSet high = ChordToneSet.For(Chord.Cmaj7, 84);

            Assert.That(high[0].Pitch.Midi, Is.EqualTo(84));
            Assert.That(high[0].Degree, Is.EqualTo(ScaleDegree.Root));
        }

        [Test]
        public void DefaultInstance_ThrowsRatherThanReturningGarbage()
        {
            ChordToneSet uninitialised = default;

            Assert.That(() => uninitialised[0], Throws.InvalidOperationException);
        }

        private static Chord ChordByName(string name) => name switch
        {
            "Dm7" => Chord.Dm7,
            "G7" => Chord.G7,
            "Cmaj7" => Chord.Cmaj7,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };
    }
}
