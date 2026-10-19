using System;

namespace Jazztures.Core.Tutorial
{
    /// <summary>
    /// How strictly the tutorial judges "the learner did it" (ADR-0053). All `[TUNABLE]`, none
    /// measured on a headset — the Unity <c>Config/MenuConfig.asset</c> mirrors these and produces
    /// one of these structs. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct TutorialThresholds
    {
        public TutorialThresholds(
            double minStepSeconds,
            double inViewHoldSeconds,
            double outOfViewHoldSeconds,
            float openHandMaxClosure,
            float thumbUpEnterDegrees,
            float thumbUpExitDegrees,
            double thumbUpHoldSeconds,
            double eventGraceSeconds,
            float inViewMaxDegrees = 50f,
            float outOfViewMinDegrees = 60f,
            double postureTimeoutSeconds = 20.0)
        {
            Require(minStepSeconds >= 0.0, nameof(minStepSeconds));
            Require(inViewHoldSeconds >= 0.0, nameof(inViewHoldSeconds));
            Require(outOfViewHoldSeconds >= 0.0, nameof(outOfViewHoldSeconds));
            Require(openHandMaxClosure > 0f && openHandMaxClosure <= 1f, nameof(openHandMaxClosure));
            Require(thumbUpEnterDegrees > 0f, nameof(thumbUpEnterDegrees));
            Require(thumbUpExitDegrees > thumbUpEnterDegrees && thumbUpExitDegrees <= 180f, nameof(thumbUpExitDegrees));
            Require(thumbUpHoldSeconds >= 0.0, nameof(thumbUpHoldSeconds));
            Require(eventGraceSeconds >= 0.0, nameof(eventGraceSeconds));
            Require(inViewMaxDegrees > 0f, nameof(inViewMaxDegrees));
            Require(outOfViewMinDegrees > inViewMaxDegrees && outOfViewMinDegrees <= 180f, nameof(outOfViewMinDegrees));
            Require(postureTimeoutSeconds >= 0.0, nameof(postureTimeoutSeconds));

            MinStepSeconds = minStepSeconds;
            InViewHoldSeconds = inViewHoldSeconds;
            OutOfViewHoldSeconds = outOfViewHoldSeconds;
            OpenHandMaxClosure = openHandMaxClosure;
            ThumbUpEnterDegrees = thumbUpEnterDegrees;
            ThumbUpExitDegrees = thumbUpExitDegrees;
            ThumbUpHoldSeconds = thumbUpHoldSeconds;
            EventGraceSeconds = eventGraceSeconds;
            InViewMaxDegrees = inViewMaxDegrees;
            OutOfViewMinDegrees = outOfViewMinDegrees;
            PostureTimeoutSeconds = postureTimeoutSeconds;
        }

        /// <summary>
        /// A step that waits on the learner's hands is shown at least this long, even if they are
        /// already doing it, so every line can be read. Default 1.5 s.
        /// </summary>
        public double MinStepSeconds { get; }

        /// <summary>How long both hands must stay in view. Default 0.5 s.</summary>
        public double InViewHoldSeconds { get; }

        /// <summary>How long both hands must stay out of view. Default 0.6 s.</summary>
        public double OutOfViewHoldSeconds { get; }

        /// <summary>The most closed any of the four long fingers may be for the hand to count as open. Default 0.35.</summary>
        public float OpenHandMaxClosure { get; }

        /// <summary>Thumb within this many degrees of straight up starts "thumb up". Schmitt enter edge. Default 35°.</summary>
        public float ThumbUpEnterDegrees { get; }

        /// <summary>The thumb must fall past this to lose "thumb up". Schmitt exit edge, wider than the enter edge. Default 50°.</summary>
        public float ThumbUpExitDegrees { get; }

        /// <summary>How long an open hand with its thumb up must be held. Default 0.6 s.</summary>
        public double ThumbUpHoldSeconds { get; }

        /// <summary>
        /// A gesture arriving this soon after a step begins is ignored: it is almost certainly the end
        /// of the motion that finished the last step, not an answer to this one. Default 0.5 s.
        /// </summary>
        public double EventGraceSeconds { get; }

        /// <summary>
        /// A hand counts as <b>in front of you</b> when it is tracked and within this many degrees of where the
        /// learner is looking. Default 50° — a hand held at chest height is about 35°.
        /// </summary>
        public float InViewMaxDegrees { get; }

        /// <summary>
        /// A hand counts as <b>out of view</b> when it is not tracked, or is at least this many degrees off where
        /// the learner is looking. Default 60°, comfortably beyond the 50° "in front" edge, so a hand in
        /// between is neither. A hand at the learner's side is 80° or more.
        /// </summary>
        public float OutOfViewMinDegrees { get; }

        /// <summary>
        /// If a posture step (hands in view, hands out of view, open hand thumb up) is still unmet after this long,
        /// the tutorial moves on rather than trap the learner behind a gate the hardware cannot see.
        /// 0 disables it. Default 20 s. Gesture steps never time out: the menu needs those.
        /// </summary>
        public double PostureTimeoutSeconds { get; }

        public static TutorialThresholds Default => new TutorialThresholds(
            minStepSeconds: 1.5,
            inViewHoldSeconds: 0.5,
            outOfViewHoldSeconds: 0.6,
            openHandMaxClosure: 0.35f,
            thumbUpEnterDegrees: 35f,
            thumbUpExitDegrees: 50f,
            thumbUpHoldSeconds: 0.6,
            eventGraceSeconds: 0.5,
            inViewMaxDegrees: 50f,
            outOfViewMinDegrees: 60f,
            postureTimeoutSeconds: 20.0);

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
