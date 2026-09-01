namespace Jazztures.Core.Ports
{
    /// <summary>
    /// The domain's window onto hand input, polled once per frame (CLAUDE.md §2.2).
    /// </summary>
    /// <remarks>
    /// Adapters: <c>MetaXRHandPoseSource</c> (Interaction SDK on the Quest),
    /// <c>ReplayHandPoseSource</c> (recorded fixture playback), <c>KeyboardHandPoseSource</c>
    /// (desktop debug), <c>FakeHandPoseSource</c> (edit-mode tests).
    /// </remarks>
    public interface IHandPoseSource
    {
        HandPoseFrame CurrentFrame { get; }
    }
}
