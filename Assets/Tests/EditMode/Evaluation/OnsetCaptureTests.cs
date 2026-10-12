using System;
using Jazztures.Core.Evaluation;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Evaluation
{
    public class OnsetCaptureTests
    {
        private static NoteEvent RightNote(double t, int midi = 72, int channel = MidiChannel.Melody) =>
            NoteEvent.On(new Pitch(midi), 90, t, channel, Handedness.Right);

        private static NoteEvent LeftNote(double t, int midi = 50, int channel = MidiChannel.Harmony) =>
            NoteEvent.On(new Pitch(midi), 90, t, channel, Handedness.Left);

        [Test]
        public void NothingIsCaptured_UntilBegun()
        {
            var capture = new OnsetCapture(Handedness.Right);

            Assert.That(capture.Observe(RightNote(1.0)), Is.False);
            Assert.That(capture.Count, Is.EqualTo(0));
            Assert.That(capture.IsCapturing, Is.False);
        }

        [Test]
        public void RightHand_CapturesMelodyNoteOns_RelativeToTheOrigin()
        {
            var capture = new OnsetCapture(Handedness.Right);
            capture.Begin(originDspTime: 10.0);

            capture.Observe(RightNote(10.5));
            capture.Observe(RightNote(11.25));

            Assert.That(capture.OnsetsSeconds, Is.EqualTo(new[] { 0.5, 1.25 }).Within(1e-12));
        }

        [Test]
        public void NoteOffs_AreNotOnsets()
        {
            var capture = new OnsetCapture(Handedness.Right);
            capture.Begin(0.0);

            capture.Observe(NoteEvent.Off(new Pitch(72), 1.0, MidiChannel.Melody, Handedness.Right));

            Assert.That(capture.Count, Is.EqualTo(0));
        }

        [Test]
        public void TheOtherHandsNotes_AreNotCaptured()
        {
            var right = new OnsetCapture(Handedness.Right);
            var left = new OnsetCapture(Handedness.Left);
            right.Begin(0.0);
            left.Begin(0.0);

            foreach (NoteEvent e in new[] { LeftNote(1.0), RightNote(2.0) })
            {
                right.Observe(e);
                left.Observe(e);
            }

            Assert.That(right.OnsetsSeconds, Is.EqualTo(new[] { 2.0 }));
            Assert.That(left.OnsetsSeconds, Is.EqualTo(new[] { 1.0 }));
        }

        [Test]
        public void SystemPlayback_OnTheAccompanimentChannel_IsNeverTheLearner()
        {
            foreach (Handedness hand in new[] { Handedness.Left, Handedness.Right })
            {
                var capture = new OnsetCapture(hand);
                capture.Begin(0.0);

                capture.Observe(NoteEvent.On(new Pitch(60), 80, 1.0, MidiChannel.Accompaniment, Handedness.Left));
                capture.Observe(NoteEvent.On(new Pitch(72), 80, 1.5, MidiChannel.Accompaniment, Handedness.Right));

                Assert.That(capture.Count, Is.EqualTo(0), hand.ToString());
            }
        }

        [Test]
        public void ANotesChannelMustMatchItsHand()
        {
            var right = new OnsetCapture(Handedness.Right);
            right.Begin(0.0);

            // A right-hand note on the harmony channel is not the melody line.
            right.Observe(RightNote(1.0, channel: MidiChannel.Harmony));

            Assert.That(right.Count, Is.EqualTo(0));
        }

        [Test]
        public void OneChordStrike_IsOneOnset_NotOnePerPitchOfTheVoicing()
        {
            var clock = new VirtualClock();
            var capture = new OnsetCapture(Handedness.Left);
            var harmony = new HarmonyEngine(clock, capture);
            capture.Begin(0.0);

            harmony.SetHeldFunction(ChordFunction.One);
            clock.Advance(1.0);
            harmony.Strike(90);

            Assert.That(capture.Count, Is.EqualTo(1), "a close voicing is four pitches, one strike");
            Assert.That(capture.OnsetsSeconds[0], Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void TwoSeparateStrikes_AreTwoOnsets()
        {
            var clock = new VirtualClock();
            var capture = new OnsetCapture(Handedness.Left);
            var harmony = new HarmonyEngine(clock, capture);
            capture.Begin(0.0);
            harmony.SetHeldFunction(ChordFunction.Two);

            clock.Advance(1.0);
            harmony.Strike(90);
            clock.Advance(0.5);
            harmony.Strike(90);

            Assert.That(capture.OnsetsSeconds, Is.EqualTo(new[] { 1.0, 1.5 }).Within(1e-12));
        }

        [Test]
        public void RightHandNotesAtTheSameInstant_AreEachTheirOwnOnset()
        {
            // Two fingertips entering two targets in one frame really are two notes, and
            // scoring should be able to call the second one extra.
            var capture = new OnsetCapture(Handedness.Right);
            capture.Begin(0.0);

            capture.Observe(RightNote(1.0, midi: 72));
            capture.Observe(RightNote(1.0, midi: 76));

            Assert.That(capture.Count, Is.EqualTo(2));
        }

        [Test]
        public void Begin_ClearsEarlierOnsets_AndRestartsTheStrikeGrouping()
        {
            var capture = new OnsetCapture(Handedness.Left);
            capture.Begin(0.0);
            capture.Observe(LeftNote(1.0));

            capture.Begin(0.0);
            capture.Observe(LeftNote(1.0)); // same instant as before, but a new attempt

            Assert.That(capture.Count, Is.EqualTo(1));
        }

        [Test]
        public void Stop_KeepsWhatWasCaptured_ButIgnoresMore()
        {
            var capture = new OnsetCapture(Handedness.Right);
            capture.Begin(0.0);
            capture.Observe(RightNote(1.0));

            capture.Stop();
            capture.Observe(RightNote(2.0));

            Assert.That(capture.OnsetsSeconds, Is.EqualTo(new[] { 1.0 }));
            Assert.That(capture.IsCapturing, Is.False);
        }

        [Test]
        public void ANoteBeforeTheOrigin_IsKeptAsANegativeTime()
        {
            var capture = new OnsetCapture(Handedness.Right);
            capture.Begin(originDspTime: 5.0);

            capture.Observe(RightNote(4.8));

            Assert.That(capture.OnsetsSeconds[0], Is.EqualTo(-0.2).Within(1e-12));
        }

        [Test]
        public void ItIsAnINoteSink_SoItCanSitOnAFanOut()
        {
            var capture = new OnsetCapture(Handedness.Right);
            var recorder = new RecordingNoteSink();
            INoteSink fanOut = new CompositeNoteSink(recorder, capture);
            capture.Begin(0.0);

            fanOut.Send(RightNote(1.0));

            Assert.That(recorder.Events, Has.Count.EqualTo(1));
            Assert.That(capture.Count, Is.EqualTo(1));
        }

        [Test]
        public void ARejectedCoincidenceWindow_IsReported()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OnsetCapture(Handedness.Left, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OnsetCapture(Handedness.Left, double.NaN));
        }

        // --- the ADR-0043 step 3 scenarios, with the real pieces wired together ------------

        private static LessonTimeline OneChordPhrase() =>
            new LessonTimelineBuilder()
                .WithTempo(Tempo.Bpm(120)) // 0.5 s per beat
                .Chord(0.0, ChordFunction.One)
                .Chord(4.0, ChordFunction.Five)
                .WithMinimumDuration(8.0)
                .Build();

        [Test]
        public void SystemChords_AreNeverCapturedAsTheLearnersOnsets()
        {
            var clock = new VirtualClock();
            var leftCapture = new OnsetCapture(Handedness.Left);
            var rightCapture = new OnsetCapture(Handedness.Right);
            var sink = new CompositeNoteSink(leftCapture, rightCapture);
            leftCapture.Begin(0.0);
            rightCapture.Begin(0.0);

            var playback = new TimelinePlayback(OneChordPhrase(), clock, sink, includeMelody: false);
            playback.Start();
            for (int i = 0; i < 100 && !playback.HasEnded; i++)
            {
                clock.Advance(0.05);
                playback.Tick();
            }

            Assert.That(playback.HasEnded, Is.True, "the chords really did play");
            Assert.That(leftCapture.Count, Is.EqualTo(0));
            Assert.That(rightCapture.Count, Is.EqualTo(0));
        }

        [Test]
        public void ARightHandTestYourselfAttempt_StillRecordsTheLearner_WhileTheChordsPlay()
        {
            // Right-hand lesson, Test Yourself: the system plays the harmony only (decision 3)
            // and the learner plays the melody over it.
            ModePolicy policy = ModePolicy.For(LearningMode.TestYourself, ActiveHands.Right);
            Assert.That(policy.SystemPlayback, Is.EqualTo(SystemPlayback.HarmonyOnly));

            var clock = new VirtualClock();
            var recorder = new RecordingNoteSink();
            var capture = new OnsetCapture(Handedness.Right);
            var sink = new CompositeNoteSink(recorder, capture);

            var harmony = new HarmonyEngine(clock, sink);
            var melody = new MelodyEngine(clock, sink, sustainSeconds: 0.5);
            harmony.ChordChanged += melody.OnChordChanged;
            var scripted = new ScriptedHarmony(clock, harmony);

            LessonTimeline timeline = OneChordPhrase();
            var playback = new TimelinePlayback(timeline, clock, sink, includeMelody: false);

            double t0 = clock.Now;
            capture.Begin(t0);
            playback.Start(t0);
            scripted.Start(timeline, t0);

            // The learner plays the root of the chord at 0.52 s, 1.02 s and 1.51 s.
            double[] played = { 0.52, 1.02, 1.51 };
            double[] expected = { 0.5, 1.0, 1.5 };
            int next = 0;
            for (int step = 0; step < 60; step++)
            {
                clock.Advance(0.05);
                scripted.Tick();
                playback.Tick();
                melody.Tick();

                if (next < played.Length && clock.Now >= played[next] - 1e-9)
                {
                    Assert.That(melody.TriggerTarget(0, 1.0f), Is.True, $"note {next} sounded");
                    next++;
                }
            }

            Assert.That(recorder.On(MidiChannel.Accompaniment), Is.Not.Empty, "the chords played");
            Assert.That(capture.Count, Is.EqualTo(3), "...and only the learner's own notes were captured");

            AttemptResult result = OnsetScorer.Evaluate(expected, capture.OnsetsSeconds, OnsetWindows.Default);
            Assert.That(result.ExtraCount, Is.EqualTo(0));
            Assert.That(result.MissedCount, Is.EqualTo(0));
            Assert.That(result.OnTimeCount, Is.EqualTo(3));
        }
    }
}
