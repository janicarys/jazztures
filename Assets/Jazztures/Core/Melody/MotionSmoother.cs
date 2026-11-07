using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// A one-pole low-pass filter for one hand signal (ADR-0078): each sample moves the output toward the input by
    /// <c>1 - exp(-dt / timeConstant)</c>, so the smoothing is the same at 72 Hz and 90 Hz tracking. It trades a little
    /// lag (about the time constant) for less tracking jitter, which would otherwise flicker the tilt state and put
    /// stray crossings on the trigger line. A time constant of 0 passes every sample through. The first sample, and the
    /// first after <see cref="Reset"/>, passes straight through. Allocation-free.
    /// </summary>
    public sealed class MotionSmoother
    {
        private readonly double _timeConstantSeconds;
        private bool _hasValue;
        private double _lastTime;
        private float _value;

        public MotionSmoother(double timeConstantSeconds)
        {
            if (timeConstantSeconds < 0.0 || double.IsNaN(timeConstantSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeConstantSeconds), timeConstantSeconds, "Must be >= 0.");
            }

            _timeConstantSeconds = timeConstantSeconds;
        }

        public float Update(double time, float raw)
        {
            if (_timeConstantSeconds <= 0.0 || !_hasValue)
            {
                _hasValue = true;
                _lastTime = time;
                _value = raw;
                return raw;
            }

            double dt = time - _lastTime;
            if (dt <= 0.0)
            {
                return _value; // a repeated or out-of-order timestamp: nothing to filter
            }

            _lastTime = time;
            _value += (raw - _value) * (float)(1.0 - Math.Exp(-dt / _timeConstantSeconds));
            return _value;
        }

        public void Reset() => _hasValue = false;
    }
}
