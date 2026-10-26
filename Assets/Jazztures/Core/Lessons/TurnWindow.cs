using System;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Which part of a lesson's repeating round is the system's turn and which is the learner's
    /// (ADR-0077) — the call-and-response of an antecedent-consequent lesson: the system plays the first
    /// <see cref="SystemTurnBeats"/> of every <see cref="RoundBeats"/>, then the learner answers for the rest.
    /// During the system's turn the lesson's chords and melody sound and the left hand does not select; in the
    /// learner's turn the system is silent and the left hand selects the chord again.
    ///
    /// <para>
    /// <c>default</c> / <see cref="None"/> is "no turns": the system's turn is the whole lesson, which is what every
    /// lesson before ADR-0077 does. Beats before 0 (the lead-in) count as the system's turn.
    /// </para>
    /// </summary>
    public readonly struct TurnWindow : IEquatable<TurnWindow>
    {
        public TurnWindow(double roundBeats, double systemTurnBeats)
        {
            if (double.IsNaN(roundBeats) || double.IsInfinity(roundBeats) || roundBeats <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(roundBeats), roundBeats, "Must be finite and positive.");
            }

            if (double.IsNaN(systemTurnBeats) || double.IsInfinity(systemTurnBeats)
                || systemTurnBeats <= 0.0 || systemTurnBeats >= roundBeats)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(systemTurnBeats), systemTurnBeats,
                    "Must be positive and shorter than the round - otherwise the learner never gets a turn.");
            }

            RoundBeats = roundBeats;
            SystemTurnBeats = systemTurnBeats;
        }

        /// <summary>No turns: the system's turn never ends.</summary>
        public static TurnWindow None => default;

        /// <summary>Length of one round in beats, or 0 when there are no turns.</summary>
        public double RoundBeats { get; }

        /// <summary>How much of each round the system plays, from the round's start, in beats.</summary>
        public double SystemTurnBeats { get; }

        public bool IsEnabled => RoundBeats > 0.0;

        /// <summary>True when the system, not the learner, has <paramref name="beat"/>.</summary>
        public bool IsSystemTurn(double beat)
        {
            if (!IsEnabled || beat < 0.0)
            {
                return true;
            }

            return beat % RoundBeats < SystemTurnBeats;
        }

        /// <summary>
        /// The beat at which the system's turn that contains <paramref name="beat"/> ends - or, if
        /// <paramref name="beat"/> is in the learner's turn, the end of the system's turn in the same round
        /// (already past). <see cref="double.PositiveInfinity"/> when there are no turns.
        /// </summary>
        public double SystemTurnEnd(double beat)
        {
            if (!IsEnabled)
            {
                return double.PositiveInfinity;
            }

            double round = Math.Floor(Math.Max(0.0, beat) / RoundBeats);
            return round * RoundBeats + SystemTurnBeats;
        }

        public bool Equals(TurnWindow other) =>
            RoundBeats == other.RoundBeats && SystemTurnBeats == other.SystemTurnBeats;

        public override bool Equals(object? obj) => obj is TurnWindow other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(RoundBeats, SystemTurnBeats);

        public override string ToString() =>
            IsEnabled ? $"{SystemTurnBeats} of every {RoundBeats} beats" : "no turns";
    }
}
