using System;
using System.Collections.Generic;

namespace Jazztures.Core.Evaluation
{
    /// <summary>
    /// Scores the timing of a completed attempt against its expected onsets (CLAUDE.md
    /// §3.7). Pure — feedback is computed once, at the end of a phrase, and returned as an
    /// <see cref="AttemptResult"/>. Nothing here runs on the per-frame path.
    /// </summary>
    public static class OnsetScorer
    {
        /// <summary>The §3.7 verdict for a single deviation (sign ignored).</summary>
        public static OnsetVerdict Classify(double deviationSeconds, OnsetWindows windows)
        {
            double magnitude = Math.Abs(deviationSeconds);

            if (magnitude <= windows.OnTimeSeconds)
            {
                return OnsetVerdict.OnTime;
            }

            return magnitude <= windows.CloseSeconds ? OnsetVerdict.Close : OnsetVerdict.Off;
        }

        /// <summary>
        /// Match the learner's <paramref name="actualSeconds"/> onsets to the
        /// <paramref name="expectedSeconds"/> beats and score the result.
        ///
        /// <para>
        /// Matching is greedy nearest-first: expected beats are considered in time order,
        /// each takes the closest still-unclaimed actual within
        /// <see cref="OnsetWindows.MatchSeconds"/>. Unclaimed expecteds are missed;
        /// unclaimed actuals are extra. Inputs need not be pre-sorted.
        /// </para>
        /// </summary>
        public static AttemptResult Evaluate(
            IReadOnlyList<double> expectedSeconds,
            IReadOnlyList<double> actualSeconds,
            OnsetWindows windows)
        {
            if (expectedSeconds == null)
            {
                throw new ArgumentNullException(nameof(expectedSeconds));
            }

            if (actualSeconds == null)
            {
                throw new ArgumentNullException(nameof(actualSeconds));
            }

            double[] expected = Sorted(expectedSeconds);
            double[] actual = Sorted(actualSeconds);
            bool[] claimed = new bool[actual.Length];

            var scored = new List<ScoredOnset>(expected.Length);
            int missed = 0;

            foreach (double beat in expected)
            {
                int best = -1;
                double bestDistance = windows.MatchSeconds;

                for (int i = 0; i < actual.Length; i++)
                {
                    if (claimed[i])
                    {
                        continue;
                    }

                    double distance = Math.Abs(actual[i] - beat);
                    if (distance <= bestDistance)
                    {
                        best = i;
                        bestDistance = distance;
                    }
                }

                if (best < 0)
                {
                    missed++;
                    continue;
                }

                claimed[best] = true;
                double deviation = actual[best] - beat;
                scored.Add(new ScoredOnset(beat, actual[best], Classify(deviation, windows)));
            }

            int extra = 0;
            for (int i = 0; i < claimed.Length; i++)
            {
                if (!claimed[i])
                {
                    extra++;
                }
            }

            return new AttemptResult(scored, missed, extra);
        }

        private static double[] Sorted(IReadOnlyList<double> values)
        {
            var copy = new double[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    throw new ArgumentOutOfRangeException(nameof(values), v, "Onset times must be finite.");
                }

                copy[i] = v;
            }

            Array.Sort(copy);
            return copy;
        }
    }
}
