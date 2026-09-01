using System;
using Jazztures.Core.Harmony;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class MelodyEngineTests
    {
        private const float Fast = 1.0f;

        private VirtualClock _clock = null!;
        private RecordingNoteSink _sink = null!;
        private MelodyEngine _engine = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new VirtualClock();
            _sink = new RecordingNoteSink();
            _engine = new MelodyEngine(_clock, _sink, sustainSeconds: 0.5);
        }

        private void HoldChord(ChordFunction function) =>
            _engine.OnChordChanged(new ChordChange(null, function));

        [Test]
        public void WithNoChordHeld_TriggerIsIgnored()
        {
            Assert.That(_engine.TriggerTarget(0, Fast), Is.False);
            Assert.That(_sink.Events, Is.Empty);
            Assert.That(_engine.ActiveChordToneSet, Is.Null);
        }

        [Test]
        public void Trigger_SoundsTheSlotsCurrentPitch_OnTheMelodyChannel()
        {
            HoldChord(ChordFunction.Two); // Dm7, slot 0 = MIDI 74
            _clock.Advance(1.0);

            bool fired = _engine.TriggerTarget(0, Fast);

            Assert.That(fired, Is.True);
            Assert.That(_sink.On(MidiChannel.Melody), Has.Count.EqualTo(1));
            NoteEvent on = _sink.On(MidiChannel.Melody)[0];
            Assert.That(on.Pitch.Midi, Is.EqualTo(74));
            Assert.That(on.Source, Is.EqualTo(Handedness.Right));
            Assert.That(on.DspTime, Is.EqualTo(1.0));
            Assert.That(on.Velocity, Is.EqualTo(VelocityCurve.FromSpeed(Fast)));
        }

        [Test]
        public void SlowEntry_IsGated()
        {
            HoldChord(ChordFunction.Two);

            Assert.That(_engine.TriggerTarget(0, MelodyEngine.EntryVelocityGateMetresPerSecond - 0.01f), Is.False);
            Assert.That(_sink.Events, Is.Empty);
        }

        [Test]
        public void RetriggerCooldown_IsPerTarget()
        {
            HoldChord(ChordFunction.Two);

            Assert.That(_engine.TriggerTarget(0, Fast), Is.True);

            _clock.Advance(MelodyEngine.RetriggerCooldownSeconds / 2);
            Assert.That(_engine.TriggerTarget(0, Fast), Is.False, "same target, still cooling down");
            Assert.That(_engine.TriggerTarget(3, Fast), Is.True, "a different target is unaffected");

            _clock.Advance(MelodyEngine.RetriggerCooldownSeconds);
            Assert.That(_engine.TriggerTarget(0, Fast), Is.True, "cooldown elapsed");
        }

        [Test]
        public void NoteOff_FiresOnceTheSustainHasElapsed_Not_Before()
        {
            HoldChord(ChordFunction.Two);
            _engine.TriggerTarget(2, Fast); // Dm7 slot 2 = MIDI 81

            _clock.Advance(0.4);
            _engine.Tick();
            Assert.That(_sink.Off(MidiChannel.Melody), Is.Empty);
            Assert.That(_engine.PendingNoteCount, Is.EqualTo(1));

            _clock.Advance(0.2); // now 0.6 > 0.5 sustain
            _engine.Tick();
            Assert.That(_sink.Off(MidiChannel.Melody), Has.Count.EqualTo(1));
            Assert.That(_sink.Off(MidiChannel.Melody)[0].Pitch.Midi, Is.EqualTo(81));
            Assert.That(_engine.PendingNoteCount, Is.Zero);
        }

        [Test]
        public void ChordChange_DoesNotCutSoundingNotes_TheyDecayOnTheirOwnTimer()
        {
            HoldChord(ChordFunction.Two);
            _engine.TriggerTarget(0, Fast); // Dm7 slot 0 = MIDI 74
            _sink.Clear();

            _clock.Advance(0.1);
            HoldChord(ChordFunction.Five); // switch to G7 — no offs expected here
            Assert.That(_sink.Off(MidiChannel.Melody), Is.Empty);

            _clock.Advance(0.5);
            _engine.Tick();
            Assert.That(_sink.Off(MidiChannel.Melody), Has.Count.EqualTo(1));
            Assert.That(_sink.Off(MidiChannel.Melody)[0].Pitch.Midi, Is.EqualTo(74), "released at the pitch it was struck");
        }

        [Test]
        public void ChordChange_ResetsPerTargetCooldown()
        {
            HoldChord(ChordFunction.Two);
            _engine.TriggerTarget(0, Fast);

            HoldChord(ChordFunction.Five); // immediately, well within 80 ms

            Assert.That(_engine.TriggerTarget(0, Fast), Is.True);
            Assert.That(_engine.ActiveChordToneSet!.Value[0].Pitch.Midi, Is.EqualTo(79), "G7 slot 0");
        }

        [Test]
        public void Release_ClearsTheActiveSet()
        {
            HoldChord(ChordFunction.Two);
            _engine.OnChordChanged(new ChordChange(ChordFunction.Two, null));

            Assert.That(_engine.ActiveChordToneSet, Is.Null);
            Assert.That(_engine.TriggerTarget(0, Fast), Is.False);
        }

        [Test]
        public void PolyphonyCap_ReleasesTheOldestNoteEarly()
        {
            // A very long sustain so nothing decays on its own during the test.
            var engine = new MelodyEngine(_clock, _sink, sustainSeconds: 1000.0);
            engine.OnChordChanged(new ChordChange(null, ChordFunction.Two));

            for (int i = 0; i < MelodyEngine.MaxPolyphony; i++)
            {
                _clock.Advance(0.1); // > 80 ms, so per-target cooldown never blocks
                engine.TriggerTarget(i % ChordToneSet.TargetCount, Fast);
            }

            Assert.That(engine.PendingNoteCount, Is.EqualTo(MelodyEngine.MaxPolyphony));
            _sink.Clear();

            _clock.Advance(0.1);
            engine.TriggerTarget(0, Fast);

            Assert.That(_sink.Off(MidiChannel.Melody), Has.Count.EqualTo(1), "oldest forced off");
            Assert.That(engine.PendingNoteCount, Is.EqualTo(MelodyEngine.MaxPolyphony));
        }

        [Test]
        public void TargetIndexOutOfRange_Throws()
        {
            HoldChord(ChordFunction.Two);

            Assert.That(() => _engine.TriggerTarget(-1, Fast), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => _engine.TriggerTarget(10, Fast), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_RejectsNonPositiveSustain()
        {
            Assert.That(
                () => new MelodyEngine(_clock, _sink, sustainSeconds: 0.0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
