using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class MelodyEnginePlayPitchTests
    {
        [Test]
        public void PlayPitch_SoundsThePitchOnTheMelodyChannel_WithNoChordHeld_AndPairsANoteOff()
        {
            var clock = new VirtualClock();
            var sink = new RecordingNoteSink();
            var engine = new MelodyEngine(clock, sink, sustainSeconds: 0.5);
            clock.Advance(1.0);

            engine.PlayPitch(new Pitch(80), 1.0f);
            clock.Advance(0.6);
            engine.Tick();

            Assert.That(sink.On(MidiChannel.Melody), Has.Count.EqualTo(1));
            Assert.That(sink.On(MidiChannel.Melody)[0].Pitch.Midi, Is.EqualTo(80));
            Assert.That(sink.On(MidiChannel.Melody)[0].Velocity, Is.EqualTo(VelocityCurve.FromSpeed(1.0f)));
            Assert.That(sink.Off(MidiChannel.Melody), Has.Count.EqualTo(1));
            Assert.That(sink.Off(MidiChannel.Melody)[0].Pitch.Midi, Is.EqualTo(80));
        }

        [Test]
        public void PlayPitch_IsNotGatedByTheEntrySpeed_ButNeverEmitsBelowTheVelocityFloor()
        {
            var clock = new VirtualClock();
            var sink = new RecordingNoteSink();
            var engine = new MelodyEngine(clock, sink);

            engine.PlayPitch(new Pitch(80), 0.0f);

            Assert.That(sink.On(MidiChannel.Melody), Has.Count.EqualTo(1));
            Assert.That(sink.On(MidiChannel.Melody)[0].Velocity, Is.GreaterThanOrEqualTo(30));
        }
    }
}
