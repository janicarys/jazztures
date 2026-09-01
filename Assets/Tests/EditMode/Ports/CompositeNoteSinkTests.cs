using System;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Ports
{
    public class CompositeNoteSinkTests
    {
        [Test]
        public void SendsEveryEvent_ToEverySink_InOrder()
        {
            var a = new RecordingNoteSink();
            var b = new RecordingNoteSink();
            var c = new RecordingNoteSink();
            var composite = new CompositeNoteSink(a, b, c);

            var note = NoteEvent.On(new Pitch(60), 64, 0.0, MidiChannel.Melody, Handedness.Right);
            composite.Send(note);

            Assert.That(a.Events, Is.EqualTo(new[] { note }));
            Assert.That(b.Events, Is.EqualTo(new[] { note }));
            Assert.That(c.Events, Is.EqualTo(new[] { note }));
        }

        [Test]
        public void Count_ReflectsTheSinkList()
        {
            Assert.That(new CompositeNoteSink(new RecordingNoteSink(), new NullNoteSink()).Count, Is.EqualTo(2));
        }

        [Test]
        public void RejectsANullSink()
        {
            Assert.That(
                () => new CompositeNoteSink(new RecordingNoteSink(), null!),
                Throws.TypeOf<ArgumentNullException>());
        }
    }
}
