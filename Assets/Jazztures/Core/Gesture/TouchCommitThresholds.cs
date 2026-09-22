using System;

namespace Jazztures.Core.Gesture
{
    /// <summary>
    /// The parameters <see cref="ChordTouchDetector"/> uses (ADR-0039). Kept separate from
    /// <see cref="GestureThresholds"/> (the strike path) and <see cref="PinchCommitThresholds"/>
    /// (the pinch path) so all three articulation mechanisms stay independently tunable and
    /// none is disturbed by the others. Unlike either of those, both values here are
    /// <b>inherited</b> from an existing, already-established default rather than newly
    /// guessed — the entry-velocity gate in particular has already been on-device validated
    /// once, for the right hand's own melody targets (ADR-0018). The Unity
    /// <c>Config/HarmonicFieldConfig.asset</c> mirrors these. Immutable value type
    /// (ADR-0007).
    /// </summary>
    public readonly struct TouchCommitThresholds
    {
        public TouchCommitThresholds(double minInterTouchSeconds, float entryVelocityGateMetresPerSecond)
        {
            Require(minInterTouchSeconds >= 0.0, nameof(minInterTouchSeconds));
            Require(entryVelocityGateMetresPerSecond >= 0f, nameof(entryVelocityGateMetresPerSecond));

            MinInterTouchSeconds = minInterTouchSeconds;
            EntryVelocityGateMetresPerSecond = entryVelocityGateMetresPerSecond;
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

        /// <summary>The ADR-0039 defaults — both inherited, neither a fresh guess.</summary>
        public static TouchCommitThresholds Default => new TouchCommitThresholds(
            minInterTouchSeconds: 0.12,
            entryVelocityGateMetresPerSecond: 0.08f);

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
