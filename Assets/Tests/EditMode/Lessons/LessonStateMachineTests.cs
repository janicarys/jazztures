using System;
using System.Collections.Generic;
using Jazztures.Core.Lessons;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class LessonStateMachineTests
    {
        private static LessonPlan Plan(params LearningMode[] modes) =>
            new LessonPlan(
                new LessonId("L1"),
                "ii-V-I chords",
                "Three shapes, one per chord. Prepare, tense, resolve.",
                modes,
                Tempo.Default,
                SwingRatio.Straight,
                ActiveHands.Left);

        [Test]
        public void StartsNotStarted_WithNoCurrentPhase()
        {
            var sm = new LessonStateMachine(Plan(LearningMode.WatchAndListen, LearningMode.TryYourself));

            Assert.That(sm.Status, Is.EqualTo(LessonStatus.NotStarted));
            Assert.That(sm.PhaseIndex, Is.EqualTo(-1));
            Assert.That(sm.CurrentPhase, Is.Null);
        }

        [Test]
        public void Begin_EntersPhaseZero_AndFires()
        {
            var sm = new LessonStateMachine(Plan(LearningMode.WatchAndListen, LearningMode.TryYourself));
            var seen = new List<LessonPhase>();
            sm.PhaseChanged += seen.Add;

            sm.Begin();

            Assert.That(sm.Status, Is.EqualTo(LessonStatus.InPhase));
            Assert.That(sm.CurrentPhase!.Value.Mode, Is.EqualTo(LearningMode.WatchAndListen));
            Assert.That(seen, Has.Count.EqualTo(1));
            Assert.That(seen[0].Index, Is.EqualTo(0));
            Assert.That(seen[0].IsLast, Is.False);
        }

        [Test]
        public void AdvancePhase_WalksTheModesInOrder_ThenCompletes()
        {
            var sm = new LessonStateMachine(Plan(
                LearningMode.WatchAndListen, LearningMode.TryYourself, LearningMode.TestYourself));
            var modes = new List<LearningMode>();
            sm.PhaseChanged += p => modes.Add(p.Mode);
            bool completed = false;
            sm.Completed += () => completed = true;

            sm.Begin();
            Assert.That(sm.AdvancePhase(), Is.True);
            Assert.That(sm.AdvancePhase(), Is.True);
            Assert.That(sm.CurrentPhase!.Value.IsLast, Is.True);

            Assert.That(sm.AdvancePhase(), Is.False, "stepping off the last phase completes");
            Assert.That(sm.Status, Is.EqualTo(LessonStatus.Completed));
            Assert.That(completed, Is.True);
            Assert.That(modes, Is.EqualTo(new[]
            {
                LearningMode.WatchAndListen, LearningMode.TryYourself, LearningMode.TestYourself,
            }));
        }

        [Test]
        public void EachPhaseExposesItsModePolicy()
        {
            var sm = new LessonStateMachine(Plan(LearningMode.WatchAndListen, LearningMode.TryYourself));

            sm.Begin();
            Assert.That(sm.CurrentPhase!.Value.Policy.UserAudio, Is.EqualTo(UserAudioGate.Never));

            sm.AdvancePhase();
            Assert.That(sm.CurrentPhase!.Value.Policy.UserAudio, Is.EqualTo(UserAudioGate.OnlyWhenGestureCorrect));
        }

        [Test]
        public void CannotBeginTwice()
        {
            var sm = new LessonStateMachine(Plan(LearningMode.WatchAndListen));
            sm.Begin();

            Assert.Throws<InvalidOperationException>(() => sm.Begin());
        }

        [Test]
        public void CannotAdvanceBeforeBeginningOrAfterCompleting()
        {
            var sm = new LessonStateMachine(Plan(LearningMode.WatchAndListen));

            Assert.Throws<InvalidOperationException>(() => sm.AdvancePhase());

            sm.Begin();
            Assert.That(sm.AdvancePhase(), Is.False); // completes
            Assert.Throws<InvalidOperationException>(() => sm.AdvancePhase());
        }

        [Test]
        public void Reset_AllowsAReRun()
        {
            var sm = new LessonStateMachine(Plan(LearningMode.WatchAndListen, LearningMode.TryYourself));
            sm.Begin();
            sm.AdvancePhase();
            sm.AdvancePhase(); // complete

            sm.Reset();
            Assert.That(sm.Status, Is.EqualTo(LessonStatus.NotStarted));
            sm.Begin();
            Assert.That(sm.CurrentPhase!.Value.Index, Is.EqualTo(0));
        }

        [Test]
        public void Session1Lessons_EachRunStartToFinishUnattended()
        {
            // CLAUDE.md §3.9: S1 = L1 + L2 + L3, and S1 must not be left-hand-only.
            var session1 = new[]
            {
                new LessonPlan(new LessonId("L1"), "ii-V-I chords", "…",
                    new[] { LearningMode.WatchAndListen, LearningMode.TryYourself },
                    Tempo.Default, SwingRatio.Straight, ActiveHands.Left),
                new LessonPlan(new LessonId("L2"), "Timing", "…",
                    new[] { LearningMode.WatchAndListen, LearningMode.TryYourself, LearningMode.TestYourself },
                    Tempo.Default, SwingRatio.Straight, ActiveHands.Left),
                new LessonPlan(new LessonId("L3"), "Chord tones", "…",
                    new[] { LearningMode.WatchAndListen, LearningMode.TryYourself },
                    Tempo.Default, SwingRatio.Straight, ActiveHands.Right),
            };

            var hands = ActiveHands.None;
            foreach (LessonPlan plan in session1)
            {
                var sm = new LessonStateMachine(plan);
                sm.Begin();
                while (sm.AdvancePhase())
                {
                }

                Assert.That(sm.Status, Is.EqualTo(LessonStatus.Completed), plan.Id.Value);
                hands |= plan.Hands;
            }

            Assert.That(hands, Is.EqualTo(ActiveHands.Both), "Session 1 is not left-hand-only");
        }

        [Test]
        public void Plan_RejectsEmptyModesAndNoHands()
        {
            Assert.Throws<ArgumentException>(() => new LessonPlan(
                new LessonId("L1"), "t", "c", Array.Empty<LearningMode>(),
                Tempo.Default, SwingRatio.Straight, ActiveHands.Left));

            Assert.Throws<ArgumentException>(() => new LessonPlan(
                new LessonId("L1"), "t", "c", new[] { LearningMode.WatchAndListen },
                Tempo.Default, SwingRatio.Straight, ActiveHands.None));
        }

        [Test]
        public void LessonId_RejectsEmptyAndComparesByValue()
        {
            Assert.Throws<ArgumentException>(() => new LessonId("  "));
            Assert.That(new LessonId("L1"), Is.EqualTo(new LessonId("L1")));
            Assert.That(new LessonId("L1"), Is.Not.EqualTo(new LessonId("L2")));
        }
    }
}
