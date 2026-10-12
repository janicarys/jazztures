using System;
using Jazztures.Core.Drills;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Drills
{
    public class TimingDrillTests
    {
        // 60 BPM keeps the arithmetic readable: one beat is one second.
        private static TimingDrillBuilder Drill() =>
            new TimingDrillBuilder()
                .Named("test")
                .WithTempo(Tempo.Bpm(60))
                .WithBars(2)
                .WithCountInBars(1)
                .WithPattern(OnsetPattern.EveryBeat);

        [Test]
        public void TheCountIn_ComesBeforeTheScoredRegion_AndHasNoExpectedOnsets()
        {
            TimingDrill drill = Drill().Build();

            Assert.That(drill.TotalBars, Is.EqualTo(3));
            Assert.That(drill.TotalBeats, Is.EqualTo(12));
            Assert.That(drill.ScoredStartBeat, Is.EqualTo(4.0));
            Assert.That(drill.CountInSeconds, Is.EqualTo(4.0).Within(1e-12));
            Assert.That(drill.TotalSeconds, Is.EqualTo(12.0).Within(1e-12));

            double[] seconds = drill.ExpectedOnsetSeconds();
            Assert.That(seconds, Is.EqualTo(new[] { 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0, 11.0 }).Within(1e-12));
        }

        [Test]
        public void NoCountIn_ScoresFromTheFirstBeat()
        {
            TimingDrill drill = Drill().WithCountInBars(0).Build();

            Assert.That(drill.ExpectedOnsetSeconds()[0], Is.EqualTo(0.0));
            Assert.That(drill.ExpectedOnsetSeconds().Length, Is.EqualTo(8));
        }

        [Test]
        public void EveryEighth_PutsTheOffBeatsOnTheSwungGrid()
        {
            TimingDrill drill = Drill()
                .WithBars(1)
                .WithCountInBars(0)
                .WithSwing(new SwingRatio(0.66))
                .WithPattern(OnsetPattern.EveryEighth)
                .Build();

            double[] seconds = drill.ExpectedOnsetSeconds();

            Assert.That(seconds.Length, Is.EqualTo(8));
            Assert.That(seconds[0], Is.EqualTo(0.0).Within(1e-12));
            Assert.That(seconds[1], Is.EqualTo(0.66).Within(1e-12), "the swung 'and' of 1");
            Assert.That(seconds[2], Is.EqualTo(1.0).Within(1e-12), "the beat itself never moves");
        }

        [Test]
        public void ACompingPattern_RepeatsPerBar_AfterTheCountIn()
        {
            TimingDrill drill = Drill()
                .ScoringHand(Handedness.Left)
                .WithPattern(OnsetPattern.PerBar(new[] { 0.0, 2.0 })) // beats 1 and 3
                .Build();

            Assert.That(drill.ExpectedOnsetSeconds(), Is.EqualTo(new[] { 4.0, 6.0, 8.0, 10.0 }).Within(1e-12));
        }

        [Test]
        public void AMetronomeOnlyDrill_ExpectsNothing()
        {
            TimingDrill drill = Drill().WithScoring(false).WithPattern(OnsetPattern.None).Build();

            Assert.That(drill.ScoringEnabled, Is.False);
            Assert.That(drill.ExpectedOnsetSeconds(), Is.Empty);
        }

        [Test]
        public void AScoredDrill_NeedsSomethingToScore()
        {
            Assert.Throws<InvalidOperationException>(() => Drill().WithPattern(OnsetPattern.None).Build());
        }

        [Test]
        public void TheMetronome_CarriesTheDrillsGrid()
        {
            TimingDrill drill = Drill()
                .WithBeatsPerBar(3)
                .WithSubdivision(MetronomeSubdivision.SwungEighths)
                .WithSwing(new SwingRatio(0.7))
                .Build();

            Metronome metronome = drill.CreateMetronome();

            Assert.That(metronome.Tempo, Is.EqualTo(Tempo.Bpm(60)));
            Assert.That(metronome.BeatsPerBar, Is.EqualTo(3));
            Assert.That(metronome.Subdivision, Is.EqualTo(MetronomeSubdivision.SwungEighths));
            Assert.That(metronome.Swing, Is.EqualTo(new SwingRatio(0.7)));
            Assert.That(metronome.Running, Is.False, "the runner starts it at the shared start");
        }

        // --- harmony -----------------------------------------------------------------------

        [Test]
        public void WithNoHarmony_ThereIsNoTimeline()
        {
            TimingDrill drill = Drill().Build();

            Assert.That(drill.HarmonyKind, Is.EqualTo(DrillHarmonyKind.None));
            Assert.That(drill.BuildHarmonyTimeline(), Is.Null);
        }

        [Test]
        public void AFixedChord_IsStruckOnEveryBarLine_ThroughTheCountIn()
        {
            TimingDrill drill = Drill().FixedChord(ChordFunction.One).Build();

            LessonTimeline timeline = drill.BuildHarmonyTimeline()!;

            Assert.That(drill.HarmonyKind, Is.EqualTo(DrillHarmonyKind.Fixed));
            Assert.That(timeline.Chords.Count, Is.EqualTo(3), "count-in bar + two scored bars");
            Assert.That(timeline.Chords[0].Beat.Position, Is.EqualTo(0.0));
            Assert.That(timeline.Chords[1].Beat.Position, Is.EqualTo(4.0));
            Assert.That(timeline.Chords[2].Beat.Position, Is.EqualTo(8.0));
            Assert.That(timeline.Chords[2].Function, Is.EqualTo(ChordFunction.One));
        }

        [Test]
        public void TheHarmonyTimeline_RunsToTheEndOfTheDrill()
        {
            TimingDrill drill = Drill().FixedChord(ChordFunction.One).Build();

            Assert.That(drill.BuildHarmonyTimeline()!.DurationBeats, Is.EqualTo(12.0));
        }

        [Test]
        public void AnAuthoredTimeline_IsUsedAsWritten_AndSorted()
        {
            TimingDrill drill = Drill()
                .ChordAt(8.0, ChordFunction.One)
                .ChordAt(0.0, ChordFunction.Two)
                .ChordAt(4.0, ChordFunction.Five)
                .Build();

            LessonTimeline timeline = drill.BuildHarmonyTimeline()!;

            Assert.That(drill.HarmonyKind, Is.EqualTo(DrillHarmonyKind.Timeline));
            Assert.That(timeline.Chords.Count, Is.EqualTo(3));
            Assert.That(timeline.Chords[0].Function, Is.EqualTo(ChordFunction.Two));
            Assert.That(timeline.Chords[2].Function, Is.EqualTo(ChordFunction.One));
        }

        [Test]
        public void HarmonyIsAudibleByDefault_AndCanBeSilencedWithoutLosingTheSelection()
        {
            Assert.That(Drill().FixedChord(ChordFunction.One).Build().HarmonyAudible, Is.True);

            TimingDrill silent = Drill().FixedChord(ChordFunction.One).WithHarmonyAudible(false).Build();
            Assert.That(silent.HarmonyAudible, Is.False);
            Assert.That(silent.BuildHarmonyTimeline(), Is.Not.Null, "the melody still follows the chord");
        }

        [Test]
        public void AFixedChordAndATimeline_AreMutuallyExclusive()
        {
            Assert.Throws<InvalidOperationException>(() =>
                Drill().FixedChord(ChordFunction.One).ChordAt(0.0, ChordFunction.Two).Build());
        }

        [Test]
        public void ALeftHandDrill_CannotAlsoHaveScriptedHarmony()
        {
            Assert.Throws<InvalidOperationException>(() =>
                Drill().ScoringHand(Handedness.Left).FixedChord(ChordFunction.One).Build());
        }

        [Test]
        public void TwoChordChangesOnOneBeat_AreRejected()
        {
            Assert.Throws<InvalidOperationException>(() =>
                Drill().ChordAt(0.0, ChordFunction.One).ChordAt(0.0, ChordFunction.Two).Build());
        }

        [Test]
        public void BuilderArguments_AreValidated()
        {
            Assert.Throws<ArgumentException>(() => new TimingDrillBuilder().Named(" "));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimingDrillBuilder().WithBars(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimingDrillBuilder().WithCountInBars(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TimingDrillBuilder().WithBeatsPerBar(0));
        }
    }
}
