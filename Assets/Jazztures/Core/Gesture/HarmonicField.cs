using System;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// Turns a continuous (height, openness) hand posture into a latched ii/V/I selection
    /// (Design A, ADR-0038) — the continuous-field counterpart of the discrete
    /// <c>ShapeRecognizer</c>/<c>TransformRecognizer</c> pose classifiers. Produces the
    /// same <see cref="HandPoseCandidate"/> those classifiers do, so it can feed the
    /// existing <see cref="GestureInterpreter"/> unchanged — confirmation hold-time, miss
    /// tolerance, tracking-loss sustain and release asymmetry are all reused as-is.
    ///
    /// <para>
    /// A <b>latch</b>, not a per-frame nearest-neighbour lookup: once a landmark is
    /// latched, it stays latched through the space between landmarks, and can only change
    /// by (a) a different landmark coming within <see cref="HarmonicFieldThresholds.LockRadius"/>
    /// while the current one has drifted past <see cref="HarmonicFieldThresholds.UnlockRadius"/>,
    /// or (b) the hand dropping below <see cref="HarmonicFieldThresholds.FloorReleaseHeight"/>.
    /// Nothing in mid-air between landmarks ever reads <see cref="HandPoseCandidate.None"/>
    /// — a release can only be produced by a deliberate hand-drop. This is a structural
    /// version of the fix ADR-0034/ADR-0037 had to build reactively for the old
    /// discrete/velocity system: there, a release could confirm from ordinary pose noise
    /// while the hand was never actually released; here, that class of bug cannot occur at
    /// all, because travelling between two landmarks is never itself evidence of a release.
    /// </para>
    ///
    /// <para>
    /// Never produces <see cref="HandPoseCandidate.Ambiguous"/> — a continuous field has no
    /// ambiguity state to hold; the ii/I ambiguity the discrete recognisers had to guard
    /// against (ADR-0014) simply dissolves, which is one of Design A's stated wins.
    /// </para>
    /// </summary>
    public sealed class HarmonicField
    {
        private readonly HarmonicFieldThresholds _thresholds;

        public HarmonicField(HarmonicFieldThresholds thresholds)
        {
            _thresholds = thresholds;
        }

        /// <summary>The currently-latched landmark, or <see cref="HandPoseCandidate.None"/>.</summary>
        public HandPoseCandidate Current { get; private set; } = HandPoseCandidate.None;

        /// <summary>
        /// Feed one (height, openness) sample — both normalised and dimensionless, see
        /// <see cref="HarmonicFieldThresholds"/>. Returns (and updates) <see cref="Current"/>.
        /// </summary>
        public HandPoseCandidate Update(float height, float openness)
        {
            if (Current == HandPoseCandidate.None)
            {
                if (height < _thresholds.FloorHeight)
                {
                    return HandPoseCandidate.None;
                }

                (HandPoseCandidate nearest, float squaredDistance) = Nearest(height, openness);
                if (squaredDistance <= Squared(_thresholds.LockRadius))
                {
                    Current = nearest;
                }

                return Current;
            }

            if (height < _thresholds.FloorReleaseHeight)
            {
                Current = HandPoseCandidate.None;
                return HandPoseCandidate.None;
            }

            (HandPoseCandidate nearestCandidate, float nearestSquaredDistance) = Nearest(height, openness);
            if (nearestCandidate != Current
                && nearestSquaredDistance <= Squared(_thresholds.LockRadius)
                && SquaredDistanceTo(Current, height, openness) >= Squared(_thresholds.UnlockRadius))
            {
                Current = nearestCandidate;
            }

            return Current;
        }

        /// <summary>Unlatch immediately, regardless of position — e.g. on tracking loss recovery.</summary>
        public void Reset() => Current = HandPoseCandidate.None;

        private (HandPoseCandidate, float) Nearest(float height, float openness)
        {
            // Deterministic tie-break in enum order: Ii, V, I.
            HandPoseCandidate best = HandPoseCandidate.Ii;
            float bestSquared = SquaredDistanceTo(HandPoseCandidate.Ii, height, openness);

            float vSquared = SquaredDistanceTo(HandPoseCandidate.V, height, openness);
            if (vSquared < bestSquared)
            {
                best = HandPoseCandidate.V;
                bestSquared = vSquared;
            }

            float iSquared = SquaredDistanceTo(HandPoseCandidate.I, height, openness);
            if (iSquared < bestSquared)
            {
                best = HandPoseCandidate.I;
                bestSquared = iSquared;
            }

            return (best, bestSquared);
        }

        private float SquaredDistanceTo(HandPoseCandidate landmark, float height, float openness)
        {
            (float landmarkHeight, float landmarkOpenness) = LandmarkOf(landmark);
            float dh = height - landmarkHeight;
            float dOpenness = openness - landmarkOpenness;
            return dh * dh + dOpenness * dOpenness;
        }

        private (float, float) LandmarkOf(HandPoseCandidate landmark) => landmark switch
        {
            HandPoseCandidate.Ii => (_thresholds.IiHeight, _thresholds.IiOpenness),
            HandPoseCandidate.V => (_thresholds.VHeight, _thresholds.VOpenness),
            HandPoseCandidate.I => (_thresholds.IHeight, _thresholds.IOpenness),
            _ => throw new ArgumentOutOfRangeException(nameof(landmark), landmark, null),
        };

        private static float Squared(float value) => value * value;
    }
}
