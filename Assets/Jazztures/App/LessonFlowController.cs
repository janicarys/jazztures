using System;
using System.Collections.Generic;
using Jazztures.Audio;
using Jazztures.Config;
using Jazztures.Core.Evaluation;
using Jazztures.Core.Lessons;
using Jazztures.Core.Menu;
using Jazztures.Core.Ports;
using Jazztures.Core.Tutorial;
using Jazztures.Events;
using Jazztures.Input;
using Jazztures.Lessons;
using Jazztures.Presentation;
using UnityEngine;

namespace Jazztures.App
{
    /// <summary>
    /// Drives the app between the gesture menu and a running lesson (CLAUDE.md §3.9; ADR-0051).
    /// Lives in <c>Jazztures.App</c> because it needs <c>Jazztures.Lessons</c> (the catalog +
    /// runner), <c>Jazztures.Presentation</c> (the menu view) and <c>Jazztures.Input</c> (the
    /// menu's hand driver) — and none of those can see each other.
    ///
    /// <para>
    /// The menu itself is pure domain: <see cref="MenuNavigator"/> holds the screens, the
    /// highlight and the choices, and <see cref="MenuGestureRecognizer"/> turns the right hand into
    /// flick / strike / fist. This class only translates between them and Unity: it builds the
    /// <see cref="MenuModel"/> from the catalog, words the cards, renders them on
    /// <see cref="MenuView"/>, and carries out the <see cref="MenuIntent"/>s the navigator raises —
    /// starting one mode of one lesson (at the tempo the learner chose) or free play. When the
    /// runner reports a mode finished it tells the navigator, which shows the post-run screen.
    /// </para>
    ///
    /// <para>
    /// <b>The tutorial</b> (ADR-0053) runs first, on startup: a <see cref="TutorialRunner"/> walks a
    /// newcomer through putting their hands in view and the three menu gestures, gated by what they
    /// actually do. It runs on the <i>real</i> menu — the lesson menu appears when the learner makes the
    /// fist, a flick really moves the highlight, the last strike really opens a lesson's modes — and
    /// hands over there. Until the menu appears the sky is black; when it does, the sunrise comes up.
    /// </para>
    ///
    /// <para>
    /// The right hand has two jobs and the recogniser is told which: in a menu it flicks, strikes
    /// and fists; while a lesson or free play runs it only fists (held longer), because the hand
    /// is busy playing. The fist is the one way out of a running lesson — the overhead Exit
    /// button it replaced is gone.
    /// </para>
    /// </summary>
    public sealed class LessonFlowController : MonoBehaviour
    {
        [SerializeField] private LessonCatalog _catalog;
        [SerializeField] private LessonRunner _runner;

        [Tooltip("The Lesson HUD — enabled only while a lesson runs.")]
        [SerializeField] private Behaviour _hud;

        [Tooltip("The right-hand melody binder (TouchTargetBinder, or the finger-bend binder on the exploration " +
                 "branch). Enabled only for a lesson whose ActiveHands includes Right (§3.9) — so a left-hand " +
                 "lesson like L1 shows no touch-target markers — and in free play. Disabling it also hides " +
                 "the ten markers.")]
        [SerializeField] private Behaviour _melodyInput;

        [Tooltip("PerformanceCompositionRoot — disabled while a menu is open so left-hand poses don't " +
                 "trigger chords while you're choosing.")]
        [SerializeField] private PerformanceCompositionRoot _performance;

        [Tooltip("The free-play metronome (ADR-0043). Optional — leave empty for a silent free " +
                 "play. Lessons run their own metronome through LessonRunner.")]
        [SerializeField] private FreePlayMetronome _freePlayMetronome;

        [Header("Menu (ADR-0051)")]
        [SerializeField] private MenuView _menuView;

        [Tooltip("Feeds the right hand to the menu recogniser every frame.")]
        [SerializeField] private MenuInputDriver _menuInput;

        [Tooltip("Menu tuning and what the menu offers. Optional — the built-in defaults apply without one.")]
        [SerializeField] private MenuConfig _menuConfig;

        [Tooltip("Where a Test Yourself attempt's score is raised (§3.7). Shown under the post-run menu, " +
                 "after the attempt — never during it. Optional.")]
        [SerializeField] private EvaluationResultChannel _evaluationChannel;

        [Tooltip("Authored lesson captions on their way to the HUD. A caption raised the instant a run ends " +
                 "(a lesson's closing line) would be switched off with the HUD before it was ever drawn, so it " +
                 "is shown under the post-run menu instead. Optional.")]
        [SerializeField] private CueActionChannel _cueChannel;

        [Header("Tutorial (ADR-0053)")]
        [Tooltip("The tutorial script. Optional - leave empty for no tutorial.")]
        [SerializeField] private TutorialDefinition _tutorial;

        [Tooltip("The tutorial's line of instruction. Required for the tutorial to run.")]
        [SerializeField] private TutorialView _tutorialView;

        [Tooltip("The ghost right hand that shows each gesture. Optional.")]
        [SerializeField] private TutorialGhostView _tutorialGhost;

        [Tooltip("The sky: black until the menu opens, then a sunrise. Optional.")]
        [SerializeField] private SkyDome _sky;

        private static readonly double[] DefaultTempoPresets = { 60.0, 70.0, 80.0, 90.0, 100.0 };

        private readonly List<LessonDefinition> _lessons = new List<LessonDefinition>();
        private readonly List<string> _labels = new List<string>();

        private MenuNavigator _navigator;
        private MenuGestureRecognizer _recognizer;
        private IMusicalClock _clock;
        private TutorialRunner _tutorialRunner;
        private float _nextTutorialLog;
        private bool _menuRevealed = true; // false while the tutorial is keeping the lesson menu off screen

        private bool _menuDrawn;
        private MenuScreen _drawnScreen;
        private int _drawnLesson;
        private string _lastResult = string.Empty;
        private string _lastCaption = string.Empty;
        private int _lastCaptionFrame = -1;
        private string _closingCaption = string.Empty;

        private void OnEnable()
        {
            if (_evaluationChannel != null) _evaluationChannel.Register(OnEvaluated);
            if (_cueChannel != null) _cueChannel.Register(OnCue);
        }

        private void OnDisable()
        {
            if (_evaluationChannel != null) _evaluationChannel.Unregister(OnEvaluated);
            if (_cueChannel != null) _cueChannel.Unregister(OnCue);
        }

        private void Start()
        {
            if (_catalog == null || _runner == null || _menuView == null || _menuInput == null)
            {
                Debug.LogError(
                    $"{nameof(LessonFlowController)}: assign catalog, runner, menu view and menu input " +
                    "(Jazztures ▸ Menu ▸ Set Up Gesture Menu does it).", this);
                enabled = false;
                return;
            }

            if (_menuConfig == null)
            {
                Debug.LogWarning(
                    $"{nameof(LessonFlowController)}: no {nameof(MenuConfig)} assigned — using the built-in menu defaults.", this);
            }

            FlattenCatalog();

            MenuGestureThresholds thresholds =
                _menuConfig != null ? _menuConfig.ToThresholds() : MenuGestureThresholds.Default;

            var infos = new List<MenuLessonInfo>(_lessons.Count);
            foreach (LessonDefinition lesson in _lessons)
            {
                infos.Add(new MenuLessonInfo(lesson.Modes, lesson.TempoBpm));
            }

            var model = new MenuModel(
                infos,
                offerFreePlay: _menuConfig == null || _menuConfig.ShowFreePlay,
                tempoPresets: _menuConfig != null ? _menuConfig.TempoPresets() : DefaultTempoPresets);

            _clock = new DspMusicalClock();
            _recognizer = new MenuGestureRecognizer(_clock, thresholds);
            _recognizer.Recognized += OnGesture;
            _menuInput.Bind(_recognizer);

            _navigator = new MenuNavigator(model);
            _navigator.Changed += Render;
            _navigator.Bumped += OnBumped;
            _navigator.IntentRaised += OnIntent;
            _navigator.RunAborted += StopRunSystems;
            _runner.Completed += OnRunCompleted;

            StopRunSystems();

            if (!BeginTutorial())
            {
                if (_sky != null) _sky.RevealImmediately(); // no tutorial: straight to the menu, and the sunrise
                Render();
            }
        }

        /// <summary>
        /// Start the tutorial if it is wanted and possible. Returns false (and the app goes straight to the
        /// menu) when it is switched off in <see cref="MenuConfig"/>, has no script or view, or its script
        /// is not valid.
        /// </summary>
        private bool BeginTutorial()
        {
            if ((_menuConfig != null && !_menuConfig.ShowTutorial) || _tutorial == null || _tutorialView == null)
            {
                return false;
            }

            TutorialPlan plan;
            try
            {
                plan = _tutorial.BuildPlan();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                return false; // never trap a learner behind a broken script
            }

            TutorialThresholds thresholds =
                _menuConfig != null ? _menuConfig.ToTutorialThresholds() : TutorialThresholds.Default;

            _tutorialRunner = new TutorialRunner(plan, _clock, thresholds);
            _tutorialRunner.StepChanged += OnTutorialStep;
            _tutorialRunner.Completed += OnTutorialCompleted;
            _tutorialRunner.TimedOut += OnTutorialTimedOut;

            _menuRevealed = false;
            _menuView.SetControlsHint(false); // the tutorial teaches flick and strike itself, in the hint's own place
            _tutorialView.SetMenuVisible(false);
            if (_sky != null) _sky.SetDark();
            Render();                 // keeps the menu off screen
            _tutorialRunner.Start();  // raises StepChanged(0): the first line
            return true;
        }

        private void OnTutorialTimedOut(int index) =>
            Debug.LogWarning($"[Tutorial] step {index} was not met in time - moving on.", this);

        private void OnTutorialStep(int index, TutorialStep step)
        {
            Debug.Log($"[Tutorial] step {index} ({step.Gate}): {step.Text}", this);
            _tutorialView.Show(step.Text);
            if (_tutorialGhost != null) _tutorialGhost.Play(step.Demo);

            if (step.ShowsMenu && !_menuRevealed)
            {
                RevealMenu(); // the fist worked: the lessons appear, and the sun comes up
            }
        }

        private void OnTutorialCompleted()
        {
            Debug.Log("[Tutorial] complete.", this);
            _tutorialView.Hide();
            if (_tutorialGhost != null) _tutorialGhost.Stop();
            _menuView.SetControlsHint(true); // from here the menu keeps reminding them
            if (!_menuRevealed)
            {
                RevealMenu();
            }
        }

        private void RevealMenu()
        {
            _menuRevealed = true;
            if (_tutorialView != null) _tutorialView.SetMenuVisible(true); // the line moves under the menu
            if (_sky != null) _sky.Reveal();
            Render();
        }

        private void OnDestroy()
        {
            if (_recognizer != null) _recognizer.Recognized -= OnGesture;
            if (_navigator != null)
            {
                _navigator.Changed -= Render;
                _navigator.Bumped -= OnBumped;
                _navigator.IntentRaised -= OnIntent;
                _navigator.RunAborted -= StopRunSystems;
            }

            if (_runner != null) _runner.Completed -= OnRunCompleted;

            if (_tutorialRunner != null)
            {
                _tutorialRunner.StepChanged -= OnTutorialStep;
                _tutorialRunner.Completed -= OnTutorialCompleted;
                _tutorialRunner.TimedOut -= OnTutorialTimedOut;
            }
        }

        private void Update()
        {
            if (_tutorialRunner != null && _tutorialRunner.IsRunning)
            {
                MenuHandFrame frame = _menuInput.LastFrame;
                _tutorialRunner.Feed(frame);
                LogTutorialWait(frame);
            }

            if (_recognizer != null)
            {
                _menuView.SetHoldProgress(_recognizer.FistProgress);
            }
        }

        /// <summary>
        /// The catalog groups lessons into sessions (§3.9), but the menu lists lessons flat, in
        /// session order — the sessions stay in the catalog, so the grouping can come back.
        /// </summary>
        private void FlattenCatalog()
        {
            _lessons.Clear();
            foreach (LessonCatalog.Session session in _catalog.Sessions)
            {
                foreach (LessonDefinition lesson in session.lessons)
                {
                    if (lesson == null)
                    {
                        Debug.LogWarning($"{nameof(LessonFlowController)}: a null lesson in '{session.title}' was skipped.", this);
                    }
                    else if (lesson.Modes.Count == 0)
                    {
                        Debug.LogWarning($"{nameof(LessonFlowController)}: '{lesson.Title}' lists no modes and was skipped.", this);
                    }
                    else
                    {
                        _lessons.Add(lesson);
                    }
                }
            }
        }

        // --- Input -------------------------------------------------------------------------

        private void OnGesture(MenuGesture gesture)
        {
            // During the tutorial the real menu is live, but a step lets through only the gestures taught so
            // far: a stray strike while the learner is still learning to flick must not pick a lesson.
            TutorialForward forward = TutorialForward.Flick | TutorialForward.Strike;
            if (_tutorialRunner != null && _tutorialRunner.IsRunning)
            {
                forward = _tutorialRunner.CurrentStep.Value.Forward;

                // "Strike to select a lesson": a strike that would land on Free play (a flick can reach it) is
                // not that, and is ignored rather than quietly starting free play.
                if (gesture == MenuGesture.Strike && _menuRevealed && !SelectedIsLesson())
                {
                    return;
                }

                _tutorialRunner.Notify(gesture); // may complete the step - or the tutorial
            }

            bool isFlick = gesture == MenuGesture.FlickLeft || gesture == MenuGesture.FlickRight;
            if ((isFlick && (forward & TutorialForward.Flick) == 0)
                || (gesture == MenuGesture.Strike && (forward & TutorialForward.Strike) == 0))
            {
                return;
            }

            // Answer a strike on the card that was struck, before the screen (if any) changes.
            if (gesture == MenuGesture.Strike && !_navigator.IsRunning)
            {
                _menuView.Pulse();
            }

            _navigator.Handle(gesture);
        }

        /// <summary>
        /// Once a second while the tutorial waits on the learner, say what it can see - so a step that will not
        /// advance can be read off <c>adb logcat</c> (hand quality and angle off the gaze, fist, openness, thumb).
        /// Rare and small; kept in release builds on purpose.
        /// </summary>
        private void LogTutorialWait(MenuHandFrame f)
        {
            if (Time.unscaledTime < _nextTutorialLog)
            {
                return;
            }

            _nextTutorialLog = Time.unscaledTime + 1f;
            Debug.Log(
                $"[Tutorial] waiting at step {_tutorialRunner.StepIndex}: right {f.Tracking} @{f.ViewAngleDegrees:0} deg, " +
                $"left {f.LeftTracking} @{f.LeftViewAngleDegrees:0} deg, fist {f.FistAmount:0.00}, " +
                $"most closed {f.MaxFingerClosure:0.00}, thumb {f.ThumbUpAngleDegrees:0} deg from up.", this);
        }

        private bool SelectedIsLesson() =>
            _navigator.Items.Count > 0 && _navigator.Items[_navigator.Selected].Kind == MenuItemKind.Lesson;

        private void OnBumped(int direction) => _menuView.Bump(direction);

        // --- Rendering ---------------------------------------------------------------------

        private void Render()
        {
            if (!_menuRevealed)
            {
                // The tutorial has not opened the menu yet: nothing on screen but its own line.
                _menuView.Hide();
                _menuDrawn = false;
                SetInputContext(MenuInputContext.Menu);
                return;
            }

            if (_navigator.IsRunning)
            {
                _menuView.Hide();
                _menuDrawn = false;
                SetInputContext(MenuInputContext.Running);
                return;
            }

            SetInputContext(MenuInputContext.Menu);

            _labels.Clear();
            for (int i = 0; i < _navigator.Items.Count; i++)
            {
                _labels.Add(LabelOf(_navigator.Items[i]));
            }

            // A new screen fades and drifts in the direction the learner went; the same screen
            // redrawn (a flick, the tempo card changing) lets the row glide instead.
            MenuTransition transition = MenuTransition.None;
            if (!_menuDrawn)
            {
                // Appearing from nothing: coming home reads as arriving from above, anything else as going in.
                transition = _navigator.Screen == MenuScreen.Main ? MenuTransition.Shallower : MenuTransition.Deeper;
            }
            else if (_drawnScreen != _navigator.Screen || _drawnLesson != _navigator.LessonIndex)
            {
                transition = DepthOf(_navigator.Screen) >= DepthOf(_drawnScreen)
                    ? MenuTransition.Deeper
                    : MenuTransition.Shallower;
            }

            _menuDrawn = true;
            _drawnScreen = _navigator.Screen;
            _drawnLesson = _navigator.LessonIndex;

            _menuView.Show(_labels, _navigator.Selected, TitleOf(_navigator.Screen), FooterOf(_navigator.Screen), transition);
        }

        /// <summary>How far in the tree a screen is. Going to a deeper (or equally deep, e.g. the next lesson) one is "in".</summary>
        private static int DepthOf(MenuScreen screen)
        {
            switch (screen)
            {
                case MenuScreen.Main:
                    return 0;
                case MenuScreen.Modes:
                    return 1;
                default:
                    return 2;
            }
        }

        private void SetInputContext(MenuInputContext context)
        {
            if (_recognizer.Context != context)
            {
                _recognizer.SetContext(context);
            }
        }

        private string LabelOf(MenuItem item)
        {
            switch (item.Kind)
            {
                case MenuItemKind.Lesson:
                    return $"{item.LessonIndex + 1}. {_lessons[item.LessonIndex].Title}";
                case MenuItemKind.FreePlay:
                    return $"{_lessons.Count + 1}. Free play";
                case MenuItemKind.Mode:
                    return $"{ModeNumber(item)}. {ModeName(item.Mode.GetValueOrDefault())}";
                case MenuItemKind.Tempo:
                    return $"Tempo\n{_navigator.TempoBpm(item.LessonIndex):0} BPM";
                case MenuItemKind.Back:
                    return "Back";
                case MenuItemKind.BackToModes:
                    return "Back to modes";
                case MenuItemKind.Restart:
                    return "Restart";
                case MenuItemKind.Next:
                    return item.Mode.HasValue ? $"Next\n{ModeName(item.Mode.Value)}" : "Next lesson";
                default:
                    return string.Empty;
            }
        }

        private string TitleOf(MenuScreen screen)
        {
            int lesson = _navigator.LessonIndex;
            switch (screen)
            {
                case MenuScreen.Main:
                    return "Choose a lesson";
                case MenuScreen.Modes:
                    return lesson >= 0 ? _lessons[lesson].Title : string.Empty;
                default:
                    return lesson >= 0 ? $"{_lessons[lesson].Title} · {ModeName(_navigator.RunMode)}" : string.Empty;
            }
        }

        /// <summary>
        /// The lesson and mode screens say, under the row, what the highlighted card is. The post-run screen
        /// reports what the run ended on instead: the lesson's closing line, if it had one, and a Test
        /// Yourself score (§3.7) — both once it is over, never during.
        /// </summary>
        private string FooterOf(MenuScreen screen)
        {
            if (screen != MenuScreen.PostRun)
            {
                return _navigator.Items.Count > 0 ? DescriptionOf(_navigator.Items[_navigator.Selected]) : string.Empty;
            }

            if (_closingCaption.Length > 0 && _lastResult.Length > 0)
            {
                return _closingCaption + "\n" + _lastResult;
            }

            return _closingCaption.Length > 0 ? _closingCaption : _lastResult;
        }

        /// <summary>
        /// Learner-facing names (§1.5 — no jargon). These are the card labels; the HUD's own phase
        /// banner words an instruction ("Your turn") rather than a choice.
        /// </summary>
        private static string ModeName(LearningMode mode)
        {
            switch (mode)
            {
                case LearningMode.GestureLearning:
                    return "Learn the shapes";
                case LearningMode.WatchAndListen:
                    return "Watch and listen";
                case LearningMode.TryYourself:
                    return "Try yourself";
                case LearningMode.TestYourself:
                    return "Test yourself";
                case LearningMode.ComposeOnTheFly:
                    return "Play freely";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// A mode's place among its lesson's modes, from 1 — the order they are meant to be worked through
        /// (§3.8). Numbered from the lesson's own list, not the card's position, so the Tempo and Back cards
        /// after the modes never shift it.
        /// </summary>
        private int ModeNumber(MenuItem item)
        {
            IReadOnlyList<LearningMode> modes = _lessons[item.LessonIndex].Modes;
            for (int i = 0; i < modes.Count; i++)
            {
                if (modes[i] == item.Mode)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// What the highlighted card is, in the words of someone who has never played (§1.5). Shown under the
        /// row, so a learner can tell what a lesson or mode is before choosing it. A lesson brings its own
        /// line (<see cref="LessonDefinition.MenuDescription"/>); the rest are the same for every lesson.
        /// </summary>
        private string DescriptionOf(MenuItem item)
        {
            switch (item.Kind)
            {
                case MenuItemKind.Lesson:
                    return _lessons[item.LessonIndex].MenuDescription;
                case MenuItemKind.FreePlay:
                    return "Play freely with both hands. No lesson, nothing to match.";
                case MenuItemKind.Mode:
                    return ModeDescription(item.Mode.GetValueOrDefault(), _lessons[item.LessonIndex].Hands);
                case MenuItemKind.Tempo:
                    return "Strike to change the speed. A bigger number is faster.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// A mode in one or two short sentences. Try Yourself is worded for the hand the learner plays: on a
        /// right-hand lesson the chords play for them and the notes are theirs (ADR-0043 decision 3), where on
        /// a left-hand one they make the shapes themselves. Kept to about 80 characters — two lines under the
        /// row, clear of the tutorial's line below it.
        /// </summary>
        private static string ModeDescription(LearningMode mode, ActiveHands hands)
        {
            bool rightHandOnly = (hands & ActiveHands.Left) == 0 && (hands & ActiveHands.Right) != 0;

            switch (mode)
            {
                case LearningMode.GestureLearning:
                    return "Practise the hand shapes. There is nothing to match yet.";
                case LearningMode.WatchAndListen:
                    return "The lesson plays itself. Watch and listen. Nothing you play is heard.";
                case LearningMode.TryYourself:
                    return rightHandOnly
                        ? "The chords play for you. Curl each finger as its letter arrives."
                        : "Copy the ghost hand. It waits until you match each shape.";
                case LearningMode.TestYourself:
                    return "No guide this time. Play it yourself, then see how you did.";
                case LearningMode.ComposeOnTheFly:
                    return "Make it up as you go. There is nothing to match.";
                default:
                    return string.Empty;
            }
        }

        // --- Runs --------------------------------------------------------------------------

        private void OnIntent(MenuIntent intent)
        {
            if (intent.Kind == MenuIntentKind.StartFreePlay)
            {
                StartFreePlay();
                return;
            }

            if (intent.LessonIndex < 0 || intent.LessonIndex >= _lessons.Count)
            {
                _navigator.GoHome();
                return;
            }

            StartLesson(_lessons[intent.LessonIndex], intent.Mode, intent.TempoBpm);
        }

        private void StartLesson(LessonDefinition lesson, LearningMode mode, double tempoBpm)
        {
            _lastResult = string.Empty;
            _closingCaption = string.Empty;

            if (_freePlayMetronome != null) _freePlayMetronome.End();

            // HUD + performance on first, so they catch the lesson's opening phase/cue events.
            if (_hud != null) _hud.enabled = true;
            if (_performance != null) _performance.enabled = true;

            try
            {
                _runner.LoadLesson(lesson, mode, tempoBpm);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                _navigator.GoHome(); // never leave the learner on a blank screen
                return;
            }

            // Right-hand melody surfaces only for a lesson that uses the right hand.
            bool usesRightHand = (_runner.ActiveHands & ActiveHands.Right) != 0;
            if (_melodyInput != null) _melodyInput.enabled = usesRightHand;
        }

        /// <summary>
        /// The sandbox (§3.8 — Compose-on-the-Fly): both hands live, no ghost, no HUD, no lesson
        /// structure. Runs until the learner makes a fist.
        /// </summary>
        private void StartFreePlay()
        {
            _runner.StopLesson(); // no lesson; StopLesson also frees the note gate

            if (_hud != null) _hud.enabled = false;
            if (_performance != null) _performance.enabled = true;
            if (_melodyInput != null) _melodyInput.enabled = true; // both hands
            if (_freePlayMetronome != null) _freePlayMetronome.Begin();
        }

        private void OnRunCompleted()
        {
            if (!_navigator.IsRunning)
            {
                return;
            }

            // A caption raised in this same frame is the lesson's closing line (the cue that fires when
            // its last phase ends): the HUD is about to be switched off, so keep it for the menu.
            _closingCaption = Time.frameCount == _lastCaptionFrame ? _lastCaption : string.Empty;

            StopRunSystems();
            _navigator.RunCompleted(); // shows the post-run menu
        }

        private void OnCue(CueAction action)
        {
            if (action.Kind == CueActionKind.ShowText)
            {
                _lastCaption = action.Text ?? string.Empty;
                _lastCaptionFrame = Time.frameCount;
            }
        }

        private void OnEvaluated(AttemptResult result)
        {
            _lastResult = result.IsEmpty
                ? string.Empty
                : $"{result.OnTimeCount} on time · {result.CloseCount} close · {result.OffCount} off";
        }

        /// <summary>Everything a lesson or free play switches on, switched off — the menu is the idle state.</summary>
        private void StopRunSystems()
        {
            if (_runner != null) _runner.StopLesson();
            if (_hud != null) _hud.enabled = false;
            if (_melodyInput != null) _melodyInput.enabled = false; // also hides the target markers
            if (_performance != null) _performance.enabled = false;
            if (_freePlayMetronome != null) _freePlayMetronome.End();
        }
    }
}
