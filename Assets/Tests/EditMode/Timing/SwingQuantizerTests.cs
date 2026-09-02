using System;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Timing
{
    public class SwingQuantizerTests
    {
        [Test]
        public void StraightRatio_IsIdentity()
        {
            var straight = SwingRatio.Straight;

            foreach (double beat in new[] { 0.0, 0.25, 0.5, 0.75, 1.0, 2.5, 3.999 })
            {
                Assert.That(SwingQuantizer.Swing(beat, straight), Is.EqualTo(beat).Within(1e-12));
            }
        }

        [Test]
        public void WholeBeats_AreFixedPoints_UnderAnySwing()
        {
            var swing = SwingRatio.Default;

            foreach (double beat in new[] { 0.0, 1.0, 2.0, 7.0 })
            {
                Assert.That(SwingQuantizer.Swing(beat, swing), Is.EqualTo(beat).Within(1e-12));
            }
        }

        [Test]
        public void OffBeatEighth_LandsAtTheSwingRatio()
        {
            // The "and" of beat 1 (straight 1.5) is delayed to 1 + ratio.
            var swing = new SwingRatio(0.66);

            Assert.That(SwingQuantizer.Swing(1.5, swing), Is.EqualTo(1.66).Within(1e-12));
        }

        [Test]
        public void Swing_IsMonotonic_AcrossTheBeat()
        {
            var swing = SwingRatio.Default;
            double previous = double.NegativeInfinity;

            for (double f = 0.0; f < 1.0; f += 0.01)
            {
                double swung = SwingQuantizer.Swing(f, swing);
                Assert.That(swung, Is.GreaterThan(previous));
                previous = swung;
            }
        }

        [Test]
        public void StraightenUndoesSwing()
        {
            var swing = new SwingRatio(0.7);

            for (double beat = 0.0; beat < 4.0; beat += 0.05)
            {
                double roundTrip = SwingQuantizer.Straighten(SwingQuantizer.Swing(beat, swing), swing);
                Assert.That(roundTrip, Is.EqualTo(beat).Within(1e-9));
            }
        }

        [Test]
        public void SwingToSeconds_UsesTheTempo()
        {
            var tempo = Tempo.Bpm(120); // 0.5 s per beat
            var swing = new SwingRatio(0.66);

            Assert.That(
                SwingQuantizer.SwingToSeconds(1.5, swing, tempo),
                Is.EqualTo(1.66 * 0.5).Within(1e-12));
        }

        [Test]
        public void RejectsAnInvalidRatio()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SwingRatio(0.49));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SwingRatio(1.0));
        }

        [Test]
        public void RejectsANonFiniteBeat()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SwingQuantizer.Swing(double.NaN, SwingRatio.Default));
        }
    }
}
