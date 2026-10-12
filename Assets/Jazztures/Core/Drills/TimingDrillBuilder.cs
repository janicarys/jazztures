using System;
using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Drills
{
    /// <summary>
    /// Assembles a <see cref="TimingDrill"/>. The Unity preset asset and the edit-mode tests
    /// both build drills through this, so the invariants live in one place.
    /// </summary>
    public sealed class TimingDrillBuilder
    {
        /// <summary>One bar of count-in, as ADR-0043 specifies for drills (lessons have none).</summary>
        public const int DefaultCountInBars = 1;

        public const int DefaultBars = 4;

        private readonly List<TimelineChord> _chordChanges = new List<TimelineChord>();

        private string _name = "Drill";
        private Tempo _tempo = Tempo.Default;
        private int _beatsPerBar = Metronome.DefaultBeatsPerBar;
        private SwingRatio _swing = SwingRatio.Straight;
        private MetronomeSubdivision _subdivision = MetronomeSubdivision.None;
        private int _bars = DefaultBars;
        private int _countInBars = DefaultCountInBars;
        private OnsetPattern _pattern = OnsetPattern.None;
        private Handedness _scoredHand = Handedness.Right;
        private bool _scoringEnabled = true;
        private bool _metronomeAudible = true;
        private ChordFunction? _fixedChord;
        private bool _harmonyAudible = true;

        public TimingDrillBuilder Named(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A drill needs a name.", nameof(name));
            }

            _name = name;
            return this;
        }

        public TimingDrillBuilder WithTempo(Tempo tempo)
        {
            _tempo = tempo;
            return this;
        }

        public TimingDrillBuilder WithBeatsPerBar(int beatsPerBar)
        {
            if (beatsPerBar <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beatsPerBar), beatsPerBar, "Must be positive.");
            }

            _beatsPerBar = beatsPerBar;
            return this;
        }

        public TimingDrillBuilder WithSwing(SwingRatio swing)
        {
            _swing = swing;
            return this;
        }

        public TimingDrillBuilder WithSubdivision(MetronomeSubdivision subdivision)
        {
            _subdivision = subdivision;
            return this;
        }

        public TimingDrillBuilder WithBars(int bars)
        {
            if (bars <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bars), bars, "Must be positive.");
            }

            _bars = bars;
            return this;
        }

        public TimingDrillBuilder WithCountInBars(int countInBars)
        {
            if (countInBars < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(countInBars), countInBars, "Must not be negative.");
            }

            _countInBars = countInBars;
            return this;
        }

        public TimingDrillBuilder WithPattern(OnsetPattern pattern)
        {
            _pattern = pattern;
            return this;
        }

        public TimingDrillBuilder ScoringHand(Handedness hand)
        {
            _scoredHand = hand;
            return this;
        }

        /// <summary>False makes a metronome-only run: no expected onsets, no scoring, no CSV.</summary>
        public TimingDrillBuilder WithScoring(bool enabled)
        {
            _scoringEnabled = enabled;
            return this;
        }

        public TimingDrillBuilder WithMetronomeAudible(bool audible)
        {
            _metronomeAudible = audible;
            return this;
        }

        /// <summary>A fixed chord, selected for the whole run and struck on every bar line.</summary>
        public TimingDrillBuilder FixedChord(ChordFunction function)
        {
            _fixedChord = function;
            return this;
        }

        /// <summary>A chord change at <paramref name="beat"/> (from the shared start) of an authored timeline.</summary>
        public TimingDrillBuilder ChordAt(double beat, ChordFunction function)
        {
            _chordChanges.Add(new TimelineChord(new Beat(beat), function));
            return this;
        }

        /// <summary>Whether the scripted chords are sounded; they are selected either way.</summary>
        public TimingDrillBuilder WithHarmonyAudible(bool audible)
        {
            _harmonyAudible = audible;
            return this;
        }

        public TimingDrill Build()
        {
            if (_fixedChord.HasValue && _chordChanges.Count > 0)
            {
                throw new InvalidOperationException(
                    "A drill has either a fixed chord or a chord timeline, not both.");
            }

            DrillHarmonyKind harmonyKind = _fixedChord.HasValue
                ? DrillHarmonyKind.Fixed
                : _chordChanges.Count > 0 ? DrillHarmonyKind.Timeline : DrillHarmonyKind.None;

            if (_scoredHand == Handedness.Left && harmonyKind != DrillHarmonyKind.None)
            {
                throw new InvalidOperationException(
                    "A left-hand drill has the learner play the harmony; scripted harmony would collide with it.");
            }

            TimelineChord[] changes = _chordChanges.ToArray();
            Array.Sort(changes, (a, b) => a.Beat.CompareTo(b.Beat));
            for (int i = 1; i < changes.Length; i++)
            {
                if (changes[i].Beat == changes[i - 1].Beat)
                {
                    throw new InvalidOperationException($"Two chord changes on {changes[i].Beat}.");
                }
            }

            var drill = new TimingDrill(
                _name, _tempo, _beatsPerBar, _swing, _subdivision, _bars, _countInBars,
                _pattern, _scoredHand, _scoringEnabled, _metronomeAudible,
                harmonyKind, _fixedChord, changes, _harmonyAudible);

            if (_scoringEnabled && drill.ExpectedOnsetBeats().Length == 0)
            {
                throw new InvalidOperationException(
                    "A scored drill needs a pattern that produces at least one expected onset.");
            }

            return drill;
        }
    }
}
