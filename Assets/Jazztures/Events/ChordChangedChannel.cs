using Jazztures.Core.Harmony;
using UnityEngine;

namespace Jazztures.Events
{
    /// <summary>
    /// Raised after the harmony engine has changed the sounding chord (CLAUDE.md §2.3).
    /// Touch targets re-pitch, tension colour updates, the HUD reads from here.
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Events/Chord Changed", fileName = "ChordChangedChannel")]
    public sealed class ChordChangedChannel : EventChannel<ChordChange>
    {
    }
}
