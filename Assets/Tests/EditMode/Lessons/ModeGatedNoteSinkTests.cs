using Jazztures.Core.Lessons;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class ModeGatedNoteSinkTests
    {
        private RecordingNoteSink _audible = null!;
        private RecordingNoteSink _unconditional = null!;
        private VirtualClock _clock = null!;
        private ModeGatedNoteSink _gate = null!;

        [SetUp]
        public void SetUp()
        {
            _audible = new RecordingNoteSink();
            _unconditional = new RecordingNoteSink();
            _clock = new VirtualClock();
            _gate = new ModeGatedNoteSink(_audible, _unconditional, _clock);
        }

        private static NoteEvent UserNote(int channel = MidiChannel.Melody) =>
            NoteEvent.On(new Pitch(72), 90, 0.0, channel, Handedness.Right);

        private static NoteEvent HarmonyOn(int midi) =>
            NoteEvent.On(new Pitch(midi), 80, 0.0, MidiChannel.Harmony, Handedness.Left);

        private static NoteEvent HarmonyOff(int midi) =>
            NoteEvent.Off(new Pitch(midi), 0.0, MidiChannel.Harmony, Handedness.Left);

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

        // §3.8: Gesture Learning is unconditional-audio pose practice — you hear every
        // shape you make. Compose and Test Yourself also always sound the learner.
        [TestCase(LearningMode.GestureLearning)]
        [TestCase(LearningMode.ComposeOnTheFly)]
        [TestCase(LearningMode.TestYourself)]
        public void AlwaysSoundsTheLearner(LearningMode mode)
        {
            _gate.SetMode(mode);
            _gate.Send(UserNote());

            Assert.That(_audible.Events, Has.Count.EqualTo(1), mode.ToString());
        }

        // Try Yourself — "audio is the reward for matching it" (§3.8).
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

        // The chord voicing is sustained (§3.2): a note-on dropped while the gate is shut
        // must be re-sounded when the gate opens, or the held chord stays silent until the
        // learner changes it — the "inconsistent audio" bug in Gesture Learning / Try Yourself.
        [Test]
        public void HeldHarmony_IsResounded_WhenTheGestureBecomesCorrect()
        {
            _gate.SetMode(LearningMode.TryYourself);
            _gate.SetGestureCorrect(false);

            _gate.Send(HarmonyOn(62));
            _gate.Send(HarmonyOn(65));
            _gate.Send(HarmonyOn(69));
            Assert.That(_audible.Events, Is.Empty, "shut gate drops the voicing");

            _clock.Advance(0.25);
            _gate.SetGestureCorrect(true);

            Assert.That(_audible.Events, Has.Count.EqualTo(3), "all three held pitches re-sound");
            Assert.That(_audible.Events, Has.All.Property(nameof(NoteEvent.Kind)).EqualTo(NoteEventKind.On));
            Assert.That(_audible.Events, Has.All.Property(nameof(NoteEvent.DspTime)).EqualTo(0.25),
                "re-sounded at 'now', not the original onset");
            Assert.That(_unconditional.Events, Has.Count.EqualTo(3), "the log saw them once, when first sent");
        }

        [Test]
        public void HeldHarmony_IsReleased_WhenTheGestureStopsBeingCorrect()
        {
            _gate.SetMode(LearningMode.TryYourself);
            _gate.SetGestureCorrect(true);

            _gate.Send(HarmonyOn(62));
            _gate.Send(HarmonyOn(65));
            Assert.That(_audible.Events, Has.Count.EqualTo(2));
            _audible.Clear();

            _gate.SetGestureCorrect(false);

            Assert.That(_audible.Events, Has.Count.EqualTo(2), "both pitches released");
            Assert.That(_audible.Events, Has.All.Property(nameof(NoteEvent.Kind)).EqualTo(NoteEventKind.Off));
        }

        [Test]
        public void HeldHarmony_ReleasedByTheLearner_IsNotResoundedLater()
        {
            _gate.SetMode(LearningMode.TryYourself);
            _gate.SetGestureCorrect(false);

            _gate.Send(HarmonyOn(62));
            _gate.Send(HarmonyOff(62)); // chord released while still muted

            _gate.SetGestureCorrect(true);

            Assert.That(_audible.Events, Is.Empty, "nothing is held, so nothing re-sounds");
        }

        [Test]
        public void HeldHarmony_ResoundThenRelease_SendsOneOnThenOneOff()
        {
            _gate.SetMode(LearningMode.TryYourself);
            _gate.SetGestureCorrect(false);
            _gate.Send(HarmonyOn(67));

            _gate.SetGestureCorrect(true);           // re-sounds
            _gate.Send(HarmonyOff(67));              // learner changes chord

            Assert.That(_audible.Events, Has.Count.EqualTo(2));
            Assert.That(_audible.Events[0].Kind, Is.EqualTo(NoteEventKind.On));
            Assert.That(_audible.Events[1].Kind, Is.EqualTo(NoteEventKind.Off));
        }

        [Test]
        public void ModeChange_ToWatchAndListen_MutesHeldLearnerHarmony()
        {
            _gate.SetMode(LearningMode.ComposeOnTheFly); // Always audible
            _gate.Send(HarmonyOn(60));
            Assert.That(_audible.Events, Has.Count.EqualTo(1));
            _audible.Clear();

            _gate.SetMode(LearningMode.WatchAndListen); // learner never audible

            Assert.That(_audible.Events, Has.Count.EqualTo(1));
            Assert.That(_audible.Events[0].Kind, Is.EqualTo(NoteEventKind.Off));
        }
    }
}
