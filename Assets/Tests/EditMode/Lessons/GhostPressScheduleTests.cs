using System;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class GhostPressScheduleTests
    {
        private const double Approach = 0.25;
        private const double Return = 0.35;

        private static GhostPressSchedule Schedule(Func<LessonTimelineBuilder, LessonTimelineBuilder> notes)
        {
            LessonTimelineBuilder builder = new LessonTimelineBuilder().Chord(0.0, ChordFunction.Two);
            return GhostPressSchedule.FromTimeline(notes(builder).Build(), Approach, Return);
        }

        // --- which finger -----------------------------------------------------------------

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        [TestCase(3, 3)]
        [TestCase(4, 4)]
        public void EachLowerOctaveTarget_IsPressedByItsOwnFinger(int targetIndex, int expectedFinger)
        {
            GhostPressSchedule schedule = Schedule(b => b.Note(4.0, targetIndex));

            schedule.ProgressAt(4.0, out int finger);

            Assert.That(finger, Is.EqualTo(expectedFinger));
        }

        [TestCase(5, 0)]
        [TestCase(7, 2)]
        [TestCase(9, 4)]
        public void TheUpperOctave_SharesTheLowerOctavesFingers(int targetIndex, int expectedFinger)
        {
            Assert.That(GhostPressSchedule.FingerFor(targetIndex), Is.EqualTo(expectedFinger));
        }

        [TestCase(-1)]
        [TestCase(10)]
        public void ATargetOutsideTheSet_IsRejected(int targetIndex)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GhostPressSchedule.FingerFor(targetIndex));
        }

        // --- shape ------------------------------------------------------------------------

        [Test]
        public void TheFingerIsDown_ExactlyOnTheNotesOnset()
        {
            GhostPressSchedule schedule = Schedule(b => b.Note(4.0, 2));

            Assert.That(schedule.ProgressAt(4.0, out int finger), Is.EqualTo(1.0));
            Assert.That(finger, Is.EqualTo(2));
        }

        [Test]
        public void TheFingerIsUp_WellBeforeAndWellAfterANote()
        {
            GhostPressSchedule schedule = Schedule(b => b.Note(4.0, 2));

            Assert.That(schedule.ProgressAt(0.0, out int before), Is.EqualTo(0.0));
            Assert.That(before, Is.EqualTo(GhostPressSchedule.NoFinger));
            Assert.That(schedule.ProgressAt(4.0 + Return + 0.01, out int after), Is.EqualTo(0.0));
            Assert.That(after, Is.EqualTo(GhostPressSchedule.NoFinger));
        }

        [Test]
        public void TheFingerStartsMovingBeforeTheNote_SoItLandsOnTheSound()
        {
            GhostPressSchedule schedule = Schedule(b => b.Note(4.0, 2));

            double midApproach = schedule.ProgressAt(4.0 - Approach / 2, out int finger);

            Assert.That(midApproach, Is.GreaterThan(0.0).And.LessThan(1.0));
            Assert.That(finger, Is.EqualTo(2));
        }

        [Test]
        public void ThePressLifts_AfterTheNote()
        {
            GhostPressSchedule schedule = Schedule(b => b.Note(4.0, 2));

            double lifting = schedule.ProgressAt(4.0 + Return / 2, out _);

            Assert.That(lifting, Is.GreaterThan(0.0).And.LessThan(1.0));
        }

        // --- timing -----------------------------------------------------------------------

        [Test]
        public void ThePressLandsOnTheSwungOnset_NotTheStraightOne()
        {
            // An off-beat eighth under swing is late, and the sound is late with it.
            SwingRatio swing = SwingRatio.Default;
            LessonTimeline timeline = new LessonTimelineBuilder()
                .WithSwing(swing)
                .Chord(0.0, ChordFunction.Two)
                .Note(0.5, 1)
                .Build();
            var schedule = GhostPressSchedule.FromTimeline(timeline, Approach, Return);

            double swungOnset = SwingQuantizer.Swing(0.5, swing);

            Assert.That(swungOnset, Is.GreaterThan(0.5));
            Assert.That(schedule.ProgressAt(swungOnset, out _), Is.EqualTo(1.0));
            Assert.That(schedule.ProgressAt(0.5, out _), Is.LessThan(1.0));
        }

        [Test]
        public void TwoFingersPressedTogether_ReportTheDeeperOne()
        {
            GhostPressSchedule schedule = Schedule(b => b.Note(4.0, 0).Note(4.1, 4));

            // At the second note's onset it is fully down; the first is already lifting.
            schedule.ProgressAt(4.1, out int finger);

            Assert.That(finger, Is.EqualTo(4));
        }

        [Test]
        public void TheSameFingerPlayedTwice_PressesTwice()
        {
            GhostPressSchedule schedule = Schedule(b => b.Note(2.0, 1).Note(3.0, 1));

            Assert.That(schedule.ProgressAt(2.0, out _), Is.EqualTo(1.0));
            Assert.That(schedule.ProgressAt(2.6, out _), Is.EqualTo(0.0));
            Assert.That(schedule.ProgressAt(3.0, out _), Is.EqualTo(1.0));
        }

        // --- the lead-in ------------------------------------------------------------------

        [Test]
        public void DuringTheLeadIn_NothingIsPressed_EvenForANoteOnTheFirstBeat()
        {
            // The runner's phrase position is negative until the lead-in (ADR-0056) has passed. A note on
            // beat 0 must not have its finger down for all of it.
            GhostPressSchedule schedule = Schedule(b => b.Note(0.0, 2));

            Assert.That(schedule.ProgressAt(-4.0, out int early), Is.EqualTo(0.0));
            Assert.That(early, Is.EqualTo(GhostPressSchedule.NoFinger));
            Assert.That(schedule.ProgressAt(-Approach - 0.01, out _), Is.EqualTo(0.0));
        }

        [Test]
        public void TheFirstNotesFingerStartsDown_JustBeforeTheLeadInEnds()
        {
            GhostPressSchedule schedule = Schedule(b => b.Note(0.0, 2));

            Assert.That(schedule.ProgressAt(-Approach / 2, out int finger), Is.GreaterThan(0.0));
            Assert.That(finger, Is.EqualTo(2));
            Assert.That(schedule.ProgressAt(0.0, out _), Is.EqualTo(1.0));
        }

        [Test]
        public void DuringTheLeadIn_AStrikeOnTheFirstBeatHasNotBegun()
        {
            var strikes = new GhostStrikeSchedule(new[] { 0.0 }, approachBeats: 0.5, returnBeats: 0.5);

            Assert.That(strikes.ProgressAt(-4.0), Is.EqualTo(0.0));
            Assert.That(strikes.ContactsUpTo(-0.01), Is.EqualTo(0));
            Assert.That(strikes.ProgressAt(0.0), Is.EqualTo(1.0));
        }

        // --- bookkeeping ------------------------------------------------------------------

        [Test]
        public void EndBeat_IsWhenTheLastPressHasFinishedLifting()
        {
            GhostPressSchedule schedule = Schedule(b => b.Note(1.0, 0).Note(6.0, 3));

            Assert.That(schedule.EndBeat, Is.EqualTo(6.0 + Return).Within(1e-9));
        }

        [Test]
        public void None_NeverPresses()
        {
            Assert.That(GhostPressSchedule.None.EndBeat, Is.EqualTo(0.0));
            Assert.That(GhostPressSchedule.None.ProgressAt(3.0, out int finger), Is.EqualTo(0.0));
            Assert.That(finger, Is.EqualTo(GhostPressSchedule.NoFinger));
        }

        [Test]
        public void ALessonWithoutNotes_NeverPresses()
        {
            GhostPressSchedule schedule = Schedule(b => b);

            Assert.That(schedule.ProgressAt(2.0, out int finger), Is.EqualTo(0.0));
            Assert.That(finger, Is.EqualTo(GhostPressSchedule.NoFinger));
        }

        // --- the frame --------------------------------------------------------------------

        [Test]
        public void AFrame_CarriesThePressAndClampsIt()
        {
            var frame = new GhostFrame(1.0, ChordFunction.Two, 0.0, -1, pressFinger: 3, pressProgress: 1.7);

            Assert.That(frame.PressFinger, Is.EqualTo(3));
            Assert.That(frame.PressProgress, Is.EqualTo(1.0));
        }

        [Test]
        public void AFrame_WithNoFinger_HasNoProgress()
        {
            var frame = new GhostFrame(1.0, ChordFunction.Two, 0.0, -1, pressFinger: -1, pressProgress: 0.8);

            Assert.That(frame.PressFinger, Is.EqualTo(GhostPressSchedule.NoFinger));
            Assert.That(frame.PressProgress, Is.EqualTo(0.0));
        }

        [Test]
        public void AFrame_CarriesTheChordTheNotesBelongTo()
        {
            // Where the system trails the ghost, the pose is the next chord's while a note of the last is
            // still being shown (ADR-0058): the two are carried separately.
            var frame = new GhostFrame(
                6.0, ChordFunction.Five, 4.0, 2, pressFinger: 1, pressProgress: 0.5, melodyPose: ChordFunction.Two);

            Assert.That(frame.DemonstratedPose, Is.EqualTo(ChordFunction.Five));
            Assert.That(frame.MelodyPose, Is.EqualTo(ChordFunction.Two));
        }

        [Test]
        public void AFrame_WithNoMelodyChord_HasNone()
        {
            Assert.That(new GhostFrame(1.0, ChordFunction.Two, 0.0, -1).MelodyPose, Is.Null);
        }

        [Test]
        public void AFrame_DefaultsToNoPress()
        {
            var frame = new GhostFrame(1.0, ChordFunction.Two, 0.0, -1);

            Assert.That(frame.PressFinger, Is.EqualTo(GhostPressSchedule.NoFinger));
            Assert.That(frame.PressProgress, Is.EqualTo(0.0));
        }
    }
}
