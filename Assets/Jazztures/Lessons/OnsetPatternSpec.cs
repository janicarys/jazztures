using System;
using System.Collections.Generic;
using Jazztures.Core.Timing;
using UnityEngine;

namespace Jazztures.Lessons
{
    /// <summary>
    /// The Inspector form of an <see cref="OnsetPattern"/> (ADR-0043): where a learner is
    /// expected to play, authored as data. Used by a lesson's scored pulse and by timing
    /// drill presets, so a new rhythm needs an asset, not C# (§3.9).
    /// </summary>
    [Serializable]
    public sealed class OnsetPatternSpec
    {
        public enum PatternKind
        {
            None = 0,
            EveryBeat = 1,
            EverySecondBeat = 2,
            EveryEighth = 3,

            /// <summary>The same positions in every bar — see <c>_positionsInBar</c>.</summary>
            PositionsPerBar = 4,
        }

        [SerializeField] private PatternKind _kind = PatternKind.None;

        [Tooltip("Positions Per Bar only: beat offsets within a bar, repeated every bar. 0 is " +
                 "the downbeat, 2 is beat 3, 0.5 is the \"and\" of beat 1. Comping on 1 and 3 is 0, 2.")]
        [SerializeField] private List<float> _positionsInBar = new List<float>();

        public PatternKind Kind => _kind;

        /// <summary>The Core pattern this spec describes. An empty positions list is no pattern.</summary>
        public OnsetPattern ToPattern()
        {
            switch (_kind)
            {
                case PatternKind.EveryBeat:
                    return OnsetPattern.EveryBeat;

                case PatternKind.EverySecondBeat:
                    return OnsetPattern.EverySecondBeat;

                case PatternKind.EveryEighth:
                    return OnsetPattern.EveryEighth;

                case PatternKind.PositionsPerBar:
                    if (_positionsInBar == null || _positionsInBar.Count == 0)
                    {
                        return OnsetPattern.None;
                    }

                    var positions = new List<double>(_positionsInBar.Count);
                    foreach (float position in _positionsInBar)
                    {
                        positions.Add(position);
                    }

                    return OnsetPattern.PerBar(positions);

                default:
                    return OnsetPattern.None;
            }
        }
    }
}
