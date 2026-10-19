using System;
using System.Collections.Generic;
using Jazztures.Core.Lessons;

namespace Jazztures.Core.Menu
{
    /// <summary>
    /// What the menu needs to know about one lesson (ADR-0051) — not the lesson, which lives in
    /// a Unity asset the domain cannot see: just which modes it offers and its authored tempo.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct MenuLessonInfo
    {
        private readonly LearningMode[]? _modes;

        public MenuLessonInfo(IEnumerable<LearningMode> modes, double tempoBpm)
        {
            if (modes == null)
            {
                throw new ArgumentNullException(nameof(modes));
            }

            if (double.IsNaN(tempoBpm) || double.IsInfinity(tempoBpm) || tempoBpm <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(tempoBpm), tempoBpm, "Tempo must be finite and positive.");
            }

            _modes = new List<LearningMode>(modes).ToArray();
            if (_modes.Length == 0)
            {
                throw new ArgumentException("A lesson needs at least one mode.", nameof(modes));
            }

            TempoBpm = tempoBpm;
        }

        /// <summary>The modes the lesson offers, in the order they are listed and stepped through.</summary>
        public IReadOnlyList<LearningMode> Modes => _modes ?? Array.Empty<LearningMode>();

        /// <summary>The lesson's authored tempo, in BPM.</summary>
        public double TempoBpm { get; }
    }
}
