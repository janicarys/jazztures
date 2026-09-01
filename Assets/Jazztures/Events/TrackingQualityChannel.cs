using Jazztures.Core.Ports;
using UnityEngine;

namespace Jazztures.Events
{
    /// <summary>
    /// Raised when a hand's tracking quality changes (CLAUDE.md §2.3, §3.5). Drives the
    /// non-modal desaturation cue and feeds the tracking-loss telemetry.
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Events/Tracking Quality", fileName = "TrackingQualityChannel")]
    public sealed class TrackingQualityChannel : EventChannel<HandTrackingState>
    {
    }
}
