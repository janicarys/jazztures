using System;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Steps a lesson through its ordered mode phases (CLAUDE.md §3.8). The explicit state
    /// machine §3.8 asks for. It owns no clock and no sink — it just tracks which phase is
    /// active and raises <see cref="PhaseChanged"/> / <see cref="Completed"/> so the runner
    /// can re-point the <c>ModeGatedNoteSink</c>, the ghost hands, and
    /// <c>LessonPhaseChannel</c>.
    /// </summary>
    public sealed class LessonStateMachine
    {
        private readonly LessonPlan _plan;

        private int _phaseIndex = -1;

        public LessonStateMachine(LessonPlan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }

        /// <summary>Raised on <see cref="Begin"/> and every <see cref="AdvancePhase"/> that lands on a phase.</summary>
        public event Action<LessonPhase>? PhaseChanged;

        /// <summary>Raised once, when <see cref="AdvancePhase"/> steps off the last phase.</summary>
        public event Action? Completed;

        public LessonId LessonId => _plan.Id;

        public LessonStatus Status { get; private set; } = LessonStatus.NotStarted;

        /// <summary>Index of the active phase, or -1 before <see cref="Begin"/>.</summary>
        public int PhaseIndex => _phaseIndex;

        public int PhaseCount => _plan.Modes.Count;

        /// <summary>The active phase, or null unless <see cref="Status"/> is <see cref="LessonStatus.InPhase"/>.</summary>
        public LessonPhase? CurrentPhase =>
            Status == LessonStatus.InPhase ? PhaseAt(_phaseIndex) : (LessonPhase?)null;

        /// <summary>Start the lesson on phase 0.</summary>
        public void Begin()
        {
            if (Status != LessonStatus.NotStarted)
            {
                throw new InvalidOperationException($"Lesson already started (status {Status}).");
            }

            _phaseIndex = 0;
            Status = LessonStatus.InPhase;
            PhaseChanged?.Invoke(PhaseAt(_phaseIndex));
        }

        /// <summary>
        /// Move to the next phase. Returns true if a new phase is now active, false if the
        /// lesson just completed.
        /// </summary>
        public bool AdvancePhase()
        {
            if (Status != LessonStatus.InPhase)
            {
                throw new InvalidOperationException($"Cannot advance — status is {Status}.");
            }

            if (_phaseIndex >= _plan.Modes.Count - 1)
            {
                Status = LessonStatus.Completed;
                Completed?.Invoke();
                return false;
            }

            _phaseIndex++;
            PhaseChanged?.Invoke(PhaseAt(_phaseIndex));
            return true;
        }

        /// <summary>Rewind to the start so the lesson can be run again.</summary>
        public void Reset()
        {
            _phaseIndex = -1;
            Status = LessonStatus.NotStarted;
        }

        private LessonPhase PhaseAt(int index) =>
            new LessonPhase(index, _plan.Modes[index], index == _plan.Modes.Count - 1);
    }
}
