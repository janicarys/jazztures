using System;
using Jazztures.Core.Gesture;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class StrikePlateVolumeTests
    {
        // The scene's plate: a 20 x 20 cm slab, 1 cm thick, centred on the origin, so its top face is at z = +0.005.
        private static StrikePlateVolume Scene() =>
            StrikePlateVolume.AroundSurface(
                visualMinX: -0.10f, visualMaxX: 0.10f, visualMinY: -0.10f, visualMaxY: 0.10f, surfaceZ: 0.005f,
                footprintMargin: 0.02f, hoverAboveSurface: 0.02f, depthBelowSurface: 0.10f);

        [Test]
        public void AHandResting_OnTheVisualsSurface_IsInside()
        {
            Assert.That(Scene().Contains(0f, 0f, 0.005f), Is.True);
        }

        [Test]
        public void AHandJustAboveTheSurface_WithinTheHoverTolerance_IsInside()
        {
            Assert.That(Scene().Contains(0f, 0f, 0.005f + 0.015f), Is.True);
        }

        [Test]
        public void AHandSeveralCentimetresInTheAir_IsNotInside_TheReportedBug()
        {
            // The old cylinder reached 8 cm above the plate's centre: a chord fired with the
            // hand a hand's-width above a slab that is only 1 cm thick.
            Assert.That(Scene().Contains(0f, 0f, 0.08f), Is.False);
            Assert.That(Scene().Contains(0f, 0f, 0.05f), Is.False);
        }

        [Test]
        public void JustPastTheHoverTolerance_IsNotInside()
        {
            Assert.That(Scene().Contains(0f, 0f, 0.005f + 0.021f), Is.False);
        }

        [Test]
        public void AHandThatOvershootsBelowTheVisual_StillCounts()
        {
            Assert.That(Scene().Contains(0f, 0f, 0.005f - 0.08f), Is.True, "depth cannot be judged mid-air");
        }

        [Test]
        public void FarBelowTheVisual_IsOutside_SoTheVolumeIsNotABottomlessPit()
        {
            Assert.That(Scene().Contains(0f, 0f, 0.005f - 0.11f), Is.False);
        }

        [Test]
        public void TheWholeVisibleSquare_IsHittable_IncludingItsCorners()
        {
            // The old 6 cm-radius cylinder missed the outer 4 cm of the 20 cm slab, corners included.
            StrikePlateVolume v = Scene();

            Assert.That(v.Contains(0.09f, 0f, 0.005f), Is.True, "edge, beyond the old 6 cm radius");
            Assert.That(v.Contains(0.095f, 0.095f, 0.005f), Is.True, "corner");
            Assert.That(v.Contains(-0.095f, -0.095f, 0.005f), Is.True, "opposite corner");
        }

        [Test]
        public void TheMargin_AddsReachBeyondTheVisualsEdge_ButNotMuch()
        {
            StrikePlateVolume v = Scene();

            Assert.That(v.Contains(0.115f, 0f, 0.005f), Is.True, "within the 2 cm margin");
            Assert.That(v.Contains(0.125f, 0f, 0.005f), Is.False, "beyond it");
        }

        [Test]
        public void AnOffCentreVisual_IsFollowedByTheVolume()
        {
            StrikePlateVolume v = StrikePlateVolume.AroundSurface(
                0.10f, 0.30f, -0.05f, 0.05f, 0f, footprintMargin: 0f, hoverAboveSurface: 0.02f, depthBelowSurface: 0.1f);

            Assert.That(v.Contains(0.2f, 0f, 0f), Is.True);
            Assert.That(v.Contains(0f, 0f, 0f), Is.False, "the origin is not under this visual");
        }

        [Test]
        public void TheBoundaries_AreInclusive()
        {
            var v = new StrikePlateVolume(-1f, 1f, -1f, 1f, -1f, 1f);

            Assert.That(v.Contains(1f, 1f, 1f), Is.True);
            Assert.That(v.Contains(-1f, -1f, -1f), Is.True);
        }

        [Test]
        public void ADegenerateBox_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StrikePlateVolume(1f, 1f, 0f, 1f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StrikePlateVolume(0f, 1f, 1f, 0f, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StrikePlateVolume(0f, 1f, 0f, 1f, 1f, 1f));
        }

        [Test]
        public void NegativeMarginsOrNoDepth_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                StrikePlateVolume.AroundSurface(-1f, 1f, -1f, 1f, 0f, -0.01f, 0.02f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                StrikePlateVolume.AroundSurface(-1f, 1f, -1f, 1f, 0f, 0f, -0.01f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                StrikePlateVolume.AroundSurface(-1f, 1f, -1f, 1f, 0f, 0f, 0.02f, 0f));
        }
    }
}
