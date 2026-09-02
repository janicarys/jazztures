using Jazztures.Core.Lessons;
using UnityEngine;

namespace Jazztures.Events
{
    /// <summary>
    /// The ghost-hand demonstration stream (ADR-0012). <c>LessonRunner</c> raises one
    /// <see cref="GhostFrame"/> per frame while a ghost-hand mode is active; the
    /// translucent-mesh renderer subscribes and owns all visual detail.
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Events/Ghost Frame", fileName = "GhostFrameChannel")]
    public sealed class GhostFrameChannel : EventChannel<GhostFrame>
    {
    }
}
