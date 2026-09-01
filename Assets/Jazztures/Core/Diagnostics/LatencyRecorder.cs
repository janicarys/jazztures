using System;

namespace Jazztures.Core.Diagnostics
{
    /// <summary>
    /// Rolling per-stage latency samples and their percentiles (CLAUDE.md §4.3). Each
    /// stage keeps the most recent <see cref="Capacity"/> samples in a fixed ring buffer —
    /// recording is allocation-free; <see cref="Summarize"/> allocates a scratch array to
    /// sort and is meant for occasional reporting, not the hot path.
    /// </summary>
    public sealed class LatencyRecorder
    {
        public const int Capacity = 512;

        private static readonly int StageCount = Enum.GetValues(typeof(LatencyStage)).Length;

        private readonly double[][] _rings;
        private readonly int[] _counts;
        private readonly int[] _heads;

        public LatencyRecorder()
        {
            _rings = new double[StageCount][];
            _counts = new int[StageCount];
            _heads = new int[StageCount];
            for (int i = 0; i < StageCount; i++)
            {
                _rings[i] = new double[Capacity];
            }
        }

        /// <summary>Record one sample. Negatives and non-finite values are dropped.</summary>
        public void Record(LatencyStage stage, double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0.0)
            {
                return;
            }

            int s = (int)stage;
            _rings[s][_heads[s]] = milliseconds;
            _heads[s] = (_heads[s] + 1) % Capacity;
            if (_counts[s] < Capacity)
            {
                _counts[s]++;
            }
        }

        public int SampleCount(LatencyStage stage) => _counts[(int)stage];

        public LatencySummary Summarize(LatencyStage stage)
        {
            int s = (int)stage;
            int n = _counts[s];
            if (n == 0)
            {
                return new LatencySummary(stage, 0, 0, 0, 0, 0, 0, 0);
            }

            var sorted = new double[n];
            Array.Copy(_rings[s], sorted, n);
            Array.Sort(sorted);

            double sum = 0.0;
            for (int i = 0; i < n; i++)
            {
                sum += sorted[i];
            }

            return new LatencySummary(
                stage,
                n,
                sorted[0],
                sorted[n - 1],
                sum / n,
                Percentile(sorted, 50),
                Percentile(sorted, 95),
                Percentile(sorted, 99));
        }

        public void Reset()
        {
            Array.Clear(_counts, 0, _counts.Length);
            Array.Clear(_heads, 0, _heads.Length);
        }

        /// <summary>Nearest-rank percentile of an already-sorted array.</summary>
        private static double Percentile(double[] sorted, double percentile)
        {
            int rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Length);
            if (rank < 1)
            {
                rank = 1;
            }
            else if (rank > sorted.Length)
            {
                rank = sorted.Length;
            }

            return sorted[rank - 1];
        }
    }
}
