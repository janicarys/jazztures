using System;
using Jazztures.Core.Music;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// A position in the active <see cref="PitchSet"/>; each swing steps it (ADR-0078). Up is +1, Same is 0,
    /// Down is -1. At the top or bottom it clamps — it plays the same pitch again and raises
    /// <see cref="AtLimit"/> — and never wraps (an up gesture must not sound down) and never drifts toward
    /// the middle by itself (that would change the learner's melody behind their back).
    ///
    /// <para>
    /// A chord change swaps the set and snaps to the member nearest the pitch the cursor was on (the lower one
    /// on a tie). <see cref="Reset(Pitch)"/> jumps to a start pitch without playing anything.
    /// </para>
    /// </summary>
    public sealed class MelodyCursor
    {
        private readonly float _startIndexFraction;
        private PitchSet? _set;
        private int _index;

        public MelodyCursor(float startIndexFraction = 0.5f)
        {
            if (startIndexFraction is < 0f or > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndexFraction), startIndexFraction, "Must be 0..1.");
            }

            _startIndexFraction = startIndexFraction;
        }

        public bool HasSet => _set != null;

        public PitchSet? Set => _set;

        public int Index => _index;

        /// <summary>The pitch the cursor is on: the last one played, or the start pitch after a reset.</summary>
        public Pitch CurrentPitch => _set != null
            ? _set[_index]
            : throw new InvalidOperationException("No pitch set yet.");

        /// <summary>True if the last <see cref="Step"/> asked to go past either end of the range.</summary>
        public bool AtLimit { get; private set; }

        /// <summary>Swap in the set for a new chord, snapping to the nearest pitch to where the cursor was.</summary>
        public void SetPitchSet(PitchSet set)
        {
            if (set == null)
            {
                throw new ArgumentNullException(nameof(set));
            }

            _index = _set == null
                ? Math.Min(set.Count - 1, (int)(set.Count * _startIndexFraction))
                : set.IndexOfNearest(_set[_index]);
            _set = set;
            AtLimit = false;
        }

        /// <summary>Move one step and return the pitch to play.</summary>
        public Pitch Step(StepDirection direction)
        {
            PitchSet set = _set ?? throw new InvalidOperationException("No pitch set yet.");
            int target = _index + (int)direction;
            AtLimit = target < 0 || target >= set.Count;
            _index = Math.Max(0, Math.Min(set.Count - 1, target));
            return set[_index];
        }

        /// <summary>Jump to <paramref name="startIndex"/> (clamped) without playing a note.</summary>
        public void Reset(int startIndex)
        {
            PitchSet set = _set ?? throw new InvalidOperationException("No pitch set yet.");
            _index = Math.Max(0, Math.Min(set.Count - 1, startIndex));
            AtLimit = false;
        }

        /// <summary>Jump to the member nearest <paramref name="startPitch"/> (the lower on a tie) without playing.</summary>
        public void Reset(Pitch startPitch)
        {
            PitchSet set = _set ?? throw new InvalidOperationException("No pitch set yet.");
            Reset(set.IndexOfNearest(startPitch));
        }

        /// <summary>Jump to the active chord's root nearest the middle of the range, without playing.</summary>
        public void ResetToChordRoot()
        {
            PitchSet set = _set ?? throw new InvalidOperationException("No pitch set yet.");
            Reset(set.IndexOfRootNearMiddle());
        }
    }
}
