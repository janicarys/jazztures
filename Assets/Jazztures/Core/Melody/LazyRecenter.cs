using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Decides the facing the body-anchored touch-target rig should use, given the head's
    /// current heading each frame (ADR-0015). Pure yaw arithmetic — no <c>Vector3</c>, so
    /// it is unit-tested headless (CLAUDE.md §2.1, §2.6). The Unity <c>TouchTargetRig</c>
    /// feeds head yaw in and applies <see cref="CommittedYawRadians"/>; all the timing
    /// lives here.
    ///
    /// <para>
    /// Behaviour: the committed facing holds still while the head yaw stays within
    /// <see cref="LazyRecenterSettings.AngleThresholdRadians"/>. Once it diverges further
    /// and stays there for <see cref="LazyRecenterSettings.DwellSeconds"/>, the committed
    /// facing eases toward the head yaw (exponential, time constant
    /// <see cref="LazyRecenterSettings.EaseSeconds"/>) until it is back inside the dead
    /// zone. A divergence that clears before the dwell elapses cancels the recenter. The
    /// dwell is what prevents boundary chatter — a re-diverge must wait the full dwell
    /// again.
    /// </para>
    ///
    /// <para>All yaw is in radians and wraps at ±π; the ease always takes the short way round.</para>
    /// </summary>
    public sealed class LazyRecenter
    {
        /// <summary>
        /// Residual the ease must close before <see cref="Phase"/> returns to
        /// <see cref="RecenterPhase.Settled"/> — "caught up". Internal tolerance, not a
        /// tuning knob: small enough to read as centred, large enough that the
        /// exponential ease reaches it in finite frames.
        /// </summary>
        private const double SettleToleranceRadians = 0.0174533; // ~1°

        private readonly LazyRecenterSettings _settings;
        private double _committedYaw;
        private double _pendingElapsed;
        private RecenterPhase _phase;

        public LazyRecenter(LazyRecenterSettings settings, double initialFacingRadians = 0.0)
        {
            _settings = settings;
            _committedYaw = Normalize(initialFacingRadians);
            _phase = RecenterPhase.Settled;
        }

        /// <summary>The facing the rig should use this frame.</summary>
        public double CommittedYawRadians => _committedYaw;

        public RecenterPhase Phase => _phase;

        /// <summary>
        /// Advance by <paramref name="deltaSeconds"/> with the head at
        /// <paramref name="headYawRadians"/>. Returns <see cref="CommittedYawRadians"/>.
        /// A negative delta (rewound clock) is treated as zero.
        /// </summary>
        public double Update(double headYawRadians, double deltaSeconds)
        {
            if (deltaSeconds < 0.0)
            {
                deltaSeconds = 0.0;
            }

            double error = Normalize(headYawRadians - _committedYaw);
            double absError = Math.Abs(error);
            bool diverged = absError > _settings.AngleThresholdRadians;

            switch (_phase)
            {
                case RecenterPhase.Settled:
                    if (diverged)
                    {
                        _phase = RecenterPhase.Pending;
                        _pendingElapsed = 0.0;
                    }

                    break;

                case RecenterPhase.Pending:
                    if (!diverged)
                    {
                        _phase = RecenterPhase.Settled;
                        _pendingElapsed = 0.0;
                        break;
                    }

                    _pendingElapsed += deltaSeconds;
                    if (_pendingElapsed >= _settings.DwellSeconds)
                    {
                        _phase = RecenterPhase.Following;
                    }

                    break;

                case RecenterPhase.Following:
                    double k = _settings.EaseSeconds <= 0.0
                        ? 1.0
                        : 1.0 - Math.Exp(-deltaSeconds / _settings.EaseSeconds);
                    _committedYaw = Normalize(_committedYaw + error * k);

                    // Ease all the way to the head — a partial recenter that stopped
                    // inside the dead zone would leave the targets permanently trailing.
                    // If the head is still turning faster than the ease, the residual
                    // stays above tolerance and Following continues to track it.
                    if (Math.Abs(Normalize(headYawRadians - _committedYaw)) <= SettleToleranceRadians)
                    {
                        _phase = RecenterPhase.Settled;
                        _pendingElapsed = 0.0;
                    }

                    break;
            }

            return _committedYaw;
        }

        /// <summary>Jump the committed facing to <paramref name="facingRadians"/> and settle.</summary>
        public void Reset(double facingRadians)
        {
            _committedYaw = Normalize(facingRadians);
            _phase = RecenterPhase.Settled;
            _pendingElapsed = 0.0;
        }

        /// <summary>Wrap an angle to (−π, π].</summary>
        private static double Normalize(double radians)
        {
            const double twoPi = 2.0 * Math.PI;
            radians %= twoPi;
            if (radians <= -Math.PI)
            {
                radians += twoPi;
            }
            else if (radians > Math.PI)
            {
                radians -= twoPi;
            }

            return radians;
        }
    }
}
