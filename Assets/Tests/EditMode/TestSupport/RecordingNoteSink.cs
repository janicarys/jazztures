using System.Collections.Generic;
using Jazztures.Core.Ports;

namespace Jazztures.Tests.EditMode.TestSupport
{
    /// <summary>An <see cref="INoteSink"/> that keeps every event for assertions.</summary>
    public sealed class RecordingNoteSink : INoteSink
    {
        public List<NoteEvent> Events { get; } = new List<NoteEvent>();

        public void Send(in NoteEvent note) => Events.Add(note);

        public void Clear() => Events.Clear();

        public IReadOnlyList<NoteEvent> On(int channel) => Filter(channel, NoteEventKind.On);

        public IReadOnlyList<NoteEvent> Off(int channel) => Filter(channel, NoteEventKind.Off);

        private List<NoteEvent> Filter(int channel, NoteEventKind kind)
        {
            var result = new List<NoteEvent>();
            foreach (NoteEvent e in Events)
            {
                if (e.Channel == channel && e.Kind == kind)
                {
                    result.Add(e);
                }
            }

            return result;
        }
    }
}
