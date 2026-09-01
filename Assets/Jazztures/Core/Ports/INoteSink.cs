namespace Jazztures.Core.Ports
{
    /// <summary>
    /// A destination for <see cref="NoteEvent"/>s. The domain emits every note here and
    /// nowhere else. In the running system this is a <see cref="CompositeNoteSink"/> that
    /// fans each event out to local audio, the OSC/DAW bridge and the telemetry log, so
    /// all three describe the same performance (CLAUDE.md §2.2).
    /// </summary>
    /// <remarks>
    /// Implementations must not throw and must not block — a slow or failed sink can
    /// never be allowed to disturb the musical path (§4.4). Passed <c>in</c> to keep the
    /// hot path allocation- and copy-free.
    /// </remarks>
    public interface INoteSink
    {
        void Send(in NoteEvent note);
    }
}
