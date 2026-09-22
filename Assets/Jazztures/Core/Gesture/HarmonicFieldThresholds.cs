using System;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// The landmark positions and hysteresis radii <see cref="HarmonicField"/> uses to turn
    /// a continuous (height, openness) posture into a latched ii/V/I selection (Design A,
    /// ADR-0038). All `[TUNABLE]` and, unlike every other threshold in this file's siblings,
    /// entirely invented — none of these ten values has been pilot-calibrated or even
    /// observed on a real hand; see <c>Docs/CALIBRATION.md</c>. The Unity
    /// <c>Config/HarmonicFieldConfig.asset</c> mirrors these. Immutable value type
    /// (ADR-0007).
    ///
    /// <para>
    /// Both axes are normalised and dimensionless: height is the hand's position between a
    /// floor and ceiling relative to a body-anchored shoulder (0 = floor, 1 = ceiling,
    /// unclamped below 0 so a hand-drop reads as negative), and openness is
    /// 1.0 = fully open/extended, 0.0 = fully curled (a fist). Mixing metres and a 0..1
    /// scalar in one Euclidean distance would make the radii meaningless — the Unity
    /// adapter is where the metres live and get normalised before this type ever sees them.
    /// </para>
    /// </summary>
    public readonly struct HarmonicFieldThresholds
    {
        public HarmonicFieldThresholds(
            float iiHeight, float iiOpenness,
            float vHeight, float vOpenness,
            float iHeight, float iOpenness,
            float floorHeight, float floorReleaseHeight,
            float lockRadius, float unlockRadius)
        {
            Require(iiOpenness >= 0f && iiOpenness <= 1f, nameof(iiOpenness));
            Require(vOpenness >= 0f && vOpenness <= 1f, nameof(vOpenness));
            Require(iOpenness >= 0f && iOpenness <= 1f, nameof(iOpenness));
            Require(floorReleaseHeight < floorHeight, nameof(floorReleaseHeight));
            Require(lockRadius > 0f, nameof(lockRadius));
            Require(unlockRadius > lockRadius, nameof(unlockRadius));

            IiHeight = iiHeight;
            IiOpenness = iiOpenness;
            VHeight = vHeight;
            VOpenness = vOpenness;
            IHeight = iHeight;
            IOpenness = iOpenness;
            FloorHeight = floorHeight;
            FloorReleaseHeight = floorReleaseHeight;
            LockRadius = lockRadius;
            UnlockRadius = unlockRadius;
        }

        /// <summary>The ii landmark's (height, openness) — "up and open."</summary>
        public float IiHeight { get; }

        public float IiOpenness { get; }

        /// <summary>The V landmark's (height, openness) — "mid-height, closed: closing is tension."</summary>
        public float VHeight { get; }

        public float VOpenness { get; }

        /// <summary>The I landmark's (height, openness) — "low and open."</summary>
        public float IHeight { get; }

        public float IOpenness { get; }

        /// <summary>
        /// Height above which a landmark can first be latched onto from nothing. Below this,
        /// with nothing currently latched, the field reads <see cref="Jazztures.Core.Ports.HandPoseCandidate.None"/>.
        /// </summary>
        public float FloorHeight { get; }

        /// <summary>
        /// Height below which an <b>already-latched</b> landmark releases — the floor's own
        /// Schmitt-trigger exit edge, always lower than <see cref="FloorHeight"/>, so a hand
        /// resting exactly at the floor does not chatter a hold in and out.
        /// </summary>
        public float FloorReleaseHeight { get; }

        /// <summary>
        /// Distance (in this normalised height/openness space) a posture must come within a
        /// landmark to latch onto it — from nothing, or from a different, already-latched
        /// landmark once that one is more than <see cref="UnlockRadius"/> away. Schmitt
        /// enter edge.
        /// </summary>
        public float LockRadius { get; }

        /// <summary>
        /// Distance an already-latched landmark must be exceeded before a <i>different</i>
        /// landmark within <see cref="LockRadius"/> is allowed to take over. Schmitt exit
        /// edge — always wider than <see cref="LockRadius"/>, so the boundary between two
        /// landmarks does not chatter.
        /// </summary>
        public float UnlockRadius { get; }

        /// <summary>
        /// Pure engineering guesses (ADR-0038) — none measured, none defensible as a
        /// finding. Pilot-calibrate at M8 (or sooner: the first on-device session should
        /// read live height/openness values off the console and replace every one of these
        /// before judging whether the mechanic works at all).
        /// </summary>
        public static HarmonicFieldThresholds Default => new HarmonicFieldThresholds(
            iiHeight: 0.80f, iiOpenness: 0.85f,
            vHeight: 0.45f, vOpenness: 0.10f,
            iHeight: 0.20f, iOpenness: 0.85f,
            floorHeight: 0.05f, floorReleaseHeight: -0.05f,
            lockRadius: 0.22f, unlockRadius: 0.34f);

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
