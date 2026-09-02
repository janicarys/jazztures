namespace Jazztures.Core.Evaluation
{
    /// <summary>
    /// One expected beat paired with the note the learner actually played for it (§3.7).
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct ScoredOnset
    {
        public ScoredOnset(double expectedSeconds, double actualSeconds, OnsetVerdict verdict)
        {
            ExpectedSeconds = expectedSeconds;
            ActualSeconds = actualSeconds;
            Verdict = verdict;
        }

        public double ExpectedSeconds { get; }

        public double ActualSeconds { get; }

        /// <summary>Signed: positive is late (dragging), negative is early (rushing).</summary>
        public double DeviationSeconds => ActualSeconds - ExpectedSeconds;

        public OnsetVerdict Verdict { get; }

        public override string ToString() =>
            $"{Verdict} ({DeviationSeconds * 1000:+0;-0} ms)";
    }
}
