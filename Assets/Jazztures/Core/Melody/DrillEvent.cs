namespace Jazztures.Core.Melody
{
    /// <summary>One scored event of a drill run (ADR-0078): a hit, a miss, an extra, or a tracking-loss interval.</summary>
    public readonly struct DrillEvent
    {
        public DrillEvent(
            DrillResult result, int targetIndex, double targetTime, StepDirection targetDirection,
            double crossingTime, SwingDirection swing, StepDirection sampledDirection, int pitchMidi,
            double offsetSeconds, double durationSeconds)
        {
            Result = result;
            TargetIndex = targetIndex;
            TargetTime = targetTime;
            TargetDirection = targetDirection;
            CrossingTime = crossingTime;
            Swing = swing;
            SampledDirection = sampledDirection;
            PitchMidi = pitchMidi;
            OffsetSeconds = offsetSeconds;
            DurationSeconds = durationSeconds;
        }

        public DrillResult Result { get; }

        /// <summary>The target it scored against, or -1 (an extra, a tracking loss).</summary>
        public int TargetIndex { get; }

        public bool HasTarget => TargetIndex >= 0;

        public double TargetTime { get; }

        public StepDirection TargetDirection { get; }

        /// <summary>When the hand crossed the line; NaN for a miss. For a tracking loss, when it began.</summary>
        public double CrossingTime { get; }

        public bool HasCrossing => !double.IsNaN(CrossingTime);

        public SwingDirection Swing { get; }

        public StepDirection SampledDirection { get; }

        /// <summary>The sounded MIDI pitch, or -1 where there was no note.</summary>
        public int PitchMidi { get; }

        /// <summary>Crossing minus target, seconds; NaN where either is missing.</summary>
        public double OffsetSeconds { get; }

        /// <summary>For a tracking loss, how long it lasted; 0 otherwise.</summary>
        public double DurationSeconds { get; }
    }
}
