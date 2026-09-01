using System;
using Jazztures.Core.Ports;

namespace Jazztures.Events
{
    /// <summary>
    /// An <see cref="INoteSink"/> that forwards every note to a
    /// <see cref="NoteTriggeredChannel"/>. Put it in the composite sink so presentation
    /// sees exactly the note stream the audio and telemetry sinks see (CLAUDE.md §2.2).
    /// </summary>
    public sealed class ChannelNoteSink : INoteSink
    {
        private readonly NoteTriggeredChannel _channel;

        public ChannelNoteSink(NoteTriggeredChannel channel)
        {
            _channel = channel ? channel : throw new ArgumentNullException(nameof(channel));
        }

        public void Send(in NoteEvent note) => _channel.Raise(note);
    }
}
