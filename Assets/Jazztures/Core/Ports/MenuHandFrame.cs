using System;

namespace Jazztures.Core.Ports
{
    /// <summary>
    /// One frame of the <b>right hand as the menu sees it</b> (ADR-0051): how fast it is
    /// moving in each of the three directions the menu cares about, and how tightly it is
    /// closed. The menu's counterpart of <see cref="HandPoseFrame"/>, which carries the left
    /// hand's chord poses. Immutable value type (ADR-0007).
    ///
    /// <para>
    /// The speeds are <b>magnitudes of the current, unbroken run of motion</b> in that
    /// direction, in metres per second — 0 means "not moving that way right now" (the same
    /// contract as <see cref="Jazztures.Core.Gesture.DownwardSpeedWindow"/>). "Left" and
    /// "right" are the learner's, not the world's: the adapter projects onto the head's
    /// flattened right vector so the menu keeps working when the learner turns (§3.4).
    /// </para>
    ///
    /// <para>
    /// The last three members exist for the tutorial (ADR-0053), which has to see things the menu
    /// itself never needs: whether the <i>left</i> hand is in view, whether the right hand is
    /// fully open, and which way its thumb points.
    /// </para>
    /// </summary>
    public readonly struct MenuHandFrame : IEquatable<MenuHandFrame>
    {
        public MenuHandFrame(
            TrackingQuality tracking,
            float leftwardSpeed = 0f,
            float rightwardSpeed = 0f,
            float downwardSpeed = 0f,
            float fistAmount = 0f,
            TrackingQuality leftTracking = TrackingQuality.NotTracked,
            float? maxFingerClosure = null,
            float thumbUpAngleDegrees = 180f,
            float viewAngleDegrees = 180f,
            float leftViewAngleDegrees = 180f)
        {
            Tracking = tracking;
            LeftwardSpeed = NonNegative(leftwardSpeed);
            RightwardSpeed = NonNegative(rightwardSpeed);
            DownwardSpeed = NonNegative(downwardSpeed);
            FistAmount = Clamp01(fistAmount);
            LeftTracking = leftTracking;
            MaxFingerClosure = Clamp01(maxFingerClosure ?? fistAmount);
            ThumbUpAngleDegrees = Angle(thumbUpAngleDegrees);
            ViewAngleDegrees = Angle(viewAngleDegrees);
            LeftViewAngleDegrees = Angle(leftViewAngleDegrees);
        }

        public TrackingQuality Tracking { get; }

        /// <summary>Peak speed of the current leftward run, metres per second, ≥ 0.</summary>
        public float LeftwardSpeed { get; }

        /// <summary>Peak speed of the current rightward run, metres per second, ≥ 0.</summary>
        public float RightwardSpeed { get; }

        /// <summary>Peak speed of the current downward run, metres per second, ≥ 0.</summary>
        public float DownwardSpeed { get; }

        /// <summary>
        /// How closed the four long fingers are, 0 (open) to 1 (tight fist) — the smallest of
        /// the four, so one finger left straight keeps it low. The thumb is ignored: a fist is
        /// still a fist with the thumb tucked or not.
        /// </summary>
        public float FistAmount { get; }

        /// <summary>The <b>left</b> hand's tracking quality — only the tutorial reads it ("put your hands in front of you").</summary>
        public TrackingQuality LeftTracking { get; }

        /// <summary>
        /// How closed the <i>most</i> closed of the four long fingers is, 0..1. The counterpart of
        /// <see cref="FistAmount"/> (the least closed): a fist needs every finger curled, an open hand
        /// needs none of them. Equals <see cref="FistAmount"/> if the adapter does not measure it.
        /// </summary>
        public float MaxFingerClosure { get; }

        /// <summary>
        /// Angle, in degrees, between the right thumb's side of the wrist and straight up — 0 is a
        /// thumb pointing at the ceiling. 180 when unknown. The SDK's own <c>WristUp</c> measure.
        /// </summary>
        public float ThumbUpAngleDegrees { get; }

        /// <summary>
        /// Angle, in degrees, between where the learner is looking and the direction to the <b>right</b> hand:
        /// 0 is dead ahead, 90 is out to the side. 180 when unknown. The tutorial judges "in front of you" and
        /// "out of view" by this, not by tracking quality: the Quest keeps tracking hands hanging at the
        /// learner's sides, so a hand can be perfectly well tracked and still not be in view.
        /// </summary>
        public float ViewAngleDegrees { get; }

        /// <summary>As <see cref="ViewAngleDegrees"/>, for the <b>left</b> hand.</summary>
        public float LeftViewAngleDegrees { get; }

        /// <summary>A frame with nothing tracked — the safe default before input arrives.</summary>
        public static MenuHandFrame Untracked => new MenuHandFrame(TrackingQuality.NotTracked);

        public bool Equals(MenuHandFrame other) =>
            Tracking == other.Tracking
            && LeftwardSpeed.Equals(other.LeftwardSpeed)
            && RightwardSpeed.Equals(other.RightwardSpeed)
            && DownwardSpeed.Equals(other.DownwardSpeed)
            && FistAmount.Equals(other.FistAmount)
            && LeftTracking == other.LeftTracking
            && MaxFingerClosure.Equals(other.MaxFingerClosure)
            && ThumbUpAngleDegrees.Equals(other.ThumbUpAngleDegrees)
            && ViewAngleDegrees.Equals(other.ViewAngleDegrees)
            && LeftViewAngleDegrees.Equals(other.LeftViewAngleDegrees);

        public override bool Equals(object? obj) => obj is MenuHandFrame other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Tracking;
                hash = (hash * 397) ^ LeftwardSpeed.GetHashCode();
                hash = (hash * 397) ^ RightwardSpeed.GetHashCode();
                hash = (hash * 397) ^ DownwardSpeed.GetHashCode();
                hash = (hash * 397) ^ FistAmount.GetHashCode();
                hash = (hash * 397) ^ (int)LeftTracking;
                hash = (hash * 397) ^ MaxFingerClosure.GetHashCode();
                hash = (hash * 397) ^ ThumbUpAngleDegrees.GetHashCode();
                hash = (hash * 397) ^ ViewAngleDegrees.GetHashCode();
                hash = (hash * 397) ^ LeftViewAngleDegrees.GetHashCode();
                return hash;
            }
        }

        public override string ToString() =>
            $"{Tracking}/L:{LeftTracking} L{LeftwardSpeed:0.00} R{RightwardSpeed:0.00} D{DownwardSpeed:0.00} fist{FistAmount:0.00} max{MaxFingerClosure:0.00} thumb{ThumbUpAngleDegrees:0}° view R{ViewAngleDegrees:0}° L{LeftViewAngleDegrees:0}°";

        private static float Angle(float degrees) =>
            float.IsNaN(degrees) ? 180f : Math.Max(0f, Math.Min(180f, degrees));

        private static float NonNegative(float value) => float.IsNaN(value) || value < 0f ? 0f : value;

        private static float Clamp01(float value) =>
            float.IsNaN(value) || value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
