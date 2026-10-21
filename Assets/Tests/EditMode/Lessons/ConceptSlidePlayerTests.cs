using System;
using Jazztures.Core.Lessons;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class ConceptSlidePlayerTests
    {
        private static ConceptSlide[] Two() => new[] { new ConceptSlide("one", 5f), new ConceptSlide("two", 7f) };

        [Test]
        public void NoDiscussion_IsOverAtOnce()
        {
            var player = new ConceptSlidePlayer();

            player.Start(Array.Empty<ConceptSlide>(), 10.0);

            Assert.That(player.IsFinished(10.0), Is.True);
            Assert.That(player.TryTake(10.0, out _), Is.False);
        }

        [Test]
        public void NeverStarted_IsOver()
        {
            Assert.That(new ConceptSlidePlayer().IsFinished(0.0), Is.True);
        }

        [Test]
        public void TheFirstSlide_IsDueTheMomentItStarts_AndTheDiscussionIsNotOver()
        {
            var player = new ConceptSlidePlayer();
            player.Start(Two(), 10.0);

            Assert.That(player.IsFinished(10.0), Is.False, "slides are still to come");
            Assert.That(player.TryTake(10.0, out ConceptSlide first), Is.True);
            Assert.That(first.Text, Is.EqualTo("one"));
        }

        [Test]
        public void ASlide_IsHeldForItsTime_BeforeTheNextIsDue()
        {
            var player = new ConceptSlidePlayer();
            player.Start(Two(), 10.0);
            player.TryTake(10.0, out _);

            Assert.That(player.TryTake(10.0, out _), Is.False, "not twice at once");
            Assert.That(player.TryTake(14.9, out _), Is.False, "still holding the first");
            Assert.That(player.TryTake(15.0, out ConceptSlide second), Is.True);
            Assert.That(second.Text, Is.EqualTo("two"));
        }

        // The bug this class exists for: "slides still going" used to mean "a slide is left to show", which was false the
        // moment the LAST one appeared - so the lesson's cues and ghost hands started under it, a whole slide early.
        [Test]
        public void TheDiscussionIsNotOver_UntilTheLastSlideHasHeldForItsTime()
        {
            var player = new ConceptSlidePlayer();
            player.Start(Two(), 10.0);
            player.TryTake(10.0, out _);
            player.TryTake(15.0, out _); // the last slide appears at 15 and asks for 7 s

            Assert.That(player.IsFinished(15.0), Is.False, "it has only just appeared");
            Assert.That(player.IsFinished(21.9), Is.False, "still on screen");
            Assert.That(player.IsFinished(22.0), Is.True, "its time is up");
        }

        [Test]
        public void ALateCall_DelaysTheNextSlide_RatherThanCuttingTheCurrentOneShort()
        {
            var player = new ConceptSlidePlayer();
            player.Start(Two(), 10.0);
            player.TryTake(10.0, out _);

            Assert.That(player.TryTake(20.0, out _), Is.True, "the second slide, late");
            Assert.That(player.IsFinished(26.9), Is.False, "held for its own 7 s from when it appeared");
            Assert.That(player.IsFinished(27.0), Is.True);
        }

        [Test]
        public void AZeroSecondSlide_IsShown_ButDoesNotDelayAnything()
        {
            var player = new ConceptSlidePlayer();
            player.Start(new[] { new ConceptSlide("A chord is three or more notes.", 0f) }, 10.0);

            Assert.That(player.IsFinished(10.0), Is.False, "not until it has been shown");
            Assert.That(player.TryTake(10.0, out _), Is.True);
            Assert.That(player.IsFinished(10.0), Is.True, "and then it is over at once");
        }

        [Test]
        public void Stop_EndsTheDiscussionAtOnce()
        {
            var player = new ConceptSlidePlayer();
            player.Start(Two(), 10.0);
            player.TryTake(10.0, out _);

            player.Stop();

            Assert.That(player.IsFinished(10.0), Is.True);
            Assert.That(player.TryTake(100.0, out _), Is.False);
        }

        [Test]
        public void StartingAgain_BeginsAFreshDiscussion()
        {
            var player = new ConceptSlidePlayer();
            player.Start(Two(), 10.0);
            player.TryTake(10.0, out _);
            player.TryTake(15.0, out _);

            player.Start(Two(), 100.0);

            Assert.That(player.IsFinished(100.0), Is.False);
            Assert.That(player.TryTake(100.0, out ConceptSlide first), Is.True);
            Assert.That(first.Text, Is.EqualTo("one"));
        }
    }
}
