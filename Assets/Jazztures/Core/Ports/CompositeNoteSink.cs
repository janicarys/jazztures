using System;

namespace Jazztures.Core.Ports
{
    /// <summary>
    /// Fans every <see cref="NoteEvent"/> out to a fixed set of sinks, in order
    /// (CLAUDE.md §2.2: "Every note event goes to all three sinks"). The set is fixed at
    /// construction — no add/remove at runtime — so the fan-out is allocation-free and
    /// its cost is predictable on the audio/gesture path.
    /// </summary>
    public sealed class CompositeNoteSink : INoteSink
    {
        private readonly INoteSink[] _sinks;

        public CompositeNoteSink(params INoteSink[] sinks)
        {
            if (sinks == null)
            {
                throw new ArgumentNullException(nameof(sinks));
            }

            var copy = new INoteSink[sinks.Length];
            for (int i = 0; i < sinks.Length; i++)
            {
                copy[i] = sinks[i] ?? throw new ArgumentNullException(
                    nameof(sinks), $"Sink {i} is null.");
            }

            _sinks = copy;
        }

        public int Count => _sinks.Length;

        public void Send(in NoteEvent note)
        {
            for (int i = 0; i < _sinks.Length; i++)
            {
                _sinks[i].Send(note);
            }
        }
    }
}
