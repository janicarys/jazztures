using System;
using Jazztures.Core.Melody;

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
            double maxAwaitingConfirmationSeconds,
            float tapSpeedMetresPerSecond = TouchContactThresholds.DefaultTapSpeed,
            float settleSpeedMetresPerSecond = TouchContactThresholds.DefaultSettleSpeed,
            int settleFrames = TouchContactThresholds.DefaultSettleFrames)
        {
            Require(minInterTouchSeconds >= 0.0, nameof(minInterTouchSeconds));
            Require(entryVelocityGateMetresPerSecond >= 0f, nameof(entryVelocityGateMetresPerSecond));
            Require(maxAwaitingConfirmationSeconds > 0.0, nameof(maxAwaitingConfirmationSeconds));

            // Validates the tap / settle / frames relationships (tap >= entry, settle < tap, frames >= 1).
            _ = new TouchContactThresholds(
                entryVelocityGateMetresPerSecond, tapSpeedMetresPerSecond, settleSpeedMetresPerSecond, settleFrames);

            MinInterTouchSeconds = minInterTouchSeconds;
            EntryVelocityGateMetresPerSecond = entryVelocityGateMetresPerSecond;
            MaxAwaitingConfirmationSeconds = maxAwaitingConfirmationSeconds;
            TapSpeedMetresPerSecond = tapSpeedMetresPerSecond;
            SettleSpeedMetresPerSecond = settleSpeedMetresPerSecond;
            SettleFrames = settleFrames;
        }

        /// <summary>
        /// Minimum gap between two touch articulations — a retrigger cooldown, the touch
        /// counterpart of <see cref="GestureThresholds.MinInterStrikeSeconds"/>. Default
        /// 120 ms (the same value, inherited rather than re-guessed).
        /// </summary>
        public double MinInterTouchSeconds { get; }

        /// <summary>
        /// Minimum <b>downward hand speed</b> on entry for it to count as a deliberate touch, not
        /// a resting hand drifting into the volume. Originally 0.08 m/s, inherited from the
        /// right hand's fingertip entry gate (ADR-0018) — right for a carefully aimed fingertip,
        /// too low for a whole hand whose joints cross the volume's edge all the time while a
        /// learner adjusts; with the plate counting any part of the hand (ADR-0047) it let
        /// unintended chords through. Now 0.15 m/s: still far under a real chop (0.5 m/s and up)
        /// and under the tap speed. `[TUNABLE]`, unmeasured for the plate.
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
        /// Speed of a <b>tap while the hand is already inside</b> the target's volume (ADR-0047,
        /// the left-hand counterpart of the right hand's ADR-0045). A slow arrival, or a chop
        /// that begins inside the volume, has no entry edge to read; a sudden movement at or
        /// above this plays instead. Default 0.25 m/s, the left-hand strike's own tuned enter
        /// speed (ADR-0028/0030); unmeasured for the plate.
        /// </summary>
        public float TapSpeedMetresPerSecond { get; }

        /// <summary>After a touch, the hand must slow to this before another tap inside can play (hysteresis). Default 0.10 m/s.</summary>
        public float SettleSpeedMetresPerSecond { get; }

        /// <summary>Consecutive frames at or below <see cref="SettleSpeedMetresPerSecond"/> that re-arm a tap. Default 3.</summary>
        public int SettleFrames { get; }

        /// <summary>The entry / tap / settle rule as the pure <see cref="TouchContact"/> state machine consumes it.</summary>
        public TouchContactThresholds ToContactThresholds() => new TouchContactThresholds(
            EntryVelocityGateMetresPerSecond, TapSpeedMetresPerSecond, SettleSpeedMetresPerSecond, SettleFrames);

        /// <summary>
        /// The ADR-0039 defaults (cooldown, velocity gate — both inherited, neither a fresh
        /// guess) plus ADR-0042's grace window and ADR-0047's tap-inside values (fresh
        /// guesses; see their properties).
        /// </summary>
        public static TouchCommitThresholds Default => new TouchCommitThresholds(
            minInterTouchSeconds: 0.12,
            entryVelocityGateMetresPerSecond: DefaultEntryVelocityGateMetresPerSecond,
            maxAwaitingConfirmationSeconds: 0.35);

        /// <summary>The plate's default entry gate — see <see cref="EntryVelocityGateMetresPerSecond"/>.</summary>
        public const float DefaultEntryVelocityGateMetresPerSecond = 0.15f;

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
