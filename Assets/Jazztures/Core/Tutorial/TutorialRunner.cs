using System;
using Jazztures.Core.Menu;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Tutorial
{
    /// <summary>
    /// Walks the learner through the tutorial script (ADR-0053), one step at a time, moving on only
    /// when they have actually done what the step asked. Pure: it is fed the right hand's
    /// <see cref="MenuHandFrame"/> every frame and told about each <see cref="MenuGesture"/> the menu
    /// recogniser recognises, and it raises <see cref="StepChanged"/> / <see cref="Completed"/> for the
    /// app to show text, ghost hands, the menu and the sky.
    ///
    /// <list type="bullet">
    ///   <item><b>Posture gates</b> (hands in view, out of view, open hand with the thumb up) must hold
    ///   for a moment, so a flicker of tracking is not "doing it". Thumb-up has a Schmitt pair of angles
    ///   (§3.4) so it does not flicker at the edge. "In view" and "out of view" are judged by <i>where the
    ///   hand is relative to the learner's gaze</i>, not by tracking quality: the Quest keeps tracking hands
    ///   hanging at the learner's sides, so waiting for tracking to be lost would wait forever. A posture
    ///   step that is still unmet after <see cref="TutorialThresholds.PostureTimeoutSeconds"/> is passed
    ///   (<see cref="TimedOut"/>) rather than trap the learner.</item>
    ///   <item><b>Gesture gates</b> (fist, flick, strike) fire on the recogniser's own events — the
    ///   tutorial teaches exactly the gestures the menu will then expect, with the same thresholds.</item>
    ///   <item>Every step is shown at least <see cref="TutorialThresholds.MinStepSeconds"/> before a
    ///   posture gate may release it (so a learner already in position still gets to read the line);
    ///   a gesture arriving in the first <see cref="TutorialThresholds.EventGraceSeconds"/> of a step is
    ///   ignored, since it is the tail of the last step's motion.</item>
    /// </list>
    /// </summary>
    public sealed class TutorialRunner
    {
        private readonly TutorialPlan _plan;
        private readonly IMusicalClock _clock;
        private readonly TutorialThresholds _thresholds;

        private int _index = -1;
        private double _stepStart;
        private double _heldSince = double.NaN;
        private bool _satisfied;
        private bool _thumbUp;
        private bool _finished;

        public TutorialRunner(TutorialPlan plan, IMusicalClock clock, TutorialThresholds thresholds)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _thresholds = thresholds;
        }

        /// <summary>Raised when a step begins (including the first), with its index.</summary>
        public event Action<int, TutorialStep>? StepChanged;

        /// <summary>Raised once, when the last step's gate is met (or <see cref="Skip"/> is called).</summary>
        public event Action? Completed;

        /// <summary>Raised with the step's index when a posture step is passed because it timed out, just before the next begins.</summary>
        public event Action<int>? TimedOut;

        public bool IsRunning => _index >= 0 && !_finished;

        public bool IsComplete => _finished;

        public int StepIndex => _index;

        public int StepCount => _plan.Count;

        /// <summary>The step in progress, or null before <see cref="Start"/> and after the end.</summary>
        public TutorialStep? CurrentStep => IsRunning ? _plan[_index] : (TutorialStep?)null;

        /// <summary>Begin at the first step. Does nothing if already started.</summary>
        public void Start()
        {
            if (_index >= 0)
            {
                return;
            }

            Enter(0);
        }

        /// <summary>End the tutorial now (a study operator skipping it, or a returning learner).</summary>
        public void Skip()
        {
            if (IsRunning)
            {
                Finish();
            }
        }

        /// <summary>Feed this frame's right hand. Call once per frame.</summary>
        public void Feed(in MenuHandFrame frame)
        {
            if (!IsRunning)
            {
                return;
            }

            double now = _clock.Now;
            TutorialStep step = _plan[_index];

            switch (step.Gate)
            {
                case TutorialGate.HandsInView:
                    Hold(InFront(frame.Tracking, frame.ViewAngleDegrees)
                         && InFront(frame.LeftTracking, frame.LeftViewAngleDegrees),
                        _thresholds.InViewHoldSeconds, now);
                    break;

                case TutorialGate.HandsOutOfView:
                    Hold(OutOfView(frame.Tracking, frame.ViewAngleDegrees)
                         && OutOfView(frame.LeftTracking, frame.LeftViewAngleDegrees),
                        _thresholds.OutOfViewHoldSeconds, now);
                    break;

                case TutorialGate.AfterSeconds:
                    if (now - _stepStart >= step.GateSeconds)
                    {
                        _satisfied = true;
                    }

                    break;

                case TutorialGate.OpenHandThumbUp:
                    bool tracked = frame.Tracking == TrackingQuality.High;
                    if (!tracked)
                    {
                        _thumbUp = false;
                    }
                    else if (_thumbUp)
                    {
                        _thumbUp = frame.ThumbUpAngleDegrees <= _thresholds.ThumbUpExitDegrees;
                    }
                    else
                    {
                        _thumbUp = frame.ThumbUpAngleDegrees <= _thresholds.ThumbUpEnterDegrees;
                    }

                    Hold(tracked && _thumbUp && frame.MaxFingerClosure <= _thresholds.OpenHandMaxClosure,
                        _thresholds.ThumbUpHoldSeconds, now);
                    break;

                default:
                    return; // gesture gates are met by Notify, not by posture
            }

            if (_satisfied && now - _stepStart >= _thresholds.MinStepSeconds)
            {
                Advance();
                return;
            }

            // The safety valve: a posture the hardware cannot see must not strand a learner (gesture steps
            // never time out - the menu needs them). Not for the timed explanation, which ends by itself.
            if (_thresholds.PostureTimeoutSeconds > 0.0
                && step.Gate != TutorialGate.AfterSeconds
                && now - _stepStart >= _thresholds.PostureTimeoutSeconds)
            {
                TimedOut?.Invoke(_index);
                Advance();
            }
        }

        private bool InFront(TrackingQuality tracking, float viewAngleDegrees) =>
            tracking == TrackingQuality.High && viewAngleDegrees <= _thresholds.InViewMaxDegrees;

        private bool OutOfView(TrackingQuality tracking, float viewAngleDegrees) =>
            tracking != TrackingQuality.High || viewAngleDegrees >= _thresholds.OutOfViewMinDegrees;

        /// <summary>
        /// Tell the tutorial about a gesture the menu recogniser just recognised. A fist ends the tutorial on any
        /// step but the one that asks for it, after the same short grace as every other gesture.
        /// </summary>
        public void Notify(MenuGesture gesture)
        {
            if (!IsRunning || _clock.Now - _stepStart < _thresholds.EventGraceSeconds)
            {
                return;
            }

            // The fist is the way home everywhere in the menu (ADR-0051). On the step that teaches it, it is the
            // answer; on any other step it means "I know this" and ends the tutorial (ADR-0053).
            if (gesture == MenuGesture.Fist && _plan[_index].Gate != TutorialGate.RightFist)
            {
                Finish();
                return;
            }

            bool met;
            switch (_plan[_index].Gate)
            {
                case TutorialGate.RightFist:
                    met = gesture == MenuGesture.Fist;
                    break;

                case TutorialGate.Flick:
                    met = gesture == MenuGesture.FlickLeft || gesture == MenuGesture.FlickRight;
                    break;

                case TutorialGate.Strike:
                    met = gesture == MenuGesture.Strike;
                    break;

                default:
                    return;
            }

            if (met)
            {
                Advance();
            }
        }

        private void Hold(bool condition, double seconds, double now)
        {
            if (!condition)
            {
                _heldSince = double.NaN;
                return;
            }

            if (double.IsNaN(_heldSince))
            {
                _heldSince = now;
            }

            if (now - _heldSince >= seconds)
            {
                _satisfied = true; // latched: stays met until the minimum display time has passed
            }
        }

        private void Advance()
        {
            if (_index + 1 >= _plan.Count)
            {
                Finish();
            }
            else
            {
                Enter(_index + 1);
            }
        }

        private void Enter(int index)
        {
            _index = index;
            _stepStart = _clock.Now;
            _heldSince = double.NaN;
            _satisfied = false;
            _thumbUp = false;
            StepChanged?.Invoke(index, _plan[index]);
        }

        private void Finish()
        {
            _finished = true;
            Completed?.Invoke();
        }
    }
}
