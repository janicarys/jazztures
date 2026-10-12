using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The levels <see cref="FingerBendDetector"/> decides a "key press" from (ADR-0050 — an
    /// exploration, not a thesis decision). All `[TUNABLE]` and unmeasured: they are engineering
    /// starting points for a press that is a modest bend of one finger, not a deep curl, and must
    /// be calibrated on the headset before anything is said about them. Every level is a
    /// <i>change</i> in a finger's normalised bend (0 = flat, 1 = fully bent) from that finger's own
    /// last turning point, never an absolute bend — a relaxed hand is already partly bent, and the
    /// resting posture differs between people. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct FingerBendThresholds
    {
        /// <summary>How far a free finger must rise above its lowest recent point to count as a press.</summary>
        public const float DefaultPressDelta = 0.14f;

        /// <summary>
        /// How far a pressed finger must fall back from its peak before it can press again — below
        /// <see cref="DefaultPressDelta"/>, so jitter at the level cannot re-fire it (the Schmitt
        /// rule, CLAUDE.md §3.4). Measured from the peak, not from a resting level, so a hand that
        /// settles back a little more bent than it began still re-arms.
        /// </summary>
        public const float DefaultReleaseDelta = 0.07f;

        /// <summary>
        /// Time constant of the lowest-recent-point memory's slow rise toward the finger's current
        /// bend, seconds. A bend slower than about <see cref="DefaultPressDelta"/> divided by this per
        /// second is absorbed as drift rather than pressed; a shorter time constant therefore misses
        /// moderately slow presses, a longer one lets slower drifts through.
        /// </summary>
        public const float DefaultBaselineSeconds = 2.0f;

        /// <summary>
        /// How much further than every other finger a finger must have bent to count as the one
        /// that was pressed. Fingers do not move independently (the ring and pinky especially), and
        /// closing the whole hand bends them all: without this margin a fist would sound five notes.
        /// </summary>
        public const float DefaultIsolationMargin = 0.06f;

        public static FingerBendThresholds Default =>
            new FingerBendThresholds(
                DefaultPressDelta, DefaultReleaseDelta, DefaultBaselineSeconds, DefaultIsolationMargin);

        public FingerBendThresholds(float pressDelta, float releaseDelta, float baselineSeconds, float isolationMargin)
        {
            if (float.IsNaN(pressDelta) || pressDelta <= 0f || pressDelta > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(pressDelta), pressDelta, "Must be in (0, 1].");
            }

            if (float.IsNaN(releaseDelta) || releaseDelta < 0f || releaseDelta >= pressDelta)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(releaseDelta), releaseDelta, "Must be non-negative and below the press delta (hysteresis).");
            }

            if (float.IsNaN(baselineSeconds) || baselineSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(baselineSeconds), baselineSeconds, "Must be positive.");
            }

            if (float.IsNaN(isolationMargin) || isolationMargin < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(isolationMargin), isolationMargin, "Must be non-negative.");
            }

            PressDelta = pressDelta;
            ReleaseDelta = releaseDelta;
            BaselineSeconds = baselineSeconds;
            IsolationMargin = isolationMargin;
        }

        public float PressDelta { get; }

        public float ReleaseDelta { get; }

        public float BaselineSeconds { get; }

        public float IsolationMargin { get; }
    }
}
