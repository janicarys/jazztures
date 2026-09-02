using System;
using System.Collections.Generic;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// The pure form of a Unity <c>LessonDefinition</c> asset (CLAUDE.md §3.9): identity,
    /// the novice-facing concept text, the ordered mode phases the lesson runs through,
    /// and the tempo / swing / hands it sets. The musical <see cref="LessonTimeline"/> and
    /// the <see cref="LessonScript"/> are attached alongside by the runner.
    ///
    /// <para>Immutable. Adding Lesson 9 is data only — no C# change (§3.9).</para>
    /// </summary>
    public sealed class LessonPlan
    {
        private readonly LearningMode[] _modes;

        public LessonPlan(
            LessonId id,
            string title,
            string conceptExplanation,
            IEnumerable<LearningMode> modes,
            Tempo tempo,
            SwingRatio swing,
            ActiveHands hands)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Lesson title must be non-empty.", nameof(title));
            }

            if (conceptExplanation == null)
            {
                throw new ArgumentNullException(nameof(conceptExplanation));
            }

            if (modes == null)
            {
                throw new ArgumentNullException(nameof(modes));
            }

            if (hands == ActiveHands.None)
            {
                throw new ArgumentException("A lesson must exercise at least one hand.", nameof(hands));
            }

            _modes = new List<LearningMode>(modes).ToArray();
            if (_modes.Length == 0)
            {
                throw new ArgumentException("A lesson needs at least one mode phase.", nameof(modes));
            }

            Id = id;
            Title = title;
            ConceptExplanation = conceptExplanation;
            Tempo = tempo;
            Swing = swing;
            Hands = hands;
        }

        public LessonId Id { get; }

        public string Title { get; }

        /// <summary>Plain-language theory, shown alongside the exercise (co-design ask, §3.9).</summary>
        public string ConceptExplanation { get; }

        /// <summary>The mode phases, in the order the lesson steps through them (§3.8/§3.9).</summary>
        public IReadOnlyList<LearningMode> Modes => _modes;

        public Tempo Tempo { get; }

        public SwingRatio Swing { get; }

        public ActiveHands Hands { get; }

        public override string ToString() => $"{Id} \"{Title}\" [{string.Join(" -> ", _modes)}]";
    }
}
