using System;
using System.Collections.Generic;
using Jazztures.Core.Lessons;

namespace Jazztures.Core.Menu
{
    /// <summary>
    /// The menu as a state machine (ADR-0051). Pure — it holds no strings and no Unity types: it
    /// knows which cards a screen has and which is highlighted, moves the highlight on a flick,
    /// and on a strike either changes screen or raises a <see cref="MenuIntent"/> for the app to
    /// carry out. The app builds a <see cref="MenuModel"/> from its lesson catalog, renders
    /// <see cref="Items"/> whenever <see cref="Changed"/> fires, and feeds it
    /// <see cref="MenuGesture"/>s.
    ///
    /// <para>The tree:</para>
    /// <code>
    /// Main        lesson · lesson · … · Free play
    ///   Modes     mode · mode · … · Tempo · Back          (one lesson)
    ///     — a mode is chosen: the menu steps aside and the run starts —
    ///   PostRun   Back to modes · Restart · Next           (when a run finishes)
    /// </code>
    /// <para>
    /// A fist is "go to Main" from anywhere, including mid-run. The highlight wraps around at the
    /// ends (flick past the last card and you are on the first), and returning to Main puts it back on
    /// the card the learner came from.
    /// </para>
    ///
    /// <para>
    /// Direction rule (§2.3): presentation reads this; it never writes to it. Lesson content is
    /// not here — the app starts the run when it sees an intent and tells the navigator when it
    /// has finished (<see cref="RunCompleted"/>); the navigator never starts anything itself.
    /// </para>
    /// </summary>
    public sealed class MenuNavigator
    {
        private readonly MenuModel _model;
        private readonly double[] _tempo;
        private readonly List<MenuItem> _items = new List<MenuItem>();

        private MenuScreen _screen;
        private int _selected;
        private int _lessonIndex = -1;

        private bool _running;
        private bool _runIsFreePlay;
        private int _runLesson = -1;
        private LearningMode _runMode;

        public MenuNavigator(MenuModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));

            _tempo = new double[model.Lessons.Count];
            for (int i = 0; i < _tempo.Length; i++)
            {
                _tempo[i] = model.Lessons[i].TempoBpm;
            }

            ShowMain(0);
        }

        /// <summary>The screen, the highlight or a card's meaning changed — redraw.</summary>
        public event Action? Changed;

        /// <summary>
        /// A flick was recognised but the highlight cannot move (a one-card row; any longer row wraps), so nothing
        /// moved (+1 = toward later cards, −1 = toward earlier). Lets the view show "heard you,
        /// nowhere to go" rather than leave the learner wondering whether the flick registered.
        /// </summary>
        public event Action<int>? Bumped;

        /// <summary>The learner chose something that starts a run. Raised after <see cref="Changed"/>.</summary>
        public event Action<MenuIntent>? IntentRaised;

        /// <summary>The learner left a running lesson or free play (fist). Raised before <see cref="Changed"/>.</summary>
        public event Action? RunAborted;

        /// <summary>
        /// The current screen. While <see cref="IsRunning"/> it is the screen the run was started
        /// from, and nothing should be drawn.
        /// </summary>
        public MenuScreen Screen => _screen;

        /// <summary>True from the moment a run is chosen until it completes or is abandoned.</summary>
        public bool IsRunning => _running;

        /// <summary>The cards on the current screen, left to right.</summary>
        public IReadOnlyList<MenuItem> Items => _items;

        /// <summary>Index into <see cref="Items"/> of the highlighted card.</summary>
        public int Selected => _selected;

        /// <summary>The lesson the Modes/PostRun screen (or the running lesson) belongs to; −1 on Main and in free play.</summary>
        public int LessonIndex => _lessonIndex;

        /// <summary>The mode just run or running. Meaningful on PostRun and while a lesson runs.</summary>
        public LearningMode RunMode => _runMode;

        /// <summary>The tempo a lesson will run at: its own, unless the learner stepped it.</summary>
        public double TempoBpm(int lessonIndex) => _tempo[lessonIndex];

        public void Handle(MenuGesture gesture)
        {
            if (gesture == MenuGesture.Fist)
            {
                GoHome();
                return;
            }

            if (_running)
            {
                return; // the right hand is playing; only the fist means anything
            }

            switch (gesture)
            {
                // Swipe convention (ADR-0051): the row follows the hand, so flicking left pulls the
                // next card in from the right and flicking right brings the previous one back.
                case MenuGesture.FlickLeft:
                    Move(+1);
                    break;

                case MenuGesture.FlickRight:
                    Move(-1);
                    break;

                case MenuGesture.Strike:
                    Select();
                    break;
            }
        }

        /// <summary>
        /// Go to the Main screen, abandoning a run if one is going. A no-op when already there.
        /// The app also calls this if a run it was asked to start could not start.
        /// </summary>
        public void GoHome()
        {
            bool wasRunning = _running;
            if (!wasRunning && _screen == MenuScreen.Main)
            {
                return;
            }

            int land = wasRunning
                ? (_runIsFreePlay ? _model.Lessons.Count : _runLesson)
                : _lessonIndex;

            _running = false;
            _runIsFreePlay = false;
            ShowMain(land);

            if (wasRunning)
            {
                RunAborted?.Invoke();
            }

            Changed?.Invoke();
        }

        /// <summary>The running lesson finished its mode. Shows the post-run screen. Ignored otherwise.</summary>
        public void RunCompleted()
        {
            if (!_running || _runIsFreePlay)
            {
                return;
            }

            _running = false;
            ShowPostRun();
            Changed?.Invoke();
        }

        private void Move(int delta)
        {
            int count = _items.Count;
            if (count == 0)
            {
                return;
            }

            if (count == 1)
            {
                Bumped?.Invoke(delta); // the only card: nowhere to go, but the flick was heard
                return;
            }

            _selected = ((_selected + delta) % count + count) % count; // wraps in both directions
            Changed?.Invoke();
        }

        private void Select()
        {
            if (_selected < 0 || _selected >= _items.Count)
            {
                return;
            }

            MenuItem item = _items[_selected];
            switch (item.Kind)
            {
                case MenuItemKind.Lesson:
                    ShowModes(item.LessonIndex, null);
                    Changed?.Invoke();
                    break;

                case MenuItemKind.FreePlay:
                    BeginFreePlay();
                    break;

                case MenuItemKind.Mode:
                    BeginRun(item.LessonIndex, item.Mode!.Value);
                    break;

                case MenuItemKind.Tempo:
                    CycleTempo(item.LessonIndex);
                    Changed?.Invoke();
                    break;

                case MenuItemKind.Back:
                    ShowMain(_lessonIndex);
                    Changed?.Invoke();
                    break;

                case MenuItemKind.BackToModes:
                    ShowModes(item.LessonIndex, _runMode);
                    Changed?.Invoke();
                    break;

                case MenuItemKind.Restart:
                    BeginRun(item.LessonIndex, item.Mode!.Value);
                    break;

                case MenuItemKind.Next:
                    if (item.Mode.HasValue)
                    {
                        BeginRun(item.LessonIndex, item.Mode.Value);
                    }
                    else
                    {
                        ShowModes(item.LessonIndex, null);
                        Changed?.Invoke();
                    }

                    break;
            }
        }

        private void BeginRun(int lesson, LearningMode mode)
        {
            _running = true;
            _runIsFreePlay = false;
            _runLesson = lesson;
            _runMode = mode;
            _lessonIndex = lesson;

            Changed?.Invoke();
            IntentRaised?.Invoke(new MenuIntent(MenuIntentKind.StartLesson, lesson, mode, _tempo[lesson]));
        }

        private void BeginFreePlay()
        {
            _running = true;
            _runIsFreePlay = true;
            _runLesson = -1;
            _lessonIndex = -1;

            Changed?.Invoke();
            IntentRaised?.Invoke(new MenuIntent(MenuIntentKind.StartFreePlay, -1, LearningMode.ComposeOnTheFly, 0.0));
        }

        private void ShowMain(int preferred)
        {
            _screen = MenuScreen.Main;
            _lessonIndex = -1;
            _items.Clear();

            for (int i = 0; i < _model.Lessons.Count; i++)
            {
                _items.Add(new MenuItem(MenuItemKind.Lesson, i));
            }

            if (_model.OfferFreePlay)
            {
                _items.Add(new MenuItem(MenuItemKind.FreePlay));
            }

            _selected = _items.Count == 0 ? 0 : Math.Max(0, Math.Min(_items.Count - 1, preferred));
        }

        private void ShowModes(int lesson, LearningMode? highlight)
        {
            _screen = MenuScreen.Modes;
            _lessonIndex = lesson;
            _items.Clear();
            _selected = 0;

            IReadOnlyList<LearningMode> modes = _model.Lessons[lesson].Modes;
            for (int i = 0; i < modes.Count; i++)
            {
                _items.Add(new MenuItem(MenuItemKind.Mode, lesson, modes[i]));
                if (highlight.HasValue && modes[i] == highlight.Value && _selected == 0)
                {
                    _selected = i;
                }
            }

            if (_model.OffersTempo)
            {
                _items.Add(new MenuItem(MenuItemKind.Tempo, lesson));
            }

            _items.Add(new MenuItem(MenuItemKind.Back, lesson));
        }

        private void ShowPostRun()
        {
            _screen = MenuScreen.PostRun;
            _lessonIndex = _runLesson;
            _items.Clear();
            _selected = 0; // the safe card: a hand still dropping from the last phrase must not start anything

            _items.Add(new MenuItem(MenuItemKind.BackToModes, _runLesson));
            _items.Add(new MenuItem(MenuItemKind.Restart, _runLesson, _runMode));

            MenuItem? next = NextAfter(_runLesson, _runMode);
            if (next.HasValue)
            {
                _items.Add(next.Value);
            }
        }

        /// <summary>The next mode of the lesson, else the next lesson; none after the last mode of the last lesson.</summary>
        private MenuItem? NextAfter(int lesson, LearningMode mode)
        {
            IReadOnlyList<LearningMode> modes = _model.Lessons[lesson].Modes;

            int at = -1;
            for (int i = 0; i < modes.Count; i++)
            {
                if (modes[i] == mode)
                {
                    at = i;
                    break;
                }
            }

            if (at >= 0 && at + 1 < modes.Count)
            {
                return new MenuItem(MenuItemKind.Next, lesson, modes[at + 1]);
            }

            if (lesson + 1 < _model.Lessons.Count)
            {
                return new MenuItem(MenuItemKind.Next, lesson + 1);
            }

            return null;
        }

        private void CycleTempo(int lesson)
        {
            IReadOnlyList<double> presets = _model.TempoPresets;
            if (presets.Count == 0)
            {
                return;
            }

            double current = _tempo[lesson];
            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i] > current + 1e-6)
                {
                    _tempo[lesson] = presets[i];
                    return;
                }
            }

            _tempo[lesson] = presets[0]; // past the fastest: wrap to the slowest
        }
    }
}
