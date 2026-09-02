using System;
using Jazztures.Core.Evaluation;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Evaluation
{
    public class OnsetScorerTests
    {
        private static readonly OnsetWindows Windows = OnsetWindows.Default;

        [TestCase(0.0, OnsetVerdict.OnTime)]
        [TestCase(0.080, OnsetVerdict.OnTime)]
        [TestCase(-0.080, OnsetVerdict.OnTime)]
        [TestCase(0.081, OnsetVerdict.Close)]
        [TestCase(0.160, OnsetVerdict.Close)]
        [TestCase(-0.160, OnsetVerdict.Close)]
        [TestCase(0.161, OnsetVerdict.Off)]
        [TestCase(0.5, OnsetVerdict.Off)]
        public void Classify_MatchesTheSection37Table(double deviation, OnsetVerdict expected)
        {
            Assert.That(OnsetScorer.Classify(deviation, Windows), Is.EqualTo(expected));
        }

        [Test]
        public void Evaluate_ScoresAPerfectAttempt()
        {
            var beats = new[] { 0.0, 0.5, 1.0, 1.5 };

            AttemptResult result = OnsetScorer.Evaluate(beats, beats, Windows);

            Assert.That(result.OnTimeCount, Is.EqualTo(4));
            Assert.That(result.MissedCount, Is.Zero);
            Assert.That(result.ExtraCount, Is.Zero);
            Assert.That(result.OnTimeFraction, Is.EqualTo(1.0));
            Assert.That(result.MeanAbsDeviationSeconds, Is.EqualTo(0.0).Within(1e-12));
        }

        [Test]
        public void Evaluate_ClassifiesEachOnsetByItsOwnDeviation()
        {
            var expected = new[] { 0.0, 1.0, 2.0 };
            var actual = new[] { 0.02, 1.12, 2.30 }; // on time, close, off

            AttemptResult result = OnsetScorer.Evaluate(expected, actual, Windows);

            Assert.That(result.OnTimeCount, Is.EqualTo(1));
            Assert.That(result.CloseCount, Is.EqualTo(1));
            Assert.That(result.OffCount, Is.EqualTo(1));
            Assert.That(result.Onsets[1].Verdict, Is.EqualTo(OnsetVerdict.Close));
        }

        [Test]
        public void Evaluate_CountsMissedAndExtraNotes()
        {
            var expected = new[] { 0.0, 1.0, 2.0, 3.0 };
            var actual = new[] { 0.01, 2.01, 5.0 }; // beat 1 & 3 missed; 5.0 is extra

            AttemptResult result = OnsetScorer.Evaluate(expected, actual, Windows);

            Assert.That(result.MatchedCount, Is.EqualTo(2));
            Assert.That(result.MissedCount, Is.EqualTo(2));
            Assert.That(result.ExtraCount, Is.EqualTo(1));
            Assert.That(result.ExpectedCount, Is.EqualTo(4));
        }

        [Test]
        public void Evaluate_ReportsRushingAsNegativeSignedDeviation()
        {
            var expected = new[] { 1.0, 2.0, 3.0 };
            var actual = new[] { 0.95, 1.94, 2.93 }; // consistently early

            AttemptResult result = OnsetScorer.Evaluate(expected, actual, Windows);

            Assert.That(result.MeanSignedDeviationSeconds, Is.LessThan(0.0));
            Assert.That(result.MeanAbsDeviationSeconds, Is.GreaterThan(0.0));
        }

        [Test]
        public void Evaluate_ToleratesUnsortedInput()
        {
            var expected = new[] { 2.0, 0.0, 1.0 };
            var actual = new[] { 1.01, 0.01, 2.01 };

            AttemptResult result = OnsetScorer.Evaluate(expected, actual, Windows);

            Assert.That(result.OnTimeCount, Is.EqualTo(3));
        }

        [Test]
        public void Evaluate_DoesNotMatchAcrossTheMatchWindow()
        {
            var expected = new[] { 0.0 };
            var actual = new[] { 0.30 + 1e-6 }; // just past MatchSeconds

            AttemptResult result = OnsetScorer.Evaluate(expected, actual, Windows);

            Assert.That(result.MissedCount, Is.EqualTo(1));
            Assert.That(result.ExtraCount, Is.EqualTo(1));
            Assert.That(result.MatchedCount, Is.Zero);
        }

        [Test]
        public void Evaluate_EmptyAttempt_IsEmpty()
        {
            AttemptResult result = OnsetScorer.Evaluate(Array.Empty<double>(), Array.Empty<double>(), Windows);

            Assert.That(result.IsEmpty, Is.True);
            Assert.That(result.OnTimeFraction, Is.EqualTo(0.0));
        }

        [Test]
        public void Evaluate_MissedPhrase_IsNotEmpty()
        {
            AttemptResult result = OnsetScorer.Evaluate(new[] { 0.0, 1.0 }, Array.Empty<double>(), Windows);

            Assert.That(result.IsEmpty, Is.False);
            Assert.That(result.MissedCount, Is.EqualTo(2));
        }

        [Test]
        public void Windows_RejectInvalidOrdering()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OnsetWindows(0.16, 0.08, 0.3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OnsetWindows(0.08, 0.16, 0.10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OnsetWindows(0.0, 0.16, 0.3));
        }

        [Test]
        public void Evaluate_RejectsNonFiniteOnsets()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => OnsetScorer.Evaluate(new[] { 0.0, double.NaN }, new[] { 0.0 }, Windows));
        }
    }
}
