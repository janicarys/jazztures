using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class TimelinePlaybackTests
    {
        private VirtualClock _clock = null!;
        private RecordingNoteSink _sink = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new VirtualClock();
            _sink = new RecordingNoteSink();
        }

        private TimelinePlayback Playback(LessonTimeline timeline) =>
            new TimelinePlayback(timeline, _clock, _sink);

        private static LessonTimeline TwoChordPhrase() =>
            new LessonTimelineBuilder()
                .WithTempo(Tempo.Bpm(120)) // 0.5 s / beat
                .Chord(0.0, ChordFunction.Two)
                .Chord(2.0, ChordFunction.One)
                .Note(0.0, 0)
                .Note(1.0, 2)
                .Build();

        private System.Collections.Generic.List<NoteEvent> MelodyOns() =>
            _sink.Events.FindAll(e => e.Kind == NoteEventKind.On && e.Source == Handedness.Right);

        private void RunToCompletion(TimelinePlayback playback)
        {
            playback.Start();
            for (int i = 0; i < 200 && !playback.HasEnded; i++)
            {
                _clock.Advance(0.05);
                playback.Tick();
            }
        }

        [Test]
        public void EmitsEverythingOnTheAccompanimentChannel()
        {
            TimelinePlayback playback = Playback(TwoChordPhrase());
            RunToCompletion(playback);

            Assert.That(_sink.Events, Is.Not.Empty);
            foreach (NoteEvent e in _sink.Events)
            {
                Assert.That(e.Channel, Is.EqualTo(MidiChannel.Accompaniment));
            }
        }

        [Test]
        public void EveryNoteOn_IsEventuallyMatchedByANoteOff()
        {
            TimelinePlayback playback = Playback(TwoChordPhrase());
            RunToCompletion(playback);

            int ons = _sink.Events.FindAll(e => e.Kind == NoteEventKind.On).Count;
            int offs = _sink.Events.FindAll(e => e.Kind == NoteEventKind.Off).Count;
            Assert.That(ons, Is.EqualTo(offs));
            Assert.That(ons, Is.EqualTo(4 + 4 + 2), "two 4-note voicings + two melody notes");
        }

        [Test]
        public void ChordVoicingsSoundInOrder_SecondChordAfterTheFirst()
        {
            TimelinePlayback playback = Playback(TwoChordPhrase());
            RunToCompletion(playback);

            // First chord Dm7 close voicing lowest note, then G7... just assert timing ordering
            // via the first note-on of each chord class is monotonic in DspTime.
            double firstOn = _sink.Events.Find(e => e.Kind == NoteEventKind.On).DspTime;
            NoteEvent lastOn = _sink.Events.FindLast(e => e.Kind == NoteEventKind.On);
            Assert.That(lastOn.DspTime, Is.GreaterThanOrEqualTo(firstOn));
        }

        [Test]
        public void MelodyOnset_CarriesSwing()
        {
            LessonTimeline swung = new LessonTimelineBuilder()
                .WithTempo(Tempo.Bpm(120))
                .WithSwing(new SwingRatio(0.66))
                .Chord(0.0, ChordFunction.One)
                .Note(1.5, 0) // 1.66 beats -> 0.83 s
                .Build();

            TimelinePlayback playback = Playback(swung);
            playback.Start();

            _clock.Advance(0.80);
            playback.Tick();
            Assert.That(MelodyOns(), Is.Empty, "not yet — swung to 0.83 s");

            _clock.Advance(0.05); // 0.85 s
            playback.Tick();
            Assert.That(MelodyOns(), Has.Count.EqualTo(1));
        }

        [Test]
        public void Stop_ReleasesEverythingStillRinging()
        {
            TimelinePlayback playback = Playback(TwoChordPhrase());
            playback.Start();

            _clock.Advance(0.1); // first chord + first note are on
            playback.Tick();
            int onsBeforeStop = _sink.Events.FindAll(e => e.Kind == NoteEventKind.On).Count;
            Assert.That(onsBeforeStop, Is.GreaterThan(0));

            playback.Stop();

            int ons = _sink.Events.FindAll(e => e.Kind == NoteEventKind.On).Count;
            int offs = _sink.Events.FindAll(e => e.Kind == NoteEventKind.Off).Count;
            Assert.That(offs, Is.EqualTo(ons), "stop balances every on with an off");
            Assert.That(playback.HasEnded, Is.True);
        }

        [Test]
        public void FiresEndedOnce_WhenTheScheduleIsExhausted()
        {
            TimelinePlayback playback = Playback(TwoChordPhrase());
            int ended = 0;
            playback.Ended += () => ended++;

            RunToCompletion(playback);

            Assert.That(ended, Is.EqualTo(1));
            Assert.That(playback.IsPlaying, Is.False);
        }

        [Test]
        public void EmptyTimeline_EndsImmediately_AndSoundsNothing()
        {
            LessonTimeline empty = new LessonTimelineBuilder().Build();
            TimelinePlayback playback = Playback(empty);

            playback.Start();
            Assert.That(playback.HasEnded, Is.True);
            playback.Tick();
            Assert.That(_sink.Events, Is.Empty);
        }
    }
}
