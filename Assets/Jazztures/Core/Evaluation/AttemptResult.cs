using System;
using System.Collections.Generic;

namespace Jazztures.Core.Evaluation
{
    /// <summary>
    /// Aggregate feedback for one completed attempt (CLAUDE.md §3.7). Produced by
    /// <see cref="OnsetScorer.Evaluate"/> <b>after</b> the phrase finishes — never mid-phrase.
    /// Immutable value type (ADR-0007); the per-onset detail is a shared read-only list.
    /// </summary>
    public readonly struct AttemptResult
    {
        private readonly IReadOnlyList<ScoredOnset>? _onsets;

        public AttemptResult(IReadOnlyList<ScoredOnset> onsets, int missedCount, int extraCount)
        {
            if (missedCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(missedCount), missedCount, null);
            }

            if (extraCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(extraCount), extraCount, null);
            }

            _onsets = onsets ?? throw new ArgumentNullException(nameof(onsets));
            MissedCount = missedCount;
            ExtraCount = extraCount;

            int onTime = 0, close = 0, off = 0;
            double sumAbs = 0.0, sumSigned = 0.0;
            for (int i = 0; i < onsets.Count; i++)
            {
                ScoredOnset onset = onsets[i];
                switch (onset.Verdict)
                {
                    case OnsetVerdict.OnTime: onTime++; break;
                    case OnsetVerdict.Close: close++; break;
                    default: off++; break;
                }

                sumAbs += Math.Abs(onset.DeviationSeconds);
                sumSigned += onset.DeviationSeconds;
            }

            OnTimeCount = onTime;
            CloseCount = close;
            OffCount = off;

            int matched = onsets.Count;
            MeanAbsDeviationSeconds = matched > 0 ? sumAbs / matched : 0.0;
            MeanSignedDeviationSeconds = matched > 0 ? sumSigned / matched : 0.0;
        }

        /// <summary>Every expected beat that got a note, with its timing verdict.</summary>
        public IReadOnlyList<ScoredOnset> Onsets => _onsets ?? Array.Empty<ScoredOnset>();

        public int OnTimeCount { get; }

        public int CloseCount { get; }

        public int OffCount { get; }

        /// <summary>Expected beats the learner played no note for.</summary>
        public int MissedCount { get; }

        /// <summary>Notes the learner played that matched no expected beat.</summary>
        public int ExtraCount { get; }

        /// <summary>Expected beats that were played (regardless of how well).</summary>
        public int MatchedCount => OnTimeCount + CloseCount + OffCount;

        /// <summary>Total expected beats in the phrase.</summary>
        public int ExpectedCount => MatchedCount + MissedCount;

        /// <summary>Fraction of expected beats played on time, in [0, 1].</summary>
        public double OnTimeFraction => ExpectedCount > 0 ? OnTimeCount / (double)ExpectedCount : 0.0;

        /// <summary>Mean unsigned onset error across matched notes.</summary>
        public double MeanAbsDeviationSeconds { get; }

        /// <summary>
        /// Mean signed onset error: positive means the learner dragged overall, negative
        /// means they rushed.
        /// </summary>
        public double MeanSignedDeviationSeconds { get; }

        public bool IsEmpty => ExpectedCount == 0 && ExtraCount == 0;

        public override string ToString() =>
            $"attempt: {OnTimeCount} on / {CloseCount} close / {OffCount} off / {MissedCount} missed / {ExtraCount} extra";
    }
}
