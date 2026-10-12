using System;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Timing
{
    public class OnsetPatternTests
    {
        [Test]
        public void None_ProducesNothing()
        {
            Assert.That(OnsetPattern.None.IsNone, Is.True);
            Assert.That(OnsetPattern.None.BeatsBetween(0.0, 8.0, 4), Is.Empty);
        }

        [Test]
        public void EveryBeat_IsOneOnsetPerBeat_AndExcludesTheEnd()
        {
            double[] beats = OnsetPattern.EveryBeat.BeatsBetween(0.0, 4.0, 4);

            Assert.That(beats, Is.EqualTo(new[] { 0.0, 1.0, 2.0, 3.0 }));
        }

        [Test]
        public void EverySecondBeat_SkipsAlternateBeats()
        {
            double[] beats = OnsetPattern.EverySecondBeat.BeatsBetween(0.0, 8.0, 4);

            Assert.That(beats, Is.EqualTo(new[] { 0.0, 2.0, 4.0, 6.0 }));
        }

        [Test]
        public void EveryEighth_HitsTheOffBeatsToo()
        {
            double[] beats = OnsetPattern.EveryEighth.BeatsBetween(0.0, 2.0, 4);

            Assert.That(beats, Is.EqualTo(new[] { 0.0, 0.5, 1.0, 1.5 }));
        }

        [Test]
        public void AnIntervalPattern_StartsFromTheStartOfTheSpan()
        {
            // The scored region of a drill begins after its count-in.
            double[] beats = OnsetPattern.EveryBeat.BeatsBetween(4.0, 8.0, 4);

            Assert.That(beats, Is.EqualTo(new[] { 4.0, 5.0, 6.0, 7.0 }));
        }

        [Test]
        public void AnIntervalPattern_DoesNotDriftOverManyBeats()
        {
            // A third of a beat is not exactly representable; multiplying, not adding, keeps
            // the 3000th onset where it belongs.
            double[] beats = OnsetPattern.Every(1.0 / 3.0).BeatsBetween(0.0, 1000.0, 4);

            Assert.That(beats.Length, Is.EqualTo(3000));
            Assert.That(beats[2999], Is.EqualTo(2999.0 / 3.0).Within(1e-9));
        }

        [Test]
        public void PerBar_RepeatsTheSamePositionsInEveryBar()
        {
            // Comping on beats 1 and 3.
            OnsetPattern pattern = OnsetPattern.PerBar(new[] { 0.0, 2.0 });

            double[] beats = pattern.BeatsBetween(4.0, 12.0, 4); // two bars after a one-bar count-in

            Assert.That(beats, Is.EqualTo(new[] { 4.0, 6.0, 8.0, 10.0 }));
        }

        [Test]
        public void PerBar_AcceptsPositionsInAnyOrder_AndReadsThemBackSorted()
        {
            OnsetPattern pattern = OnsetPattern.PerBar(new[] { 2.5, 0.0, 1.0 });

            Assert.That(pattern.BeatsBetween(0.0, 4.0, 4), Is.EqualTo(new[] { 0.0, 1.0, 2.5 }));
        }

        [Test]
        public void PerBar_CutsAPartialLastBarAtTheEnd()
        {
            OnsetPattern pattern = OnsetPattern.PerBar(new[] { 0.0, 3.0 });

            double[] beats = pattern.BeatsBetween(0.0, 6.0, 4); // bar 2 is half there

            Assert.That(beats, Is.EqualTo(new[] { 0.0, 3.0, 4.0 }));
        }

        [Test]
        public void PerBar_RejectsAPositionOutsideTheBar_WhenTheBarLengthIsKnown()
        {
            OnsetPattern pattern = OnsetPattern.PerBar(new[] { 0.0, 4.0 });

            Assert.Throws<ArgumentOutOfRangeException>(() => pattern.BeatsBetween(0.0, 8.0, 4));
        }

        [Test]
        public void PerBar_RejectsEmptyDuplicateAndNegativePositions()
        {
            Assert.Throws<ArgumentException>(() => OnsetPattern.PerBar(new double[0]));
            Assert.Throws<ArgumentException>(() => OnsetPattern.PerBar(new[] { 1.0, 1.0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => OnsetPattern.PerBar(new[] { -0.5 }));
        }

        [TestCase(0.0)]
        [TestCase(-1.0)]
        [TestCase(double.NaN)]
        public void Every_RejectsANonPositiveInterval(double beats)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => OnsetPattern.Every(beats));
        }

        [Test]
        public void ASpanThatEndsBeforeItStarts_IsEmpty()
        {
            Assert.That(OnsetPattern.EveryBeat.BeatsBetween(8.0, 4.0, 4), Is.Empty);
        }

        [Test]
        public void ADefaultStruct_IsNone()
        {
            Assert.That(default(OnsetPattern).IsNone, Is.True);
        }
    }
}
