using Jazztures.Core.Lessons;

namespace Jazztures.Core.Menu
{
    /// <summary>
    /// A request raised by <see cref="MenuNavigator"/> for the app to act on (ADR-0051): the
    /// menu decides <i>what</i>, the app owns <i>how</i>. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct MenuIntent
    {
        public MenuIntent(MenuIntentKind kind, int lessonIndex, LearningMode mode, double tempoBpm)
        {
            Kind = kind;
            LessonIndex = lessonIndex;
            Mode = mode;
            TempoBpm = tempoBpm;
        }

        public MenuIntentKind Kind { get; }

        /// <summary>Index into the lesson list the model was built from; −1 for free play.</summary>
        public int LessonIndex { get; }

        /// <summary>The mode to run. Meaningful only for <see cref="MenuIntentKind.StartLesson"/>.</summary>
        public LearningMode Mode { get; }

        /// <summary>The tempo to run the lesson at — the lesson's own unless the learner stepped it. 0 for free play.</summary>
        public double TempoBpm { get; }

        public override string ToString() =>
            Kind == MenuIntentKind.StartFreePlay
                ? "StartFreePlay"
                : $"StartLesson(L{LessonIndex}, {Mode}, {TempoBpm:0.##} BPM)";
    }
}
