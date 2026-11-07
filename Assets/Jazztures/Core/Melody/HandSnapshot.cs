namespace Jazztures.Core.Melody
{
    /// <summary>What the hand's held state was at one moment: tilt direction and height zone (ADR-0078).</summary>
    public readonly struct HandSnapshot
    {
        public HandSnapshot(StepDirection direction, int zone)
        {
            Direction = direction;
            Zone = zone;
        }

        public StepDirection Direction { get; }

        /// <summary>The register zone, 0 = lowest. 0 when height zones are not in use.</summary>
        public int Zone { get; }
    }
}
