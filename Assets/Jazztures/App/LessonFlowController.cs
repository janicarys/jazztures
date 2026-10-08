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
    /// </summary>
    public sealed class LessonFlowController : MonoBehaviour
    {
        [SerializeField] private LessonCatalog _catalog;
        [SerializeField] private LessonRunner _runner;
        [SerializeField] private LessonSelector _selector;

        [Tooltip("The Lesson HUD — enabled only while a lesson runs.")]
        [SerializeField] private Behaviour _hud;

        [Tooltip("The right-hand melody binder (TouchTargetBinder). Enabled only for a lesson " +
                 "whose ActiveHands includes Right (§3.9) — so a left-hand lesson like L1 shows " +
                 "no touch-target markers. Disabling it also hides the ten markers.")]
        [SerializeField] private Behaviour _melodyInput;

        [Tooltip("PerformanceCompositionRoot — disabled on the selector screen so left-hand " +
                 "poses don't trigger chords while you're choosing a lesson. (Disabling the " +
                 "melody binder also hides the ten touch-target markers.)")]
        [SerializeField] private PerformanceCompositionRoot _performance;

        [Tooltip("The overhead Exit control. Shown during any lesson and in free play; a " +
                 "pinch on it returns to the selector.")]
        [SerializeField] private ExitButton _exitButton;

        [Tooltip("The free-play metronome (ADR-0043). Optional — leave empty for a silent free " +
                 "play. Lessons run their own metronome through LessonRunner.")]
        [SerializeField] private FreePlayMetronome _freePlayMetronome;

        [Tooltip("Show the free-play click's +/- tempo buttons beside the session menu. A testing " +
                 "affordance (ADR-0043), not part of the learner's task — untick to remove it " +
                 "from the menu, e.g. in a study build (§3.10: visual restraint).")]
        [SerializeField] private bool _showTempoControl = true;

        private const string BackRow = "←  Back";
        private const string FreePlayRow = "Free play";

        private int _session = -1;
        private bool _freePlay;

        private void Awake()
        {
            if (_selector != null) _selector.Chosen += OnChosen;
            if (_selector != null) _selector.SideStepped += OnTempoStepped;
            if (_runner != null) _runner.Completed += OnLessonCompleted;
            if (_exitButton != null) _exitButton.Exited += OnExitPressed;
        }

        private void Start()
        {
            if (_catalog == null || _selector == null || _runner == null)
            {
                Debug.LogError($"{nameof(LessonFlowController)}: assign catalog, selector and runner.", this);
                enabled = false;
                return;
            }

            ShowSessions();
        }

        private void OnDestroy()
        {
            if (_selector != null) _selector.Chosen -= OnChosen;
            if (_selector != null) _selector.SideStepped -= OnTempoStepped;
            if (_runner != null) _runner.Completed -= OnLessonCompleted;
            if (_exitButton != null) _exitButton.Exited -= OnExitPressed;
        }

        private void ShowSessions()
        {
            _session = -1;
            ExitToSelector();

            var rows = new List<string>();
            foreach (LessonCatalog.Session s in _catalog.Sessions)
            {
                rows.Add(s.title);
            }

            rows.Add(FreePlayRow); // last, after the numbered sessions

            _selector.Show(rows, "Choose a session");
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
            if (_session >= 0 || _freePlayMetronome == null)
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

            var rows = new List<string>();
            foreach (LessonDefinition lesson in _catalog.Sessions[session].lessons)
            {
                rows.Add(lesson != null ? lesson.Title : "(missing lesson)");
            }

            rows.Add(BackRow); // last, so the lesson numbers line up with the number keys

            _selector.Show(rows, _catalog.Sessions[session].title);
            _selector.ClearSideControl();
        }

        private void OnChosen(int index)
        {
            if (_session < 0)
            {
                if (index == _catalog.Sessions.Count) // the Free play row, appended last
                {
                    EnterFreePlay();
                }
                else if (index >= 0 && index < _catalog.Sessions.Count)
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
            if (!_freePlay && _session >= 0)
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
            if (_runner != null) _runner.StopLesson();
            if (_hud != null) _hud.enabled = false;
            if (_melodyInput != null) _melodyInput.enabled = false; // also hides the target markers
            if (_performance != null) _performance.enabled = false;
            if (_exitButton != null) _exitButton.Hide();
            if (_freePlayMetronome != null) _freePlayMetronome.End();
        }
    }
}
