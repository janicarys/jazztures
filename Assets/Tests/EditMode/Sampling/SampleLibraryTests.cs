using System;
using System.Collections.Generic;
using Jazztures.Core.Music;
using Jazztures.Core.Sampling;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Sampling
{
    public class SampleLibraryTests
    {
        private static SampleLibrary TwoRoots() => new SampleLibrary(new[]
        {
            new SampleEntry(new Pitch(60), VelocityLayer.Soft, 0), // C4
            new SampleEntry(new Pitch(60), VelocityLayer.Hard, 1),
            new SampleEntry(new Pitch(72), VelocityLayer.Soft, 2), // C5
            new SampleEntry(new Pitch(72), VelocityLayer.Hard, 3),
        });

        [Test]
        public void ExactRoot_PlaysAtRateOne()
        {
            SampleSelection sel = TwoRoots().Resolve(new Pitch(60), velocity: 100);

            Assert.That(sel.PlaybackRate, Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void OneSemitoneUp_PlaysSlightlyFaster()
        {
            SampleSelection sel = TwoRoots().Resolve(new Pitch(61), velocity: 100);

            Assert.That(sel.ClipIndex, Is.EqualTo(1), "nearest root C4, Hard layer");
            Assert.That(sel.PlaybackRate, Is.EqualTo(Math.Pow(2.0, 1.0 / 12.0)).Within(1e-12));
        }

        [Test]
        public void OneSemitoneDown_PlaysSlightlySlower()
        {
            SampleSelection sel = TwoRoots().Resolve(new Pitch(71), velocity: 20);

            Assert.That(sel.ClipIndex, Is.EqualTo(2), "nearest root C5, Soft layer");
            Assert.That(sel.PlaybackRate, Is.EqualTo(Math.Pow(2.0, -1.0 / 12.0)).Within(1e-12));
        }

        [Test]
        public void EquidistantTargets_PickTheLowerRoot()
        {
            SampleSelection sel = TwoRoots().Resolve(new Pitch(66), velocity: 100); // 6 from C4, 6 from C5

            Assert.That(sel.PlaybackRate, Is.EqualTo(Math.Pow(2.0, 6.0 / 12.0)).Within(1e-12), "shifted up from C4");
        }

        [Test]
        public void VelocitySplit_ChoosesTheLayer()
        {
            SampleLibrary lib = TwoRoots();

            Assert.That(lib.Resolve(new Pitch(60), SampleLibrary.DefaultLayerSplitVelocity).ClipIndex, Is.EqualTo(1));
            Assert.That(lib.Resolve(new Pitch(60), (byte)(SampleLibrary.DefaultLayerSplitVelocity - 1)).ClipIndex, Is.EqualTo(0));
        }

        [Test]
        public void MissingPreferredLayer_FallsBackToTheOther()
        {
            var lib = new SampleLibrary(new[]
            {
                new SampleEntry(new Pitch(60), VelocityLayer.Soft, 0), // only Soft at C4
            });

            SampleSelection loud = lib.Resolve(new Pitch(60), velocity: 127);

            Assert.That(loud.ClipIndex, Is.EqualTo(0));
        }

        [Test]
        public void Constructor_RejectsAnEmptySet()
        {
            Assert.That(() => new SampleLibrary(Array.Empty<SampleEntry>()), Throws.ArgumentException);
        }

        // --- against the real Salamander file list ---

        private static SampleLibrary Salamander()
        {
            var index = new Dictionary<string, int>();
            for (int i = 0; i < SalamanderSampleSet.FileNames.Length; i++)
            {
                index[SalamanderSampleSet.FileNames[i]] = i;
            }

            return SampleLibrary.FromFileNames(SalamanderSampleSet.FileNames, name => index[name]);
        }

        [Test]
        public void SalamanderSet_ParsesAll66Entries()
        {
            Assert.That(Salamander().Count, Is.EqualTo(66));
        }

        [Test]
        public void SalamanderSet_CoversThePianoRange()
        {
            SampleLibrary lib = Salamander();

            Assert.That(lib.LowestRoot.Midi, Is.EqualTo(21));  // A0
            Assert.That(lib.HighestRoot.Midi, Is.EqualTo(108)); // C8
        }

        [Test]
        public void SalamanderSet_NeverPitchShiftsMoreThanTwoSemitones()
        {
            SampleLibrary lib = Salamander();

            for (int midi = 21; midi <= 108; midi++)
            {
                double rate = lib.Resolve(new Pitch(midi), velocity: 90).PlaybackRate;
                double semitones = Math.Abs(12.0 * Math.Log(rate, 2.0));

                Assert.That(semitones, Is.LessThanOrEqualTo(2.0 + 1e-9), $"MIDI {midi}");
            }
        }

        [TestCase(60, 90, 1.0)]   // C4 recorded (vL only -> falls back), rate 1
        [TestCase(69, 90, 1.0)]   // A4 recorded
        [TestCase(108, 90, 1.0)]  // C8 recorded
        public void SalamanderSet_RecordedPitchesPlayUnshifted(int midi, int velocity, double expectedRate)
        {
            Assert.That(
                Salamander().Resolve(new Pitch(midi), (byte)velocity).PlaybackRate,
                Is.EqualTo(expectedRate).Within(1e-12));
        }

        [Test]
        public void SalamanderSet_C4RequestsFallBackToItsOnlyLayer()
        {
            // C4 exists as vL only; a loud C4 must still resolve to that clip.
            int expected = System.Array.IndexOf(SalamanderSampleSet.FileNames, "C4vL.wav");

            Assert.That(Salamander().Resolve(new Pitch(60), velocity: 127).ClipIndex, Is.EqualTo(expected));
        }
    }
}
