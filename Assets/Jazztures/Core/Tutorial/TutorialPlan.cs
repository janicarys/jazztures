using System;
using System.Collections.Generic;

namespace Jazztures.Core.Tutorial
{
    /// <summary>
    /// The tutorial script as the domain sees it (ADR-0053): an ordered list of
    /// <see cref="TutorialStep"/>s. The pure form of a Unity <c>TutorialDefinition</c> asset, as
    /// <see cref="Jazztures.Core.Lessons.LessonPlan"/> is of a lesson. Immutable.
    /// </summary>
    public sealed class TutorialPlan
    {
        private readonly TutorialStep[] _steps;

        public TutorialPlan(IEnumerable<TutorialStep> steps)
        {
            if (steps == null)
            {
                throw new ArgumentNullException(nameof(steps));
            }

            _steps = new List<TutorialStep>(steps).ToArray();
            if (_steps.Length == 0)
            {
                throw new ArgumentException("A tutorial needs at least one step.", nameof(steps));
            }
        }

        public IReadOnlyList<TutorialStep> Steps => _steps;

        public int Count => _steps.Length;

        public TutorialStep this[int index] => _steps[index];
    }
}
