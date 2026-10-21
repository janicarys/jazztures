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
    ///   <item>
    ///     <b>The drag veto (ADR-0068).</b> A <i>held</i> finger is left out of that comparison, so that
    ///     legato works - but the finger that has just pressed is exactly the one dragging its neighbours
    ///     along, a few frames behind and a little less. Left out, it let the ring press after the middle
    ///     had, and the pinky after the ring: one bend, three notes. So a held finger that is still moving
    ///     (or stopped within <see cref="FingerBendThresholds.CouplingSeconds"/>) is consulted after all: a
    ///     finger that has not out-risen it by the margin is dragged, not played. Its level is then taken as
    ///     its new rest - it cannot relax while the other finger stays bent - so it needs a fresh rise from
    ///     there to play on purpose, and cannot fire late once the driver stops.
    ///   </item>
    ///   <item>
    ///     <b>The thumb must mean it.</b> It is the noisiest finger, and a one-frame spike is not a press a
    ///     hand can make, so it must qualify on <see cref="FingerBendThresholds.ThumbConfirmFrames"/> frames running.
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
        private readonly int[] _blockedBy = new int[FingerCount];
        private readonly bool[] _dragged = new bool[FingerCount];

        // A held finger's own history, for the drag veto: the trough it pressed from, the peak it last advanced
        // past by a real amount, and when - "still moving" means that was within CouplingSeconds.
        private readonly float[] _pressTrough = new float[FingerCount];
        private readonly float[] _moveReference = new float[FingerCount];
        private readonly double[] _lastMovedAt = new double[FingerCount];
        private readonly int[] _confirm = new int[FingerCount];
        private double _time;
        private bool _seeded;

        // Diagnostics only - nothing here feeds the press decision. A free finger that rose a real amount toward its press level and
        // fell back without reaching it (a "near miss"), and the whole stroke of a finger that did press, reported when it lets go.
        private readonly float[] _rise = new float[FingerCount];      // peak of the rise in progress; 0 = none
        private readonly int[] _riseDriver = new int[FingerCount];
        private readonly bool[] _spent = new bool[FingerCount];       // reached the press level: not a near miss, however it ends
        private readonly bool[] _nearMiss = new bool[FingerCount];
        private readonly float[] _nearMissPeak = new float[FingerCount];
        private readonly int[] _nearMissDriver = new int[FingerCount];
        private readonly bool[] _released = new bool[FingerCount];
        private readonly float[] _releasePeak = new float[FingerCount];

        /// <summary>
        /// A rise counts as an attempt once it passes this fraction of the finger's press level: clear of tracking noise
        /// (about +-0.03), and low enough to see a curl that fell short. Diagnostics only.
        /// </summary>
        public const float NearMissFraction = 0.6f;

        // How far a held finger's peak must advance to count as moving. Larger than tracking noise on a held finger
        // (which only ever lifts the peak once, to the noise's own top), so a still hand is never "moving".
        private const float MoveDelta = 0.04f;

        public FingerBendDetector(FingerBendThresholds thresholds)
        {
            _thresholds = thresholds;
            ClearHistory();
        }

        /// <summary>True when <paramref name="finger"/> was pressed by the last <see cref="Update"/>.</summary>
        public bool JustPressed(int finger) => _pressed[CheckFinger(finger)];

        /// <summary>
        /// True when <paramref name="finger"/> had risen far enough to press at the last
        /// <see cref="Update"/> but another finger had moved nearly as much, so it was not
        /// isolated and stayed silent. For diagnostics: it is the "I bent it and nothing played" case.
        /// </summary>
        public bool Blocked(int finger) => _blocked[CheckFinger(finger)];

        /// <summary>
        /// For a finger that <see cref="Blocked"/> at the last <see cref="Update"/>: the other finger it lost to - the
        /// free finger that moved nearly as much, or the held finger that was still moving and dragging it (ADR-0068).
        /// -1 when it was not blocked. For diagnostics.
        /// </summary>
        public int BlockedBy(int finger) => _blockedBy[CheckFinger(finger)];

        /// <summary>True when the last <see cref="Update"/> judged <paramref name="finger"/> to be dragged along by a held one.</summary>
        public bool WasDragged(int finger) => _dragged[CheckFinger(finger)];

        /// <summary>True from a press until the finger has fallen back from its peak.</summary>
        public bool IsDown(int finger) => _down[CheckFinger(finger)];

        /// <summary>The rise <paramref name="finger"/> needs, from its trough, to press: what a near miss fell short of.</summary>
        public float PressLevel(int finger) => _thresholds.PressDeltaFor(CheckFinger(finger));

        /// <summary>
        /// This frame, <paramref name="finger"/> finished a rise that passed <see cref="NearMissFraction"/> of its press level
        /// but never reached it (and it was not pressed or blocked in the meantime): a curl too small to sound.
        /// </summary>
        public bool NearMiss(int finger) => _nearMiss[CheckFinger(finger)];

        /// <summary>How far that near miss rose, from its trough.</summary>
        public float NearMissPeak(int finger) => _nearMissPeak[CheckFinger(finger)];

        /// <summary>
        /// The finger that was pressed and still moving while the near miss rose - so it was probably dragged along, not
        /// played - or -1 if none was: a weak deliberate curl.
        /// </summary>
        public int NearMissDrivenBy(int finger) => _nearMissDriver[CheckFinger(finger)];

        /// <summary>This frame, <paramref name="finger"/> let go of a press (the next one is armed).</summary>
        public bool Released(int finger) => _released[CheckFinger(finger)];

        /// <summary>How far that pressed finger had risen, from the trough it pressed from, at its peak.</summary>
        public float ReleasePeak(int finger) => _releasePeak[CheckFinger(finger)];

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
            Array.Clear(_rise, 0, FingerCount);
            Array.Clear(_spent, 0, FingerCount);
            Array.Clear(_nearMiss, 0, FingerCount);
            Array.Clear(_released, 0, FingerCount);
            ClearHistory();
        }

        private void ClearHistory()
        {
            for (int i = 0; i < FingerCount; i++)
            {
                _blockedBy[i] = -1;
                _dragged[i] = false;
                _confirm[i] = 0;
                _riseDriver[i] = -1;
                _nearMissDriver[i] = -1;
                _lastMovedAt[i] = double.NegativeInfinity;
            }
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
            Array.Clear(_dragged, 0, FingerCount);
            Array.Clear(_nearMiss, 0, FingerCount);
            Array.Clear(_released, 0, FingerCount);
            for (int i = 0; i < FingerCount; i++)
            {
                _blockedBy[i] = -1;
            }

            if (deltaSeconds > 0.0 && !double.IsNaN(deltaSeconds) && !double.IsInfinity(deltaSeconds))
            {
                _time += deltaSeconds;
            }

            if (!_seeded)
            {
                for (int i = 0; i < FingerCount; i++)
                {
                    _anchor[i] = curls[i];
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
                        if (curl - _moveReference[i] >= MoveDelta)
                        {
                            _moveReference[i] = curl;
                            _lastMovedAt[i] = _time;
                        }

                        _anchor[i] = curl;
                    }
                    else if (_anchor[i] - curl >= _thresholds.ReleaseDelta)
                    {
                        _released[i] = true;
                        _releasePeak[i] = _anchor[i] - _pressTrough[i];
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
            // spoken for, so legato - the next finger pressing while the last is still down - works), and
            // whether a held finger that is still moving is the one dragging this one along (ADR-0068).
            for (int i = 0; i < FingerCount; i++)
            {
                if (_down[i] || _delta[i] < _thresholds.PressDeltaFor(i))
                {
                    _confirm[i] = 0;
                    continue;
                }

                // Isolated: ahead of every other free finger by that pair's margin (smaller between two coupled fingers,
                // which drag each other). The finger it is weakest against is the one reported as blocking it.
                bool isolated = true;
                float tightest = float.PositiveInfinity;
                int strongestIndex = -1;
                for (int j = 0; j < FingerCount; j++)
                {
                    if (j == i || _down[j])
                    {
                        continue;
                    }

                    float slack = _delta[i] - _delta[j] - _thresholds.IsolationMarginFor(i, j);
                    if (slack < 0f)
                    {
                        isolated = false;
                    }

                    if (slack < tightest)
                    {
                        tightest = slack;
                        strongestIndex = j;
                    }
                }

                // Dragged: some held finger is still moving and this one has not out-risen its whole stroke by that
                // pair's margin. Of the fingers that qualify, the one with the biggest stroke is the driver.
                int driver = -1;
                float driverRise = float.NegativeInfinity;
                if (_thresholds.CouplingSeconds > 0f)
                {
                    for (int j = 0; j < FingerCount; j++)
                    {
                        if (j != i && _down[j] && _time - _lastMovedAt[j] <= _thresholds.CouplingSeconds)
                        {
                            float rise = curls[j] - _pressTrough[j]; // its whole stroke, from the trough it pressed from
                            if (rise > driverRise && _delta[i] - rise < _thresholds.IsolationMarginFor(i, j))
                            {
                                driverRise = rise;
                                driver = j;
                            }
                        }
                    }
                }

                bool dragged = driver >= 0;

                if (isolated && !dragged)
                {
                    // The thumb has to hold the press for a few frames: a spike is tracking, not a thumb.
                    _confirm[i]++;
                    if (_confirm[i] >= (i == 0 ? _thresholds.ThumbConfirmFrames : 1))
                    {
                        _pressed[i] = true;
                    }
                }
                else
                {
                    _confirm[i] = 0;
                    _blocked[i] = true;
                    _dragged[i] = dragged;
                    _blockedBy[i] = dragged ? driver : strongestIndex;
                }
            }

            for (int i = 0; i < FingerCount; i++)
            {
                if (_pressed[i])
                {
                    _down[i] = true;
                    _confirm[i] = 0;
                    _pressTrough[i] = _anchor[i]; // the trough it pressed from, before the anchor becomes the peak
                    _anchor[i] = curls[i];        // the peak so far
                    _moveReference[i] = curls[i];
                    _lastMovedAt[i] = _time;
                }
                else if (_dragged[i])
                {
                    // Dragged, not played: where it has been dragged to is its rest now. Left as it was, the drag
                    // would stay above the press level and fire the moment the driver stopped moving.
                    _anchor[i] = curls[i];
                }
            }

            TrackNearMisses();
            ForgetTroughsUpward(curls, deltaSeconds);
        }

        private void TrackNearMisses()
        {
            for (int i = 0; i < FingerCount; i++)
            {
                if (_down[i])
                {
                    _rise[i] = 0f;
                    _spent[i] = true;
                    continue;
                }

                float press = _thresholds.PressDeltaFor(i);
                float floor = press * NearMissFraction;

                if (_delta[i] >= press)
                {
                    _rise[i] = 0f;
                    _spent[i] = true; // it got there - pressed, or blocked (which is reported as such), but not a near miss
                    continue;
                }

                if (_spent[i])
                {
                    if (_delta[i] < floor * 0.5f)
                    {
                        _spent[i] = false; // back near its trough: the next rise is a new attempt
                    }

                    continue;
                }

                if (_rise[i] <= 0f)
                {
                    if (_delta[i] < floor)
                    {
                        continue;
                    }

                    _rise[i] = _delta[i];
                    _riseDriver[i] = -1;
                }
                else if (_delta[i] > _rise[i])
                {
                    _rise[i] = _delta[i];
                }
                else if (_delta[i] < _rise[i] * 0.5f)
                {
                    _nearMiss[i] = true;
                    _nearMissPeak[i] = _rise[i];
                    _nearMissDriver[i] = _riseDriver[i];
                    _rise[i] = 0f;
                    continue;
                }

                if (_riseDriver[i] < 0 && _thresholds.CouplingSeconds > 0f)
                {
                    for (int j = 0; j < FingerCount; j++)
                    {
                        if (j != i && _down[j] && _time - _lastMovedAt[j] <= _thresholds.CouplingSeconds)
                        {
                            _riseDriver[i] = j;
                            break;
                        }
                    }
                }
            }
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
