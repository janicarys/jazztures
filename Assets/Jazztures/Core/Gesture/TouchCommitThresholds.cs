using System;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// The parameters <see cref="ChordTouchDetector"/> uses (ADR-0039). Kept separate from
    /// <see cref="GestureThresholds"/> (the strike path) and <see cref="PinchCommitThresholds"/>
    /// (the pinch path) so all three articulation mechanisms stay independently tunable and
    /// none is disturbed by the others. Unlike either of those, the retrigger cooldown and
    /// entry-velocity gate are <b>inherited</b> from an existing, already-established
    /// default rather than newly guessed — the entry-velocity gate in particular has
    /// already been on-device validated once, for the right hand's own melody targets
    /// (ADR-0018). Wired directly from <see cref="Default"/> in
    /// <c>PerformanceCompositionRoot</c> — there is no config asset for this path yet.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct TouchCommitThresholds
    {
        public TouchCommitThresholds(
            double minInterTouchSeconds,
            float entryVelocityGateMetresPerSecond,
            double maxAwaitingConfirmationSeconds)
        {
            Require(minInterTouchSeconds >= 0.0, nameof(minInterTouchSeconds));
            Require(entryVelocityGateMetresPerSecond >= 0f, nameof(entryVelocityGateMetresPerSecond));
            Require(maxAwaitingConfirmationSeconds > 0.0, nameof(maxAwaitingConfirmationSeconds));

            MinInterTouchSeconds = minInterTouchSeconds;
            EntryVelocityGateMetresPerSecond = entryVelocityGateMetresPerSecond;
            MaxAwaitingConfirmationSeconds = maxAwaitingConfirmationSeconds;
        }

        /// <summary>
        /// Minimum gap between two touch articulations — a retrigger cooldown, the touch
        /// counterpart of <see cref="GestureThresholds.MinInterStrikeSeconds"/>. Default
        /// 120 ms (the same value, inherited rather than re-guessed).
        /// </summary>
        public double MinInterTouchSeconds { get; }

        /// <summary>
        /// Minimum fingertip speed on entry for it to count as a deliberate touch, not a
        /// resting hand drifting into the volume — the same question §3.3's melody entry
        /// gate answers for the right hand. Default 0.08 m/s, inherited directly from
        /// <c>MelodyConfig</c>'s own entry gate (ADR-0018), which fixed the identical
        /// problem ("careful aiming... landed under the gate and was dropped silently") for
        /// the right hand's ten targets — this is the one commit-signal default in this
        /// project's history that has already been on-device pressure-tested, just for a
        /// different hand.
        /// </summary>
        public float EntryVelocityGateMetresPerSecond { get; }

        /// <summary>
        /// How long a rising edge that arrived before <c>GestureInterpreter.ConfirmedFunction</c>
        /// was set is still allowed to articulate once confirmation lands, provided the
        /// fingertip is still resting inside the target the whole time (ADR-0042). Bounded,
        /// not indefinite, so a touch attempted long before any function is ever confirmed
        /// again doesn't fire on some much-later, unrelated confirmation. Default 350 ms —
        /// a fresh guess, sized to comfortably cover <see cref="GestureThresholds.PoseHoldSeconds"/>
        /// (150 ms) plus its own miss-tolerance retries, not measured on-device.
        /// </summary>
        public double MaxAwaitingConfirmationSeconds { get; }

        /// <summary>
        /// The ADR-0039 defaults (cooldown, velocity gate — both inherited, neither a fresh
        /// guess) plus ADR-0042's grace window (a fresh guess; see
        /// <see cref="MaxAwaitingConfirmationSeconds"/>).
        /// </summary>
        public static TouchCommitThresholds Default => new TouchCommitThresholds(
            minInterTouchSeconds: 0.12,
            entryVelocityGateMetresPerSecond: 0.08f,
            maxAwaitingConfirmationSeconds: 0.35);

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
