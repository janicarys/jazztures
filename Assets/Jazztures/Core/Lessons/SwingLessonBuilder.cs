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
            // Straight into swing: C up to C and back down, twice over the four bars. The first scale is Ionian (C major);
            // the other modes are further steps once the lesson can give the regions their scale.
            new SwingStepSpec(
                "Ionian",
                new[]
                {
                    "Use your right hand. Open your palm, facing left.",
                    "Move your hand up and down to choose a note. Swing it across the line to play.",
                    "Follow the circles: they show how high to be and when to swing. Go up, then back down.",
                },
                new[] { "1 2 3 4 5 6 7 8", "8 7 6 5 4 3 2 1", "1 2 3 4 5 6 7 8", "8 7 6 5 4 3 2 1" },
                straightBars: 0,
                new[] { WL, TY, WL, TY }),
        };
    }
}
