using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Scores crossings against a <see cref="DrillSchedule"/> (ADR-0078). A crossing is matched to the nearest
    /// still-unmatched target within the window (`[TUNABLE]`, ±150 ms by default): the right direction is a
    /// <see cref="DrillResult.Hit"/>, the wrong one a <see cref="DrillResult.WrongDirection"/>. A target whose window
    /// closes with no crossing is a <see cref="DrillResult.Miss"/>; a crossing that matches no target is an
    /// <see cref="DrillResult.Extra"/> (how a double-trigger shows up). The same scoring runs for the finger-bend input, which
    /// supplies a direction taken from its pitch movement.
    /// </summary>
    public sealed class DrillMatcher
    {
        public const double DefaultWindowSeconds = 0.150;

        private readonly DrillSchedule _schedule;
        private readonly double _window;
        private readonly bool[] _resolved;
        private int _firstUnresolved;
        private int _hits, _wrong, _misses, _extras, _offsetCount;
        private double _offsetSum;

        public DrillMatcher(DrillSchedule schedule, double windowSeconds = DefaultWindowSeconds)
        {
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            if (windowSeconds <= 0.0 || double.IsNaN(windowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(windowSeconds), windowSeconds, "Must be > 0.");
            }

            _window = windowSeconds;
            _resolved = new bool[schedule.Count];
        }

        /// <summary>Raised for every scored event, as it is decided.</summary>
        public event Action<DrillEvent>? Scored;

        public DrillSchedule Schedule => _schedule;

        public bool IsComplete => _firstUnresolved >= _schedule.Count;

        public DrillSummary Summary => new DrillSummary(
            _hits, _wrong, _misses, _extras, _offsetCount > 0 ? _offsetSum / _offsetCount : 0.0);

        /// <summary>A note was swung (or, for finger bend, pressed) at <paramref name="time"/>.</summary>
        public void OnCrossing(double time, StepDirection sampledDirection, SwingDirection swing, int pitchMidi)
        {
            int best = -1;
            double bestDistance = double.MaxValue;
            for (int i = _firstUnresolved; i < _schedule.Count; i++)
            {
                double targetTime = _schedule[i].Time;
                if (targetTime - time > _window)
                {
                    break; // targets are in time order: nothing later can be within the window
                }

                if (_resolved[i])
                {
                    continue;
                }

                double distance = Math.Abs(time - targetTime);
                if (distance <= _window && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            if (best < 0)
            {
                _extras++;
                Scored?.Invoke(new DrillEvent(
                    DrillResult.Extra, -1, double.NaN, StepDirection.Same, time, swing, sampledDirection,
                    pitchMidi, double.NaN, 0.0));
                return;
            }

            DrillTarget target = _schedule[best];
            _resolved[best] = true;
            double offset = time - target.Time;
            _offsetSum += Math.Abs(offset);
            _offsetCount++;
            bool hit = sampledDirection == target.Direction;
            if (hit)
            {
                _hits++;
            }
            else
            {
                _wrong++;
            }

            Scored?.Invoke(new DrillEvent(
                hit ? DrillResult.Hit : DrillResult.WrongDirection, best, target.Time, target.Direction, time, swing,
                sampledDirection, pitchMidi, offset, 0.0));
            AdvancePointer();
        }

        /// <summary>Time has reached <paramref name="now"/>: any target whose window has closed unmatched is a miss.</summary>
        public void Advance(double now)
        {
            for (int i = _firstUnresolved; i < _schedule.Count; i++)
            {
                if (_schedule[i].Time + _window >= now)
                {
                    break;
                }

                if (!_resolved[i])
                {
                    Miss(i);
                }
            }

            AdvancePointer();
        }

        /// <summary>The run is over: every target still open is a miss.</summary>
        public void Finish()
        {
            for (int i = _firstUnresolved; i < _schedule.Count; i++)
            {
                if (!_resolved[i])
                {
                    Miss(i);
                }
            }

            AdvancePointer();
        }

        /// <summary>The gate was closed from <paramref name="start"/> for <paramref name="durationSeconds"/>. Logged, not counted.</summary>
        public void OnTrackingLost(double start, double durationSeconds)
        {
            Scored?.Invoke(new DrillEvent(
                DrillResult.TrackingLost, -1, double.NaN, StepDirection.Same, start, SwingDirection.LeftToRight,
                StepDirection.Same, -1, double.NaN, durationSeconds));
        }

        private void Miss(int index)
        {
            _resolved[index] = true;
            _misses++;
            DrillTarget target = _schedule[index];
            Scored?.Invoke(new DrillEvent(
                DrillResult.Miss, index, target.Time, target.Direction, double.NaN, SwingDirection.LeftToRight,
                StepDirection.Same, -1, double.NaN, 0.0));
        }

        private void AdvancePointer()
        {
            while (_firstUnresolved < _schedule.Count && _resolved[_firstUnresolved])
            {
                _firstUnresolved++;
            }
        }
    }
}
