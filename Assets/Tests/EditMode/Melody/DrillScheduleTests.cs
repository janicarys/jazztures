using Jazztures.Core.Melody;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class DrillScheduleTests
    {
        [Test]
        public void StraightQuarters_AreOnePerBeat_FromTheFirstBeatTime()
        {
            var schedule = new DrillSchedule(
                Tempo.Bpm(60), DrillRhythm.StraightQuarters, SwingRatio.Default, SwingDrill.UpUp, 10.0, tasks: 2);

            Assert.That(schedule.Count, Is.EqualTo(8));
            Assert.That(schedule[0].Time, Is.EqualTo(10.0).Within(1e-9));
            Assert.That(schedule[3].Time, Is.EqualTo(13.0).Within(1e-9));
            Assert.That(schedule.EndTime, Is.EqualTo(17.0).Within(1e-9));
        }

        [Test]
        public void SwungEighths_PutEveryOtherTargetOnTheSwungAnd()
        {
            var schedule = new DrillSchedule(
                Tempo.Bpm(60), DrillRhythm.SwungEighths, new SwingRatio(0.66), SwingDrill.UpUp, 10.0, tasks: 1);

            Assert.That(schedule[0].Time, Is.EqualTo(10.0).Within(1e-9));
            Assert.That(schedule[1].Time, Is.EqualTo(10.66).Within(1e-9));
            Assert.That(schedule[2].Time, Is.EqualTo(11.0).Within(1e-9));
            Assert.That(schedule[3].Time, Is.EqualTo(11.66).Within(1e-9));
        }

        [Test]
        public void TheTempoScalesTheTimes()
        {
            var schedule = new DrillSchedule(
                Tempo.Bpm(120), DrillRhythm.StraightQuarters, SwingRatio.Default, SwingDrill.UpUp, 0.0, tasks: 1);

            Assert.That(schedule[2].Time, Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void TasksRestartEveryNotesPerTask_AndThePatternRepeats()
        {
            var schedule = new DrillSchedule(
                Tempo.Bpm(60), DrillRhythm.StraightQuarters, SwingRatio.Default, SwingDrill.UpDown, 0.0, tasks: 2);

            Assert.That(schedule[0].StartsTask, Is.True);
            Assert.That(schedule[1].StartsTask, Is.False);
            Assert.That(schedule[4].StartsTask, Is.True);
            Assert.That(schedule[3].Task, Is.EqualTo(0));
            Assert.That(schedule[4].Task, Is.EqualTo(1));
            Assert.That(new[] { schedule[0].Direction, schedule[1].Direction, schedule[2].Direction, schedule[3].Direction },
                Is.EqualTo(new[] { StepDirection.Up, StepDirection.Down, StepDirection.Up, StepDirection.Down }));
        }

        [Test]
        public void EachDrill_StartsWhereItHasRoom()
        {
            Assert.That(SwingDrill.UpUp.StartMidi, Is.LessThan(SwingDrill.UpDown.StartMidi)); // up-up starts low
            Assert.That(SwingDrill.DownDown.StartMidi, Is.GreaterThan(SwingDrill.UpDown.StartMidi)); // down-down starts high
            Assert.That(SwingDrill.All.Length, Is.EqualTo(4));
        }

        [Test]
        public void FingerBendPresses_AreScoredByTheirPitchMovement()
        {
            Assert.That(StepDirections.FromPitchMovement(-1, 74), Is.EqualTo(StepDirection.Same));
            Assert.That(StepDirections.FromPitchMovement(74, 74), Is.EqualTo(StepDirection.Same));
            Assert.That(StepDirections.FromPitchMovement(74, 77), Is.EqualTo(StepDirection.Up));
            Assert.That(StepDirections.FromPitchMovement(77, 74), Is.EqualTo(StepDirection.Down));
        }
    }
}
