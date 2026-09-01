using Jazztures.Core.Gesture;
using UnityEngine;

namespace Jazztures.Events
{
    /// <summary>
    /// Raised when the gesture interpreter's phase changes (CLAUDE.md §2.3). Ghost hands
    /// and the "detecting" affordance read from here.
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Events/Gesture State", fileName = "GestureStateChannel")]
    public sealed class GestureStateChannel : EventChannel<GestureState>
    {
    }
}
