using Jazztures.Core.Lessons;
using UnityEngine;

namespace Jazztures.Events
{
    /// <summary>
    /// Authored lesson cues on their way to presentation (ADR-0011, CLAUDE.md §2.3): the
    /// captions, target highlights and tension-colour changes a lesson script fires at
    /// beats, markers or learner actions.
    ///
    /// <para>
    /// <c>LessonRunner</c> consumes the control-flow cues itself (wait-for-input, scoring,
    /// advance-phase) and raises everything presentational here. Without this channel the
    /// cue track had nowhere to go but the Console.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Events/Cue Action", fileName = "CueActionChannel")]
    public sealed class CueActionChannel : EventChannel<CueAction>
    {
    }
}
