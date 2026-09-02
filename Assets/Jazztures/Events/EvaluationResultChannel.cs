using Jazztures.Core.Evaluation;
using UnityEngine;

namespace Jazztures.Events
{
    /// <summary>
    /// Raised once, at the end of a Test-Yourself attempt, with the aggregate onset
    /// scoring (CLAUDE.md §2.3, §3.7). Never raised mid-phrase — feedback is deferred to
    /// the end of the attempt.
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Events/Evaluation Result", fileName = "EvaluationResultChannel")]
    public sealed class EvaluationResultChannel : EventChannel<AttemptResult>
    {
    }
}
