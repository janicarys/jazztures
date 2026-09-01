using System;
using System.Collections.Generic;
using Jazztures.Core.Music;

namespace Jazztures.Core.Harmony
{
    /// <summary>
    /// The canonical facts of the C major ii-V-I (CLAUDE.md §1.3, §3.1). The mapping
    /// between a <see cref="ChordFunction"/> and its <see cref="Chord"/> lives here and
    /// nowhere else.
    /// </summary>
    public static class Progression
    {
        /// <summary>
        /// The functions in their textbook order. The lesson layer may use this to
        /// <i>prompt</i> a sequence; the harmony engine must not use it to constrain
        /// input (§3.2).
        /// </summary>
        public static readonly IReadOnlyList<ChordFunction> IiViOrder = new[]
        {
            ChordFunction.Two,
            ChordFunction.Five,
            ChordFunction.One,
        };

        public static Chord ChordFor(ChordFunction function) => function switch
        {
            ChordFunction.Two => Chord.Dm7,
            ChordFunction.Five => Chord.G7,
            ChordFunction.One => Chord.Cmaj7,
            _ => throw new ArgumentOutOfRangeException(nameof(function), function, null),
        };

        /// <summary>
        /// The function a chord fills in this progression, or null if the chord is not
        /// one of the three.
        /// </summary>
        public static ChordFunction? FunctionOf(Chord chord)
        {
            if (chord == Chord.Dm7)
            {
                return ChordFunction.Two;
            }

            if (chord == Chord.G7)
            {
                return ChordFunction.Five;
            }

            if (chord == Chord.Cmaj7)
            {
                return ChordFunction.One;
            }

            return null;
        }
    }
}
