using System.Collections.Generic;
using System.Linq;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    // ADR-0081: the Swing lesson's pure parts - swing timing, the pattern text, the fixed scale regions, the looping progression.
    public class SwingLessonTests
    {
        private static readonly Tempo Sixty = new Tempo(60);
        private static readonly SwingRatio TwoThirds = new SwingRatio(2.0 / 3.0);

        private static LessonTimeline Build(string bar, int straightBars = 0) => SwingLessonBuilder.BuildTimeline(
            new SwingStepSpec("t", new string[0], new[] { bar, "", "", "" }, straightBars, new[] { LearningMode.WatchAndListen }),
            Sixty, TwoThirds);

        private static int[] Midis(RegionPitchSet set) =>
            Enumerable.Range(0, set.Count).Select(i => set[i].Midi).ToArray();

        // --- Swing timing -------------------------------------------------------------------

        [Test]
        public void ASwungOffBeatEighth_IsPlayedAtTwoThirdsOfTheBeat()
        {
            LessonTimeline timeline = Build("1 2 3 4");

            double[] played = timeline.Notes.Select(n => n.PlayedBeat(timeline.Swing)).ToArray();

            Assert.That(played, Is.EqualTo(new[] { 0.0, 2.0 / 3.0, 1.0, 1.0 + 2.0 / 3.0 }).Within(1e-9));
        }

        [Test]
        public void AStraightEighth_IsPlayedAtHalfTheBeat_EvenInASwungLesson()
        {
            LessonTimeline timeline = Build("1 2 3 4", straightBars: 1);

            double[] played = timeline.Notes.Select(n => n.PlayedBeat(timeline.Swing)).ToArray();

            Assert.That(played, Is.EqualTo(new[] { 0.0, 0.5, 1.0, 1.5 }).Within(1e-9));
        }

        [Test]
        public void TheBarsAfterTheStraightOnes_AreSwung()
        {
            var step = new SwingStepSpec(
                "t", new string[0], new[] { "1 2", "1 2", "1 2", "1 2" }, 2, new[] { LearningMode.WatchAndListen });

            LessonTimeline timeline = SwingLessonBuilder.BuildTimeline(step, Sixty, TwoThirds);

            // Bars 1-2 straight: the "and" sits at +0.5 of its beat; bars 3-4 swung: at +2/3.
            double[] andOffsets = timeline.Notes.Where((n, i) => i % 2 == 1)
                .Select(n => n.PlayedBeat(timeline.Swing) % 1.0).ToArray();
            Assert.That(andOffsets, Is.EqualTo(new[] { 0.5, 0.5, 2.0 / 3.0, 2.0 / 3.0 }).Within(1e-9));
        }

        [Test]
        public void TheExpectedOnsetSeconds_AreTheSwungNoteTimes_SoCirclesArriveWhenTheNotesSound()
        {
            LessonTimeline timeline = Build("1 2 3 4");

            double[] expected = timeline.ExpectedOnsetSeconds();
            double[] fromNotes = timeline.Notes.Select(n => Sixty.BeatsToSeconds(n.PlayedBeat(timeline.Swing))).ToArray();

            Assert.That(expected, Is.EqualTo(fromNotes).Within(1e-9));
        }

        [Test]
        public void TheCircleSchedule_ArrivesOnEachNotesSwungBeat_AtItsFixedPitch_InTimeOrder()
        {
            LessonTimeline timeline = Build("1 2 3 4");

            NoteCueSchedule cues = NoteCueSchedule.FromTimeline(timeline);

            Assert.That(cues.Count, Is.EqualTo(4));
            double[] onsets = Enumerable.Range(0, cues.Count).Select(cues.OnsetBeat).ToArray();
            Assert.That(onsets, Is.EqualTo(timeline.Notes.Select(n => n.PlayedBeat(timeline.Swing)).ToArray()).Within(1e-9));
            Assert.That(Enumerable.Range(0, cues.Count).Select(cues.MidiPitch), Is.EqualTo(new[] { 60, 62, 64, 65 }));
            Assert.That(cues.FirstAtOrAfter(0.7), Is.EqualTo(2)); // the note at 2/3 has gone; the one on beat 2 is next
        }

        [Test]
        public void ALongLessonBeat_SchedulesNothingPast_ARestBar()
        {
            NoteCueSchedule cues = NoteCueSchedule.FromTimeline(Build("1 2 3 4"));

            Assert.That(cues.FirstAtOrAfter(2.0), Is.EqualTo(cues.Count)); // bars 2-4 rest: no more circles
        }

        // --- Pattern data -------------------------------------------------------------------

        [Test]
        public void TheDegrees_MapToTheCMajorScaleFromC4ToD5()
        {
            int[] midi = Enumerable.Range(1, 9).Select(SwingPatternText.MidiOfDegree).ToArray();

            Assert.That(midi, Is.EqualTo(new[] { 60, 62, 64, 65, 67, 69, 71, 72, 74 }));
        }

        [Test]
        public void ABarIsEightEighthSlots_AndARestIsADash()
        {
            List<SwingPatternText.Slot> slots = SwingPatternText.ParseBar("1 - 3 4 - - - 9");

            Assert.That(slots.Select(s => s.BeatInBar), Is.EqualTo(new[] { 0.0, 1.0, 1.5, 3.5 }));
            Assert.That(slots.Select(s => s.Degree), Is.EqualTo(new[] { 1, 3, 4, 9 }));
        }

        [TestCase("1 2 3 4 5 6 7 8 9")]
        [TestCase("1 x")]
        [TestCase("0")]
        [TestCase("10")]
        public void ABadBar_IsRejected(string text) =>
            Assert.Throws<System.FormatException>(() => SwingPatternText.ParseBar(text));

        [Test]
        public void TheLesson_GoesStraightIntoSwing_WithOneIonianStepOfFourBars()
        {
            IReadOnlyList<SwingStepSpec> steps = SwingLessonBuilder.DefaultSteps();

            Assert.That(steps, Has.Count.EqualTo(1));
            Assert.That(steps[0].Name, Is.EqualTo("Ionian"));
            Assert.That(steps[0].Bars, Has.Count.EqualTo(4));
            Assert.That(steps[0].StraightBars, Is.EqualTo(0), "no straight half: the lesson swings from the first note");
        }

        [Test]
        public void TheIonianStep_RunsCUpToCAndBackDown_EightNotesABar()
        {
            SwingStepSpec step = SwingLessonBuilder.DefaultSteps()[0];

            Assert.That(step.Bars, Is.EqualTo(new[]
            {
                "1 2 3 4 5 6 7 8", "8 7 6 5 4 3 2 1", "1 2 3 4 5 6 7 8", "8 7 6 5 4 3 2 1",
            }));
            Assert.That(step.Bars.Select(b => SwingPatternText.ParseBar(b).Count), Is.EqualTo(new[] { 8, 8, 8, 8 }));

            LessonTimeline timeline = SwingLessonBuilder.BuildTimeline(step, Sixty, TwoThirds);
            int[] midi = timeline.Notes.Take(8).Select(n => n.MidiPitch).ToArray();
            Assert.That(midi, Is.EqualTo(new[] { 60, 62, 64, 65, 67, 69, 71, 72 })); // C4 up to C5
        }

        [Test]
        public void TheStep_AlternatesWatchAndTry_Twice()
        {
            Assert.That(SwingLessonBuilder.DefaultSteps()[0].Modes, Is.EqualTo(new[]
            {
                LearningMode.WatchAndListen, LearningMode.TryYourself, LearningMode.WatchAndListen, LearningMode.TryYourself,
            }));
        }

        [Test]
        public void EveryLessonPitch_IsOnARegion_UnderTheCMajorScaleOverride()
        {
            RegionPitchSet regions = LessonPitchSet.CMajorScale(60, 9).Build(Chord.Cmaj7);

            foreach (SwingStepSpec step in SwingLessonBuilder.DefaultSteps())
            {
                LessonTimeline timeline = SwingLessonBuilder.BuildTimeline(step, Sixty, TwoThirds);
                foreach (TimelineNote note in timeline.Notes)
                {
                    Assert.That(regions.TryRegionOf(new Pitch(note.MidiPitch), out _), Is.True, step.Name + ": " + note.MidiPitch);
                }
            }
        }

        [Test]
        public void NoCardText_NamesANoteOrHasADigit()
        {
            var noteName = new System.Text.RegularExpressions.Regex(@"\b[A-G]\b");
            foreach (SwingStepSpec step in SwingLessonBuilder.DefaultSteps())
            {
                foreach (string card in step.Cards)
                {
                    // The one numbers allowed are the beat count the brief quotes ("1 and 2 and"): counting time, not naming notes.
                    string unquoted = System.Text.RegularExpressions.Regex.Replace(card, "\"[^\"]*\"", string.Empty);
                    Assert.That(unquoted.Any(char.IsDigit), Is.False, card);
                    Assert.That(noteName.IsMatch(card), Is.False, card);
                }
            }
        }

        // --- The loop -----------------------------------------------------------------------

        [Test]
        public void TheProgressionCycles_ii_V_I_I_OneChordPerBar_AndIsCompedOnBeatsOneAndThree()
        {
            LessonTimeline timeline = Build("1 2 3 4");

            Assert.That(
                timeline.Chords.Select(c => c.Beat.Position),
                Is.EqualTo(new[] { 0.0, 2.0, 4.0, 6.0, 8.0, 10.0, 12.0, 14.0 }));
            Assert.That(
                Enumerable.Range(0, 8).Select(SwingLessonBuilder.ChordOfBar),
                Is.EqualTo(new[]
                {
                    ChordFunction.Two, ChordFunction.Five, ChordFunction.One, ChordFunction.One,
                    ChordFunction.Two, ChordFunction.Five, ChordFunction.One, ChordFunction.One,
                }));
            foreach (TimelineChord chord in timeline.Chords)
            {
                int bar = (int)(chord.Beat.Position / SwingLessonBuilder.BeatsPerBar);
                Assert.That(chord.Function, Is.EqualTo(SwingLessonBuilder.ChordOfBar(bar)));
            }
        }

        [Test]
        public void EveryBlock_IsExactlyFourBars_SoWatchAndTryStayBarAlignedAcrossStepsAndPhases()
        {
            foreach (SwingStepSpec step in SwingLessonBuilder.DefaultSteps())
            {
                LessonTimeline timeline = SwingLessonBuilder.BuildTimeline(step, Sixty, TwoThirds);

                Assert.That(timeline.DurationBeats, Is.EqualTo(16.0), step.Name); // the rest bars of step 4 still count
            }
        }

        // --- The pitch-set override ---------------------------------------------------------

        [Test]
        public void WithTheScaleOverride_TheRegionsDoNotChangeOnAChordChange()
        {
            var pipeline = new SwingSlicesPipeline(new PalmSwingParameters(), Chord.Cmaj7);
            pipeline.ApplyOverride(LessonPitchSet.CMajorScale(60, 9));
            int[] before = Midis(pipeline.Pitches);

            pipeline.OnChordChanged(Chord.Dm7);
            pipeline.OnChordChanged(Chord.G7);
            int[] after = Midis(pipeline.Pitches);

            Assert.That(before, Is.EqualTo(new[] { 60, 62, 64, 65, 67, 69, 71, 72, 74 }));
            Assert.That(after, Is.EqualTo(before));
            Assert.That(pipeline.Layout.RegionCount, Is.EqualTo(9));
        }

        [Test]
        public void WithoutAnOverride_TheDefaultStillRetunesOnAChordChange()
        {
            var pipeline = new SwingSlicesPipeline(new PalmSwingParameters(), Chord.Cmaj7);
            int[] cmaj = Midis(pipeline.Pitches);

            pipeline.OnChordChanged(Chord.Dm7);

            Assert.That(Midis(pipeline.Pitches), Is.Not.EqualTo(cmaj));
            Assert.That(pipeline.Layout.RegionCount, Is.EqualTo(HeightRegionLayout.DefaultRegionCount));
        }

        [Test]
        public void ClearingTheOverride_ReturnsToTheDefault()
        {
            var pipeline = new SwingSlicesPipeline(new PalmSwingParameters(), Chord.Cmaj7);
            pipeline.ApplyOverride(LessonPitchSet.CMajorScale(60, 9));

            pipeline.ApplyOverride(null);

            Assert.That(pipeline.Override, Is.Null);
            Assert.That(pipeline.Layout.RegionCount, Is.EqualTo(HeightRegionLayout.DefaultRegionCount));
            Assert.That(pipeline.Pitches[0].Midi, Is.EqualTo(72));
        }

        [Test]
        public void AFixedList_MustAscend()
        {
            Assert.Throws<System.ArgumentException>(() => LessonPitchSet.Fixed(60, 62, 62));
            Assert.That(LessonPitchSet.Fixed(60, 64, 67).Build(Chord.Cmaj7)[2].Midi, Is.EqualTo(67));
        }
    }
}
