#nullable enable
using System;
using System.Collections.Generic;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// The Swing lesson's patterns as editable text (ADR-0081). One bar is eight eighth-note slots; each slot is a scale
    /// degree 1..9 (1 = C4 ... 9 = D5 on the C major scale) or <c>-</c> for a rest, separated by spaces. Missing trailing
    /// slots are rests. <c>"1 2 3 4 - - - -"</c> is a four-note run on beats 1 and 2 with beats 3 and 4 resting. Slot
    /// <i>n</i> is notated at <i>n</i>/2 of a beat: the swing ratio then pushes the off-beat slots late, so the same text
    /// is straight or swung depending on the lesson's ratio (or a note's straight flag). Pure.
    /// </summary>
    public static class SwingPatternText
    {
        public const int SlotsPerBar = 8;

        /// <summary>MIDI pitch of each scale degree 1..9: the C major scale from C4.</summary>
        private static readonly int[] DegreeMidi = { 60, 62, 64, 65, 67, 69, 71, 72, 74 };

        public static int DegreeCount => DegreeMidi.Length;

        public static int MidiOfDegree(int degree)
        {
            if (degree < 1 || degree > DegreeMidi.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(degree), degree, $"Must be 1..{DegreeMidi.Length}.");
            }

            return DegreeMidi[degree - 1];
        }

        /// <summary>One parsed note: where in the bar (in beats, notated) and which degree.</summary>
        public readonly struct Slot
        {
            public Slot(double beatInBar, int degree)
            {
                BeatInBar = beatInBar;
                Degree = degree;
            }

            public double BeatInBar { get; }

            public int Degree { get; }
        }

        /// <summary>The notes of one bar's text, in order. Throws <see cref="FormatException"/> on a bad token or too many slots.</summary>
        public static List<Slot> ParseBar(string text)
        {
            var notes = new List<Slot>();
            string[] tokens = (text ?? string.Empty).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > SlotsPerBar)
            {
                throw new FormatException($"A bar has {SlotsPerBar} slots; got {tokens.Length} in \"{text}\".");
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == "-")
                {
                    continue;
                }

                if (!int.TryParse(tokens[i], out int degree) || degree < 1 || degree > DegreeMidi.Length)
                {
                    throw new FormatException($"\"{tokens[i]}\" is not a degree 1..{DegreeMidi.Length} or '-' (in \"{text}\").");
                }

                notes.Add(new Slot(i * 0.5, degree));
            }

            return notes;
        }

        /// <summary>
        /// Add a block of bars to a timeline: for bar <i>b</i>, its notes at <c>b * beatsPerBar + slot</c>, each at
        /// the degree's fixed pitch.
        /// </summary>
        public static void AddBars(
            LessonTimelineBuilder builder, IReadOnlyList<string> bars, int beatsPerBar, bool straight, int offsetBars = 0,
            byte velocity = 90)
        {
            for (int bar = 0; bar < bars.Count; bar++)
            {
                foreach (Slot slot in ParseBar(bars[bar]))
                {
                    builder.PitchedNote(
                        (offsetBars + bar) * beatsPerBar + slot.BeatInBar, MidiOfDegree(slot.Degree), velocity, straight);
                }
            }
        }
    }
}
