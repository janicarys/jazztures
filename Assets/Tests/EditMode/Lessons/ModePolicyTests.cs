using Jazztures.Core.Lessons;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class ModePolicyTests
    {
        // The CLAUDE.md §3.8 table, row by row. Gesture Learning is unconditional-audio,
        // ungated pose practice ("pose fluency only, no musical target"); Try Yourself is
        // the reward-gated, step-through-the-target mode ("audio is the reward for matching
        // it") — the runner then only engages GateOnGesture on a left-hand lesson.
        [TestCase(LearningMode.GestureLearning, SystemPlayback.None, true, UserAudioGate.Always, false, false)]
        [TestCase(LearningMode.WatchAndListen, SystemPlayback.Full, true, UserAudioGate.Never, false, false)]
        [TestCase(LearningMode.TryYourself, SystemPlayback.None, true, UserAudioGate.OnlyWhenGestureCorrect, false, true)]
        [TestCase(LearningMode.TestYourself, SystemPlayback.None, false, UserAudioGate.Always, true, false)]
        [TestCase(LearningMode.ComposeOnTheFly, SystemPlayback.BackingOnly, false, UserAudioGate.Always, false, false)]
        public void For_MatchesTheModeTable(
            LearningMode mode,
            SystemPlayback playback,
            bool ghost,
            UserAudioGate userAudio,
            bool deferFeedback,
            bool gateOnGesture)
        {
            ModePolicy policy = ModePolicy.For(mode);

            Assert.That(policy.SystemPlayback, Is.EqualTo(playback));
            Assert.That(policy.GhostHandsVisible, Is.EqualTo(ghost));
            Assert.That(policy.UserAudio, Is.EqualTo(userAudio));
            Assert.That(policy.DeferFeedback, Is.EqualTo(deferFeedback));
            Assert.That(policy.GateOnGesture, Is.EqualTo(gateOnGesture));
        }

        // ADR-0043 decision 3: in a right-hand lesson there is no left hand to play the
        // chords, so the two modes that otherwise play nothing play the harmony.
        [TestCase(LearningMode.TryYourself)]
        [TestCase(LearningMode.TestYourself)]
        public void ARightHandLesson_PlaysTheHarmony_InTryAndTestYourself(LearningMode mode)
        {
            ModePolicy policy = ModePolicy.For(mode, ActiveHands.Right);

            Assert.That(policy.SystemPlayback, Is.EqualTo(SystemPlayback.HarmonyOnly));
        }

        [TestCase(LearningMode.TryYourself)]
        [TestCase(LearningMode.TestYourself)]
        public void ALeftHandOrTwoHandLesson_IsUnchanged_BecauseTheLearnerPlaysTheChords(LearningMode mode)
        {
            Assert.That(ModePolicy.For(mode, ActiveHands.Left).SystemPlayback, Is.EqualTo(SystemPlayback.None));
            Assert.That(ModePolicy.For(mode, ActiveHands.Both).SystemPlayback, Is.EqualTo(SystemPlayback.None));
        }

        [Test]
        public void TheHarmonyIsTheOnlyThingHandsChange()
        {
            foreach (LearningMode mode in System.Enum.GetValues(typeof(LearningMode)))
            {
                ModePolicy plain = ModePolicy.For(mode);
                ModePolicy right = ModePolicy.For(mode, ActiveHands.Right);

                Assert.That(right.GhostHandsVisible, Is.EqualTo(plain.GhostHandsVisible), mode.ToString());
                Assert.That(right.UserAudio, Is.EqualTo(plain.UserAudio), mode.ToString());
                Assert.That(right.DeferFeedback, Is.EqualTo(plain.DeferFeedback), mode.ToString());
                Assert.That(right.GateOnGesture, Is.EqualTo(plain.GateOnGesture), mode.ToString());
            }
        }

        [Test]
        public void OtherModes_AreUnchangedForARightHandLesson()
        {
            foreach (LearningMode mode in new[]
                     {
                         LearningMode.GestureLearning, LearningMode.WatchAndListen, LearningMode.ComposeOnTheFly,
                     })
            {
                Assert.That(
                    ModePolicy.For(mode, ActiveHands.Right).SystemPlayback,
                    Is.EqualTo(ModePolicy.For(mode).SystemPlayback),
                    mode.ToString());
            }
        }

        [Test]
        public void WithoutHands_ForIsTheOriginalTable()
        {
            foreach (LearningMode mode in System.Enum.GetValues(typeof(LearningMode)))
            {
                Assert.That(
                    ModePolicy.For(mode, ActiveHands.Both).SystemPlayback,
                    Is.EqualTo(ModePolicy.For(mode).SystemPlayback),
                    mode.ToString());
            }
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
