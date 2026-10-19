using System;
using Jazztures.Core.Lessons;

namespace Jazztures.Core.Menu
{
    /// <summary>
    /// One card on a menu screen (ADR-0051). Carries what choosing it means, not how it is
    /// worded — labels belong to the presentation side, which knows the lesson titles.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct MenuItem : IEquatable<MenuItem>
    {
        public MenuItem(MenuItemKind kind, int lessonIndex = -1, LearningMode? mode = null)
        {
            Kind = kind;
            LessonIndex = lessonIndex;
            Mode = mode;
        }

        public MenuItemKind Kind { get; }

        /// <summary>
        /// The lesson this card belongs to or leads to; −1 when it names none (Free play, Back).
        /// For <see cref="MenuItemKind.Next"/> it is the lesson the card leads to.
        /// </summary>
        public int LessonIndex { get; }

        /// <summary>
        /// The mode this card starts, for <see cref="MenuItemKind.Mode"/> and for a
        /// <see cref="MenuItemKind.Next"/> that stays within the lesson. A <c>Next</c> that
        /// moves to another lesson has none: it opens that lesson's mode screen.
        /// </summary>
        public LearningMode? Mode { get; }

        public bool Equals(MenuItem other) =>
            Kind == other.Kind && LessonIndex == other.LessonIndex && Mode == other.Mode;

        public override bool Equals(object? obj) => obj is MenuItem other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = (hash * 397) ^ LessonIndex;
                hash = (hash * 397) ^ (Mode.HasValue ? (int)Mode.Value + 1 : 0);
                return hash;
            }
        }

        public override string ToString() =>
            Mode.HasValue ? $"{Kind}(L{LessonIndex}, {Mode.Value})" : $"{Kind}(L{LessonIndex})";
    }
}
