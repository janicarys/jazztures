using Jazztures.Core.Ports;

namespace Jazztures.Presentation
{
    /// <summary>
    /// Whether a hand is one the running lesson is about (CLAUDE.md §3.9; ADR-0063). A view that hangs something
    /// over a hand — <see cref="ChordNameLabel"/> over the left — asks it, so a lesson that uses one hand leaves the
    /// other bare instead of labelling a hand that does nothing. Implemented by whatever already knows the running
    /// lesson (the performance root), so Presentation reads it without depending on the app layer (§2.3).
    ///
    /// <para>
    /// Only the labels' business: the hand is still tracked and recognised either way (§3.8), and what is heard is
    /// gated separately, in the note sink.
    /// </para>
    /// </summary>
    public interface IHandsInUse
    {
        /// <summary>True when no lesson is running (the free instrument uses both hands) or the lesson uses <paramref name="hand"/>.</summary>
        bool IsInUse(Handedness hand);
    }
}
