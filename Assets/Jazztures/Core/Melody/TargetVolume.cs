using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The shape of one melody target's trigger volume (CLAUDE.md §3.3, ADR-0016/0018): a
    /// cylinder aligned with the target's local Z — the axis the learner's fingertip
    /// approaches along.
    ///
    /// <para>
    /// The XY face keeps the §3.1 spatial map: <see cref="Radius"/> still selects scale
    /// degree and octave, and must stay well under half the inter-target spacing. Depth is
    /// forgiving on purpose — mid-air VR gives no contact cue and §3.10 rules out haptics,
    /// so Z is the one axis a person cannot judge, and a sphere punishes Z error exactly as
    /// hard as XY error (ADR-0018).
    /// </para>
    ///
    /// <para>
    /// Pure scalar math — <see cref="Contains"/> takes the fingertip already expressed in
    /// the target's local frame, because <c>Core</c> has no vector type by design
    /// (<see cref="LazyRecenter"/> makes the same choice). Immutable value type (ADR-0007).
    /// </para>
    /// </summary>
    public readonly struct TargetVolume
    {
        public TargetVolume(float radius, float halfDepth)
        {
            Require(radius > 0f, nameof(radius));
            Require(halfDepth > 0f, nameof(halfDepth));

            Radius = radius;
            HalfDepth = halfDepth;
        }

        /// <summary>Radius of the circular XY face, metres. Selects degree + octave.</summary>
        public float Radius { get; }

        /// <summary>Half the cylinder's length along local Z, metres — the aim-depth tolerance.</summary>
        public float HalfDepth { get; }

        /// <summary>
        /// True when a fingertip — given as its offset from the target centre, already
        /// rotated into the target's local frame — lies within the cylinder.
        /// </summary>
        /// <param name="localX">Local-X offset from the target centre, metres.</param>
        /// <param name="localY">Local-Y offset from the target centre, metres.</param>
        /// <param name="localZ">Local-Z (approach-axis) offset from the target centre, metres.</param>
        public bool Contains(float localX, float localY, float localZ)
        {
            if (localZ < 0f)
            {
                localZ = -localZ;
            }

            if (localZ > HalfDepth)
            {
                return false;
            }

            return localX * localX + localY * localY <= Radius * Radius;
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
