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
    }
}
