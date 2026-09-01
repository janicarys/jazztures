using Jazztures.Core.Gesture;
using Jazztures.Core.Ports;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class HandPoseRecorderTests
    {
        [Test]
        public void Capture_StoresTimesRelativeToTheFirstFrame()
        {
            var recorder = new HandPoseRecorder();

            recorder.Capture(100.0, new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High));
            recorder.Capture(100.5, new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High));
            recorder.Capture(101.25, new HandPoseFrame(HandPoseCandidate.V, TrackingQuality.High, TrackingQuality.High));

            HandPoseRecording recording = recorder.Build();

            Assert.That(recording.Count, Is.EqualTo(3));
            Assert.That(recording.Samples[0].TimeSeconds, Is.EqualTo(0.0));
            Assert.That(recording.Samples[1].TimeSeconds, Is.EqualTo(0.5).Within(1e-9));
            Assert.That(recording.Samples[2].TimeSeconds, Is.EqualTo(1.25).Within(1e-9));
        }

        [Test]
        public void Capture_RejectsTimeGoingBackwards()
        {
            var recorder = new HandPoseRecorder();
            recorder.Capture(10.0, HandPoseFrame.Untracked);

            Assert.That(() => recorder.Capture(9.0, HandPoseFrame.Untracked), Throws.ArgumentException);
        }

        [Test]
        public void Clear_ResetsTheTimeOrigin()
        {
            var recorder = new HandPoseRecorder();
            recorder.Capture(5.0, HandPoseFrame.Untracked);
            recorder.Clear();
            recorder.Capture(20.0, HandPoseFrame.Untracked);

            Assert.That(recorder.Build().Samples[0].TimeSeconds, Is.EqualTo(0.0));
        }
    }
}
