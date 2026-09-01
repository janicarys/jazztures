using System.Collections.Generic;
using Jazztures.Core.Gesture;
using Jazztures.Core.Harmony;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Gesture
{
    public class ReplayHandPoseSourceTests
    {
        private static HandPoseRecording Recording() => new HandPoseRecording(new[]
        {
            new HandPoseSample(0.0, new HandPoseFrame(HandPoseCandidate.None, TrackingQuality.High, TrackingQuality.High)),
            new HandPoseSample(1.0, new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High)),
            new HandPoseSample(2.0, new HandPoseFrame(HandPoseCandidate.V, TrackingQuality.High, TrackingQuality.High)),
        });

        [Test]
        public void BeforeFirstSample_ReportsUntracked()
        {
            var clock = new VirtualClock(50.0);
            var replay = new ReplayHandPoseSource(new HandPoseRecording(new[]
            {
                new HandPoseSample(0.5, new HandPoseFrame(HandPoseCandidate.Ii, TrackingQuality.High, TrackingQuality.High)),
            }), clock);

            Assert.That(replay.CurrentFrame, Is.EqualTo(HandPoseFrame.Untracked));

            clock.Advance(0.5);
            Assert.That(replay.CurrentFrame.LeftCandidate, Is.EqualTo(HandPoseCandidate.Ii));
        }

        [Test]
        public void ReturnsTheMostRecentSample_ForTheCurrentPosition()
        {
            var clock = new VirtualClock();
            var replay = new ReplayHandPoseSource(Recording(), clock);

            Assert.That(replay.CurrentFrame.LeftCandidate, Is.EqualTo(HandPoseCandidate.None));

            clock.Advance(1.5);
            Assert.That(replay.CurrentFrame.LeftCandidate, Is.EqualTo(HandPoseCandidate.Ii));

            clock.Advance(1.0); // t = 2.5
            Assert.That(replay.CurrentFrame.LeftCandidate, Is.EqualTo(HandPoseCandidate.V));
        }

        [Test]
        public void AfterTheEnd_HoldsTheLastSample_AndReportsEnded()
        {
            var clock = new VirtualClock();
            var replay = new ReplayHandPoseSource(Recording(), clock);

            clock.Advance(10.0);

            Assert.That(replay.CurrentFrame.LeftCandidate, Is.EqualTo(HandPoseCandidate.V));
            Assert.That(replay.HasEnded, Is.True);
        }

        [Test]
        public void PositionIsRelativeToConstructionTime()
        {
            var clock = new VirtualClock(123.0);
            var replay = new ReplayHandPoseSource(Recording(), clock);

            clock.Advance(1.0);

            Assert.That(replay.Position, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(replay.CurrentFrame.LeftCandidate, Is.EqualTo(HandPoseCandidate.Ii));
        }

        // The point of the whole feature: record -> serialise -> parse -> replay through
        // the interpreter -> a deterministic result, no headset.
        [Test]
        public void RecordedSession_ReplayedThroughTheInterpreter_ConfirmsTheExpectedChords()
        {
            // 1. "Record" a session: hold ii, then V, then release, at a 60 Hz cadence.
            var recorder = new HandPoseRecorder();
            double t = 1000.0;
            void Hold(HandPoseCandidate c, double seconds)
            {
                for (double e = 0; e < seconds; e += 1.0 / 60.0)
                {
                    recorder.Capture(t, new HandPoseFrame(c, TrackingQuality.High, TrackingQuality.High));
                    t += 1.0 / 60.0;
                }
            }

            Hold(HandPoseCandidate.None, 0.1);
            Hold(HandPoseCandidate.Ii, 0.4);
            Hold(HandPoseCandidate.V, 0.4);
            Hold(HandPoseCandidate.None, 0.4);

            // 2. Serialise and parse back.
            Assert.That(
                HandPoseRecording.TryParseJsonl(recorder.Build().ToJsonl(), out HandPoseRecording recording),
                Is.True);

            // 3. Replay it through a fresh interpreter, stepping the clock frame by frame.
            var clock = new VirtualClock();
            var replay = new ReplayHandPoseSource(recording, clock);
            var interpreter = new GestureInterpreter(clock, GestureThresholds.Default);
            var confirmed = new List<ChordFunction?>();
            interpreter.ConfirmedFunctionChanged += f => confirmed.Add(f);

            while (!replay.HasEnded)
            {
                interpreter.Feed(replay.CurrentFrame);
                clock.Advance(1.0 / 60.0);
            }

            Assert.That(confirmed, Is.EqualTo(new ChordFunction?[]
            {
                ChordFunction.Two, ChordFunction.Five, null,
            }));
        }
    }
}
