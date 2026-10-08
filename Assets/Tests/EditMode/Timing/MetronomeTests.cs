using System.Collections.Generic;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Timing
{
    public class MetronomeTests
    {
        private static List<MetronomeClick> DrainUpTo(Metronome metronome, double horizon)
        {
            var clicks = new List<MetronomeClick>();
            while (metronome.TryDequeueClick(horizon, out MetronomeClick click))
            {
                clicks.Add(click);
            }

            return clicks;
        }

        [Test]
        public void EmitsNothing_BeforeStarted()
        {
            var metronome = new Metronome(Tempo.Bpm(120));

            Assert.That(metronome.Running, Is.False);
            Assert.That(metronome.TryDequeueClick(1000.0, out _), Is.False);
        }

        [Test]
        public void SchedulesBeatsOnTheTempoGrid_FromTheStartTime()
        {
            var metronome = new Metronome(Tempo.Bpm(120)); // 0.5 s per beat
            metronome.Start(atDspTime: 10.0);

            List<MetronomeClick> clicks = DrainUpTo(metronome, horizon: 11.0);

            Assert.That(clicks.Count, Is.EqualTo(3)); // 10.0, 10.5, 11.0
            Assert.That(clicks[0].DspTime, Is.EqualTo(10.0).Within(1e-12));
            Assert.That(clicks[1].DspTime, Is.EqualTo(10.5).Within(1e-12));
            Assert.That(clicks[2].DspTime, Is.EqualTo(11.0).Within(1e-12));
        }

        [Test]
        public void LookAhead_OnlyReturnsClicksWithinTheHorizon()
        {
            var metronome = new Metronome(Tempo.Bpm(60)); // 1 s per beat
            metronome.Start(0.0);

            Assert.That(DrainUpTo(metronome, 2.5).Count, Is.EqualTo(3)); // 0, 1, 2
            Assert.That(metronome.NextBeatIndex, Is.EqualTo(3));

            Assert.That(DrainUpTo(metronome, 2.9).Count, Is.EqualTo(0));
            Assert.That(DrainUpTo(metronome, 3.0).Count, Is.EqualTo(1));
        }

        [Test]
        public void MarksDownbeats_PerBarLength()
        {
            var metronome = new Metronome(Tempo.Bpm(120), beatsPerBar: 3);
            metronome.Start(0.0);

            List<MetronomeClick> clicks = DrainUpTo(metronome, 3.5); // beats 0..7

            Assert.That(clicks[0].IsDownbeat, Is.True);
            Assert.That(clicks[0].BeatInBar, Is.EqualTo(0));
            Assert.That(clicks[1].BeatInBar, Is.EqualTo(1));
            Assert.That(clicks[2].BeatInBar, Is.EqualTo(2));
            Assert.That(clicks[3].IsDownbeat, Is.True);
            Assert.That(clicks[6].IsDownbeat, Is.True);
        }

        [Test]
        public void Stop_HaltsEmission_AndRestartResetsTheGrid()
        {
            var metronome = new Metronome(Tempo.Bpm(120));
            metronome.Start(0.0);
            DrainUpTo(metronome, 1.0);

            metronome.Stop();
            Assert.That(metronome.TryDequeueClick(1000.0, out _), Is.False);

            metronome.Start(atDspTime: 50.0);
            Assert.That(metronome.NextBeatIndex, Is.EqualTo(0));

            List<MetronomeClick> clicks = DrainUpTo(metronome, 50.5);
            Assert.That(clicks[0].DspTime, Is.EqualTo(50.0).Within(1e-12));
            Assert.That(clicks[1].DspTime, Is.EqualTo(50.5).Within(1e-12));
        }

        [Test]
        public void DspTimeOf_MatchesDequeuedClicks()
        {
            var metronome = new Metronome(Tempo.Default);
            metronome.Start(atDspTime: 3.0);

            Assert.That(metronome.DspTimeOf(0), Is.EqualTo(3.0).Within(1e-12));
            Assert.That(
                metronome.DspTimeOf(4),
                Is.EqualTo(3.0 + 4 * Tempo.Default.SecondsPerBeat).Within(1e-12));
        }

        [Test]
        public void WithoutASubdivision_ClicksAreDownbeatThenBeats_AndNeverOffBeats()
        {
            var metronome = new Metronome(Tempo.Bpm(120));
            metronome.Start(0.0);

            List<MetronomeClick> clicks = DrainUpTo(metronome, 3.9); // beats 0..7

            Assert.That(clicks.Count, Is.EqualTo(8));
            for (int i = 0; i < clicks.Count; i++)
            {
                MetronomeClickKind expected = i % 4 == 0 ? MetronomeClickKind.Downbeat : MetronomeClickKind.Beat;
                Assert.That(clicks[i].Kind, Is.EqualTo(expected), $"click {i}");
                Assert.That(clicks[i].IsOffBeat, Is.False);
                Assert.That(clicks[i].BeatPosition, Is.EqualTo(i));
            }
        }

        [Test]
        public void StraightEighths_PutAnOffBeatClickHalfwayBetweenBeats()
        {
            var metronome = new Metronome(Tempo.Bpm(120), subdivision: MetronomeSubdivision.Eighths); // 0.5 s / beat
            metronome.Start(10.0);

            List<MetronomeClick> clicks = DrainUpTo(metronome, 11.0);

            // beat 0 @10.0, and @10.25, beat 1 @10.5, and @10.75, beat 2 @11.0
            Assert.That(clicks.Count, Is.EqualTo(5));
            Assert.That(clicks[1].Kind, Is.EqualTo(MetronomeClickKind.OffBeat));
            Assert.That(clicks[1].DspTime, Is.EqualTo(10.25).Within(1e-12));
            Assert.That(clicks[1].BeatPosition, Is.EqualTo(0.5).Within(1e-12));
            Assert.That(clicks[1].BeatIndex, Is.EqualTo(0), "an off-beat belongs to the beat it follows");
            Assert.That(clicks[3].DspTime, Is.EqualTo(10.75).Within(1e-12));
        }

        [Test]
        public void SwungEighths_PlaceTheOffBeatAtTheSwingRatio()
        {
            var ratio = new SwingRatio(0.66);
            var metronome = new Metronome(
                Tempo.Bpm(60), subdivision: MetronomeSubdivision.SwungEighths, swing: ratio); // 1 s / beat
            metronome.Start(0.0);

            List<MetronomeClick> clicks = DrainUpTo(metronome, 2.0);

            Assert.That(clicks[1].Kind, Is.EqualTo(MetronomeClickKind.OffBeat));
            Assert.That(clicks[1].DspTime, Is.EqualTo(0.66).Within(1e-12));
            Assert.That(clicks[3].DspTime, Is.EqualTo(1.66).Within(1e-12));
            Assert.That(
                clicks[1].BeatPosition,
                Is.EqualTo(SwingQuantizer.Swing(0.5, ratio)).Within(1e-12),
                "same warp the melody uses, so the click agrees with a swung note");
        }

        [Test]
        public void SwungEighths_DefaultToTheStandardSwingRatio()
        {
            var metronome = new Metronome(Tempo.Bpm(60), subdivision: MetronomeSubdivision.SwungEighths);

            Assert.That(metronome.Swing, Is.EqualTo(SwingRatio.Default));
        }

        [Test]
        public void StraightEighths_IgnoreAnySwingPassedIn()
        {
            var metronome = new Metronome(
                Tempo.Bpm(60), subdivision: MetronomeSubdivision.Eighths, swing: new SwingRatio(0.75));
            metronome.Start(0.0);

            List<MetronomeClick> clicks = DrainUpTo(metronome, 1.0);

            Assert.That(metronome.Swing, Is.EqualTo(SwingRatio.Straight));
            Assert.That(clicks[1].DspTime, Is.EqualTo(0.5).Within(1e-12));
        }

        [Test]
        public void TheQuarterPulse_IsIdenticalWithAndWithoutASubdivision()
        {
            foreach (MetronomeSubdivision subdivision in new[]
                     {
                         MetronomeSubdivision.Eighths, MetronomeSubdivision.SwungEighths,
                     })
            {
                var plain = new Metronome(Tempo.Bpm(93), beatsPerBar: 3);
                var subdivided = new Metronome(Tempo.Bpm(93), beatsPerBar: 3, subdivision: subdivision);
                plain.Start(7.0);
                subdivided.Start(7.0);

                List<MetronomeClick> expected = DrainUpTo(plain, 20.0);
                List<MetronomeClick> onBeats = DrainUpTo(subdivided, 20.0)
                    .FindAll(c => !c.IsOffBeat);

                Assert.That(onBeats.Count, Is.EqualTo(expected.Count), subdivision.ToString());
                for (int i = 0; i < expected.Count; i++)
                {
                    Assert.That(onBeats[i].DspTime, Is.EqualTo(expected[i].DspTime).Within(1e-12));
                    Assert.That(onBeats[i].Kind, Is.EqualTo(expected[i].Kind));
                }
            }
        }

        [Test]
        public void OffBeats_NeverCarryTheDownbeatAccent()
        {
            var metronome = new Metronome(Tempo.Bpm(120), beatsPerBar: 2, subdivision: MetronomeSubdivision.Eighths);
            metronome.Start(0.0);

            foreach (MetronomeClick click in DrainUpTo(metronome, 4.0))
            {
                if (click.IsOffBeat)
                {
                    Assert.That(click.IsDownbeat, Is.False);
                }
            }
        }

        [Test]
        public void WithASubdivision_ClicksStayInTimeOrder_AndRespectTheLookAheadHorizon()
        {
            var metronome = new Metronome(
                Tempo.Bpm(80), subdivision: MetronomeSubdivision.SwungEighths, swing: new SwingRatio(0.7));
            metronome.Start(2.0);

            double previous = double.NegativeInfinity;
            double horizon = 2.0;
            for (int step = 0; step < 40; step++)
            {
                horizon += 0.1;
                while (metronome.TryDequeueClick(horizon, out MetronomeClick click))
                {
                    Assert.That(click.DspTime, Is.GreaterThan(previous));
                    Assert.That(click.DspTime, Is.LessThanOrEqualTo(horizon));
                    previous = click.DspTime;
                }
            }

            Assert.That(previous, Is.GreaterThan(5.5), "clicks kept coming across 4 s of horizon steps");
        }

        [Test]
        public void NextBeatIndex_CountsWholeBeats_EvenWithASubdivision()
        {
            var metronome = new Metronome(Tempo.Bpm(60), subdivision: MetronomeSubdivision.Eighths);
            metronome.Start(0.0);

            DrainUpTo(metronome, 0.5); // beat 0 and its "and"
            Assert.That(metronome.NextBeatIndex, Is.EqualTo(1));

            DrainUpTo(metronome, 1.0); // beat 1
            Assert.That(metronome.NextBeatIndex, Is.EqualTo(1), "beat 1's own 'and' is still pending");
        }

        [Test]
        public void Restart_ResetsTheSubdividedGrid()
        {
            var metronome = new Metronome(Tempo.Bpm(120), subdivision: MetronomeSubdivision.Eighths);
            metronome.Start(0.0);
            DrainUpTo(metronome, 1.0);

            metronome.Start(50.0);
            List<MetronomeClick> clicks = DrainUpTo(metronome, 50.3);

            Assert.That(clicks.Count, Is.EqualTo(2));
            Assert.That(clicks[0].Kind, Is.EqualTo(MetronomeClickKind.Downbeat));
            Assert.That(clicks[0].DspTime, Is.EqualTo(50.0).Within(1e-12));
            Assert.That(clicks[1].Kind, Is.EqualTo(MetronomeClickKind.OffBeat));
        }

        [Test]
        public void ExposesItsGrid_SoARunCanBeLoggedAndReconstructed()
        {
            var metronome = new Metronome(
                Tempo.Bpm(72), beatsPerBar: 3, subdivision: MetronomeSubdivision.SwungEighths,
                swing: new SwingRatio(0.6));
            metronome.Start(12.5);

            Assert.That(metronome.StartDspTime, Is.EqualTo(12.5));
            Assert.That(metronome.Tempo, Is.EqualTo(Tempo.Bpm(72)));
            Assert.That(metronome.BeatsPerBar, Is.EqualTo(3));
            Assert.That(metronome.Subdivision, Is.EqualTo(MetronomeSubdivision.SwungEighths));
            Assert.That(metronome.Swing, Is.EqualTo(new SwingRatio(0.6)));
            Assert.That(
                metronome.DspTimeOfPosition(2.6),
                Is.EqualTo(12.5 + Tempo.Bpm(72).BeatsToSeconds(2.6)).Within(1e-12));
        }

        [Test]
        public void RejectsAnUnknownSubdivision()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new Metronome(Tempo.Default, subdivision: (MetronomeSubdivision)99));
        }
    }
}
