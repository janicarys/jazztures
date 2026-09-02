using System;
using Jazztures.Core.Harmony;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Plays a <see cref="LessonScript"/> against the phrase clock and the learner's
    /// actions, raising <see cref="ActionFired"/> for each cue as it comes due (ADR-0011).
    /// The Unity lesson runner pumps <see cref="AdvanceTo"/> every frame and calls
    /// <see cref="Notify"/> from the domain events; it routes presentation actions to the
    /// HUD and control actions to the state machine.
    ///
    /// <para>
    /// Each cue fires at most once per play-through. <see cref="AtMarker"/> triggers are
    /// resolved to beats against the timeline at construction — an unknown marker name is
    /// an authoring error and throws here, not silently at runtime.
    /// </para>
    /// </summary>
    public sealed class LessonCuePlayer
    {
        private readonly TimeCue[] _timeCues;
        private readonly LessonCue[] _actionCues;
        private readonly bool[] _timeFired;
        private readonly bool[] _actionFired;

        private double _lastBeat = double.NegativeInfinity;

        public LessonCuePlayer(LessonScript script, LessonTimeline timeline)
        {
            if (script == null)
            {
                throw new ArgumentNullException(nameof(script));
            }

            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            int timeCount = 0;
            int actionCount = 0;
            foreach (LessonCue cue in script.Cues)
            {
                if (cue.Trigger.Kind == CueTriggerKind.OnLearnerAction)
                {
                    actionCount++;
                }
                else
                {
                    timeCount++;
                }
            }

            _timeCues = new TimeCue[timeCount];
            _actionCues = new LessonCue[actionCount];
            _timeFired = new bool[timeCount];
            _actionFired = new bool[actionCount];

            int t = 0;
            int a = 0;
            foreach (LessonCue cue in script.Cues)
            {
                switch (cue.Trigger.Kind)
                {
                    case CueTriggerKind.AtBeat:
                        _timeCues[t++] = new TimeCue(cue.Trigger.Beat, cue.Action);
                        break;

                    case CueTriggerKind.AtMarker:
                        double? markerBeat = timeline.MarkerBeat(cue.Trigger.Marker!);
                        if (!markerBeat.HasValue)
                        {
                            throw new InvalidOperationException(
                                $"Cue triggers on marker \"{cue.Trigger.Marker}\" but the timeline has no such marker.");
                        }

                        _timeCues[t++] = new TimeCue(markerBeat.Value, cue.Action);
                        break;

                    default:
                        _actionCues[a++] = cue;
                        break;
                }
            }

            Array.Sort(_timeCues, (x, y) => x.Beat.CompareTo(y.Beat));
        }

        /// <summary>Raised once per cue as it comes due, in trigger order.</summary>
        public event Action<CueAction>? ActionFired;

        /// <summary>The furthest beat the player has advanced to.</summary>
        public double CurrentBeat { get; private set; }

        /// <summary>Time-triggered cues still waiting to fire.</summary>
        public int PendingTimeCues
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _timeFired.Length; i++)
                {
                    if (!_timeFired[i])
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        /// <summary>
        /// Move the phrase clock to <paramref name="beat"/> (monotonic), firing every
        /// beat- and marker-triggered cue in <c>(previous, beat]</c>.
        /// </summary>
        public void AdvanceTo(double beat)
        {
            if (double.IsNaN(beat) || double.IsInfinity(beat))
            {
                throw new ArgumentOutOfRangeException(nameof(beat), beat, "Must be finite.");
            }

            if (beat < CurrentBeat)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(beat), beat, "The phrase clock only moves forward — call Reset to replay.");
            }

            for (int i = 0; i < _timeCues.Length; i++)
            {
                if (!_timeFired[i] && _timeCues[i].Beat > _lastBeat && _timeCues[i].Beat <= beat)
                {
                    _timeFired[i] = true;
                    ActionFired?.Invoke(_timeCues[i].Action);
                }
            }

            _lastBeat = beat;
            CurrentBeat = beat;
        }

        /// <summary>
        /// Report a learner action, firing every matching <see cref="CueTriggerKind.OnLearnerAction"/>
        /// cue that has not fired yet.
        /// </summary>
        public void Notify(LearnerAction action, ChordFunction? function = null, int? targetIndex = null)
        {
            for (int i = 0; i < _actionCues.Length; i++)
            {
                if (_actionFired[i])
                {
                    continue;
                }

                CueTrigger trigger = _actionCues[i].Trigger;
                if (trigger.Action != action)
                {
                    continue;
                }

                if (trigger.Function.HasValue && trigger.Function != function)
                {
                    continue;
                }

                if (trigger.TargetIndex.HasValue && trigger.TargetIndex != targetIndex)
                {
                    continue;
                }

                _actionFired[i] = true;
                ActionFired?.Invoke(_actionCues[i].Action);
            }
        }

        /// <summary>Rewind for a re-attempt: every cue is armed again and the clock is at zero.</summary>
        public void Reset()
        {
            Array.Clear(_timeFired, 0, _timeFired.Length);
            Array.Clear(_actionFired, 0, _actionFired.Length);
            _lastBeat = double.NegativeInfinity;
            CurrentBeat = 0.0;
        }

        private readonly struct TimeCue
        {
            public TimeCue(double beat, CueAction action)
            {
                Beat = beat;
                Action = action;
            }

            public double Beat { get; }

            public CueAction Action { get; }
        }
    }
}
