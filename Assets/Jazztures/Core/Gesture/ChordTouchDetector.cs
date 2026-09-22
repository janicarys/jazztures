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
    /// Reads <see cref="GestureInterpreter.ConfirmedFunction"/>: with nothing selected, an
    /// entry is simply spent, no re-arm budget needed — like a pinch, a deliberate approach
    /// into a fixed volume is unambiguous, unlike a stray strike.
    /// </para>
    ///
    /// <para>
    /// Reads <see cref="GestureInterpreter.TrackingUsable"/> before updating the rising-edge
    /// state, freezing it through a dropout — the same principle as the pinch detector,
    /// protecting the edge itself rather than arm/settle state.
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
        /// start here too). Prefer this over calling <see cref="Feed(bool, float)"/> and
        /// <see cref="GestureInterpreter.Feed"/> separately.
        /// </summary>
        public void Feed(HandPoseFrame frame)
        {
            Feed(frame.LeftIsTouchingTarget, frame.LeftTouchEntrySpeedMetresPerSecond);
            _interpreter.Feed(frame);
        }

        /// <summary>
        /// Feed whether the left fingertip is inside the target volume this frame, and its
        /// peak entry speed (<see cref="HandPoseFrame.LeftIsTouchingTarget"/>,
        /// <see cref="HandPoseFrame.LeftTouchEntrySpeedMetresPerSecond"/>).
        /// </summary>
        public void Feed(bool isInsideTarget, float entrySpeedMetresPerSecond)
        {
            if (!_interpreter.TrackingUsable)
            {
                // Freeze the rising-edge detector through a dropout, before even reading
                // isInsideTarget against _wasInside — a tracking blip's own recovery
                // transition must not read as a fresh entry.
                return;
            }

            bool rising = isInsideTarget && !_wasInside;
            _wasInside = isInsideTarget;

            if (!_interpreter.ConfirmedFunction.HasValue)
            {
                // Nothing selected, nothing to articulate — an entry here is simply spent.
                return;
            }

            if (!rising)
            {
                return;
            }

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
