#nullable enable
using System;
using System.Collections.Generic;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// One step of the Swing lesson as plain data (ADR-0081): the cards shown first, four bars of pattern text
    /// (<see cref="SwingPatternText"/>) played over the looping ii-V-I, how many leading bars are straight rather than
    /// swung (the listening step), and the learning modes the four bars are run in, in order (Watch and Listen, Try Yourself,
    /// twice). Immutable.
    /// </summary>
    public sealed class SwingStepSpec
    {
        private readonly string[] _cards;
        private readonly string[] _bars;
        private readonly LearningMode[] _modes;

        public SwingStepSpec(
            string name, IEnumerable<string> cards, IEnumerable<string> bars, int straightBars, IEnumerable<LearningMode> modes)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _cards = new List<string>(cards ?? throw new ArgumentNullException(nameof(cards))).ToArray();
            _bars = new List<string>(bars ?? throw new ArgumentNullException(nameof(bars))).ToArray();
            _modes = new List<LearningMode>(modes ?? throw new ArgumentNullException(nameof(modes))).ToArray();
            StraightBars = straightBars;
        }

        public string Name { get; }

        public IReadOnlyList<string> Cards => _cards;

        /// <summary>Pattern text per bar, in order.</summary>
        public IReadOnlyList<string> Bars => _bars;

        /// <summary>The first this-many bars play with even eighths; the rest swing.</summary>
        public int StraightBars { get; }

        public IReadOnlyList<LearningMode> Modes => _modes;
    }
}
