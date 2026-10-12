using System;
using System.Collections.Generic;

namespace Jazztures.Core.Timing
{
    /// <summary>
    /// Where a learner is expected to play, as a repeatable rule rather than a list of
    /// notes (ADR-0043): every beat, every second beat, every eighth, or an authored set of
    /// positions that repeats every bar. Used by timing drills and by a lesson's scored
    /// pulse (L2). Pure data — it never reads a clock.
    ///
    /// <para>
    /// Positions are in <b>straight</b> beats; swing is applied afterwards by whoever turns
    /// beats into seconds (<see cref="SwingQuantizer"/>), so an every-eighth pattern's
    /// off-beats swing with the rest of the phrase. Immutable value type (ADR-0007).
    /// </para>
    /// </summary>
    public readonly struct OnsetPattern
    {
        private enum PatternKind
        {
            None = 0,
            Interval = 1,
            PerBar = 2,
        }

        private const double Epsilon = 1e-9;

        private readonly PatternKind _kind;
        private readonly double _intervalBeats;
        private readonly double[]? _barPositions;

        private OnsetPattern(PatternKind kind, double intervalBeats, double[]? barPositions)
        {
            _kind = kind;
            _intervalBeats = intervalBeats;
            _barPositions = barPositions;
        }

        /// <summary>No expected onsets.</summary>
        public static OnsetPattern None => default;

        public static OnsetPattern EveryBeat => Every(1.0);

        public static OnsetPattern EverySecondBeat => Every(2.0);

        public static OnsetPattern EveryEighth => Every(0.5);

        /// <summary>An onset every <paramref name="beats"/> beats, starting on the first beat of the span.</summary>
        public static OnsetPattern Every(double beats)
        {
            if (double.IsNaN(beats) || double.IsInfinity(beats) || beats <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(beats), beats, "Must be finite and positive.");
            }

            return new OnsetPattern(PatternKind.Interval, beats, null);
        }

        /// <summary>
        /// The same set of <paramref name="positionsInBar"/> in every bar, each a beat
        /// offset from the bar's start (0 is the downbeat, 0.5 the "and" of 1). Order and
        /// duplicates are rejected so an authored list reads back as written.
        /// </summary>
        public static OnsetPattern PerBar(IEnumerable<double> positionsInBar)
        {
            if (positionsInBar == null)
            {
                throw new ArgumentNullException(nameof(positionsInBar));
            }

            var positions = new List<double>(positionsInBar);
            if (positions.Count == 0)
            {
                throw new ArgumentException("A per-bar pattern needs at least one position.", nameof(positionsInBar));
            }

            positions.Sort();
            for (int i = 0; i < positions.Count; i++)
            {
                double p = positions[i];
                if (double.IsNaN(p) || double.IsInfinity(p) || p < 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(positionsInBar), p, "Positions must be finite and not negative.");
                }

                if (i > 0 && Math.Abs(p - positions[i - 1]) < Epsilon)
                {
                    throw new ArgumentException($"Duplicate position {p}.", nameof(positionsInBar));
                }
            }

            return new OnsetPattern(PatternKind.PerBar, 0.0, positions.ToArray());
        }

        public bool IsNone => _kind == PatternKind.None;

        /// <summary>
        /// The pattern's beats within [<paramref name="fromBeat"/>, <paramref name="toBeat"/>),
        /// in ascending order. A per-bar pattern's bars are counted from
        /// <paramref name="fromBeat"/>, with <paramref name="beatsPerBar"/> beats each.
        /// </summary>
        public double[] BeatsBetween(double fromBeat, double toBeat, int beatsPerBar)
        {
            if (double.IsNaN(fromBeat) || double.IsNaN(toBeat) || double.IsInfinity(fromBeat) || double.IsInfinity(toBeat))
            {
                throw new ArgumentOutOfRangeException(nameof(fromBeat), "Beat span must be finite.");
            }

            if (beatsPerBar <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beatsPerBar), beatsPerBar, "Must be positive.");
            }

            var beats = new List<double>();

            switch (_kind)
            {
                case PatternKind.Interval:
                    for (long k = 0; ; k++)
                    {
                        double beat = fromBeat + k * _intervalBeats;
                        if (beat >= toBeat - Epsilon)
                        {
                            break;
                        }

                        beats.Add(beat);
                    }

                    break;

                case PatternKind.PerBar:
                    double[] positions = _barPositions!;
                    if (positions[positions.Length - 1] >= beatsPerBar)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(beatsPerBar), beatsPerBar,
                            $"A position ({positions[positions.Length - 1]}) falls outside a {beatsPerBar}-beat bar.");
                    }

                    for (long bar = 0; ; bar++)
                    {
                        double barStart = fromBeat + bar * beatsPerBar;
                        if (barStart >= toBeat - Epsilon)
                        {
                            break;
                        }

                        foreach (double position in positions)
                        {
                            double beat = barStart + position;
                            if (beat < toBeat - Epsilon)
                            {
                                beats.Add(beat);
                            }
                        }
                    }

                    break;
            }

            return beats.ToArray();
        }

        public override string ToString() => _kind switch
        {
            PatternKind.Interval => $"every {_intervalBeats:0.###} beat(s)",
            PatternKind.PerBar => $"per bar at {string.Join(", ", _barPositions!)}",
            _ => "none",
        };
    }
}
