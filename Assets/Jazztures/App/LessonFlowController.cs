using System.Collections.Generic;
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
    /// selector, enables the lesson surfaces, and re-binds the <see cref="LessonRunner"/>.
    /// On completion the selector returns to that session's lesson list.
    /// </para>
    /// </summary>
    public sealed class LessonFlowController : MonoBehaviour
    {
        [SerializeField] private LessonCatalog _catalog;
        [SerializeField] private LessonRunner _runner;
        [SerializeField] private LessonSelector _selector;

        [Tooltip("The Lesson HUD — enabled only while a lesson runs.")]
        [SerializeField] private Behaviour _hud;

        [Tooltip("The right-hand melody binder (TouchTargetBinder) — disabled while the " +
                 "selector is open so a dwell can't double as a note trigger (§3.10).")]
        [SerializeField] private Behaviour _melodyInput;

        [Tooltip("PerformanceCompositionRoot — disabled on the selector screen so left-hand " +
                 "poses don't trigger chords while you're choosing a lesson. (Disabling the " +
                 "melody binder also hides the ten touch-target markers.)")]
        [SerializeField] private PerformanceCompositionRoot _performance;

        private const string BackRow = "←  Back";

        private int _session = -1;

        private void Awake()
        {
            if (_selector != null) _selector.Chosen += OnChosen;
            if (_runner != null) _runner.Completed += OnLessonCompleted;
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
            if (_runner != null) _runner.Completed -= OnLessonCompleted;
        }

        private void ShowSessions()
        {
            _session = -1;
            SetLessonSurfacesActive(false);

            var rows = new List<string>();
            foreach (LessonCatalog.Session s in _catalog.Sessions)
            {
                rows.Add(s.title);
            }

            _selector.Show(rows, "Choose a session");
        }

        private void ShowLessons(int session)
        {
            _session = session;
            SetLessonSurfacesActive(false);

            var rows = new List<string>();
            foreach (LessonDefinition lesson in _catalog.Sessions[session].lessons)
            {
                rows.Add(lesson != null ? lesson.Title : "(missing lesson)");
            }

            rows.Add(BackRow); // last, so the lesson numbers line up with the number keys

            _selector.Show(rows, _catalog.Sessions[session].title);
        }

        private void OnChosen(int index)
        {
            if (_session < 0)
            {
                if (index >= 0 && index < _catalog.Sessions.Count)
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

            _selector.Hide();
            SetLessonSurfacesActive(true);
            _runner.LoadLesson(lessons[index]);
        }

        private void OnLessonCompleted()
        {
            if (_session >= 0)
            {
                ShowLessons(_session);
            }
            else
            {
                ShowSessions();
            }
        }

        private void SetLessonSurfacesActive(bool active)
        {
            if (_hud != null) _hud.enabled = active;
            if (_melodyInput != null) _melodyInput.enabled = active; // also shows/hides the target markers
            if (_performance != null) _performance.enabled = active;
        }
    }
}
