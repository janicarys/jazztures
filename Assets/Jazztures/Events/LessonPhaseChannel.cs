using Jazztures.Core.Lessons;
using UnityEngine;

namespace Jazztures.Events
{
    /// <summary>
    /// Raised when a lesson enters a new mode phase (CLAUDE.md §2.3, §3.8). The HUD,
    /// ghost hands and mode-gated sink react; nothing writes back.
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Events/Lesson Phase", fileName = "LessonPhaseChannel")]
    public sealed class LessonPhaseChannel : EventChannel<LessonPhaseInfo>
    {
    }
}
