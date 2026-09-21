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
    ///   <b>and</b> match for <see cref="GestureThresholds.ConfirmingFrames"/> frames
    ///   before it takes effect — tolerating up to
    ///   <see cref="GestureThresholds.ConfirmationMissTolerance"/> consecutive
    ///   non-matching frames along the way without losing progress (ADR-0025: a real pose
    ///   transition produces brief tracking noise, and penalising it made confirmation
    ///   time unpredictable rather than merely slow);</item>
    ///   <item>confirmed changes are debounced by
    ///   <see cref="GestureThresholds.MinInterChordSeconds"/>;</item>
    ///   <item>if ii and I both match (<see cref="HandPoseCandidate.Ambiguous"/>) the
    ///   previous state is held and nothing is emitted — never guess;</item>
    ///   <item>while tracking is Low or lost, all transitions are suppressed and the
    ///   current function is <b>sustained, not released</b>; input resumes only after
    ///   <see cref="GestureThresholds.HighFramesToResumeAfterLoss"/> consecutive
    ///   High-quality frames.</item>
    ///   <item>releasing <b>from</b> an already-confirmed function needs
    ///   <see cref="GestureThresholds.ReleaseHoldSeconds"/> /
    ///   <see cref="GestureThresholds.ReleaseMissTolerance"/> — more conservative than
    ///   confirming a pose (ADR-0027: a chord strike is itself a fast hand motion that can
    ///   disrupt the pose reading for longer than ordinary noise, and a false release is
    ///   audible — it cuts the sounding chord — where a merely late one is not).</item>
    /// </list>
    ///
    /// This only decides <b>which</b> function is selected. <b>When</b> it sounds is a
    /// separate, explicit event — see <see cref="ChordStrikeDetector"/> and
    /// <see cref="Jazztures.Core.Harmony.HarmonyEngine.Strike"/> (ADR-0025) — so pose
    /// selection can stay this tolerant without blurring musical timing.
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
        private int _pendingMatchFrames;
        private int _pendingMissRun;
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

        /// <summary>
        /// The function being <b>reached for</b> — the pose currently detected (even before
        /// it has been held long enough to confirm), or the confirmed one if the hand is
        /// steady. For UI that should react to intent as fast as free play does: a lesson
        /// gate uses this so it accepts the pose the moment the learner clearly makes it,
        /// not <see cref="GestureThresholds.PoseHoldSeconds"/> later.
        /// </summary>
        public ChordFunction? ReachingFunction => _hasPending ? TargetOf(_pendingCandidate) : _confirmed;

        /// <summary>What the interpreter is doing, for the gesture-state channel.</summary>
        public GesturePhase Phase { get; private set; }

        /// <summary>
        /// True once tracking is good enough to accept input (§3.5 — after
        /// <see cref="GestureThresholds.HighFramesToResumeAfterLoss"/> consecutive High
        /// frames). <see cref="Jazztures.Core.Gesture.ChordStrikeDetector"/> reads this so a
        /// strike cannot be manufactured from a tracking glitch (ADR-0025).
        /// </summary>
        public bool TrackingUsable => _trackingUsable;

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

        /// <summary>
        /// Seconds the just-confirmed pose was held (candidate first seen → confirmed) —
        /// the latency segment §4.3 says is the only one we control. Updated on each
        /// <see cref="ConfirmedFunctionChanged"/>; NaN before the first confirmation.
        /// </summary>
        public double LastConfirmationHoldSeconds { get; private set; } = double.NaN;

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

        /// <summary>
        /// ADR-0025: confirmation tolerates up to
        /// <see cref="GestureThresholds.ConfirmationMissTolerance"/> consecutive
        /// non-matching frames — a tracking blip during a real pose transition — without
        /// losing progress. Only a *sustained* mismatch (more misses than the tolerance)
        /// means the hand has genuinely moved on. This is what lets a learner re-take a
        /// pose at tempo instead of paying a fresh <see cref="GestureThresholds.PoseHoldSeconds"/>
        /// window every time the recogniser hiccups.
        /// </summary>
        private void ProcessCandidate(HandPoseCandidate candidate, double now)
        {
            if (candidate == HandPoseCandidate.Ambiguous)
            {
                RegisterMiss(now, newCandidate: null);
                return;
            }

            ChordFunction? target = TargetOf(candidate);

            if (_hasPending && _pendingCandidate == candidate)
            {
                RegisterMatch(target, now);
                return;
            }

            if (!_hasPending && target == _confirmed)
            {
                // Steady state: nothing in progress and this frame simply reaffirms what is
                // already confirmed. Not even a miss — the common case stays branch-light.
                SetPhase(_confirmed.HasValue ? GesturePhase.Confirmed : GesturePhase.Idle);
                return;
            }

            RegisterMiss(now, candidate);
        }

        /// <summary>
        /// This frame did not match the pose currently being confirmed (or nothing was
        /// pending). Within tolerance, the pending timer and match count are left exactly
        /// as they were — a blip costs the learner nothing. Once the tolerance is
        /// exceeded, start fresh on whatever this frame actually is.
        ///
        /// <para>
        /// ADR-0027: if what is pending is a <b>release</b> — the hand is reading
        /// <see cref="HandPoseCandidate.None"/> while a function is still confirmed — the
        /// tolerance is <see cref="GestureThresholds.ReleaseMissTolerance"/>, not
        /// <see cref="GestureThresholds.ConfirmationMissTolerance"/>. A chord strike is
        /// itself a fast hand motion that can disrupt the pose reading for longer than
        /// ordinary tracking noise; a false release is audible (it cuts the sounding
        /// chord), so releasing gets the more conservative budget.
        /// </para>
        ///
        /// <para>
        /// ADR-0029: that elevated budget applies only to <i>weak</i> evidence against the
        /// release — <see cref="HandPoseCandidate.Ambiguous"/>, <see cref="HandPoseCandidate.None"/>
        /// itself, or a reading that matches what's already confirmed (the hand bouncing
        /// back to the pose it never really left). A reading of a <b>different, concrete</b>
        /// pose is unambiguous: the learner is not mid-release, they are switching. That
        /// case always uses the ordinary tolerance, however this attempt started — a
        /// pose-to-pose switch commonly passes through one <c>None</c> frame first, and
        /// that single frame must not lock the whole switch to the release-attempt's
        /// far more patient budget.
        /// </para>
        /// </summary>
        private void RegisterMiss(double now, HandPoseCandidate? newCandidate)
        {
            if (_hasPending)
            {
                _pendingMissRun++;

                bool weakEvidence = !newCandidate.HasValue
                    || newCandidate.Value == HandPoseCandidate.None
                    || TargetOf(newCandidate.Value) == _confirmed;
                int tolerance = IsPendingRelease && weakEvidence
                    ? _thresholds.ReleaseMissTolerance
                    : _thresholds.ConfirmationMissTolerance;

                if (_pendingMissRun <= tolerance)
                {
                    SetPhase(GesturePhase.Detecting);
                    return;
                }
            }

            if (newCandidate.HasValue && TargetOf(newCandidate.Value) != _confirmed)
            {
                StartPending(newCandidate.Value, now);
            }
            else
            {
                ResetPending();
                SetPhase(_confirmed.HasValue ? GesturePhase.Confirmed : GesturePhase.Idle);
            }
        }

        /// <summary>
        /// ADR-0027: a release (<paramref name="target"/> null while a function is already
        /// confirmed) needs <see cref="GestureThresholds.ReleaseHoldSeconds"/>, not the
        /// ordinary <see cref="GestureThresholds.PoseHoldSeconds"/> — see
        /// <see cref="RegisterMiss"/> for why. Confirming a fresh pose (nothing was held) or
        /// switching between two concrete poses is unaffected and stays exactly as
        /// responsive as before.
        /// </summary>
        private void RegisterMatch(ChordFunction? target, double now)
        {
            _pendingMatchFrames++;
            _pendingMissRun = 0;
            SetPhase(GesturePhase.Detecting);

            bool isRelease = target == null && _confirmed.HasValue;
            double holdSeconds = isRelease ? _thresholds.ReleaseHoldSeconds : _thresholds.PoseHoldSeconds;

            bool heldLongEnough = now - _pendingSince >= holdSeconds;
            bool enoughFrames = _pendingMatchFrames >= _thresholds.ConfirmingFrames;
            bool debounceElapsed = now - _lastConfirmChangeTime >= _thresholds.MinInterChordSeconds;

            if (heldLongEnough && enoughFrames && debounceElapsed)
            {
                _confirmed = target;
                _lastConfirmChangeTime = now;
                LastConfirmationHoldSeconds = now - _pendingSince;
                ResetPending();
                SetPhase(_confirmed.HasValue ? GesturePhase.Confirmed : GesturePhase.Idle);
                ConfirmedFunctionChanged?.Invoke(_confirmed);
            }
        }

        private bool IsPendingRelease => TargetOf(_pendingCandidate) == null && _confirmed.HasValue;

        private void StartPending(HandPoseCandidate candidate, double now)
        {
            _hasPending = true;
            _pendingCandidate = candidate;
            _pendingMatchFrames = 1;
            _pendingMissRun = 0;
            _pendingSince = now;
            SetPhase(GesturePhase.Detecting);
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
            _pendingMatchFrames = 0;
            _pendingMissRun = 0;
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
