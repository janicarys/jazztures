using System;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Menu
{
    /// <summary>
    /// Turns the right hand's motion into the four menu gestures (ADR-0051): a sideways flick
    /// (either way), a downward strike, and a held fist. Pure — it is fed
    /// <see cref="MenuHandFrame"/>s and raises <see cref="Recognized"/>; it knows nothing about
    /// menus. The menu's counterpart of <see cref="Jazztures.Core.Gesture.ChordStrikeDetector"/>,
    /// and it follows the same rules (§3.4, §3.5):
    /// <list type="bullet">
    ///   <item><b>Hysteresis.</b> Each motion has an enter and a lower exit speed. After a flick
    ///   or strike the hand must <i>settle</i> — several consecutive quiet frames (ADR-0030) —
    ///   before the next can arm, so a decelerating or rebounding stroke cannot fire twice.</item>
    ///   <item><b>Never guess.</b> A motion only counts if its axis clearly dominates the other;
    ///   a diagonal swipe does nothing.</item>
    ///   <item><b>Return strokes.</b> A flick the opposite way soon after a flick is the hand
    ///   coming back, not a second request, and is swallowed.</item>
    ///   <item><b>Tracking loss.</b> Nothing fires while the hand is Low/lost, and nothing
    ///   fires until a few consecutive High frames have come back. A fist hold in progress is
    ///   cancelled rather than resumed.</item>
    ///   <item><b>A fist is not a gesture-in-waiting.</b> While a fist is closing, held or
    ///   latched, flicks and strikes are suppressed (closing the hand to go back must not also
    ///   choose the highlighted card), and a fist only fires after the hand has been seen open.</item>
    /// </list>
    ///
    /// <para>
    /// <see cref="Context"/> says what the right hand is for. In <see cref="MenuInputContext.Running"/>
    /// only the fist is live and it needs the longer hold. Changing context re-arms (see
    /// <see cref="Rearm"/>).
    /// </para>
    ///
    /// <para>
    /// Time is read from the injected <see cref="IMusicalClock"/>, as in every other gesture
    /// class here. These are coarse UI timings, so the DSP clock's buffer-sized steps do not
    /// matter; the <i>speeds</i> are computed upstream by the adapter with frame time.
    /// </para>
    /// </summary>
    public sealed class MenuGestureRecognizer
    {
        private enum FistState
        {
            Open,
            Closing,
            Fired,
            Latched,
        }

        private readonly IMusicalClock _clock;
        private readonly MenuGestureThresholds _thresholds;

        private MenuInputContext _context = MenuInputContext.Menu;

        private bool _trackingUsable;
        private int _highFrames;

        private bool _armed;
        private int _settleFrames;
        private double _graceUntil = double.NegativeInfinity;
        private double _lastGestureTime = double.NegativeInfinity;
        private MenuGesture? _lastFlick;
        private double _lastFlickTime = double.NegativeInfinity;

        private FistState _fist = FistState.Latched;
        private double _fistStart;
        private int _fistMisses;

        public MenuGestureRecognizer(IMusicalClock clock, MenuGestureThresholds thresholds)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _thresholds = thresholds;
        }

        /// <summary>Raised once per recognised gesture.</summary>
        public event Action<MenuGesture>? Recognized;

        public MenuInputContext Context => _context;

        /// <summary>
        /// How far through the fist hold the hand is, 0..1 — 0 unless a fist is closing. Drives
        /// the one non-modal cue the hold needs (a hold with no feedback just reads as latency).
        /// </summary>
        public float FistProgress { get; private set; }

        /// <summary>Change what the right hand is for, and re-arm.</summary>
        public void SetContext(MenuInputContext context)
        {
            _context = context;
            Rearm();
        }

        /// <summary>
        /// Treat the hand as being in transition: ignore flicks and strikes for
        /// <see cref="MenuGestureThresholds.EntryGraceSeconds"/> and until the hand has settled,
        /// and require the fist to be opened before it can fire. Called whenever the right hand
        /// changes job — a lesson ending leaves it mid-way through striking targets, and leaving a
        /// lesson with a fist leaves it closed.
        /// </summary>
        public void Rearm()
        {
            _armed = false;
            _settleFrames = 0;
            _graceUntil = _clock.Now + _thresholds.EntryGraceSeconds;
            _lastFlick = null;
            _fist = FistState.Latched;
            _fistMisses = 0;
            FistProgress = 0f;
        }

        public void Feed(MenuHandFrame frame)
        {
            double now = _clock.Now;

            if (!UpdateTracking(frame.Tracking))
            {
                return;
            }

            if (UpdateFist(frame.FistAmount, now))
            {
                // Raised last and nothing touched after: a handler may switch context, which
                // re-arms this recogniser mid-call.
                Recognized?.Invoke(MenuGesture.Fist);
                return;
            }

            if (_fist != FistState.Open || _context != MenuInputContext.Menu)
            {
                _armed = false;
                _settleFrames = 0;
                return;
            }

            float lateral = Math.Max(frame.LeftwardSpeed, frame.RightwardSpeed);
            float down = frame.DownwardSpeed;

            if (!_armed)
            {
                bool quiet = lateral <= _thresholds.FlickExitSpeedMetresPerSecond
                          && down <= _thresholds.StrikeExitSpeedMetresPerSecond;
                _settleFrames = quiet ? _settleFrames + 1 : 0;

                if (_settleFrames >= _thresholds.SettleFrames && now >= _graceUntil)
                {
                    _armed = true;
                    _settleFrames = 0;
                }

                return;
            }

            if (now - _lastGestureTime < _thresholds.MinInterGestureSeconds)
            {
                return;
            }

            bool flick = lateral >= _thresholds.FlickEnterSpeedMetresPerSecond
                      && lateral >= _thresholds.AxisDominanceRatio * down;
            if (flick)
            {
                MenuGesture direction = frame.RightwardSpeed >= frame.LeftwardSpeed
                    ? MenuGesture.FlickRight
                    : MenuGesture.FlickLeft;

                _armed = false;
                _settleFrames = 0;

                bool returnStroke = _lastFlick.HasValue
                                 && _lastFlick.Value != direction
                                 && now - _lastFlickTime < _thresholds.OppositeFlickLockoutSeconds;
                if (returnStroke)
                {
                    return; // disarmed, so the rest of the return stroke is swallowed too
                }

                _lastFlick = direction;
                _lastFlickTime = now;
                _lastGestureTime = now;
                Recognized?.Invoke(direction);
                return;
            }

            bool strike = down >= _thresholds.StrikeEnterSpeedMetresPerSecond
                       && down >= _thresholds.AxisDominanceRatio * lateral;
            if (strike)
            {
                _armed = false;
                _settleFrames = 0;
                _lastGestureTime = now;
                Recognized?.Invoke(MenuGesture.Strike);
            }
        }

        /// <summary>
        /// §3.5: nothing counts while the hand is Low or lost, and only after
        /// <see cref="MenuGestureThresholds.HighFramesToResumeAfterLoss"/> consecutive High
        /// frames does it count again. Returns whether this frame may be acted on.
        /// </summary>
        private bool UpdateTracking(TrackingQuality quality)
        {
            if (quality == TrackingQuality.NotTracked || quality == TrackingQuality.Low)
            {
                _trackingUsable = false;
                _highFrames = 0;
                _armed = false;
                _settleFrames = 0;
                _fist = FistState.Latched; // a fist held across a dropout must be re-opened first
                _fistMisses = 0;
                FistProgress = 0f;
                return false;
            }

            if (!_trackingUsable)
            {
                _highFrames = quality == TrackingQuality.High ? _highFrames + 1 : 0;
                if (_highFrames >= _thresholds.HighFramesToResumeAfterLoss)
                {
                    _trackingUsable = true;
                }
            }

            return _trackingUsable;
        }

        /// <summary>Advance the fist machine; returns true on the frame the hold completes.</summary>
        private bool UpdateFist(float amount, double now)
        {
            bool closed = amount >= _thresholds.FistEnterAmount;
            bool open = amount < _thresholds.FistExitAmount;

            switch (_fist)
            {
                case FistState.Latched:
                    FistProgress = 0f;
                    if (open)
                    {
                        _fist = FistState.Open;
                    }

                    return false;

                case FistState.Open:
                    FistProgress = 0f;
                    if (closed)
                    {
                        _fist = FistState.Closing;
                        _fistStart = now;
                        _fistMisses = 0;
                    }

                    return false;

                case FistState.Closing:
                {
                    if (open)
                    {
                        _fistMisses++;
                        if (_fistMisses > _thresholds.FistMissToleranceFrames)
                        {
                            _fist = FistState.Open;
                            FistProgress = 0f;
                            return false;
                        }
                    }
                    else
                    {
                        _fistMisses = 0;
                    }

                    double hold = _context == MenuInputContext.Menu
                        ? _thresholds.FistHoldSecondsInMenu
                        : _thresholds.FistHoldSecondsInRun;
                    double held = now - _fistStart;

                    if (held >= hold)
                    {
                        _fist = FistState.Fired;
                        FistProgress = 0f;
                        return true;
                    }

                    FistProgress = hold > 0.0 ? (float)(held / hold) : 1f;
                    return false;
                }

                default: // Fired: wait for the hand to open before another can begin
                    FistProgress = 0f;
                    if (open)
                    {
                        _fist = FistState.Open;
                    }

                    return false;
            }
        }
    }
}
