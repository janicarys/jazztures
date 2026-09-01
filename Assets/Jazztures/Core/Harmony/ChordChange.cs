using Jazztures.Core.Music;

namespace Jazztures.Core.Harmony
{
    /// <summary>
    /// Payload for <see cref="ProgressionState.Changed"/>: what the active harmony was and
    /// what it is now. Nulls mean "no chord held" — a legal state on both sides (§3.2).
    /// Carries both the function and the resolved <see cref="Chord"/> so a listener can
    /// release the outgoing voicing and sound the incoming one without re-deriving them.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct ChordChange
    {
        public ChordFunction? PreviousFunction { get; }

        public ChordFunction? CurrentFunction { get; }

        public ChordChange(ChordFunction? previousFunction, ChordFunction? currentFunction)
        {
            PreviousFunction = previousFunction;
            CurrentFunction = currentFunction;
        }

        public Chord? PreviousChord =>
            PreviousFunction.HasValue ? Progression.ChordFor(PreviousFunction.Value) : (Chord?)null;

        public Chord? CurrentChord =>
            CurrentFunction.HasValue ? Progression.ChordFor(CurrentFunction.Value) : (Chord?)null;

        public override string ToString() =>
            $"{PreviousFunction?.ToString() ?? "-"} -> {CurrentFunction?.ToString() ?? "-"}";
    }
}
