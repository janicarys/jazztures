using System;
using Jazztures.Core.Melody;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// Turns the left fingertip entering a virtual object's volume into a chord
    /// articulation event (ADR-0039) — a third articulation commit alongside the strike
    /// (ADR-0025) and the pinch (ADR-0038). Separate from <see cref="GestureInterpreter"/>
    /// by design, same as the other two: the interpreter decides <b>which</b> function is
    /// selected; this decides <b>when</b> it sounds. Wire <see cref="Articulated"/> to
    /// <see cref="Jazztures.Core.Harmony.HarmonyEngine.Strike"/>.
    ///
    /// <para>
    /// Unlike the strike detector, containment is a boolean the Unity adapter computes
    /// (fingertip inside <c>ChordStrikeTarget</c>'s <see cref="TargetVolume"/> or not), not
    /// a continuous signal this detector must itself threshold — the exact volume-entry
    /// test the right hand's ten melody targets already use (ADR-0016/0018), reused for
    /// harmony rather than re-derived. There is no arm/settle state and no Schmitt trigger
    /// here either, same reasoning as the pinch detector: a discrete "inside/outside" edge
    /// plus a retrigger cooldown replace what a continuous threshold otherwise needs.
    /// </para>
    ///
    /// <para>
    /// Re-derives velocity from <see cref="VelocityCurve.FromSpeed"/> against the
    /// fingertip's peak entry speed — the same curve and the same right-hand precedent the
    /// strike detector already uses, and the same entry-velocity gate
    /// (<see cref="TouchCommitThresholds.EntryVelocityGateMetresPerSecond"/>) melody's own
    /// targets use to reject a resting hand that merely drifts in rather than deliberately
    /// approaches (ADR-0018).
    /// </para>
    ///
    /// <para>
    /// Reads <see cref="GestureInterpreter.ConfirmedFunction"/>: with nothing selected yet,
    /// a rising edge doesn't get discarded outright — it goes <i>pending</i> (ADR-0042) and
    /// retries every subsequent frame the fingertip is still resting inside the target,
    /// until either confirmation lands (fires immediately) or
    /// <see cref="TouchCommitThresholds.MaxAwaitingConfirmationSeconds"/> elapses (spent,
    /// no re-arm). This matters because the reach that carries the fingertip into the
    /// target is exactly the motion most likely to still be mid-confirmation when it
    /// arrives — the original "an entry with nothing selected is simply spent" cut required
    /// selection to already be settled before the hand reached the plate, which a single
    /// continuous reach-and-touch motion routinely violates. Unlike a stray strike
    /// (ADR-0025's force-re-arm), a fingertip resting inside a fixed volume is unambiguous
    /// evidence of a deliberate approach, so waiting on it — briefly — is the right call
    /// rather than discarding it.
    /// </para>
    ///
    /// <para>
    /// Reads <see cref="GestureInterpreter.TrackingUsable"/> before updating the rising-edge
    /// state, freezing it through a dropout — the same principle as the pinch detector,
    /// protecting the edge itself rather than arm/settle state. A pending entry is likewise
    /// frozen, not ticked or discarded, while tracking is unusable.
    /// </para>
    ///
    /// <para>
    /// On every successful touch, calls <see cref="GestureInterpreter.NotifyStruck"/>
    /// (reused from ADR-0034 unchanged) for the same reason the pinch detector does.
    /// </para>
    /// </summary>
    public sealed class ChordTouchDetector
    {
        private readonly IMusicalClock _clock;
        private readonly GestureInterpreter _interpreter;
        private readonly TouchCommitThresholds _thresholds;

        private bool _wasInside;
        private bool _awaitingConfirmation;
        private float _awaitingEntrySpeed;
        private double _awaitingSince;
        private double _lastTouchTime = double.NegativeInfinity;

        public ChordTouchDetector(IMusicalClock clock, GestureInterpreter interpreter, TouchCommitThresholds thresholds)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
            _thresholds = thresholds;
        }

        /// <summary>Raised with the articulated MIDI velocity when a touch rising edge commits the confirmed chord.</summary>
        public event Action<byte>? Articulated;

        /// <summary>
        /// Feed one frame: checks/fires the touch first, then hands the frame to
        /// <see cref="GestureInterpreter.Feed"/> (ADR-0037's ordering, built in from the
        /// start here too), then retries a pending entry once more (ADR-0042) — this frame's
        /// own confirmation, just processed, may be exactly what a still-resting fingertip
        /// was waiting on, and re-checking here (rather than waiting for the next call)
        /// costs it zero extra latency. Prefer this over calling
        /// <see cref="Feed(bool, float)"/> and <see cref="GestureInterpreter.Feed"/>
        /// separately.
        /// </summary>
        public void Feed(HandPoseFrame frame)
        {
            Feed(frame.LeftIsTouchingTarget, frame.LeftTouchEntrySpeedMetresPerSecond);
            _interpreter.Feed(frame);
            TryFireAwaitingEntry();
        }

        /// <summary>
        /// Feed whether the left fingertip is inside the target volume this frame, and its
        /// peak entry speed (<see cref="HandPoseFrame.LeftIsTouchingTarget"/>,
        /// <see cref="HandPoseFrame.LeftTouchEntrySpeedMetresPerSecond"/>).
        /// </summary>
        public void Feed(bool isInsideTarget, float entrySpeedMetresPerSecond)
        {
            // A pending entry from an earlier frame may resolve against whatever
            // GestureInterpreter.ConfirmedFunction already is, before this frame's own
            // containment is even considered.
            TryFireAwaitingEntry();

            if (!_interpreter.TrackingUsable)
            {
                // Freeze the rising-edge detector through a dropout, before even reading
                // isInsideTarget against _wasInside — a tracking blip's own recovery
                // transition must not read as a fresh entry. A pending entry is likewise
                // left exactly as it was (TryFireAwaitingEntry itself declines to resolve
                // while tracking is unusable).
                return;
            }

            bool rising = isInsideTarget && !_wasInside;
            _wasInside = isInsideTarget;

            if (!isInsideTarget)
            {
                // Left the volume — an entry we were still waiting on is moot; the next
                // approach must produce its own fresh rising edge and speed.
                _awaitingConfirmation = false;
                return;
            }

            if (!rising)
            {
                return;
            }

            if (!_interpreter.ConfirmedFunction.HasValue)
            {
                // Nothing selected yet (ADR-0042). Hold this entry's speed and retry once
                // confirmation lands, rather than discarding it outright — see the class
                // doc comment.
                _awaitingConfirmation = true;
                _awaitingEntrySpeed = entrySpeedMetresPerSecond;
                _awaitingSince = _clock.Now;
                return;
            }

            TryArticulate(entrySpeedMetresPerSecond);
        }

        /// <summary>
        /// Resolve a pending entry (ADR-0042): fire it if confirmation has landed, give up
        /// on it if its grace window has elapsed, or leave it pending otherwise. No-ops
        /// while tracking is unusable or nothing is pending, so it is safe to call on every
        /// frame regardless of state.
        /// </summary>
        private void TryFireAwaitingEntry()
        {
            if (!_awaitingConfirmation || !_interpreter.TrackingUsable)
            {
                return;
            }

            if (_interpreter.ConfirmedFunction.HasValue)
            {
                _awaitingConfirmation = false;
                TryArticulate(_awaitingEntrySpeed);
                return;
            }

            if (_clock.Now - _awaitingSince > _thresholds.MaxAwaitingConfirmationSeconds)
            {
                // Grace window elapsed with nothing ever confirmed — give up rather than
                // wait indefinitely for some unrelated, much-later confirmation.
                _awaitingConfirmation = false;
            }
        }

        private void TryArticulate(float entrySpeedMetresPerSecond)
        {
            if (entrySpeedMetresPerSecond < _thresholds.EntryVelocityGateMetresPerSecond)
            {
                // A resting hand drifting into the volume, not a deliberate touch — the
                // same question §3.3's melody entry gate answers (ADR-0018).
                return;
            }

            double now = _clock.Now;
            if (now - _lastTouchTime < _thresholds.MinInterTouchSeconds)
            {
                return;
            }

            _lastTouchTime = now;
            _interpreter.NotifyStruck();
            Articulated?.Invoke(VelocityCurve.FromSpeed(entrySpeedMetresPerSecond));
        }
    }
}
