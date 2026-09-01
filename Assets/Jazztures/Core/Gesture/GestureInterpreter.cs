using System;
using Jazztures.Core.Harmony;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// Turns the per-frame left-hand pose match (<see cref="HandPoseFrame"/>) into a
    /// confirmed <see cref="ChordFunction"/>, applying every temporal rule from
    /// CLAUDE.md §3.4 and §3.5:
    ///
    /// <list type="bullet">
    ///   <item>a pose must be held for <see cref="GestureThresholds.PoseHoldSeconds"/>
    ///   <b>and</b> for <see cref="GestureThresholds.ConfirmingFrames"/> consecutive
    ///   frames before it takes effect;</item>
    ///   <item>confirmed changes are debounced by
    ///   <see cref="GestureThresholds.MinInterChordSeconds"/>;</item>
    ///   <item>if ii and I both match (<see cref="HandPoseCandidate.Ambiguous"/>) the
    ///   previous state is held and nothing is emitted — never guess;</item>
    ///   <item>while tracking is Low or lost, all transitions are suppressed and the
    ///   current function is <b>sustained, not released</b>; input resumes only after
    ///   <see cref="GestureThresholds.HighFramesToResumeAfterLoss"/> consecutive
    ///   High-quality frames.</item>
    /// </list>
    ///
    /// Pure and deterministic — driven by an injected <see cref="IMusicalClock"/> so it
    /// can be unit-tested and replayed against recorded fixtures without a headset (§2.6).
    /// Feed it one <see cref="HandPoseFrame"/> per frame via <see cref="Feed"/>.
    /// </summary>
    public sealed class GestureInterpreter
    {
        private readonly IMusicalClock _clock;
        private readonly GestureThresholds _thresholds;

        private ChordFunction? _confirmed;
        private double _lastConfirmChangeTime = double.NegativeInfinity;

        private bool _hasPending;
        private HandPoseCandidate _pendingCandidate;
        private int _pendingFrameCount;
        private double _pendingSince;

        private bool _trackingUsable;
        private int _highFrames;
        private double _trackingLostSince = double.NaN;

        public GestureInterpreter(IMusicalClock clock, GestureThresholds thresholds)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _thresholds = thresholds;
            Phase = GesturePhase.Suppressed;
        }

        /// <summary>The confirmed chord function, or null for "no chord held".</summary>
        public ChordFunction? ConfirmedFunction => _confirmed;

        /// <summary>What the interpreter is doing, for the gesture-state channel.</summary>
        public GesturePhase Phase { get; private set; }

        private void SetPhase(GesturePhase phase)
        {
            if (Phase == phase)
            {
                return;
            }

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        /// <summary>
        /// True once tracking has been lost for at least
        /// <see cref="GestureThresholds.TrackingLossCueSeconds"/> — the trigger for the
        /// non-modal desaturation cue (§3.5.2). Never true before the first good tracking.
        /// </summary>
        public bool TrackingCueActive { get; private set; }

        /// <summary>Raised only when <see cref="ConfirmedFunction"/> actually changes.</summary>
        public event Action<ChordFunction?>? ConfirmedFunctionChanged;

        /// <summary>Raised only when <see cref="Phase"/> actually changes.</summary>
        public event Action<GesturePhase>? PhaseChanged;

        public void Feed(HandPoseFrame frame)
        {
            double now = _clock.Now;
            TrackingQuality tracking = frame.LeftTracking;

            if (tracking == TrackingQuality.NotTracked || tracking == TrackingQuality.Low)
            {
                EnterTrackingLoss(now);
                UpdateTrackingCue(now);
                return;
            }

            if (!_trackingUsable)
            {
                if (tracking == TrackingQuality.High)
                {
                    _highFrames++;
                    if (_highFrames >= _thresholds.HighFramesToResumeAfterLoss)
                    {
                        _trackingUsable = true;
                        _trackingLostSince = double.NaN;
                    }
                }
                else
                {
                    _highFrames = 0; // Medium does not count towards resuming
                }

                if (!_trackingUsable)
                {
                    ResetPending();
                    SetPhase(GesturePhase.Suppressed);
                    UpdateTrackingCue(now);
                    return;
                }
            }

            UpdateTrackingCue(now);
            ProcessCandidate(frame.LeftCandidate, now);
        }

        private void EnterTrackingLoss(double now)
        {
            if (_trackingUsable)
            {
                _trackingLostSince = now;
            }

            _trackingUsable = false;
            _highFrames = 0;
            ResetPending();
            SetPhase(GesturePhase.Suppressed);
            // _confirmed is deliberately left untouched — sustain, do not release (§3.5.1).
        }

        private void ProcessCandidate(HandPoseCandidate candidate, double now)
        {
            if (candidate == HandPoseCandidate.Ambiguous)
            {
                ResetPending();
                SetPhase(_confirmed.HasValue ? GesturePhase.Confirmed : GesturePhase.Idle);
                return;
            }

            ChordFunction? target = TargetOf(candidate);

            if (target == _confirmed)
            {
                ResetPending();
                SetPhase(_confirmed.HasValue ? GesturePhase.Confirmed : GesturePhase.Idle);
                return;
            }

            if (!_hasPending || _pendingCandidate != candidate)
            {
                _hasPending = true;
                _pendingCandidate = candidate;
                _pendingFrameCount = 1;
                _pendingSince = now;
                SetPhase(GesturePhase.Detecting);
                return;
            }

            _pendingFrameCount++;
            SetPhase(GesturePhase.Detecting);

            bool heldLongEnough = now - _pendingSince >= _thresholds.PoseHoldSeconds;
            bool enoughFrames = _pendingFrameCount >= _thresholds.ConfirmingFrames;
            bool debounceElapsed = now - _lastConfirmChangeTime >= _thresholds.MinInterChordSeconds;

            if (heldLongEnough && enoughFrames && debounceElapsed)
            {
                _confirmed = target;
                _lastConfirmChangeTime = now;
                ResetPending();
                SetPhase(_confirmed.HasValue ? GesturePhase.Confirmed : GesturePhase.Idle);
                ConfirmedFunctionChanged?.Invoke(_confirmed);
            }
        }

        private void UpdateTrackingCue(double now)
        {
            TrackingCueActive = !_trackingUsable
                && !double.IsNaN(_trackingLostSince)
                && now - _trackingLostSince >= _thresholds.TrackingLossCueSeconds;
        }

        private void ResetPending()
        {
            _hasPending = false;
            _pendingFrameCount = 0;
        }

        private static ChordFunction? TargetOf(HandPoseCandidate candidate) => candidate switch
        {
            HandPoseCandidate.None => null,
            HandPoseCandidate.Ii => ChordFunction.Two,
            HandPoseCandidate.V => ChordFunction.Five,
            HandPoseCandidate.I => ChordFunction.One,
            _ => throw new ArgumentOutOfRangeException(nameof(candidate), candidate, null),
        };
    }
}
