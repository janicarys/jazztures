namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Turns the hand's pitch angle into <see cref="StepDirection"/>, with hysteresis so a hand resting near
    /// a boundary does not flicker (ADR-0078). Pure, allocation-free.
    /// </summary>
    public sealed class DirectionClassifier
    {
        private readonly DirectionThresholds _thresholds;

        public DirectionClassifier(DirectionThresholds thresholds)
        {
            _thresholds = thresholds;
        }

        public StepDirection Current { get; private set; } = StepDirection.Same;

        /// <summary>Classify one angle (degrees, + = fingers up) and return the held direction.</summary>
        public StepDirection Update(float pitchAngleDegrees)
        {
            float hysteresis = _thresholds.HysteresisDegrees;
            switch (Current)
            {
                case StepDirection.Up:
                    if (pitchAngleDegrees < _thresholds.UpDegrees - hysteresis)
                    {
                        Current = pitchAngleDegrees <= _thresholds.DownDegrees ? StepDirection.Down : StepDirection.Same;
                    }

                    break;
                case StepDirection.Down:
                    if (pitchAngleDegrees > _thresholds.DownDegrees + hysteresis)
                    {
                        Current = pitchAngleDegrees >= _thresholds.UpDegrees ? StepDirection.Up : StepDirection.Same;
                    }

                    break;
                default:
                    if (pitchAngleDegrees >= _thresholds.UpDegrees)
                    {
                        Current = StepDirection.Up;
                    }
                    else if (pitchAngleDegrees <= _thresholds.DownDegrees)
                    {
                        Current = StepDirection.Down;
                    }

                    break;
            }

            return Current;
        }

        public void Reset() => Current = StepDirection.Same;
    }
}
