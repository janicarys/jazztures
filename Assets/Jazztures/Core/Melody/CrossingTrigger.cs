using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Emits one event each time the hand crosses the vertical trigger line, in either direction
    /// (ADR-0078). The hand's lateral position is in the lane frame (metres, 0 = the line, + = right).
    ///
    /// <para>
    /// A crossing counts only when the hand goes from beyond one edge of the dead band to beyond the other,
    /// so hovering or jittering on the line never fires. It remembers the last side it was <i>clearly</i> on;
    /// inside the band the side does not change. After a crossing, further ones are ignored for the lockout
    /// (the side still follows the hand, so the next real crossing after the lockout is judged correctly).
    /// The first clear side after <see cref="Reset"/> only seeds the state: a hand that appears across the line
    /// cannot fire a note by arriving. Allocation-free.
    /// </para>
    /// </summary>
    public sealed class CrossingTrigger
    {
        private readonly float _halfDeadzone;
        private readonly double _lockoutSeconds;

        private int _side;                    // -1 left, +1 right, 0 not yet known
        private double _lockedUntil = double.NegativeInfinity;
        private bool _hasPrevious;
        private float _previousX;
        private double _previousTime;
        private float _previousSpeed;

        public CrossingTrigger(CrossingThresholds thresholds)
        {
            _halfDeadzone = thresholds.DeadzoneMetres * 0.5f;
            _lockoutSeconds = thresholds.LockoutSeconds;
        }

        /// <summary>The direction of the last crossing; meaningful once <see cref="Update"/> has returned true.</summary>
        public SwingDirection LastDirection { get; private set; }

        /// <summary>Time of the last crossing (the sample that cleared the far edge of the band).</summary>
        public double LastTime { get; private set; } = double.NegativeInfinity;

        /// <summary>
        /// Lateral speed at the last crossing, m/s: the faster of the last two sample-to-sample speeds, so a
        /// swing that decelerates right at the line is not read as a gentle one.
        /// </summary>
        public float LastSpeedMetresPerSecond { get; private set; }

        /// <summary>Feed one sample; true if it completed a crossing that is not locked out.</summary>
        public bool Update(double time, float lateralMetres)
        {
            float speed = 0f;
            if (_hasPrevious && time > _previousTime)
            {
                speed = Math.Abs(lateralMetres - _previousX) / (float)(time - _previousTime);
            }

            float peak = Math.Max(speed, _previousSpeed);
            _hasPrevious = true;
            _previousX = lateralMetres;
            _previousTime = time;
            _previousSpeed = speed;

            int clearSide = lateralMetres >= _halfDeadzone ? 1 : lateralMetres <= -_halfDeadzone ? -1 : 0;
            if (clearSide == 0)
            {
                return false;
            }

            int previousSide = _side;
            _side = clearSide;
            if (previousSide == 0 || previousSide == clearSide || time < _lockedUntil)
            {
                return false;
            }

            LastDirection = clearSide > 0 ? SwingDirection.LeftToRight : SwingDirection.RightToLeft;
            LastTime = time;
            LastSpeedMetresPerSecond = peak;
            _lockedUntil = time + _lockoutSeconds;
            return true;
        }

        /// <summary>Forget the side and the lockout. Call when the gate closes or tracking is lost.</summary>
        public void Reset()
        {
            _side = 0;
            _lockedUntil = double.NegativeInfinity;
            _hasPrevious = false;
            _previousSpeed = 0f;
        }
    }
}
