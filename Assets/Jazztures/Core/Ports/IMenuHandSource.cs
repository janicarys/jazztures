namespace Jazztures.Core.Ports
{
    /// <summary>
    /// Where the menu's view of the right hand comes from (CLAUDE.md §2.2; ADR-0051).
    /// The menu's counterpart of <see cref="IHandPoseSource"/>.
    /// </summary>
    /// <remarks>
    /// Adapters: <c>MetaXRMenuHandSource</c> (Interaction SDK), <c>KeyboardMenuHandSource</c>
    /// (desk), and — in tests — a hand-fed frame.
    /// </remarks>
    public interface IMenuHandSource
    {
        /// <summary>The right hand this frame. Read once per frame by the driver.</summary>
        MenuHandFrame CurrentFrame { get; }
    }
}
