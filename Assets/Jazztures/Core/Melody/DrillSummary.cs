namespace Jazztures.Core.Melody
{
    /// <summary>Totals for one drill run (ADR-0078): the on-screen summary.</summary>
    public readonly struct DrillSummary
    {
        public DrillSummary(int hits, int wrongDirections, int misses, int extras, double meanAbsoluteOffsetSeconds)
        {
            Hits = hits;
            WrongDirections = wrongDirections;
            Misses = misses;
            Extras = extras;
            MeanAbsoluteOffsetSeconds = meanAbsoluteOffsetSeconds;
        }

        public int Hits { get; }

        public int WrongDirections { get; }

        public int Misses { get; }

        public int Extras { get; }

        /// <summary>Mean |crossing - target| over the hits and wrong directions (the ones that matched a beat); 0 if none.</summary>
        public double MeanAbsoluteOffsetSeconds { get; }
    }
}
