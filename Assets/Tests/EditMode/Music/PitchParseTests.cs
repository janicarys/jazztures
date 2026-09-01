using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Music
{
    public class PitchParseTests
    {
        [TestCase("C4", 60)]
        [TestCase("C-1", 0)]
        [TestCase("A0", 21)]   // lowest piano key
        [TestCase("C8", 108)]  // highest piano key
        [TestCase("A4", 69)]   // A440
        [TestCase("D#4", 63)]
        [TestCase("F#7", 102)]
        [TestCase("Db4", 61)]  // flats accepted on input
        [TestCase("B#3", 60)]  // enharmonic
        [TestCase("g5", 79)]   // lower-case letter
        public void TryParse_AcceptsScientificPitchNotation(string text, int expectedMidi)
        {
            Assert.That(Pitch.TryParse(text, out Pitch pitch), Is.True);
            Assert.That(pitch.Midi, Is.EqualTo(expectedMidi));
        }

        [TestCase("")]
        [TestCase("H4")]
        [TestCase("C")]
        [TestCase("C4x")]
        [TestCase("#4")]
        [TestCase("C99")]   // out of MIDI range
        [TestCase(null)]
        public void TryParse_RejectsMalformedOrOutOfRange(string? text)
        {
            Assert.That(Pitch.TryParse(text, out Pitch pitch), Is.False);
            Assert.That(pitch, Is.EqualTo(default(Pitch)));
        }

        [Test]
        public void ToStringAndTryParse_RoundTripEveryMidiNote()
        {
            for (int midi = Pitch.MinMidi; midi <= Pitch.MaxMidi; midi++)
            {
                var original = new Pitch(midi);

                Assert.That(Pitch.TryParse(original.ToString(), out Pitch parsed), Is.True, original.ToString());
                Assert.That(parsed, Is.EqualTo(original));
            }
        }
    }
}
