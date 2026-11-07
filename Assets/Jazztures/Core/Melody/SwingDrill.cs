using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// A sandbox drill (ADR-0078): a two-step direction pattern the target beats repeat, and the pitch each
    /// task starts from. The start pitch is chosen so the pattern has room — a down-down drill starts high, an
    /// up-up drill low — which is the phrase-reset answer to "direction sets the register too".
    /// </summary>
    public sealed class SwingDrill
    {
        private readonly StepDirection[] _pattern;

        public SwingDrill(string name, StepDirection[] pattern, int startMidi)
        {
            if (pattern == null || pattern.Length == 0)
            {
                throw new ArgumentException("A drill needs at least one step.", nameof(pattern));
            }

            Name = name ?? throw new ArgumentNullException(nameof(name));
            _pattern = (StepDirection[])pattern.Clone();
            StartMidi = startMidi;
        }

        public static SwingDrill UpUp => new SwingDrill("UU", new[] { StepDirection.Up, StepDirection.Up }, 72);

        public static SwingDrill DownDown => new SwingDrill("DD", new[] { StepDirection.Down, StepDirection.Down }, 88);

        public static SwingDrill UpDown => new SwingDrill("UD", new[] { StepDirection.Up, StepDirection.Down }, 79);

        public static SwingDrill DownUp => new SwingDrill("DU", new[] { StepDirection.Down, StepDirection.Up }, 79);

        public static SwingDrill[] All => new[] { UpUp, DownDown, UpDown, DownUp };

        public string Name { get; }

        public int StartMidi { get; }

        public int PatternLength => _pattern.Length;

        /// <summary>The direction target number <paramref name="targetIndex"/> asks for; the pattern repeats.</summary>
        public StepDirection DirectionAt(int targetIndex) => _pattern[targetIndex % _pattern.Length];
    }
}
