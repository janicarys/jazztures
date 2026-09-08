using System;
using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class TargetVolumeTests
    {
        // 3.5 cm radius face, ±7.5 cm depth — the ADR-0018 defaults.
        private static TargetVolume New(float radius = 0.035f, float halfDepth = 0.075f) =>
            new TargetVolume(radius, halfDepth);

        [Test]
        public void Constructor_RejectsNonPositiveDimensions()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TargetVolume(0f, 0.05f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TargetVolume(0.05f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TargetVolume(-0.01f, 0.05f));
        }

        [Test]
        public void Centre_IsContained()
        {
            Assert.That(New().Contains(0f, 0f, 0f), Is.True);
        }

        [Test]
        public void OutsideTheRadialFace_IsNotContained()
        {
            TargetVolume v = New();
            Assert.That(v.Contains(0.04f, 0f, 0f), Is.False);
            Assert.That(v.Contains(0f, 0.04f, 0f), Is.False);
            // 0.025 + 0.025 in quadrature clears 0.035.
            Assert.That(v.Contains(0.025f, 0.025f, 0f), Is.False);
        }

        [Test]
        public void InsideTheRadialFace_IsContained()
        {
            Assert.That(New().Contains(0.02f, 0.02f, 0f), Is.True);
        }

        [Test]
        public void BeyondTheDepthBand_IsNotContained_EitherDirection()
        {
            TargetVolume v = New();
            Assert.That(v.Contains(0f, 0f, 0.1f), Is.False);
            Assert.That(v.Contains(0f, 0f, -0.1f), Is.False);
        }

        [Test]
        public void WithinTheDepthBand_IsContained_EitherDirection()
        {
            TargetVolume v = New();
            Assert.That(v.Contains(0f, 0f, 0.05f), Is.True);
            Assert.That(v.Contains(0f, 0f, -0.05f), Is.True);
        }

        [Test]
        public void Boundaries_AreInclusive()
        {
            TargetVolume v = New(0.03f, 0.06f);
            Assert.That(v.Contains(0.03f, 0f, 0f), Is.True, "exactly on the radial edge");
            Assert.That(v.Contains(0f, 0f, 0.06f), Is.True, "exactly on the depth edge");
        }

        /// <summary>
        /// The case ADR-0018 exists for: dead-on in XY, well off in Z. A sphere would
        /// reject this; the cylinder accepts it, because depth is the axis nobody can aim.
        /// </summary>
        [Test]
        public void DeadOnLaterally_ButDeepInZ_IsContained()
        {
            TargetVolume v = New();
            Assert.That(v.Contains(0f, 0f, 0.07f), Is.True);
            // ...and a sphere of the same radius would not have been:
            Assert.That(0.07f, Is.GreaterThan(v.Radius));
        }
    }
}
