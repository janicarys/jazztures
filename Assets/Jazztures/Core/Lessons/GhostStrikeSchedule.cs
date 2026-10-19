using System;
using System.Collections.Generic;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// When the ghost's left hand strikes the pad, and how far through each strike it is
    /// (ADR-0052). A strike is a movement, not a pose, so it cannot be captured by the Ghost Pose
    /// Recorder; it is described by one number per strike — the beat the hand meets the pad — and
    /// two shared durations. <see cref="ProgressAt"/> turns a beat into a 0..1 "how far toward the
    /// pad" that the view maps to a position, and <see cref="ContactsUpTo"/> counts the strikes
    /// that have landed so the view can flash the pad once per strike.
    ///
    /// <para>
    /// The shape of a strike: the hand accelerates onto the pad (progress rises as the square of
    /// time, so it arrives fast, like a real strike) and reaches 1 exactly on the strike beat, then
    /// lifts away on a smooth ease. Where two strikes overlap, the nearer one wins.
    /// </para>
    ///
    /// <para>Immutable. Allocation-free after construction.</para>
    /// </summary>
    public sealed class GhostStrikeSchedule
    {
        private readonly double[] _beats;
        private readonly double _approachBeats;
        private readonly double _returnBeats;

        public GhostStrikeSchedule(IEnumerable<double> strikeBeats, double approachBeats, double returnBeats)
        {
            if (strikeBeats == null)
            {
                throw new ArgumentNullException(nameof(strikeBeats));
            }

            if (double.IsNaN(approachBeats) || double.IsInfinity(approachBeats) || approachBeats <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(approachBeats), approachBeats, "Must be finite and positive.");
            }

            if (double.IsNaN(returnBeats) || double.IsInfinity(returnBeats) || returnBeats <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(returnBeats), returnBeats, "Must be finite and positive.");
            }

            var beats = new List<double>(strikeBeats);
            foreach (double beat in beats)
            {
                if (double.IsNaN(beat) || double.IsInfinity(beat))
                {
                    throw new ArgumentOutOfRangeException(nameof(strikeBeats), beat, "Strike beats must be finite.");
                }
            }

            beats.Sort();
            _beats = beats.ToArray();
            _approachBeats = approachBeats;
            _returnBeats = returnBeats;
        }

        /// <summary>No strikes — the ghost hand stays where it is.</summary>
        public static GhostStrikeSchedule None { get; } =
            new GhostStrikeSchedule(Array.Empty<double>(), 0.5, 0.5);

        /// <summary>The beats the hand meets the pad, ascending.</summary>
        public IReadOnlyList<double> Beats => _beats;

        public int Count => _beats.Length;

        /// <summary>
        /// The beat by which the last strike has finished lifting away — the earliest the phrase
        /// can end without cutting a strike short. 0 when there are none.
        /// </summary>
        public double EndBeat => _beats.Length == 0 ? 0.0 : _beats[_beats.Length - 1] + _returnBeats;

        /// <summary>
        /// How far toward the pad the hand is at <paramref name="beat"/>: 0 at rest, 1 at the
        /// instant of contact. See the type remarks for the shape.
        /// </summary>
        public double ProgressAt(double beat)
        {
            double progress = 0.0;

            for (int i = 0; i < _beats.Length; i++)
            {
                double strike = _beats[i];
                double since = beat - strike;

                double p;
                if (since < -_approachBeats)
                {
                    break; // sorted: no later strike has started either
                }
                else if (since <= 0.0)
                {
                    double t = (since + _approachBeats) / _approachBeats;
                    p = t * t;
                }
                else if (since < _returnBeats)
                {
                    double u = since / _returnBeats;
                    p = 1.0 - u * u * (3.0 - 2.0 * u);
                }
                else
                {
                    continue;
                }

                if (p > progress)
                {
                    progress = p;
                }
            }

            return progress;
        }

        /// <summary>How many strikes have landed by <paramref name="beat"/> (a strike lands on its beat).</summary>
        public int ContactsUpTo(double beat)
        {
            int count = 0;
            for (int i = 0; i < _beats.Length && _beats[i] <= beat; i++)
            {
                count++;
            }

            return count;
        }
    }
}
