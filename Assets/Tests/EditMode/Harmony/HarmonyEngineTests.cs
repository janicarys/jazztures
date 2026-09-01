using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Harmony
{
    public class HarmonyEngineTests
    {
        private VirtualClock _clock = null!;
        private RecordingNoteSink _sink = null!;
        private HarmonyEngine _engine = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new VirtualClock();
            _sink = new RecordingNoteSink();
            _engine = new HarmonyEngine(_clock, _sink);
        }

        [Test]
        public void HoldingAFunction_SoundsItsCloseVoicing()
        {
            _clock.Advance(1.0);

            _engine.SetHeldFunction(ChordFunction.Two);

            AssertPitches(_sink.On(MidiChannel.Harmony), 50, 53, 57, 60);
            Assert.That(_sink.Off(MidiChannel.Harmony), Is.Empty);
            foreach (NoteEvent on in _sink.On(MidiChannel.Harmony))
            {
                Assert.That(on.Velocity, Is.EqualTo(HarmonyEngine.DefaultVoicingVelocity));
                Assert.That(on.Source, Is.EqualTo(Handedness.Left));
                Assert.That(on.DspTime, Is.EqualTo(1.0));
            }

            Assert.That(_engine.ActiveChord, Is.EqualTo(Chord.Dm7));
            Assert.That(_engine.SoundingVoicing, Is.EqualTo(Voicing.Close(Chord.Dm7)));
        }

        [Test]
        public void ChangingChord_ReleasesTheOldVoicingBeforeSoundingTheNew()
        {
            _engine.SetHeldFunction(ChordFunction.Two);
            _sink.Clear();
            _clock.Advance(2.0);

            _engine.SetHeldFunction(ChordFunction.Five);

            // Offs for Dm7 must all precede ons for G7.
            var kinds = new List<NoteEventKind>();
            foreach (NoteEvent e in _sink.Events)
            {
                kinds.Add(e.Kind);
            }

            int lastOff = kinds.LastIndexOf(NoteEventKind.Off);
            int firstOn = kinds.IndexOf(NoteEventKind.On);
            Assert.That(lastOff, Is.LessThan(firstOn));

            AssertPitches(_sink.Off(MidiChannel.Harmony), 50, 53, 57, 60);
            AssertPitches(_sink.On(MidiChannel.Harmony), 55, 59, 62, 65);
        }

        [Test]
        public void Releasing_SendsOffsAndNothingElse()
        {
            _engine.SetHeldFunction(ChordFunction.One);
            _sink.Clear();

            _engine.SetHeldFunction(null);

            AssertPitches(_sink.Off(MidiChannel.Harmony), 48, 52, 55, 59);
            Assert.That(_sink.On(MidiChannel.Harmony), Is.Empty);
            Assert.That(_engine.SoundingVoicing, Is.Null);
        }

        [Test]
        public void HoldingTheSameFunction_IsSilent()
        {
            _engine.SetHeldFunction(ChordFunction.Two);
            _sink.Clear();

            bool changed = _engine.SetHeldFunction(ChordFunction.Two);

            Assert.That(changed, Is.False);
            Assert.That(_sink.Events, Is.Empty);
        }

        [Test]
        public void ChordChangedEvent_FiresAfterTheNotesAreSent()
        {
            int eventsWhenChanged = -1;
            _engine.ChordChanged += _ => eventsWhenChanged = _sink.Events.Count;

            _engine.SetHeldFunction(ChordFunction.Five);

            Assert.That(eventsWhenChanged, Is.EqualTo(4));
        }

        private static void AssertPitches(IReadOnlyList<NoteEvent> events, params int[] expectedMidi)
        {
            var actual = new List<int>();
            foreach (NoteEvent e in events)
            {
                actual.Add(e.Pitch.Midi);
            }

            actual.Sort();
            var expected = new List<int>(expectedMidi);
            expected.Sort();
            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
