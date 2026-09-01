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
            bool deferFeedback)
        {
            SystemPlayback = systemPlayback;
            GhostHandsVisible = ghostHandsVisible;
            UserAudio = userAudio;
            DeferFeedback = deferFeedback;
        }

        public SystemPlayback SystemPlayback { get; }

        public bool GhostHandsVisible { get; }

        public UserAudioGate UserAudio { get; }

        /// <summary>True when correctness feedback is withheld until the attempt completes (§3.7).</summary>
        public bool DeferFeedback { get; }

        public static ModePolicy For(LearningMode mode) => mode switch
        {
            LearningMode.GestureLearning => new ModePolicy(
                SystemPlayback.None, ghostHandsVisible: true, UserAudioGate.Always, deferFeedback: false),

            LearningMode.WatchAndListen => new ModePolicy(
                SystemPlayback.Full, ghostHandsVisible: true, UserAudioGate.Never, deferFeedback: false),

            LearningMode.TryYourself => new ModePolicy(
                SystemPlayback.None, ghostHandsVisible: true, UserAudioGate.OnlyWhenGestureCorrect, deferFeedback: false),

            LearningMode.TestYourself => new ModePolicy(
                SystemPlayback.None, ghostHandsVisible: false, UserAudioGate.Always, deferFeedback: true),

            LearningMode.ComposeOnTheFly => new ModePolicy(
                SystemPlayback.BackingOnly, ghostHandsVisible: false, UserAudioGate.Always, deferFeedback: false),

            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }
}
