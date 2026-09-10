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
        /// it, rather than advancing on the phrase clock. Gesture Learning steps through
        /// the poses this way (§3.8 — "pose fluency"); the timed modes do not.
        /// </summary>
        public bool GateOnGesture { get; }

        public static ModePolicy For(LearningMode mode) => mode switch
        {
            LearningMode.GestureLearning => new ModePolicy(
                SystemPlayback.None, ghostHandsVisible: true, UserAudioGate.OnlyWhenGestureCorrect,
                deferFeedback: false, gateOnGesture: true),

            LearningMode.WatchAndListen => new ModePolicy(
                SystemPlayback.Full, ghostHandsVisible: true, UserAudioGate.Never,
                deferFeedback: false, gateOnGesture: false),

            LearningMode.TryYourself => new ModePolicy(
                SystemPlayback.None, ghostHandsVisible: true, UserAudioGate.OnlyWhenGestureCorrect,
                deferFeedback: false, gateOnGesture: false),

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
