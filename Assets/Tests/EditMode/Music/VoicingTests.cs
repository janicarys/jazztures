using System;
using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Music
{
    public class VoicingTests
    {
        // Close voicings for the three chords with the root in MIDI 48..60 (§3.1).
        // Reference values cross-checked against the deleted prototype (DECISIONS ADR-0001).
        [Test]
        public void Dm7_CloseVoicing()
        {
            AssertVoicing(Voicing.Close(Chord.Dm7), 50, 53, 57, 60);
        }

        [Test]
        public void G7_CloseVoicing()
        {
            AssertVoicing(Voicing.Close(Chord.G7), 55, 59, 62, 65);
        }

        [Test]
        public void Cmaj7_CloseVoicing()
        {
            AssertVoicing(Voicing.Close(Chord.Cmaj7), 48, 52, 55, 59);
        }

        [Test]
        public void CloseVoicing_TonesAscend_WithinAnOctave()
        {
            foreach (Chord chord in new[] { Chord.Dm7, Chord.G7, Chord.Cmaj7 })
            {
                ChordVoicing v = Voicing.Close(chord);

                Assert.That(v.Root < v.Third, $"{chord}: root < third");
                Assert.That(v.Third < v.Fifth, $"{chord}: third < fifth");
                Assert.That(v.Fifth < v.Seventh, $"{chord}: fifth < seventh");
                Assert.That(v.Highest.Midi - v.Lowest.Midi, Is.LessThan(12), $"{chord}: spans < octave");
            }
        }

        [Test]
        public void CloseVoicing_RootIsWithinTheConfiguredRegister()
        {
            foreach (Chord chord in new[] { Chord.Dm7, Chord.G7, Chord.Cmaj7 })
            {
                ChordVoicing v = Voicing.Close(chord);

                Assert.That(v.Root.Midi, Is.InRange(
                    Voicing.DefaultRootFloorMidi, Voicing.DefaultRootCeilingMidi));
                Assert.That(v.Root.Class, Is.EqualTo(chord.Root));
            }
        }

        [Test]
        public void CloseVoicing_Lowest_IsTheRoot()
        {
            ChordVoicing v = Voicing.Close(Chord.G7);

            Assert.That(v.Lowest, Is.EqualTo(v.Root));
            Assert.That(v.Highest, Is.EqualTo(v.Seventh));
        }

        [Test]
        public void CloseVoicing_EnumeratesRootThirdFifthSeventh()
        {
            CollectionAssert.AreEqual(
                new[] { new Pitch(48), new Pitch(52), new Pitch(55), new Pitch(59) },
                Voicing.Close(Chord.Cmaj7));
        }

        [Test]
        public void CloseVoicing_TooNarrowRegister_Throws()
        {
            // No D exists in MIDI 48..49.
            Assert.That(
                () => Voicing.Close(Chord.Dm7, 48, 49),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void CloseVoicing_InvertedRegister_Throws()
        {
            Assert.That(() => Voicing.Close(Chord.Dm7, 60, 48), Throws.ArgumentException);
        }

        private static void AssertVoicing(ChordVoicing v, int root, int third, int fifth, int seventh)
        {
            Assert.That(v.Root.Midi, Is.EqualTo(root), "root");
            Assert.That(v.Third.Midi, Is.EqualTo(third), "third");
            Assert.That(v.Fifth.Midi, Is.EqualTo(fifth), "fifth");
            Assert.That(v.Seventh.Midi, Is.EqualTo(seventh), "seventh");
        }
    }
}
