using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Decides which finger of one hand "pressed a key" (ADR-0050 — an exploration: one finger
    /// bend = one chord tone). Pure: it is fed each finger's normalised bend (0 = flat, 1 = fully
    /// bent, from the SDK's own curl and flexion features on the Unity side) once a frame and
    /// reports presses.
    ///
    /// <para>
    /// Each finger is a small <b>peak/trough</b> machine. While <i>up</i> it remembers its lowest
    /// recent point (the trough); it presses when it has risen
    /// <see cref="FingerBendThresholds.PressDelta"/> above that. While <i>down</i> it remembers its
    /// highest point (the peak); it is re-armed when it has fallen
    /// <see cref="FingerBendThresholds.ReleaseDelta"/> below that. Everything is measured from the
    /// finger's own last turning point, never from a fixed "resting level", so a hand that settles
    /// back a little more curled than it started still re-arms and still presses again — the way a
    /// finger actually plays repeated notes — and the press level needs no calibration to the
    /// player's rest posture.
    /// </para>
    ///
    /// <para>
    /// Individual finger bends are the least reliable signal this hardware gives (DESIGN-SPACE D4),
    /// so two things keep it from firing by accident:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Hysteresis.</b> A press needs a full <see cref="FingerBendThresholds.PressDelta"/> rise
    ///     from the last trough, and a re-arm needs a fall from the peak, so jitter at a level is one
    ///     note, not a trill.
    ///   </item>
    ///   <item>
    ///     <b>Isolation.</b> A finger presses only if it has risen
    ///     <see cref="FingerBendThresholds.IsolationMargin"/> further than every other finger that is
    ///     not already held down. A fist, or a hand relaxing from open to rest, bends all five
    ///     together and sounds nothing; a neighbour dragged along by tendon coupling does not sound.
    ///     A finger held back by this is reported through <see cref="Blocked"/>.
    ///   </item>
    /// </list>
    ///
    /// <para>
    /// The trough forgets upward slowly (<see cref="FingerBendThresholds.BaselineSeconds"/>), so a very
    /// slow drift of one finger is absorbed rather than counted as a press. Allocation-free per frame.
    /// Not thread-safe.
    /// </para>
    /// </summary>
    public sealed class FingerBendDetector
    {
        /// <summary>Thumb, index, middle, ring, pinky — the order every array here uses.</summary>
        public const int FingerCount = 5;

        private readonly FingerBendThresholds _thresholds;

        // Per finger: the trough while up, the peak while down.
        private readonly float[] _anchor = new float[FingerCount];
        private readonly float[] _delta = new float[FingerCount];
        private readonly bool[] _down = new bool[FingerCount];
        private readonly bool[] _pressed = new bool[FingerCount];
        private readonly bool[] _blocked = new bool[FingerCount];
        private bool _seeded;

        public FingerBendDetector(FingerBendThresholds thresholds)
        {
            _thresholds = thresholds;
        }

        /// <summary>True when <paramref name="finger"/> was pressed by the last <see cref="Update"/>.</summary>
        public bool JustPressed(int finger) => _pressed[CheckFinger(finger)];

        /// <summary>
        /// True when <paramref name="finger"/> had risen far enough to press at the last
        /// <see cref="Update"/> but another finger had moved nearly as much, so it was not
        /// isolated and stayed silent. For diagnostics: it is the "I bent it and nothing played" case.
        /// </summary>
        public bool Blocked(int finger) => _blocked[CheckFinger(finger)];

        /// <summary>True from a press until the finger has fallen back from its peak.</summary>
        public bool IsDown(int finger) => _down[CheckFinger(finger)];

        /// <summary>
        /// How far the finger was above its trough (up) or relative to its peak (down, so zero or
        /// negative) at the last <see cref="Update"/> — for diagnostics and calibration only.
        /// </summary>
        public float LastDelta(int finger) => _delta[CheckFinger(finger)];

        /// <summary>
        /// Forget everything. Call whenever the hand is not tracked well: the next
        /// <see cref="Update"/> only learns where the fingers are and reports no presses, so a hand
        /// that comes back in some other shape cannot fire a note by arriving.
        /// </summary>
        public void Reset()
        {
            _seeded = false;
            Array.Clear(_down, 0, FingerCount);
            Array.Clear(_pressed, 0, FingerCount);
            Array.Clear(_blocked, 0, FingerCount);
            Array.Clear(_delta, 0, FingerCount);
        }

        /// <summary>
        /// Advance one frame. <paramref name="curls"/> holds <see cref="FingerCount"/> values in
        /// 0..1; <paramref name="deltaSeconds"/> is the time since the previous call.
        /// </summary>
        public void Update(float[] curls, double deltaSeconds)
        {
            if (curls == null)
            {
                throw new ArgumentNullException(nameof(curls));
            }

            if (curls.Length != FingerCount)
            {
                throw new ArgumentException($"Expected {FingerCount} curls, got {curls.Length}.", nameof(curls));
            }

            Array.Clear(_pressed, 0, FingerCount);
            Array.Clear(_blocked, 0, FingerCount);

            if (!_seeded)
            {
                for (int i = 0; i < FingerCount; i++)
                {
                    _anchor[i] = curls[i];
                    _delta[i] = 0f;
                }

                _seeded = true;
                return;
            }

            // Turning points: a held finger follows its peak up and is re-armed by a fall from it;
            // a free finger follows its trough down, and its rise above it is what may become a press.
            for (int i = 0; i < FingerCount; i++)
            {
                float curl = curls[i];
                if (_down[i])
                {
                    if (curl >= _anchor[i])
                    {
                        _anchor[i] = curl;
                    }
                    else if (_anchor[i] - curl >= _thresholds.ReleaseDelta)
                    {
                        _down[i] = false;
                        _anchor[i] = curl; // the new trough: the next press is a rise from here
                    }
                }
                else if (curl < _anchor[i])
                {
                    _anchor[i] = curl;
                }

                _delta[i] = curl - _anchor[i];
            }

            // Who rose furthest among the fingers that are free to press (a held finger is already
            // spoken for, so legato — the next finger pressing while the last is still down — works).
            for (int i = 0; i < FingerCount; i++)
            {
                if (_down[i] || _delta[i] < _thresholds.PressDelta)
                {
                    continue;
                }

                float strongestOther = float.NegativeInfinity;
                for (int j = 0; j < FingerCount; j++)
                {
                    if (j != i && !_down[j] && _delta[j] > strongestOther)
                    {
                        strongestOther = _delta[j];
                    }
                }

                if (_delta[i] - strongestOther >= _thresholds.IsolationMargin)
                {
                    _pressed[i] = true;
                }
                else
                {
                    _blocked[i] = true;
                }
            }

            for (int i = 0; i < FingerCount; i++)
            {
                if (_pressed[i])
                {
                    _down[i] = true;
                    _anchor[i] = curls[i]; // the peak so far
                }
            }

            ForgetTroughsUpward(curls, deltaSeconds);
        }

        private void ForgetTroughsUpward(float[] curls, double deltaSeconds)
        {
            if (deltaSeconds <= 0.0 || double.IsNaN(deltaSeconds))
            {
                return;
            }

            float follow = 1f - (float)Math.Exp(-deltaSeconds / _thresholds.BaselineSeconds);
            for (int i = 0; i < FingerCount; i++)
            {
                if (!_down[i] && curls[i] > _anchor[i])
                {
                    _anchor[i] += (curls[i] - _anchor[i]) * follow;
                }
            }
        }

        private static int CheckFinger(int finger)
        {
            if (finger < 0 || finger >= FingerCount)
            {
                throw new ArgumentOutOfRangeException(nameof(finger), finger, null);
            }

            return finger;
        }
    }
}
