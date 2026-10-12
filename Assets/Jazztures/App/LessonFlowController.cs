using System.Collections.Generic;
using Jazztures.Core.Lessons;
using Jazztures.Lessons;
using Jazztures.Presentation;
using UnityEngine;

namespace Jazztures.App
{
    /// <summary>
    /// Drives the app between the lesson selector and a running lesson (CLAUDE.md §3.9).
    /// Lives in <c>Jazztures.App</c> because it needs both <c>Jazztures.Lessons</c> (the
    /// catalog + runner) and <c>Jazztures.Presentation</c> (the selector view) — and
    /// <c>Presentation</c> cannot see <c>Lessons</c>.
    ///
    /// <para>
    /// Two levels: sessions → lessons (§3.9's S1 = L1+L2+L3 …). Choosing a lesson hides the
    /// selector, enables the HUD and gesture pipeline, re-binds the <see cref="LessonRunner"/>,
    /// and enables the right-hand melody surfaces only when the lesson's <c>ActiveHands</c>
    /// includes Right — a left-hand lesson (L1) shows no touch-target markers. On completion
    /// the selector returns to that session's lesson list.
    /// </para>
    ///
    /// <para>
    /// Beside the sessions sit two tester tools (ADR-0043): <b>Free play</b>, and a
    /// <b>Timing test</b> menu of drill presets run by <see cref="TimingDrillRunner"/>, whose
    /// first row switches the right-hand mechanic so one headset session can run the same
    /// drill on each. A drill's result is shown after the run, under the menu (§3.7).
    /// </para>
    /// </summary>
    public sealed class LessonFlowController : MonoBehaviour
    {
        /// <summary>The right-hand mechanics that exist today; the ladder's triggers join them at ADR-0043 step 7.</summary>
        private enum RightHandMechanic
        {
            ArcTouch = 0,
            ArcPinch = 1,
        }

        [SerializeField] private LessonCatalog _catalog;
        [SerializeField] private LessonRunner _runner;
        [SerializeField] private LessonSelector _selector;

        [Tooltip("The Lesson HUD — enabled only while a lesson runs.")]
        [SerializeField] private Behaviour _hud;

        [Tooltip("The right-hand melody binder (TouchTargetBinder). Enabled only for a lesson " +
                 "whose ActiveHands includes Right (§3.9) — so a left-hand lesson like L1 shows " +
                 "no touch-target markers. Disabling it also hides the ten markers. The Timing " +
                 "test's mechanic row switches its trigger at runtime.")]
        [SerializeField] private Behaviour _melodyInput;

        [Tooltip("PerformanceCompositionRoot — disabled on the selector screen so left-hand " +
                 "poses don't trigger chords while you're choosing a lesson. (Disabling the " +
                 "melody binder also hides the ten touch-target markers.)")]
        [SerializeField] private PerformanceCompositionRoot _performance;

        [Tooltip("The overhead Exit control. Shown during any lesson, in free play and during a " +
                 "timing drill; a pinch on it returns to the selector.")]
        [SerializeField] private ExitButton _exitButton;

        [Tooltip("The free-play metronome (ADR-0043). Optional — leave empty for a silent free " +
                 "play. Lessons run their own metronome through LessonRunner.")]
        [SerializeField] private FreePlayMetronome _freePlayMetronome;

        [Tooltip("Show the free-play click's +/- tempo buttons beside the session menu. A testing " +
                 "affordance (ADR-0043), not part of the learner's task — untick to remove it " +
                 "from the menu, e.g. in a study build (§3.10: visual restraint).")]
        [SerializeField] private bool _showTempoControl = true;

        [Tooltip("Runs the timing drills (ADR-0043). Optional — leave empty, or leave the catalog's " +
                 "drill list empty, to have no \"Timing test\" row. A testing affordance, not part " +
                 "of the learner's task: remove it for a study build (§3.10).")]
        [SerializeField] private TimingDrillRunner _drillRunner;

        private const string BackRow = "←  Back";
        private const string FreePlayRow = "Free play";
        private const string TimingTestRow = "Timing test";
        private const string TimingTitle = "Timing test";

        private int _session = -1;
        private bool _freePlay;
        private bool _inTimingMenu;
        private bool _inDrill;
        private string _lastTimingResult = string.Empty;

        private RightHandMechanic _mechanic = RightHandMechanic.ArcTouch;
        private bool _defaultPinchSpike;
        private bool _warnedNoBinder;

        private bool HasTimingTest =>
            _drillRunner != null && _catalog != null && _catalog.TimingDrills.Count > 0;

        private void Awake()
        {
            if (_selector != null) _selector.Chosen += OnChosen;
            if (_selector != null) _selector.SideStepped += OnTempoStepped;
            if (_runner != null) _runner.Completed += OnLessonCompleted;
            if (_exitButton != null) _exitButton.Exited += OnExitPressed;
            if (_drillRunner != null) _drillRunner.Finished += OnDrillFinished;
        }

        private void Start()
        {
            if (_catalog == null || _selector == null || _runner == null)
            {
                Debug.LogError($"{nameof(LessonFlowController)}: assign catalog, selector and runner.", this);
                enabled = false;
                return;
            }

            if (_melodyInput is TouchTargetBinder binder)
            {
                _defaultPinchSpike = binder.PinchSpike;
            }

            ShowSessions();
        }

        private void OnDestroy()
        {
            if (_selector != null) _selector.Chosen -= OnChosen;
            if (_selector != null) _selector.SideStepped -= OnTempoStepped;
            if (_runner != null) _runner.Completed -= OnLessonCompleted;
            if (_exitButton != null) _exitButton.Exited -= OnExitPressed;
            if (_drillRunner != null) _drillRunner.Finished -= OnDrillFinished;
        }

        private void ShowSessions()
        {
            _session = -1;
            ExitToSelector();
            _inTimingMenu = false;
            RestoreMechanic();

            var rows = new List<string>();
            foreach (LessonCatalog.Session s in _catalog.Sessions)
            {
                rows.Add(s.title);
            }

            rows.Add(FreePlayRow); // after the numbered sessions
            if (HasTimingTest)
            {
                rows.Add(TimingTestRow); // after Free play
            }

            _selector.Show(rows, "Choose a session");
            _selector.ClearFooter();
            RefreshTempoControl();
        }

        /// <summary>
        /// The free-play click's tempo stepper, beside the session menu. Only here: it sets
        /// free play's pulse, and a lesson carries its own tempo (§3.9), so it would mislead
        /// on a lesson list.
        /// </summary>
        private void RefreshTempoControl()
        {
            if (!_showTempoControl || _freePlayMetronome == null || !_freePlayMetronome.IsConfigured)
            {
                _selector.ClearSideControl();
                return;
            }

            _selector.SetSideControl(
                $"Free-play\nclick\n{_freePlayMetronome.TempoBpm:0} BPM",
                _freePlayMetronome.CanStepTempo(-1),
                _freePlayMetronome.CanStepTempo(1));
        }

        private void OnTempoStepped(int direction)
        {
            // Only offered on the session menu; ignore a stray event from anywhere else.
            if (_session >= 0 || _inTimingMenu || _inDrill || _freePlayMetronome == null)
            {
                return;
            }

            _freePlayMetronome.StepTempo(direction);
            RefreshTempoControl();
        }

        private void ShowLessons(int session)
        {
            _session = session;
            ExitToSelector();
            _inTimingMenu = false;
            RestoreMechanic();

            var rows = new List<string>();
            foreach (LessonDefinition lesson in _catalog.Sessions[session].lessons)
            {
                rows.Add(lesson != null ? lesson.Title : "(missing lesson)");
            }

            rows.Add(BackRow); // last, so the lesson numbers line up with the number keys

            _selector.Show(rows, _catalog.Sessions[session].title);
            _selector.ClearSideControl();
            _selector.ClearFooter();
        }

        // --- Timing test -------------------------------------------------------------------

        private void ShowTimingMenu()
        {
            _session = -1;
            ExitToSelector();
            _inTimingMenu = true;

            var rows = new List<string> { MechanicRow() }; // row 0 cycles the mechanic
            foreach (TimingDrillDefinition drill in _catalog.TimingDrills)
            {
                rows.Add(drill != null ? drill.DisplayName : "(missing drill)");
            }

            rows.Add(BackRow);

            _selector.Show(rows, TimingTitle);
            _selector.ClearSideControl();
            _selector.SetFooter(_lastTimingResult);
        }

        private void OnTimingChosen(int index)
        {
            if (index == 0)
            {
                _mechanic = _mechanic == RightHandMechanic.ArcTouch
                    ? RightHandMechanic.ArcPinch
                    : RightHandMechanic.ArcTouch;
                ShowTimingMenu(); // redraw with the new label
                return;
            }

            int drillIndex = index - 1;
            if (drillIndex < 0 || drillIndex >= _catalog.TimingDrills.Count)
            {
                ShowSessions(); // the Back row, or out of range
                return;
            }

            TimingDrillDefinition drill = _catalog.TimingDrills[drillIndex];
            if (drill != null)
            {
                StartDrill(drill);
            }
        }

        private void StartDrill(TimingDrillDefinition drill)
        {
            _freePlay = false;
            _selector.Hide();

            if (_freePlayMetronome != null) _freePlayMetronome.End();
            if (_hud != null) _hud.enabled = false;
            if (_performance != null) _performance.enabled = true;
            if (_melodyInput != null) _melodyInput.enabled = (drill.ActiveHands & ActiveHands.Right) != 0;
            if (_exitButton != null) _exitButton.Show();
            ApplyMechanic();

            string commit = _performance != null ? _performance.CommitGesture.ToString() : "unknown";
            _inDrill = _drillRunner.Run(drill, MechanicTag(), commit);

            if (!_inDrill)
            {
                _lastTimingResult = $"{drill.DisplayName} could not start — see the Console.";
                ShowTimingMenu();
            }
        }

        private void OnDrillFinished(DrillOutcome outcome)
        {
            _inDrill = false;
            _lastTimingResult = $"{outcome.Drill.Name}: {outcome.SummaryLine}"
                + (outcome.Report != null && outcome.CsvPath == null ? " (CSV not saved)" : string.Empty);
            ShowTimingMenu(); // the result arrives after the run, under the menu (§3.7)
        }

        // Short: a menu row is about 15 characters wide in the headset.
        private string MechanicRow() => $"Mechanic: {MechanicLabel()}";

        private string MechanicLabel() => _mechanic == RightHandMechanic.ArcPinch ? "pinch" : "touch";

        /// <summary>The machine-readable form written into a run's CSV.</summary>
        private string MechanicTag() => _mechanic == RightHandMechanic.ArcPinch ? "arc+pinch" : "arc+touch";

        private void ApplyMechanic() => SetPinchSpike(_mechanic == RightHandMechanic.ArcPinch);

        /// <summary>Leaving the Timing test puts the right hand back to however the scene was authored.</summary>
        private void RestoreMechanic() => SetPinchSpike(_defaultPinchSpike);

        private void SetPinchSpike(bool pinch)
        {
            if (_melodyInput is TouchTargetBinder binder)
            {
                binder.PinchSpike = pinch;
            }
            else if (!_warnedNoBinder && pinch)
            {
                _warnedNoBinder = true;
                Debug.LogWarning(
                    $"{nameof(LessonFlowController)}: the melody input is not a TouchTargetBinder, so the " +
                    "Timing test's mechanic row has no effect.", this);
            }
        }

        // --- Lessons and free play ---------------------------------------------------------

        private void OnChosen(int index)
        {
            if (_inTimingMenu)
            {
                OnTimingChosen(index);
                return;
            }

            if (_session < 0)
            {
                int sessions = _catalog.Sessions.Count;
                if (index == sessions) // the Free play row, appended after the sessions
                {
                    EnterFreePlay();
                }
                else if (HasTimingTest && index == sessions + 1) // the Timing test row, after Free play
                {
                    ShowTimingMenu();
                }
                else if (index >= 0 && index < sessions)
                {
                    ShowLessons(index);
                }

                return;
            }

            List<LessonDefinition> lessons = _catalog.Sessions[_session].lessons;

            if (index < 0 || index >= lessons.Count) // the Back row, or out of range
            {
                ShowSessions();
                return;
            }

            if (lessons[index] == null)
            {
                return;
            }

            _freePlay = false;
            _selector.Hide();

            // Cut any tempo preview still ringing from the menu before the lesson starts.
            if (_freePlayMetronome != null) _freePlayMetronome.End();

            // HUD + performance on first, so they catch the lesson's opening phase/cue events.
            if (_hud != null) _hud.enabled = true;
            if (_performance != null) _performance.enabled = true;
            if (_exitButton != null) _exitButton.Show();

            _runner.LoadLesson(lessons[index]);

            // Right-hand melody surfaces only for a lesson that uses the right hand.
            bool usesRightHand = (_runner.ActiveHands & ActiveHands.Right) != 0;
            if (_melodyInput != null) _melodyInput.enabled = usesRightHand;
        }

        /// <summary>
        /// The sandbox (§3.8 — Compose on the Fly): both hands live, no ghost, no HUD, no
        /// lesson structure. Runs until the learner pinches the Exit button.
        /// </summary>
        private void EnterFreePlay()
        {
            _freePlay = true;
            _session = -1;
            _selector.Hide();

            _runner.StopLesson(); // no lesson; StopLesson also frees the note gate

            if (_hud != null) _hud.enabled = false;
            if (_performance != null) _performance.enabled = true;
            if (_melodyInput != null) _melodyInput.enabled = true; // both hands
            if (_exitButton != null) _exitButton.Show();
            if (_freePlayMetronome != null) _freePlayMetronome.Begin();
        }

        private void OnLessonCompleted() => ReturnToSelector();

        private void OnExitPressed() => ReturnToSelector();

        private void ReturnToSelector()
        {
            if (_inDrill)
            {
                _lastTimingResult = "Run stopped early - nothing recorded.";
                ShowTimingMenu(); // stops the drill; back to the menu it was started from
            }
            else if (!_freePlay && _session >= 0)
            {
                ShowLessons(_session); // back to this session's lesson list
            }
            else
            {
                ShowSessions();
            }
        }

        private void ExitToSelector()
        {
            _freePlay = false;
            _inDrill = false;
            if (_drillRunner != null) _drillRunner.Abort();
            if (_runner != null) _runner.StopLesson();
            if (_hud != null) _hud.enabled = false;
            if (_melodyInput != null) _melodyInput.enabled = false; // also hides the target markers
            if (_performance != null) _performance.enabled = false;
            if (_exitButton != null) _exitButton.Hide();
            if (_freePlayMetronome != null) _freePlayMetronome.End();
        }
    }
}
