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
        public void HoldingAFunction_SelectsWithoutSounding()
        {
            _engine.SetHeldFunction(ChordFunction.Two);

            Assert.That(_sink.Events, Is.Empty, "selection alone must not sound anything (ADR-0025)");
            Assert.That(_engine.ActiveChord, Is.EqualTo(Chord.Dm7));
            Assert.That(_engine.SoundingVoicing, Is.Null);
        }

        [Test]
        public void Strike_SoundsTheSelectedChordsCloseVoicing()
        {
            _engine.SetHeldFunction(ChordFunction.Two);
            _clock.Advance(1.0);

            bool struck = _engine.Strike(100);

            Assert.That(struck, Is.True);
            AssertPitches(_sink.On(MidiChannel.Harmony), 50, 53, 57, 60);
            Assert.That(_sink.Off(MidiChannel.Harmony), Is.Empty);
            foreach (NoteEvent on in _sink.On(MidiChannel.Harmony))
            {
                Assert.That(on.Velocity, Is.EqualTo((byte)100));
                Assert.That(on.Source, Is.EqualTo(Handedness.Left));
                Assert.That(on.DspTime, Is.EqualTo(1.0));
            }

            Assert.That(_engine.SoundingVoicing, Is.EqualTo(Voicing.Close(Chord.Dm7)));
        }

        [Test]
        public void Strike_WithNothingSelected_IsANoOp()
        {
            bool struck = _engine.Strike(100);

            Assert.That(struck, Is.False);
            Assert.That(_sink.Events, Is.Empty);
        }

        [Test]
        public void StrikingTheSameFunctionTwice_ReSoundsIt()
        {
            // The bug ADR-0025 fixes: re-articulating a held chord (comping on the beat)
            // was impossible because ProgressionState.Hold no-ops on the same function.
            // Strike is exactly the event that was missing.
            _engine.SetHeldFunction(ChordFunction.Two);
            _engine.Strike(90);
            _sink.Clear();
            _clock.Advance(0.5);

            bool struck = _engine.Strike(110);

            Assert.That(struck, Is.True);
            var kinds = new List<NoteEventKind>();
            foreach (NoteEvent e in _sink.Events)
            {
                kinds.Add(e.Kind);
            }

            int lastOff = kinds.LastIndexOf(NoteEventKind.Off);
            int firstOn = kinds.IndexOf(NoteEventKind.On);
            Assert.That(lastOff, Is.LessThan(firstOn), "the re-strike must cut the first ring before sounding again");

            AssertPitches(_sink.Off(MidiChannel.Harmony), 50, 53, 57, 60);
            AssertPitches(_sink.On(MidiChannel.Harmony), 50, 53, 57, 60);
            foreach (NoteEvent on in _sink.On(MidiChannel.Harmony))
            {
                Assert.That(on.DspTime, Is.EqualTo(0.5));
                Assert.That(on.Velocity, Is.EqualTo((byte)110));
            }
        }

        [Test]
        public void SelectingADifferentChord_DoesNotCutAStillRingingStrike_ItDecaysOnItsOwnTimer()
        {
            _engine.SetHeldFunction(ChordFunction.Two);
            _engine.Strike(100);
            _sink.Clear();

            _clock.Advance(0.1);
            _engine.SetHeldFunction(ChordFunction.Five); // selection only — no strike

            Assert.That(_sink.Off(MidiChannel.Harmony), Is.Empty, "mere selection must not cut the ringing Dm7");
            Assert.That(_engine.ActiveChord, Is.EqualTo(Chord.G7), "but the selection itself did change");

            _clock.Advance(HarmonyEngine.DefaultSustainSeconds);
            _engine.Tick();

            AssertPitches(_sink.Off(MidiChannel.Harmony), 50, 53, 57, 60);
        }

        [Test]
        public void Tick_SendsTheOff_OnceTheSustainHasElapsed_NotBefore()
        {
            _engine.SetHeldFunction(ChordFunction.One);
            _engine.Strike(100);
            _sink.Clear();

            _clock.Advance(HarmonyEngine.DefaultSustainSeconds - 0.1);
            _engine.Tick();
            Assert.That(_sink.Off(MidiChannel.Harmony), Is.Empty);

            _clock.Advance(0.2);
            _engine.Tick();
            AssertPitches(_sink.Off(MidiChannel.Harmony), 48, 52, 55, 59);
        }

        [Test]
        public void Releasing_CutsARingingStrike_SendsOffsAndNothingElse()
        {
            _engine.SetHeldFunction(ChordFunction.One);
            _engine.Strike(100);
            _sink.Clear();

            _engine.SetHeldFunction(null);

            AssertPitches(_sink.Off(MidiChannel.Harmony), 48, 52, 55, 59);
            Assert.That(_sink.On(MidiChannel.Harmony), Is.Empty);
            Assert.That(_engine.SoundingVoicing, Is.Null);
        }

        [Test]
        public void ReleasingWithNothingSounding_IsSilent()
        {
            _engine.SetHeldFunction(ChordFunction.One); // selected but never struck
            _sink.Clear();

            _engine.SetHeldFunction(null);

            Assert.That(_sink.Events, Is.Empty);
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
        public void ChordChangedEvent_ToNull_FiresAfterTheReleaseOffsAreSent()
        {
            _engine.SetHeldFunction(ChordFunction.Five);
            _engine.Strike(100);

            int eventsWhenChanged = -1;
            _engine.ChordChanged += _ => eventsWhenChanged = _sink.Events.Count;
            _sink.Clear();

            _engine.SetHeldFunction(null);

            Assert.That(eventsWhenChanged, Is.EqualTo(4), "the 4 release offs, sent before the event fires");
        }

        [Test]
        public void ChordChangedEvent_OnSelection_CarriesNoNoteEvents()
        {
            int eventsWhenChanged = -1;
            _engine.ChordChanged += _ => eventsWhenChanged = _sink.Events.Count;

            _engine.SetHeldFunction(ChordFunction.Five);

            Assert.That(eventsWhenChanged, Is.EqualTo(0), "selection alone sounds nothing (ADR-0025)");
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
