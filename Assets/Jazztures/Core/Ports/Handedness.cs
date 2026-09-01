namespace Jazztures.Core.Ports
{
    /// <summary>
    /// Which hand a pose, gesture, or note event originates from.
    /// Pure domain value (CLAUDE.md §2.1) — no <c>None</c> / <c>Both</c> member:
    /// callers that need "no hand held" use a nullable <see cref="Handedness"/>?.
    /// </summary>
    public enum Handedness
    {
        Left,
        Right,
    }
}
