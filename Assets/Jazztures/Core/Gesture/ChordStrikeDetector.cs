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
    /// Reads <see cref="GestureInterpreter.TrackingUsable"/> so a tracking glitch can never
    /// manufacture a strike, and re-derives velocity from
    /// <see cref="VelocityCurve.FromSpeed"/> — the same curve the right hand's touch
    /// targets use — rather than a second bespoke mapping.
    /// </para>
    ///
    /// <para>
    /// Also reads <see cref="GestureInterpreter.ConfirmedFunction"/>: with nothing
    /// selected there is nothing to strike, so a downward motion in that state is treated
    /// the same as a tracking glitch — it forces a re-arm rather than counting toward one.
    /// This keeps a stray hand-drop between chords from consuming the cooldown budget a
    /// real strike would need moments later.
    /// </para>
    ///
    /// <para>
    /// Re-arming needs <see cref="GestureThresholds.StrikeSettleFrames"/> consecutive
    /// frames at or below the exit speed, not one (ADR-0030) — a real strike's own
    /// deceleration often rebounds slightly, and a single qualifying frame mid-rebound
    /// would re-arm early enough for that same rebound to fire a second, unintended strike.
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
        /// Feed the left hand's signed vertical speed for this frame (negative = downward,
        /// <see cref="HandPoseFrame.LeftVerticalSpeedMetresPerSecond"/>).
        /// </summary>
        public void Feed(float leftVerticalSpeedMetresPerSecond)
        {
            if (!_interpreter.TrackingUsable || !_interpreter.ConfirmedFunction.HasValue)
            {
                // Don't let a glitch (or a hand-drop with nothing selected) count as
                // "settled" either — the very next good frame must not read as
                // already-armed-and-crossing.
                _armed = true;
                _settleFrames = 0;
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
            Struck?.Invoke(VelocityCurve.FromSpeed(downwardSpeed));
        }
    }
}
