using System;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// The behaviour a <see cref="LearningMode"/> dictates, straight from the CLAUDE.md
    /// §3.8 table. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct ModePolicy
    {
        private ModePolicy(
            SystemPlayback systemPlayback,
            bool ghostHandsVisible,
            UserAudioGate userAudio,
            bool deferFeedback,
            bool gateOnGesture)
        {
            SystemPlayback = systemPlayback;
            GhostHandsVisible = ghostHandsVisible;
            UserAudio = userAudio;
            DeferFeedback = deferFeedback;
            GateOnGesture = gateOnGesture;
        }

        public SystemPlayback SystemPlayback { get; }

        public bool GhostHandsVisible { get; }

        public UserAudioGate UserAudio { get; }

        /// <summary>True when correctness feedback is withheld until the attempt completes (§3.7).</summary>
        public bool DeferFeedback { get; }

        /// <summary>
        /// True when the lesson holds on each demonstrated pose until the learner confirms
        /// it, rather than advancing on the phrase clock. Try Yourself steps through the
        /// targets this way — "audio is the reward for matching it" (§3.8) — but only on a
        /// left-hand lesson, where the target is a chord pose; the runner leaves it off for
        /// a melody lesson. Gesture Learning does <b>not</b> gate: it is free pose practice
        /// with unconditional audio ("pose fluency only, no musical target", §3.8).
        /// </summary>
        public bool GateOnGesture { get; }

        /// <summary>
        /// The §3.8 behaviour for <paramref name="mode"/> in a lesson that exercises
        /// <paramref name="hands"/>. Identical to <see cref="For(LearningMode)"/> except for
        /// one case (ADR-0043 decision 3): in a <b>right-hand-only</b> lesson there is no
        /// left hand to play the chords, so Try Yourself and Test Yourself — which otherwise
        /// play nothing — play the harmony (<see cref="SystemPlayback.HarmonyOnly"/>) and
        /// leave the melody to the learner. A lesson where the learner plays the left hand
        /// (left-only or both) is unchanged.
        /// </summary>
        public static ModePolicy For(LearningMode mode, ActiveHands hands)
        {
            ModePolicy policy = For(mode);

            bool learnerPlaysNoChords = (hands & ActiveHands.Left) == 0 && (hands & ActiveHands.Right) != 0;
            bool silentMode = mode == LearningMode.TryYourself || mode == LearningMode.TestYourself;

            return learnerPlaysNoChords && silentMode
                ? new ModePolicy(
                    SystemPlayback.HarmonyOnly,
                    policy.GhostHandsVisible,
                    policy.UserAudio,
                    policy.DeferFeedback,
                    policy.GateOnGesture)
                : policy;
        }

        public static ModePolicy For(LearningMode mode) => mode switch
        {
            LearningMode.GestureLearning => new ModePolicy(
                SystemPlayback.None, ghostHandsVisible: true, UserAudioGate.Always,
                deferFeedback: false, gateOnGesture: false),

            LearningMode.WatchAndListen => new ModePolicy(
                SystemPlayback.Full, ghostHandsVisible: true, UserAudioGate.Never,
                deferFeedback: false, gateOnGesture: false),

            LearningMode.TryYourself => new ModePolicy(
                SystemPlayback.None, ghostHandsVisible: true, UserAudioGate.OnlyWhenGestureCorrect,
                deferFeedback: false, gateOnGesture: true),

            LearningMode.TestYourself => new ModePolicy(
                SystemPlayback.None, ghostHandsVisible: false, UserAudioGate.Always,
                deferFeedback: true, gateOnGesture: false),

            LearningMode.ComposeOnTheFly => new ModePolicy(
                SystemPlayback.BackingOnly, ghostHandsVisible: false, UserAudioGate.Always,
                deferFeedback: false, gateOnGesture: false),

            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }
}
