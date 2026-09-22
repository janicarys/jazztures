using System;
using Jazztures.Core.Melody;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// Turns an index-to-thumb pinch into a chord articulation event (Design A, ADR-0038)
    /// — the pinch-commit counterpart of <see cref="ChordStrikeDetector"/>. Separate from
    /// <see cref="GestureInterpreter"/> by design, same as the strike detector: the
    /// interpreter decides <b>which</b> function is selected (here, from
    /// <see cref="HarmonicField"/> instead of the discrete pose recognisers); this decides
    /// <b>when</b> it sounds. Wire <see cref="Articulated"/> to
    /// <see cref="Jazztures.Core.Harmony.HarmonyEngine.Strike"/>.
    ///
    /// <para>
    /// Unlike the strike detector, there is no arm/settle state and no Schmitt trigger on
    /// the pinch itself — the SDK's own pinch boolean is the edge, and a rising-edge check
    /// plus a retrigger cooldown do all the debouncing a continuous velocity threshold
    /// otherwise needs. That absence is the point of this pivot: every bug the strike
    /// detector accumulated (ADR-0030, -0031, -0035) was about a continuous signal's
    /// history leaking into a frame where the physical motion had already moved on. A
    /// discrete self-contact event has no history to leak.
    /// </para>
    ///
    /// <para>
    /// Re-derives velocity from <see cref="VelocityCurve.FromNormalized"/> against a
    /// pinch-<i>closing rate</i> (not raw pinch strength, which is nearly constant at the
    /// rising edge and carries almost no dynamic range) — the pinch-commit analogue of the
    /// strike detector's fingertip speed.
    /// </para>
    ///
    /// <para>
    /// Reads <see cref="GestureInterpreter.ConfirmedFunction"/>: with nothing selected a
    /// pinch is simply spent, no re-arm budget needed — unlike a stray strike, a pinch is
    /// unambiguously deliberate, so there is nothing to protect against.
    /// </para>
    ///
    /// <para>
    /// Reads <see cref="GestureInterpreter.TrackingUsable"/> <i>before</i> updating the
    /// rising-edge state, freezing it through a dropout — the same ADR-0031 principle one
    /// level down, at finger-confidence granularity: the Unity adapter holds the last
    /// reported pinch value through a confidence blip rather than forcing it false, so this
    /// detector must not treat the blip's own recovery transition as a fresh rising edge.
    /// </para>
    ///
    /// <para>
    /// On every successful pinch, calls <see cref="GestureInterpreter.NotifyStruck"/>
    /// (reused from ADR-0034 unchanged) — a pinch is at least as strong evidence the hand
    /// is still engaged with the selected landmark as a strike is, which cancels a release
    /// that happens to be pending rather than leaving it to tick down regardless.
    /// </para>
    /// </summary>
    public sealed class ChordPinchDetector
    {
        private readonly IMusicalClock _clock;
        private readonly GestureInterpreter _interpreter;
        private readonly PinchCommitThresholds _thresholds;

        private bool _wasPinching;
        private double _lastPinchTime = double.NegativeInfinity;

        public ChordPinchDetector(IMusicalClock clock, GestureInterpreter interpreter, PinchCommitThresholds thresholds)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
            _thresholds = thresholds;
        }

        /// <summary>Raised with the articulated MIDI velocity when a pinch rising edge commits the confirmed chord.</summary>
        public event Action<byte>? Articulated;

        /// <summary>
        /// Feed one frame: checks/fires the pinch first, then hands the frame to
        /// <see cref="GestureInterpreter.Feed"/> (ADR-0037's ordering, reused here from the
        /// start rather than discovered the hard way again). A release that has been
        /// pending can cross <see cref="GestureThresholds.ReleaseHoldSeconds"/> on the very
        /// same frame as a genuine pinch, and <see cref="GestureInterpreter.NotifyStruck"/>
        /// can only cancel a pending release once a commit gesture has actually fired.
        /// Feeding the interpreter first would let that same-frame release win the race.
        /// Prefer this over calling <see cref="Feed(bool, float)"/> and
        /// <see cref="GestureInterpreter.Feed"/> separately.
        /// </summary>
        public void Feed(HandPoseFrame frame)
        {
            Feed(frame.LeftIsPinching, frame.LeftPinchClosingRatePerSecond);
            _interpreter.Feed(frame);
        }

        /// <summary>
        /// Feed the left hand's pinch state and closing rate for this frame
        /// (<see cref="HandPoseFrame.LeftIsPinching"/>, <see cref="HandPoseFrame.LeftPinchClosingRatePerSecond"/>).
        /// </summary>
        public void Feed(bool isPinching, float pinchClosingRatePerSecond)
        {
            if (!_interpreter.TrackingUsable)
            {
                // Freeze the rising-edge detector through a dropout, before even reading
                // isPinching against _wasPinching — a confidence blip's own recovery
                // transition must not read as a fresh pinch.
                return;
            }

            bool rising = isPinching && !_wasPinching;
            _wasPinching = isPinching;

            if (!_interpreter.ConfirmedFunction.HasValue)
            {
                // Nothing selected, nothing to articulate. A pinch here is simply spent —
                // no re-arm budget to protect, unlike a stray strike, because a pinch is
                // unambiguously deliberate.
                return;
            }

            if (!rising)
            {
                return;
            }

            double now = _clock.Now;
            if (now - _lastPinchTime < _thresholds.MinInterPinchSeconds)
            {
                return;
            }

            _lastPinchTime = now;
            _interpreter.NotifyStruck();

            float range = _thresholds.PinchRateAtMaxVelocityPerSecond - _thresholds.PinchRateAtMinVelocityPerSecond;
            float t = (pinchClosingRatePerSecond - _thresholds.PinchRateAtMinVelocityPerSecond) / range;
            Articulated?.Invoke(VelocityCurve.FromNormalized(t));
        }
    }
}
