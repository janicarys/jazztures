using System;
using Jazztures.Core.Evaluation;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class LessonTimelineTests
    {
        private static LessonTimeline BuildSimplePhrase() =>
            new LessonTimelineBuilder()
                .WithTempo(Tempo.Bpm(120)) // 0.5 s / beat
                .Chord(0.0, ChordFunction.Two)
                .Chord(4.0, ChordFunction.Five)
                .Chord(8.0, ChordFunction.One)
                .Note(0.0, 0)
                .Note(1.0, 2)
                .Note(4.5, 3)
                .Marker(8.0, "resolution")
                .Build();

        [Test]
        public void Build_SortsEventsRegardlessOfInsertionOrder()
        {
            LessonTimeline timeline = new LessonTimelineBuilder()
                .Chord(0.0, ChordFunction.Two)
                .Note(2.0, 1)
                .Note(0.5, 0)
                .Build();

            Assert.That(timeline.Notes[0].Beat.Position, Is.EqualTo(0.5));
            Assert.That(timeline.Notes[1].Beat.Position, Is.EqualTo(2.0));
        }

        [Test]
        public void ChordFunctionAt_ReturnsTheLastChangeAtOrBeforeTheBeat()
        {
            LessonTimeline timeline = BuildSimplePhrase();

            Assert.That(timeline.ChordFunctionAt(0.0), Is.EqualTo(ChordFunction.Two));
            Assert.That(timeline.ChordFunctionAt(3.99), Is.EqualTo(ChordFunction.Two));
            Assert.That(timeline.ChordFunctionAt(4.0), Is.EqualTo(ChordFunction.Five));
            Assert.That(timeline.ChordFunctionAt(100.0), Is.EqualTo(ChordFunction.One));
        }

        [Test]
        public void ChordFunctionAt_IsNullBeforeTheFirstChord()
        {
            LessonTimeline timeline = new LessonTimelineBuilder()
                .Chord(2.0, ChordFunction.Two)
                .Build();

            Assert.That(timeline.ChordFunctionAt(1.9), Is.Null);
        }

        [Test]
        public void ExpectedOnsetSeconds_AppliesTempoAndSwing()
        {
            LessonTimeline straight = BuildSimplePhrase();
            Assert.That(straight.ExpectedOnsetSeconds(), Is.EqualTo(new[] { 0.0, 0.5, 2.25 }).Within(1e-12));

            LessonTimeline swung = new LessonTimelineBuilder()
                .WithTempo(Tempo.Bpm(120))
                .WithSwing(new SwingRatio(0.66))
                .Chord(0.0, ChordFunction.One)
                .Note(1.5, 0) // the "and" of beat 1 -> 1.66 beats -> 0.83 s
                .Build();

            Assert.That(swung.ExpectedOnsetSeconds()[0], Is.EqualTo(0.83).Within(1e-12));
        }

        [Test]
        public void ExpectedOnsetSeconds_FeedsTheOnsetScorerDirectly()
        {
            LessonTimeline timeline = BuildSimplePhrase();
            double[] expected = timeline.ExpectedOnsetSeconds();

            AttemptResult result = OnsetScorer.Evaluate(expected, expected, OnsetWindows.Default);

            Assert.That(result.OnTimeCount, Is.EqualTo(3));
        }

        [Test]
        public void MarkerBeat_FindsNamedMarkersAndReturnsNullOtherwise()
        {
            LessonTimeline timeline = BuildSimplePhrase();

            Assert.That(timeline.MarkerBeat("resolution"), Is.EqualTo(8.0));
            Assert.That(timeline.MarkerBeat("nope"), Is.Null);
        }

        [Test]
        public void DurationBeats_IsTheLastEvent()
        {
            Assert.That(BuildSimplePhrase().DurationBeats, Is.EqualTo(8.0));
        }

        [Test]
        public void Build_RejectsANoteWithNoChordActive()
        {
            Assert.Throws<InvalidOperationException>(() => new LessonTimelineBuilder()
                .Chord(2.0, ChordFunction.Two)
                .Note(0.0, 0)
                .Build());
        }

        [Test]
        public void Build_RejectsTwoChordsOnTheSameBeat()
        {
            Assert.Throws<InvalidOperationException>(() => new LessonTimelineBuilder()
                .Chord(0.0, ChordFunction.Two)
                .Chord(0.0, ChordFunction.Five)
                .Build());
        }

        [Test]
        public void Build_RejectsDuplicateMarkerNames()
        {
            Assert.Throws<InvalidOperationException>(() => new LessonTimelineBuilder()
                .Marker(0.0, "x")
                .Marker(1.0, "x")
                .Build());
        }

        [Test]
        public void Builder_RejectsAnOutOfRangeTargetIndex()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LessonTimelineBuilder().Note(0.0, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LessonTimelineBuilder().Note(0.0, -1));
        }
    }
}
