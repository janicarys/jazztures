using Jazztures.Core.Ports;
using UnityEngine;

namespace Jazztures.Events
{
    /// <summary>
    /// Raised for every <see cref="NoteEvent"/> the domain emits (CLAUDE.md §2.3). Fed by
    /// <see cref="ChannelNoteSink"/> in the composite sink, so it sees exactly what the
    /// audio and telemetry sinks see.
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Events/Note Triggered", fileName = "NoteTriggeredChannel")]
    public sealed class NoteTriggeredChannel : EventChannel<NoteEvent>
    {
    }
}
