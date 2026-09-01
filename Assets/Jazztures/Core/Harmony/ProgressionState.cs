using System;
using Jazztures.Core.Music;

namespace Jazztures.Core.Harmony
{
    /// <summary>
    /// Holds the one piece of harmonic state: which <see cref="ChordFunction"/> the left
    /// hand is currently holding, or none. Gesture-driven and <b>unordered</b> — the
    /// learner may hold the functions in any order; this type teaches ii-V-I as a
    /// functional relationship, never enforces a sequence (CLAUDE.md §3.2).
    ///
    /// <para>
    /// Clock-free by design. Debounce, the minimum inter-chord interval and confidence
    /// gating (§3.4, §3.5) belong upstream in the gesture/harmony engine that feeds this;
    /// <see cref="ProgressionState"/> just reflects what is held right now and announces
    /// every change.
    /// </para>
    /// </summary>
    public sealed class ProgressionState
    {
        /// <summary>The function currently held, or null for "no gesture" (a legal state).</summary>
        public ChordFunction? Active { get; private set; }

        /// <summary>The chord the active function resolves to, or null when nothing is held.</summary>
        public Chord? ActiveChord =>
            Active.HasValue ? Progression.ChordFor(Active.Value) : (Chord?)null;

        /// <summary>Raised on every actual change of <see cref="Active"/>, never on a no-op.</summary>
        public event Action<ChordChange>? Changed;

        /// <summary>
        /// Hold <paramref name="function"/>. Returns true and raises <see cref="Changed"/>
        /// if this changed the active function; returns false if it was already active.
        /// </summary>
        public bool Hold(ChordFunction function)
        {
            if (Active == function)
            {
                return false;
            }

            ChordFunction? previous = Active;
            Active = function;
            Changed?.Invoke(new ChordChange(previous, function));
            return true;
        }

        /// <summary>
        /// Release whatever is held. Returns true and raises <see cref="Changed"/> if
        /// something was held; returns false if nothing was.
        /// </summary>
        public bool Release()
        {
            if (!Active.HasValue)
            {
                return false;
            }

            ChordFunction? previous = Active;
            Active = null;
            Changed?.Invoke(new ChordChange(previous, null));
            return true;
        }
    }
}
