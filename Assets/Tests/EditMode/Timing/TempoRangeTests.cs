using System;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Timing
{
    public class TempoRangeTests
    {
        [Test]
        public void TheDefaultTempo_IsInsideTheValidatedRange()
        {
            Assert.That(TempoRange.IsValidated(Tempo.Default.BeatsPerMinute), Is.True);
        }

        [TestCase(40.0, 40.0)]
        [TestCase(80.0, 80.0)]
        [TestCase(100.0, 100.0)]
        public void ATempoInsideTheRange_IsLeftAlone(double bpm, double expected)
        {
            Assert.That(TempoRange.Clamp(bpm, allowUnvalidated: false), Is.EqualTo(expected));
        }

        [Test]
        public void BelowTheFloor_ClampsUp_WithOrWithoutTheFlag()
        {
            Assert.That(TempoRange.Clamp(10.0, false), Is.EqualTo(TempoRange.MinBpm));
            Assert.That(TempoRange.Clamp(10.0, true), Is.EqualTo(TempoRange.MinBpm));
        }

        [Test]
        public void AboveTheValidatedMax_ClampsDown_UnlessUnvalidatedTemposAreAllowed()
        {
            Assert.That(TempoRange.Clamp(140.0, false), Is.EqualTo(TempoRange.ValidatedMaxBpm));
            Assert.That(TempoRange.Clamp(140.0, true), Is.EqualTo(140.0));
        }

        [Test]
        public void EvenWithTheFlag_ThereIsAnAbsoluteCeiling()
        {
            Assert.That(TempoRange.Clamp(9000.0, true), Is.EqualTo(TempoRange.AbsoluteMaxBpm));
        }

        [Test]
        public void IsValidated_IsFalseOutsideTheRange()
        {
            Assert.That(TempoRange.IsValidated(39.9), Is.False);
            Assert.That(TempoRange.IsValidated(100.1), Is.False);
        }

        [Test]
        public void ClampToTempo_WrapsTheClampedValue()
        {
            Assert.That(TempoRange.ClampToTempo(150.0, false), Is.EqualTo(Tempo.Bpm(TempoRange.ValidatedMaxBpm)));
        }

        [Test]
        public void ANaNTempo_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TempoRange.Clamp(double.NaN, false));
        }
    }
}
