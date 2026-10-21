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

        /// <summary>
        /// How long after a pressed finger last moved it still counts as the finger dragging its neighbours (ADR-0068).
        /// Tendon coupling makes a neighbour follow the bent finger a few frames behind, so it can cross the press level
        /// after the bent finger has already pressed. While that finger is still moving, or has stopped within this long,
        /// a neighbour that did not out-rise it by the isolation margin is dragged, not played. Short enough that a real
        /// second note a beat later is unaffected; 0 turns it off (a held finger is never consulted, as before).
        /// </summary>
        public const float DefaultCouplingSeconds = 0.10f;

        /// <summary>
        /// Consecutive frames the thumb must qualify as a press before it sounds (ADR-0068). The thumb is the noisiest
        /// of the five - it flickers in confidence and folds out of sight behind the fingers - and a single-frame spike
        /// is not a press a hand can make. One frame is roughly 11-16 ms at the headset's tracking rate. 1 = judge at once,
        /// as the other fingers are.
        /// </summary>
        public const int DefaultThumbConfirmFrames = 2;

        /// <summary>
        /// Neutral: the coupled fingers (see <see cref="IsCoupledFinger"/>) are held to the same press level and isolation
        /// margin as the others. The scene's config asset supplies the real, smaller values.
        /// </summary>
        public const float NeutralCoupledScale = 1f;

        /// <summary>
        /// What the scene's config starts the coupled-finger scales at (ADR-0073). `[TUNABLE]`, chosen by a model of a player
        /// working through the Swing lesson, not measured on a hand: unmeasured until M8.
        /// </summary>
        public const float DefaultCoupledPressScale = 0.75f;

        /// <summary>See <see cref="DefaultCoupledPressScale"/>.</summary>
        public const float DefaultCoupledIsolationScale = 0.3f;

        /// <summary>
        /// What the scene's config starts the ring and pinky's own press scale at (ADR-0074): lower than the middle finger's,
        /// because they are the two a learner most often cannot curl far. `[TUNABLE]`, unmeasured.
        /// </summary>
        public const float DefaultRingPinkyPressScale = 0.65f;

        public static FingerBendThresholds Default =>
            new FingerBendThresholds(
                DefaultPressDelta, DefaultReleaseDelta, DefaultBaselineSeconds, DefaultIsolationMargin);

        public FingerBendThresholds(
            float pressDelta,
            float releaseDelta,
            float baselineSeconds,
            float isolationMargin,
            float couplingSeconds = DefaultCouplingSeconds,
            int thumbConfirmFrames = DefaultThumbConfirmFrames,
            float coupledPressScale = NeutralCoupledScale,
            float coupledIsolationScale = NeutralCoupledScale,
            float? ringPinkyPressScale = null)
        {
            float ringPinkyScale = ringPinkyPressScale ?? coupledPressScale;
            if (float.IsNaN(ringPinkyScale) || ringPinkyScale <= 0f || ringPinkyScale > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(ringPinkyPressScale), ringPinkyScale, "Must be in (0, 1].");
            }

            if (float.IsNaN(pressDelta) || pressDelta <= 0f || pressDelta > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(pressDelta), pressDelta, "Must be in (0, 1].");
            }

            if (float.IsNaN(coupledPressScale) || coupledPressScale <= 0f || coupledPressScale > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(coupledPressScale), coupledPressScale, "Must be in (0, 1].");
            }

            if (float.IsNaN(coupledIsolationScale) || coupledIsolationScale < 0f || coupledIsolationScale > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(coupledIsolationScale), coupledIsolationScale, "Must be in [0, 1].");
            }

            // Every finger's own press level, the coupled fingers' included, stays above the release level.
            if (float.IsNaN(releaseDelta) || releaseDelta < 0f || releaseDelta >= pressDelta * Math.Min(coupledPressScale, ringPinkyScale))
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

            if (float.IsNaN(couplingSeconds) || float.IsInfinity(couplingSeconds) || couplingSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(couplingSeconds), couplingSeconds, "Must be finite and non-negative.");
            }

            if (thumbConfirmFrames < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(thumbConfirmFrames), thumbConfirmFrames, "Must be at least 1.");
            }

            PressDelta = pressDelta;
            ReleaseDelta = releaseDelta;
            BaselineSeconds = baselineSeconds;
            IsolationMargin = isolationMargin;
            CouplingSeconds = couplingSeconds;
            ThumbConfirmFrames = thumbConfirmFrames;
            CoupledPressScale = coupledPressScale;
            CoupledIsolationScale = coupledIsolationScale;
            RingPinkyPressScale = ringPinkyScale;
        }

        public float PressDelta { get; }

        public float ReleaseDelta { get; }

        public float BaselineSeconds { get; }

        public float IsolationMargin { get; }

        /// <summary>See <see cref="DefaultCouplingSeconds"/>.</summary>
        public float CouplingSeconds { get; }

        /// <summary>See <see cref="DefaultThumbConfirmFrames"/>.</summary>
        public int ThumbConfirmFrames { get; }

        /// <summary>
        /// Multiplies <see cref="PressDelta"/> for the middle, ring and pinky: they share one flexor muscle belly, so a
        /// deliberate curl of any of them is smaller and slower to stand clear of the others than the index's or thumb's.
        /// 1 = no difference.
        /// </summary>
        public float CoupledPressScale { get; }

        /// <summary>
        /// Multiplies <see cref="PressDelta"/> for the ring and pinky alone, in place of <see cref="CoupledPressScale"/> (which then
        /// applies to the middle finger only). They are the two fingers that drag each other hardest and curl least far, so
        /// a learner's deliberate curl of them is the smallest; lower this if their notes are missed, raise it if they sound by
        /// themselves. Defaults to <see cref="CoupledPressScale"/>.
        /// </summary>
        public float RingPinkyPressScale { get; }

        /// <summary>
        /// Multiplies <see cref="IsolationMargin"/> where the finger pressing and the finger it is judged against are both
        /// coupled fingers: each drags the other, so the one that was meant leads by less. Between a coupled finger and the
        /// index or thumb the full margin applies. 1 = no difference.
        /// </summary>
        public float CoupledIsolationScale { get; }

        /// <summary>The middle, ring and pinky: the fingers whose curls are tied together by a shared flexor muscle.</summary>
        public static bool IsCoupledFinger(int finger) => finger >= 2;

        /// <summary>The four fingers, as opposed to the thumb, which moves on its own: any two of them drag each other along.</summary>
        public static bool IsFinger(int finger) => finger >= 1;

        /// <summary>How far <paramref name="finger"/> must rise above its trough to count as a press.</summary>
        public float PressDeltaFor(int finger) =>
            finger >= 3 ? PressDelta * RingPinkyPressScale
            : finger == 2 ? PressDelta * CoupledPressScale
            : PressDelta;

        /// <summary>How much further than <paramref name="other"/> a press of <paramref name="finger"/> must have risen.</summary>
        public float IsolationMarginFor(int finger, int other) =>
            IsFinger(finger) && IsFinger(other) ? IsolationMargin * CoupledIsolationScale : IsolationMargin;
    }
}
