using Jazztures.Core.Diagnostics;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Diagnostics
{
    public class LatencyRecorderTests
    {
        [Test]
        public void EmptyStage_SummarisesAsEmpty()
        {
            LatencySummary summary = new LatencyRecorder().Summarize(LatencyStage.EndToEnd);

            Assert.That(summary.IsEmpty, Is.True);
            Assert.That(summary.Count, Is.Zero);
        }

        [Test]
        public void Percentiles_OverAKnownDistribution()
        {
            var recorder = new LatencyRecorder();
            for (int i = 1; i <= 100; i++)
            {
                recorder.Record(LatencyStage.PoseToConfirm, i);
            }

            LatencySummary s = recorder.Summarize(LatencyStage.PoseToConfirm);

            Assert.That(s.Count, Is.EqualTo(100));
            Assert.That(s.MinMs, Is.EqualTo(1));
            Assert.That(s.MaxMs, Is.EqualTo(100));
            Assert.That(s.MeanMs, Is.EqualTo(50.5));
            Assert.That(s.P50Ms, Is.EqualTo(50));
            Assert.That(s.P95Ms, Is.EqualTo(95));
            Assert.That(s.P99Ms, Is.EqualTo(99));
        }

        [Test]
        public void DropsNegativeAndNonFiniteSamples()
        {
            var recorder = new LatencyRecorder();
            recorder.Record(LatencyStage.EndToEnd, -1);
            recorder.Record(LatencyStage.EndToEnd, double.NaN);
            recorder.Record(LatencyStage.EndToEnd, double.PositiveInfinity);
            recorder.Record(LatencyStage.EndToEnd, 12.5);

            Assert.That(recorder.SampleCount(LatencyStage.EndToEnd), Is.EqualTo(1));
            Assert.That(recorder.Summarize(LatencyStage.EndToEnd).P50Ms, Is.EqualTo(12.5));
        }

        [Test]
        public void KeepsOnlyTheMostRecentCapacitySamples()
        {
            var recorder = new LatencyRecorder();
            for (int i = 0; i < LatencyRecorder.Capacity + 200; i++)
            {
                recorder.Record(LatencyStage.NoteEventToScheduled, i);
            }

            LatencySummary s = recorder.Summarize(LatencyStage.NoteEventToScheduled);

            Assert.That(s.Count, Is.EqualTo(LatencyRecorder.Capacity));
            Assert.That(s.MinMs, Is.EqualTo(200), "the oldest 200 samples rolled off");
            Assert.That(s.MaxMs, Is.EqualTo(LatencyRecorder.Capacity + 199));
        }

        [Test]
        public void StagesAreIndependent()
        {
            var recorder = new LatencyRecorder();
            recorder.Record(LatencyStage.PoseToConfirm, 100);
            recorder.Record(LatencyStage.EndToEnd, 5);

            Assert.That(recorder.Summarize(LatencyStage.PoseToConfirm).P50Ms, Is.EqualTo(100));
            Assert.That(recorder.Summarize(LatencyStage.EndToEnd).P50Ms, Is.EqualTo(5));
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var recorder = new LatencyRecorder();
            recorder.Record(LatencyStage.EndToEnd, 10);
            recorder.Reset();

            Assert.That(recorder.Summarize(LatencyStage.EndToEnd).IsEmpty, Is.True);
        }
    }
}
