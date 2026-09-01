using Jazztures.Core.Music;
using Jazztures.Core.Sampling;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Sampling
{
    public class SampleFileNameTests
    {
        [TestCase("A0vL.wav", 21, VelocityLayer.Soft)]
        [TestCase("A0vH.wav", 21, VelocityLayer.Hard)]
        [TestCase("D#4vH.wav", 63, VelocityLayer.Hard)]
        [TestCase("F#7vL.wav", 102, VelocityLayer.Soft)]
        [TestCase("C8vH", 108, VelocityLayer.Hard)] // extension optional
        public void TryParse_SplitsPitchAndLayer(string fileName, int expectedMidi, VelocityLayer expectedLayer)
        {
            Assert.That(SampleFileName.TryParse(fileName, out Pitch root, out VelocityLayer layer), Is.True);
            Assert.That(root.Midi, Is.EqualTo(expectedMidi));
            Assert.That(layer, Is.EqualTo(expectedLayer));
        }

        [TestCase("A0.wav")]     // no layer suffix
        [TestCase("A0vX.wav")]   // unknown layer
        [TestCase("vH.wav")]     // no pitch
        [TestCase("")]
        [TestCase(null)]
        public void TryParse_RejectsAnythingElse(string? fileName)
        {
            Assert.That(SampleFileName.TryParse(fileName, out _, out _), Is.False);
        }

        [Test]
        public void EveryFileInTheSalamanderSet_Parses()
        {
            foreach (string fileName in SalamanderSampleSet.FileNames)
            {
                Assert.That(SampleFileName.TryParse(fileName, out _, out _), Is.True, fileName);
            }
        }
    }
}
