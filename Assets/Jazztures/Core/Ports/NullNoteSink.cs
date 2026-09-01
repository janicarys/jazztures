namespace Jazztures.Core.Ports
{
    /// <summary>
    /// A sink that discards every note. Used where a real sink is not wired yet, and as
    /// the "audible" slot in learning modes that log-but-do-not-sound (CLAUDE.md §3.8).
    /// </summary>
    public sealed class NullNoteSink : INoteSink
    {
        public static readonly NullNoteSink Instance = new NullNoteSink();

        public void Send(in NoteEvent note)
        {
        }
    }
}
