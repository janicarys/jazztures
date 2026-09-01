using System.Globalization;

namespace Jazztures.Core.Diagnostics
{
    /// <summary>
    /// Aggregate latency statistics for one <see cref="LatencyStage"/>, in milliseconds.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct LatencySummary
    {
        public LatencySummary(
            LatencyStage stage,
            int count,
            double minMs,
            double maxMs,
            double meanMs,
            double p50Ms,
            double p95Ms,
            double p99Ms)
        {
            Stage = stage;
            Count = count;
            MinMs = minMs;
            MaxMs = maxMs;
            MeanMs = meanMs;
            P50Ms = p50Ms;
            P95Ms = p95Ms;
            P99Ms = p99Ms;
        }

        public LatencyStage Stage { get; }

        public int Count { get; }

        public double MinMs { get; }

        public double MaxMs { get; }

        public double MeanMs { get; }

        public double P50Ms { get; }

        public double P95Ms { get; }

        public double P99Ms { get; }

        /// <summary>True when no samples have been recorded for the stage.</summary>
        public bool IsEmpty => Count == 0;

        public override string ToString() => IsEmpty
            ? $"{Stage}: (no samples)"
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0}: n={1}  p50={2:0.0}  p95={3:0.0}  p99={4:0.0}  min={5:0.0}  max={6:0.0}  mean={7:0.0} ms",
                Stage, Count, P50Ms, P95Ms, P99Ms, MinMs, MaxMs, MeanMs);
    }
}
