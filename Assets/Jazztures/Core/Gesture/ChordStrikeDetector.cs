using System;
using Jazztures.Core.Melody;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// Turns a downward hand motion into a chord articulation event (ADR-0025). Separate
    /// from <see cref="GestureInterpreter"/> by design: the interpreter decides <b>which</b>
    /// function is selected (a static pose, tolerant of noise); this decides <b>when</b> it
    /// sounds (a ballistic motion, Schmitt-triggered like every other gesture threshold in
    /// §3.4). Wire <see cref="Struck"/> to <see cref="Jazztures.Core.Harmony.HarmonyEngine.Strike"/>.
    ///
    /// <para>
    /// Re-derives velocity from <see cref="VelocityCurve.FromSpeed"/> — the same curve
    /// the right hand's touch targets use — rather than a second bespoke mapping.
    /// </para>
    ///
    /// <para>
    /// Reads <see cref="GestureInterpreter.ConfirmedFunction"/>: with nothing selected
    /// there is nothing to strike, so a downward motion in that state is ignored and
    /// forces a re-arm — this keeps a stray hand-drop between chords from consuming the
    /// cooldown/settle budget a real strike will need against whatever gets selected next.
    /// </para>
    ///
    /// <para>
    /// Also reads <see cref="GestureInterpreter.TrackingUsable"/> so a tracking glitch can
    /// never manufacture a strike — but, unlike the no-selection case above, a
    /// tracking-unusable frame leaves arm/settle state exactly as it was rather than
    /// forcing a re-arm (ADR-0031). A strike is fast motion, which is exactly what
    /// degrades optical tracking, so a blip landing mid-strike is a realistic case, not an
    /// edge case; forcing a re-arm there would let that same strike's still-fast downward
    /// motion fire a second time the instant tracking resumes — the ADR-0030 double-fire,
    /// reopened through a different door. §3.5's "sustain, do not release" applies here
    /// too: whatever this detector's own state was going into a tracking loss is exactly
    /// what should still apply coming out of it.
    /// </para>
    ///
    /// <para>
    /// Re-arming needs <see cref="GestureThresholds.StrikeSettleFrames"/> consecutive
    /// frames at or below the exit speed, not one (ADR-0030) — a real strike's own
    /// deceleration often rebounds slightly, and a single qualifying frame mid-rebound
    /// would re-arm early enough for that same rebound to fire a second, unintended strike.
    /// </para>
    ///
    /// <para>
    /// On every successful strike, calls <see cref="GestureInterpreter.NotifyStruck"/>
    /// (ADR-0034) — a strike is definitive proof the hand is still holding the pose it just
    /// articulated, which cancels a release that happens to be pending at that moment
    /// rather than leaving it to tick down in the background and cut the chord moments
    /// later regardless of the strike.
    /// </para>
    /// </summary>
    public sealed class ChordStrikeDetector
    {
        private readonly IMusicalClock _clock;
        private readonly GestureInterpreter _interpreter;
        private readonly GestureThresholds _thresholds;

        private bool _armed = true;
        private int _settleFrames;
        private double _lastStrikeTime = double.NegativeInfinity;

        public ChordStrikeDetector(IMusicalClock clock, GestureInterpreter interpreter, GestureThresholds thresholds)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
            _thresholds = thresholds;
        }

        /// <summary>Raised with the struck MIDI velocity when a downward motion crosses the enter threshold.</summary>
        public event Action<byte>? Struck;

        /// <summary>
        /// Feed one frame: strikes first, then hands the frame to
        /// <see cref="GestureInterpreter.Feed"/> (ADR-0037). This order is load-bearing,
        /// not incidental — a release that has been pending can cross
        /// <see cref="GestureThresholds.ReleaseHoldSeconds"/> on the very same frame as a
        /// genuine strike, and <see cref="GestureInterpreter.NotifyStruck"/> can only
        /// cancel a pending release once a strike has actually fired. Feeding the
        /// interpreter first would let that same-frame release win the race and clear
        /// <see cref="GestureInterpreter.ConfirmedFunction"/> before the strike ever gets
        /// to check it, silently swallowing a real strike. Feeding the strike detector
        /// first evaluates it against whatever the interpreter was still holding at the
        /// end of the previous frame, so a genuine strike always gets first claim.
        /// Prefer this over calling <see cref="Feed(float)"/> and
        /// <see cref="GestureInterpreter.Feed"/> separately, where the correct order has
        /// to be remembered by every caller instead of being guaranteed here.
        /// </summary>
        public void Feed(HandPoseFrame frame)
        {
            Feed(frame.LeftVerticalSpeedMetresPerSecond);
            _interpreter.Feed(frame);
        }

        /// <summary>
        /// Feed the left hand's signed vertical speed for this frame (negative = downward,
        /// <see cref="HandPoseFrame.LeftVerticalSpeedMetresPerSecond"/>).
        /// </summary>
        public void Feed(float leftVerticalSpeedMetresPerSecond)
        {
            if (!_interpreter.ConfirmedFunction.HasValue)
            {
                // Nothing selected, nothing to strike. Force a re-arm so a stray
                // hand-drop between chords can't consume the cooldown/settle budget a
                // real strike will need against whatever gets selected next.
                _armed = true;
                _settleFrames = 0;
                return;
            }

            if (!_interpreter.TrackingUsable)
            {
                // ADR-0031: freeze arm/settle state during a tracking dropout instead of
                // forcing a re-arm. A strike is fast motion — exactly what degrades
                // optical tracking — so a blip can land mid-strike; forcing _armed back
                // to true here would let that same strike's still-fast downward motion
                // fire a second time the instant tracking resumes (the ADR-0030
                // double-fire, reopened through a different door). Whatever state
                // existed going into the loss still applies coming out of it — the same
                // "sustain, do not release" policy §3.5 already applies to the confirmed
                // chord itself.
                return;
            }

            float downwardSpeed = -leftVerticalSpeedMetresPerSecond;

            if (!_armed)
            {
                // ADR-0030: re-arming needs StrikeSettleFrames consecutive frames at or
                // below the exit speed, not just one. A real strike's own deceleration often
                // includes a brief rebound; one qualifying frame mid-rebound would re-arm
                // early and let the rebound's small secondary motion fire a second strike.
                if (downwardSpeed <= _thresholds.StrikeExitSpeedMetresPerSecond)
                {
                    _settleFrames++;
                    if (_settleFrames >= _thresholds.StrikeSettleFrames)
                    {
                        _armed = true;
                        _settleFrames = 0;
                    }
                }
                else
                {
                    _settleFrames = 0;
                }

                return;
            }

            if (downwardSpeed < _thresholds.StrikeEnterSpeedMetresPerSecond)
            {
                return;
            }

            double now = _clock.Now;
            if (now - _lastStrikeTime < _thresholds.MinInterStrikeSeconds)
            {
                return;
            }

            _armed = false;
            _settleFrames = 0;
            _lastStrikeTime = now;
            _interpreter.NotifyStruck();
            Struck?.Invoke(VelocityCurve.FromSpeed(downwardSpeed));
        }
    }
}
