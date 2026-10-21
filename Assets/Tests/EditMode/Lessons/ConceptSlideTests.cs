using System;
using Jazztures.Core.Lessons;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class ConceptSlideTests
    {
        private static LessonPlan Plan(string explanation, params ConceptSlide[] slides) =>
            new LessonPlan(
                new LessonId("L1"), "Chords", explanation, new[] { LearningMode.WatchAndListen },
                Tempo.Default, SwingRatio.Straight, ActiveHands.Left, slides);

        [Test]
        public void AuthoredSlides_KeepOrderAndSumTheirTime()
        {
            LessonPlan plan = Plan("ignored", new ConceptSlide("one", 5f), new ConceptSlide("two", 7.5f));

            Assert.That(plan.ConceptSlides.Count, Is.EqualTo(2));
            Assert.That(plan.ConceptSlides[0].Text, Is.EqualTo("one"));
            Assert.That(plan.ConceptSlides[1].Text, Is.EqualTo("two"));
            Assert.That(plan.ConceptSeconds, Is.EqualTo(12.5f));
        }

        [Test]
        public void PlainExplanation_IsOneSlide_ThatDoesNotDelayTheLesson()
        {
            LessonPlan plan = Plan("A chord is three or more notes.");

            Assert.That(plan.ConceptSlides.Count, Is.EqualTo(1));
            Assert.That(plan.ConceptSlides[0].Text, Is.EqualTo("A chord is three or more notes."));
            Assert.That(plan.ConceptSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void NoExplanation_MeansNoSlides()
        {
            Assert.That(Plan(string.Empty).ConceptSlides, Is.Empty);
        }

        [Test]
        public void Slide_RejectsBlankTextAndNegativeTime()
        {
            Assert.Throws<ArgumentException>(() => new ConceptSlide(" ", 3f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConceptSlide("x", -1f));
        }

        [Test]
        public void ShowText_CarriesItsHoldTime()
        {
            Assert.That(CueAction.ShowText("hi", holdSeconds: 6f).HoldSeconds, Is.EqualTo(6f));
            Assert.That(CueAction.ShowText("hi").HoldSeconds, Is.EqualTo(0f));
        }
    }
}
