using System;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// The region of space that counts as touching the left-hand strike plate (ADR-0048): an
    /// axis-aligned box in the plate's own frame, with local Z pointing up (the anchor lays the
    /// plate flat, ADR-0040). Pure scalar math, like <see cref="Melody.TargetVolume"/> — the
    /// Unity side hands in the point already expressed in the plate's frame.
    ///
    /// <para>
    /// Built <b>around the visual's top surface</b> (<see cref="AroundSurface"/>) instead of being
    /// centred on the plate's origin. The earlier symmetric cylinder (6 cm radius, ±8 cm) put a
    /// third of its height — and its whole top half — above a 1 cm slab, so a chord fired with the
    /// hand several centimetres in the air, while the slab's outer margin was out of reach. This
    /// box covers the visual's footprint plus a small margin, reaches only a hover tolerance above
    /// the surface, and extends well below it: depth is the axis a person cannot judge in mid-air
    /// (ADR-0018), so a hand that overshoots still counts — a touch is never "missed" by going too
    /// far, only by stopping too short.
    /// </para>
    ///
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct StrikePlateVolume
    {
        public StrikePlateVolume(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            Require(maxX > minX, nameof(maxX));
            Require(maxY > minY, nameof(maxY));
            Require(maxZ > minZ, nameof(maxZ));

            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public float MinX { get; }

        public float MaxX { get; }

        public float MinY { get; }

        public float MaxY { get; }

        /// <summary>Lowest point that still counts (below the surface), local Z.</summary>
        public float MinZ { get; }

        /// <summary>Highest point that counts (a little above the surface), local Z.</summary>
        public float MaxZ { get; }

        /// <summary>True when the point, in the plate's local frame (Z up), is inside the box.</summary>
        public bool Contains(float x, float y, float z) =>
            x >= MinX && x <= MaxX
            && y >= MinY && y <= MaxY
            && z >= MinZ && z <= MaxZ;

        /// <summary>
        /// A volume matched to a flat visual whose footprint spans [<paramref name="visualMinX"/>,
        /// <paramref name="visualMaxX"/>] × [<paramref name="visualMinY"/>, <paramref name="visualMaxY"/>]
        /// and whose top face is at <paramref name="surfaceZ"/>: the footprint grown by
        /// <paramref name="footprintMargin"/> on every side, reaching <paramref name="hoverAboveSurface"/>
        /// above the surface and <paramref name="depthBelowSurface"/> below it.
        /// </summary>
        public static StrikePlateVolume AroundSurface(
            float visualMinX, float visualMaxX, float visualMinY, float visualMaxY, float surfaceZ,
            float footprintMargin, float hoverAboveSurface, float depthBelowSurface)
        {
            Require(footprintMargin >= 0f, nameof(footprintMargin));
            Require(hoverAboveSurface >= 0f, nameof(hoverAboveSurface));
            Require(depthBelowSurface > 0f, nameof(depthBelowSurface));

            return new StrikePlateVolume(
                visualMinX - footprintMargin, visualMaxX + footprintMargin,
                visualMinY - footprintMargin, visualMaxY + footprintMargin,
                surfaceZ - depthBelowSurface, surfaceZ + hoverAboveSurface);
        }

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
