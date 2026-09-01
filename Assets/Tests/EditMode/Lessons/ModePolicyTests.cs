using Jazztures.Core.Lessons;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class ModePolicyTests
    {
        // The CLAUDE.md §3.8 table, row by row.
        [TestCase(LearningMode.GestureLearning, SystemPlayback.None, true, UserAudioGate.Always, false)]
        [TestCase(LearningMode.WatchAndListen, SystemPlayback.Full, true, UserAudioGate.Never, false)]
        [TestCase(LearningMode.TryYourself, SystemPlayback.None, true, UserAudioGate.OnlyWhenGestureCorrect, false)]
        [TestCase(LearningMode.TestYourself, SystemPlayback.None, false, UserAudioGate.Always, true)]
        [TestCase(LearningMode.ComposeOnTheFly, SystemPlayback.BackingOnly, false, UserAudioGate.Always, false)]
        public void For_MatchesTheModeTable(
            LearningMode mode,
            SystemPlayback playback,
            bool ghost,
            UserAudioGate userAudio,
            bool deferFeedback)
        {
            ModePolicy policy = ModePolicy.For(mode);

            Assert.That(policy.SystemPlayback, Is.EqualTo(playback));
            Assert.That(policy.GhostHandsVisible, Is.EqualTo(ghost));
            Assert.That(policy.UserAudio, Is.EqualTo(userAudio));
            Assert.That(policy.DeferFeedback, Is.EqualTo(deferFeedback));
        }

        [Test]
        public void OnlyTestYourself_DefersFeedback()
        {
            foreach (LearningMode mode in System.Enum.GetValues(typeof(LearningMode)))
            {
                Assert.That(
                    ModePolicy.For(mode).DeferFeedback,
                    Is.EqualTo(mode == LearningMode.TestYourself),
                    mode.ToString());
            }
        }
    }
}
