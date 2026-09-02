using System;
using Jazztures.Core.Harmony;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// The condition half of a <see cref="LessonCue"/> (ADR-0011). A tagged value — the
    /// <see cref="Kind"/> says which fields matter. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct CueTrigger
    {
        private CueTrigger(
            CueTriggerKind kind,
            double beat,
            string? marker,
            LearnerAction action,
            ChordFunction? function,
            int? targetIndex)
        {
            Kind = kind;
            Beat = beat;
            Marker = marker;
            Action = action;
            Function = function;
            TargetIndex = targetIndex;
        }

        public CueTriggerKind Kind { get; }

        /// <summary>Beat position — meaningful when <see cref="Kind"/> is <see cref="CueTriggerKind.AtBeat"/>.</summary>
        public double Beat { get; }

        /// <summary>Marker name — meaningful when <see cref="Kind"/> is <see cref="CueTriggerKind.AtMarker"/>.</summary>
        public string? Marker { get; }

        /// <summary>Which learner action — meaningful when <see cref="Kind"/> is <see cref="CueTriggerKind.OnLearnerAction"/>.</summary>
        public LearnerAction Action { get; }

        /// <summary>Narrows <see cref="LearnerAction.ChordConfirmed"/> to one function; null means any.</summary>
        public ChordFunction? Function { get; }

        /// <summary>Narrows <see cref="LearnerAction.MelodyNotePlayed"/> to one target; null means any.</summary>
        public int? TargetIndex { get; }

        public static CueTrigger AtBeat(double beat)
        {
            if (double.IsNaN(beat) || double.IsInfinity(beat) || beat < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(beat), beat, "Must be finite and non-negative.");
            }

            return new CueTrigger(CueTriggerKind.AtBeat, beat, null, default, null, null);
        }

        public static CueTrigger AtMarker(string marker)
        {
            if (string.IsNullOrWhiteSpace(marker))
            {
                throw new ArgumentException("Marker name must be non-empty.", nameof(marker));
            }

            return new CueTrigger(CueTriggerKind.AtMarker, 0.0, marker, default, null, null);
        }

        public static CueTrigger WhenPhraseStarts() =>
            new CueTrigger(CueTriggerKind.OnLearnerAction, 0.0, null, LearnerAction.PhraseStarted, null, null);

        public static CueTrigger WhenChordConfirmed(ChordFunction? function = null) =>
            new CueTrigger(CueTriggerKind.OnLearnerAction, 0.0, null, LearnerAction.ChordConfirmed, function, null);

        public static CueTrigger WhenNotePlayed(int? targetIndex = null) =>
            new CueTrigger(CueTriggerKind.OnLearnerAction, 0.0, null, LearnerAction.MelodyNotePlayed, null, targetIndex);

        public static CueTrigger WhenAttemptCompleted() =>
            new CueTrigger(CueTriggerKind.OnLearnerAction, 0.0, null, LearnerAction.AttemptCompleted, null, null);

        public override string ToString() => Kind switch
        {
            CueTriggerKind.AtBeat => $"@beat {Beat:0.###}",
            CueTriggerKind.AtMarker => $"@marker \"{Marker}\"",
            _ => $"on {Action}"
                 + (Function.HasValue ? $"({Function})" : string.Empty)
                 + (TargetIndex.HasValue ? $"(target {TargetIndex})" : string.Empty),
        };
    }
}
