using Jazztures.Core.Gesture;
using Jazztures.Core.Ports;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class HandPoseRecordingTests
    {
        private static HandPoseRecording SampleRecording() => new HandPoseRecording(new[]
        {
            new HandPoseSample(0.0, new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.NotTracked, TrackingQuality.NotTracked)),
            new HandPoseSample(0.5, new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High)),
            new HandPoseSample(1.25, new HandPoseFrame(HandPoseCandidate.Ambiguous, TrackingQuality.Medium, TrackingQuality.Low)),
            new HandPoseSample(2.0, new HandPoseFrame(HandPoseCandidate.V, TrackingQuality.High, TrackingQuality.High)),
        });

        [Test]
        public void Jsonl_RoundTrips()
        {
            HandPoseRecording original = SampleRecording();

            Assert.That(HandPoseRecording.TryParseJsonl(original.ToJsonl(), out HandPoseRecording parsed), Is.True);
            Assert.That(parsed.Samples, Is.EqualTo(original.Samples));
            Assert.That(parsed.DurationSeconds, Is.EqualTo(2.0));
        }

        [Test]
        public void Parse_IgnoresBlankLinesAndComments()
        {
            string jsonl =
                "# recorded on the Quest, 2026-09-01\n" +
                "\n" +
                "{\"t\":0,\"c\":\"None\",\"lt\":\"High\",\"rt\":\"High\"}\n" +
                "   \n" +
                "{\"t\":0.1,\"c\":\"V\",\"lt\":\"High\",\"rt\":\"High\"}\n";

            Assert.That(HandPoseRecording.TryParseJsonl(jsonl, out HandPoseRecording parsed), Is.True);
            Assert.That(parsed.Count, Is.EqualTo(2));
        }

        [Test]
        public void Parse_ToleratesReorderedKeysAndWhitespace()
        {
            string jsonl = "{ \"c\":\"Ii\", \"lt\":\"High\", \"t\":0.25, \"rt\":\"Medium\" }\n";

            Assert.That(HandPoseRecording.TryParseJsonl(jsonl, out HandPoseRecording parsed), Is.True);
            Assert.That(parsed.Samples[0].TimeSeconds, Is.EqualTo(0.25));
            Assert.That(parsed.Samples[0].Frame.RightTracking, Is.EqualTo(TrackingQuality.Medium));
        }

        [TestCase("{\"t\":0,\"c\":\"None\",\"lt\":\"High\"}")]            // missing rt
        [TestCase("{\"t\":oops,\"c\":\"None\",\"lt\":\"High\",\"rt\":\"High\"}")]
        [TestCase("{\"t\":0,\"c\":\"Nope\",\"lt\":\"High\",\"rt\":\"High\"}")]
        [TestCase("{\"t\":-1,\"c\":\"None\",\"lt\":\"High\",\"rt\":\"High\"}")]
        [TestCase("not json at all")]
        public void Parse_RejectsMalformedLines(string line)
        {
            Assert.That(HandPoseRecording.TryParseJsonl(line + "\n", out _), Is.False);
        }

        [Test]
        public void Parse_RejectsOutOfOrderSamples()
        {
            string jsonl =
                "{\"t\":1.0,\"c\":\"None\",\"lt\":\"High\",\"rt\":\"High\"}\n" +
                "{\"t\":0.5,\"c\":\"V\",\"lt\":\"High\",\"rt\":\"High\"}\n";

            Assert.That(HandPoseRecording.TryParseJsonl(jsonl, out _), Is.False);
        }

        [Test]
        public void Constructor_RejectsOutOfOrderSamples()
        {
            Assert.That(
                () => new HandPoseRecording(new[]
                {
                    new HandPoseSample(1.0, HandPoseFrame.Untracked),
                    new HandPoseSample(0.5, HandPoseFrame.Untracked),
                }),
                Throws.ArgumentException);
        }
    }
}
