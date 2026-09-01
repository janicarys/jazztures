using Jazztures.Core.Lessons;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class ModeGatedNoteSinkTests
    {
        private RecordingNoteSink _audible = null!;
        private RecordingNoteSink _unconditional = null!;
        private ModeGatedNoteSink _gate = null!;

        [SetUp]
        public void SetUp()
        {
            _audible = new RecordingNoteSink();
            _unconditional = new RecordingNoteSink();
            _gate = new ModeGatedNoteSink(_audible, _unconditional);
        }

        private static NoteEvent UserNote(int channel = MidiChannel.Melody) =>
            NoteEvent.On(new Pitch(72), 90, 0.0, channel, Handedness.Right);

        private static NoteEvent SystemNote() =>
            NoteEvent.On(new Pitch(60), 80, 0.0, MidiChannel.Accompaniment, Handedness.Left);

        [Test]
        public void EveryNote_AlwaysReachesTheUnconditionalSink()
        {
            foreach (LearningMode mode in System.Enum.GetValues(typeof(LearningMode)))
            {
                _unconditional.Clear();
                _gate.SetMode(mode);
                _gate.Send(UserNote());

                Assert.That(_unconditional.Events, Has.Count.EqualTo(1), $"{mode}: logged regardless");
            }
        }

        [Test]
        public void WatchAndListen_SilencesTheLearner_ButNotTheDemo()
        {
            _gate.SetMode(LearningMode.WatchAndListen);

            _gate.Send(UserNote());
            _gate.Send(SystemNote());

            Assert.That(_audible.Events, Has.Count.EqualTo(1));
            Assert.That(_audible.Events[0].Channel, Is.EqualTo(MidiChannel.Accompaniment));
            Assert.That(_unconditional.Events, Has.Count.EqualTo(2), "both still logged");
        }

        [Test]
        public void GestureLearningAndCompose_SoundTheLearner()
        {
            foreach (LearningMode mode in new[] { LearningMode.GestureLearning, LearningMode.ComposeOnTheFly, LearningMode.TestYourself })
            {
                _audible.Clear();
                _gate.SetMode(mode);
                _gate.Send(UserNote());

                Assert.That(_audible.Events, Has.Count.EqualTo(1), mode.ToString());
            }
        }

        [Test]
        public void TryYourself_SoundsTheLearnerOnlyWhenTheGestureIsCorrect()
        {
            _gate.SetMode(LearningMode.TryYourself);

            _gate.SetGestureCorrect(false);
            _gate.Send(UserNote());
            Assert.That(_audible.Events, Is.Empty);

            _gate.SetGestureCorrect(true);
            _gate.Send(UserNote());
            Assert.That(_audible.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryYourself_AlwaysSoundsTheSystemBacking()
        {
            _gate.SetMode(LearningMode.TryYourself);
            _gate.SetGestureCorrect(false);

            _gate.Send(SystemNote());

            Assert.That(_audible.Events, Has.Count.EqualTo(1));
        }
    }
}
