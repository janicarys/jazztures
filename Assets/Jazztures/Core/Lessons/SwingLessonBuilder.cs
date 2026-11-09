#nullable enable
using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Bakes a <see cref="SwingStepSpec"/> into a <see cref="LessonTimeline"/> (ADR-0081): the system's looping
    /// Dm7 | G7 | Cmaj7 | Cmaj7, comped on beats 1 and 3, with the step's notes on top at fixed scale pitches. Pure.
    /// </summary>
    public static class SwingLessonBuilder
    {
        public const int BeatsPerBar = 4;
        public const int BarsPerBlock = 4;

        /// <summary>One chord per bar of the loop: ii, V, I, I.</summary>
        private static readonly ChordFunction[] Loop =
        {
            ChordFunction.Two, ChordFunction.Five, ChordFunction.One, ChordFunction.One,
        };

        public static LessonTimeline BuildTimeline(SwingStepSpec step, Tempo tempo, SwingRatio swing)
        {
            var builder = new LessonTimelineBuilder()
                .WithTempo(tempo)
                .WithSwing(swing)
                .WithBeatsPerBar(BeatsPerBar)
                .WithMinimumDuration(BarsPerBlock * BeatsPerBar);

            for (int bar = 0; bar < BarsPerBlock; bar++)
            {
                // Comping: the bar's chord is struck on beats 1 and 3.
                builder.Chord(bar * BeatsPerBar, Loop[bar]);
                builder.Chord(bar * BeatsPerBar + 2, Loop[bar]);
            }

            for (int bar = 0; bar < step.Bars.Count; bar++)
            {
                SwingPatternText.AddBars(builder, new[] { step.Bars[bar] }, BeatsPerBar, bar < step.StraightBars, offsetBars: bar);
            }

            return builder.Build();
        }

        /// <summary>The loop's chord for bar <paramref name="bar"/> (0-based), which wraps every four bars.</summary>
        public static ChordFunction ChordOfBar(int bar) => Loop[((bar % BarsPerBlock) + BarsPerBlock) % BarsPerBlock];

        private static readonly LearningMode WL = LearningMode.WatchAndListen;
        private static readonly LearningMode TY = LearningMode.TryYourself;

        /// <summary>The five steps as authored (the asset holds the same data, editable; a test keeps them equal).</summary>
        public static IReadOnlyList<SwingStepSpec> DefaultSteps() => new[]
        {
            new SwingStepSpec(
                "Hear the difference",
                new[]
                {
                    "Swing is the rhythm that makes jazz sound like jazz.",
                    "Count \"1 and 2 and\". In swing, each \"1\" is long and each \"and\" is short: long-short, long-short.",
                },
                new[] { "1 2 3 4", "1 2 3 4", "1 2 3 4", "1 2 3 4" },
                straightBars: 2,
                new[] { WL, WL }),

            new SwingStepSpec(
                "Ascending sequence",
                new[]
                {
                    "Use your right hand. Open your palm, facing left.",
                    "Move your hand up and down to choose a note. Swing it across the line to play.",
                    "Follow the circles: they show how high to be and when to swing.",
                },
                new[] { "1 2 3 4", "2 3 4 5", "3 4 5 6", "4 5 6 7" },
                straightBars: 0,
                new[] { WL, TY, WL, TY }),

            new SwingStepSpec(
                "Descending sequence",
                new[] { "Use your right hand. Now the same idea, going down." },
                new[] { "8 7 6 5", "7 6 5 4", "6 5 4 3", "5 4 3 2" },
                straightBars: 0,
                new[] { WL, TY, WL, TY }),

            new SwingStepSpec(
                "Up and down",
                new[] { "Use your right hand. Up, then down. Keep the long-short feel." },
                new[] { "1 2 3 4", "5 4 3 2", "3 4 5 6", "7 6 5 4" },
                straightBars: 0,
                new[] { WL, TY, WL, TY }),

            new SwingStepSpec(
                "The full run",
                new[] { "Use your right hand. Last one: one long run up, then back down, swinging all the way." },
                new[] { "1 2 3 4 5 6 7 8", "8 7 6 5 4 3 2 1", string.Empty, string.Empty },
                straightBars: 0,
                new[] { WL, TY, WL, TY }),
        };
    }
}
