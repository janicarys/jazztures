namespace Jazztures.Core.Melody
{
    /// <summary>One target beat of a drill: when it falls and which way it asks the swing to step (ADR-0078).</summary>
    public readonly struct DrillTarget
    {
        public DrillTarget(int index, double time, StepDirection direction, int task, bool startsTask)
        {
            Index = index;
            Time = time;
            Direction = direction;
            Task = task;
            StartsTask = startsTask;
        }

        public int Index { get; }

        /// <summary>Seconds on the musical clock.</summary>
        public double Time { get; }

        public StepDirection Direction { get; }

        /// <summary>Which task (a short run of targets that restarts from the drill's start pitch) it belongs to.</summary>
        public int Task { get; }

        public bool StartsTask { get; }
    }
}
