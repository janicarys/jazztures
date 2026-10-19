using System;
using Jazztures.Core.Harmony;
using System.Collections.Generic;
using Jazztures.Core.Lessons;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class GhostStrikeScheduleTests
    {
        private static GhostStrikeSchedule Schedule(params double[] beats) =>
            new GhostStrikeSchedule(beats, approachBeats: 0.5, returnBeats: 0.5);

        // --- shape ----------------------------------------------------------------------

        [Test]
        public void TheHandIsAtRest_WellBeforeAndWellAfterAStrike()
        {
            GhostStrikeSchedule schedule = Schedule(4.0);

            Assert.That(schedule.ProgressAt(0.0), Is.EqualTo(0.0));
            Assert.That(schedule.ProgressAt(3.49), Is.EqualTo(0.0));
            Assert.That(schedule.ProgressAt(4.5), Is.EqualTo(0.0));
            Assert.That(schedule.ProgressAt(20.0), Is.EqualTo(0.0));
        }

        [Test]
        public void TheHandReachesThePad_ExactlyOnTheStrikeBeat()
        {
            Assert.That(Schedule(4.0).ProgressAt(4.0), Is.EqualTo(1.0));
        }

        [Test]
        public void TheApproachAccelerates_SoItArrivesFast()
        {
            GhostStrikeSchedule schedule = Schedule(4.0);

            double halfway = schedule.ProgressAt(3.75); // half the approach
            double nearlyThere = schedule.ProgressAt(3.9);

            Assert.That(halfway, Is.EqualTo(0.25).Within(1e-9), "t squared: a quarter of the way at half the time");
            Assert.That(nearlyThere, Is.GreaterThan(halfway));
            Assert.That(nearlyThere, Is.LessThan(1.0));
        }

        [Test]
        public void AfterContactTheHandLiftsAway_Smoothly()
        {
            GhostStrikeSchedule schedule = Schedule(4.0);

            double a = schedule.ProgressAt(4.1);
            double b = schedule.ProgressAt(4.25);
            double c = schedule.ProgressAt(4.4);

            Assert.That(a, Is.LessThan(1.0).And.GreaterThan(b));
            Assert.That(b, Is.EqualTo(0.5).Within(1e-9), "the ease is symmetric about its midpoint");
            Assert.That(c, Is.LessThan(b).And.GreaterThan(0.0));
        }

        [Test]
        public void Progress_NeverLeavesZeroToOne()
        {
            GhostStrikeSchedule schedule = Schedule(0.0, 1.0, 2.0, 3.0);

            for (double beat = -2.0; beat < 6.0; beat += 0.013)
            {
                double p = schedule.ProgressAt(beat);
                Assert.That(p, Is.InRange(0.0, 1.0), $"at beat {beat}");
            }
        }

        [Test]
        public void ToleratesStrikesCloserTogetherThanTheirOwnMotion()
        {
            // One strike per beat with a 0.5 + 0.5 beat motion: the hand is never fully at rest between them.
            GhostStrikeSchedule schedule = Schedule(0.0, 1.0, 2.0);

            Assert.That(schedule.ProgressAt(0.5), Is.EqualTo(0.0).Within(1e-9));
            Assert.That(schedule.ProgressAt(1.0), Is.EqualTo(1.0));
            Assert.That(schedule.ProgressAt(1.25), Is.EqualTo(0.5).Within(1e-9));
        }

        [Test]
        public void WhenTwoStrikesOverlap_TheNearerOneWins()
        {
            GhostStrikeSchedule schedule = new GhostStrikeSchedule(new[] { 4.0, 4.3 }, 0.5, 0.5);

            Assert.That(schedule.ProgressAt(4.3), Is.EqualTo(1.0));

            foreach (double beat in new[] { 3.6, 4.0, 4.15, 4.3, 4.6, 4.75 })
            {
                double expected = Math.Max(Schedule(4.0).ProgressAt(beat), Schedule(4.3).ProgressAt(beat));
                Assert.That(schedule.ProgressAt(beat), Is.EqualTo(expected).Within(1e-12), $"at beat {beat}");
            }
        }

        // --- contacts ---------------------------------------------------------------------

        [Test]
        public void ContactsCountTheStrikesThatHaveLanded()
        {
            GhostStrikeSchedule schedule = Schedule(2.0, 6.0, 10.0);

            Assert.That(schedule.ContactsUpTo(1.99), Is.EqualTo(0));
            Assert.That(schedule.ContactsUpTo(2.0), Is.EqualTo(1));
            Assert.That(schedule.ContactsUpTo(9.0), Is.EqualTo(2));
            Assert.That(schedule.ContactsUpTo(100.0), Is.EqualTo(3));
        }

        [Test]
        public void Strikes_AreSortedWhateverOrderTheyWereGivenIn()
        {
            GhostStrikeSchedule schedule = Schedule(10.0, 2.0, 6.0);

            Assert.That(schedule.Beats, Is.EqualTo(new[] { 2.0, 6.0, 10.0 }));
        }

        [Test]
        public void EndBeat_IsWhenTheLastStrikeHasLiftedAway()
        {
            Assert.That(Schedule(2.0, 6.0).EndBeat, Is.EqualTo(6.5));
            Assert.That(GhostStrikeSchedule.None.EndBeat, Is.EqualTo(0.0));
        }

        [Test]
        public void NoStrikes_MeansNoMotionAndNoContacts()
        {
            Assert.That(GhostStrikeSchedule.None.Count, Is.EqualTo(0));
            Assert.That(GhostStrikeSchedule.None.ProgressAt(3.0), Is.EqualTo(0.0));
            Assert.That(GhostStrikeSchedule.None.ContactsUpTo(100.0), Is.EqualTo(0));
        }

        // --- validation -------------------------------------------------------------------

        [Test]
        public void ANonPositiveMotionTime_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GhostStrikeSchedule(new[] { 1.0 }, 0.0, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GhostStrikeSchedule(new[] { 1.0 }, 0.5, -1.0));
        }

        [Test]
        public void ANonFiniteStrikeBeat_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GhostStrikeSchedule(new[] { double.NaN }, 0.5, 0.5));
        }

        // --- where the strikes come from --------------------------------------------------

        [Test]
        public void WithoutAScoredPulse_EachChordIsStruckOnceItsPoseHasFormed()
        {
            LessonTimeline timeline = new LessonTimelineBuilder()
                .Chord(0.0, ChordFunction.Two)
                .Chord(4.0, ChordFunction.Five)
                .Chord(8.0, ChordFunction.One)
                .Build();

            Assert.That(timeline.StrikeBeats(afterPoseBeats: 2.0), Is.EqualTo(new[] { 2.0, 6.0, 10.0 }));
        }

        [Test]
        public void WithAScoredPulse_TheStrikesAreThePulse()
        {
            LessonTimeline timeline = new LessonTimelineBuilder()
                .Chord(0.0, ChordFunction.Two)
                .Chord(4.0, ChordFunction.Two)
                .WithScoredPulse(OnsetPattern.EveryBeat)
                .Build();

            Assert.That(timeline.StrikeBeats(afterPoseBeats: 2.0), Is.EqualTo(new[] { 0.0, 1.0, 2.0, 3.0 }),
                "the pulse, not the chord changes, is what the learner is asked to strike on");
        }

        [Test]
        public void WatchAndListen_StrikesTheChords_EvenWhenALessonScoresAPulse()
        {
            LessonTimeline timeline = new LessonTimelineBuilder()
                .Chord(0.0, ChordFunction.Two)
                .Chord(4.0, ChordFunction.Two)
                .WithScoredPulse(OnsetPattern.EveryBeat)
                .Build();

            Assert.That(timeline.StrikeBeats(afterPoseBeats: 2.0, useScoredPulse: false), Is.EqualTo(new[] { 2.0, 6.0 }),
                "the system sounds the two chords, not the metronome-rate pulse");
        }

        [Test]
        public void WatchAndListen_TheChordSoundsExactlyWhenTheGhostsHandLands()
        {
            // The runner starts the system's playback `lead` beats late and strikes each chord `lead` beats
            // after its pose appears. If those two ever drift apart the ghost hits the pad out of time with
            // the sound it is demonstrating - the whole point of the mode.
            const double lead = 2.0;
            Tempo tempo = Tempo.Bpm(80);
            LessonTimeline timeline = new LessonTimelineBuilder()
                .WithTempo(tempo)
                .Chord(0.0, ChordFunction.Two)
                .Chord(4.0, ChordFunction.Five)
                .Chord(8.0, ChordFunction.One)
                .Build();

            double[] strikes = timeline.StrikeBeats(lead, useScoredPulse: false);

            var clock = new VirtualClock();
            var sink = new RecordingNoteSink();
            var playback = new TimelinePlayback(timeline, clock, sink);
            playback.Start(tempo.BeatsToSeconds(lead));

            var firstSoundAt = new List<double>();
            double lastChordOn = double.NegativeInfinity;
            const double step = 0.005;
            for (int i = 0; i < 4000 && !playback.HasEnded; i++)
            {
                clock.Advance(step);
                int before = sink.Events.Count;
                playback.Tick();
                for (int e = before; e < sink.Events.Count; e++)
                {
                    NoteEvent note = sink.Events[e];
                    if (note.Kind == NoteEventKind.On && note.Channel == MidiChannel.Accompaniment
                        && note.DspTime > lastChordOn + 0.5)
                    {
                        firstSoundAt.Add(note.DspTime);
                        lastChordOn = note.DspTime;
                    }
                }
            }

            Assert.That(firstSoundAt, Has.Count.EqualTo(strikes.Length));
            for (int k = 0; k < strikes.Length; k++)
            {
                Assert.That(firstSoundAt[k], Is.EqualTo(tempo.BeatsToSeconds(strikes[k])).Within(1e-9),
                    $"chord {k} sounds at the same instant the ghost's hand lands");
            }
        }

        [Test]
        public void ALessonWithNoChords_HasNoStrikes()
        {
            LessonTimeline timeline = new LessonTimelineBuilder().Marker(0.0, "start").Build();

            Assert.That(timeline.StrikeBeats(2.0), Is.Empty);
        }

        [Test]
        public void StrikeBeats_FollowTheSwing()
        {
            SwingRatio swing = new SwingRatio(0.66);
            LessonTimeline timeline = new LessonTimelineBuilder()
                .WithSwing(swing)
                .Chord(0.0, ChordFunction.Two)
                .WithScoredPulse(OnsetPattern.EveryEighth)
                .WithMinimumDuration(2.0)
                .Build();

            double[] beats = timeline.StrikeBeats(2.0);

            Assert.That(beats[0], Is.EqualTo(0.0));
            Assert.That(beats[1], Is.EqualTo(SwingQuantizer.Swing(0.5, swing)).Within(1e-9));
            Assert.That(beats[1], Is.Not.EqualTo(0.5), "the off-beat eighth has moved");
        }

        [Test]
        public void AGhostFrame_CarriesTheStrikeAndClampsIt()
        {
            var frame = new GhostFrame(1.0, ChordFunction.Two, 0.0, -1, strikeProgress: 1.7, strikeCount: 3);

            Assert.That(frame.StrikeProgress, Is.EqualTo(1.0));
            Assert.That(frame.StrikeCount, Is.EqualTo(3));
            Assert.That(GhostFrame.Hidden.StrikeProgress, Is.EqualTo(0.0));
        }
    }
}
