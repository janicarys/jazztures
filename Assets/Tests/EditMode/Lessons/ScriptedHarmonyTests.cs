using System;
using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Timing;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class ScriptedHarmonyTests
    {
        private VirtualClock _clock = null!;
        private RecordingNoteSink _sink = null!;
        private HarmonyEngine _harmony = null!;
        private ScriptedHarmony _scripted = null!;
        private List<ChordChange> _changes = null!;

        [SetUp]
        public void SetUp()
        {
            _clock = new VirtualClock();
            _sink = new RecordingNoteSink();
            _harmony = new HarmonyEngine(_clock, _sink);
            _scripted = new ScriptedHarmony(_clock, _harmony);
            _changes = new List<ChordChange>();
            _harmony.ChordChanged += _changes.Add;
        }

        private static LessonTimeline IiVI() =>
            new LessonTimelineBuilder()
                .WithTempo(Tempo.Bpm(120)) // 0.5 s per beat
                .Chord(0.0, ChordFunction.Two)
                .Chord(4.0, ChordFunction.Five)
                .Chord(8.0, ChordFunction.One)
                .Build();

        [Test]
        public void AFixedChord_IsSelectedAtOnce_AndHeld()
        {
            _scripted.StartFixed(ChordFunction.One);

            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.One));
            Assert.That(_scripted.IsRunning, Is.True);

            _clock.Advance(30.0);
            _scripted.Tick();
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.One));
        }

        [Test]
        public void ATimeline_SelectsEachChordAtItsBeat()
        {
            _scripted.Start(IiVI(), startDspTime: 0.0);
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.Two), "beat 0 is due immediately");

            _clock.Advance(1.9);
            _scripted.Tick();
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.Two), "beat 4 is at 2.0 s");

            _clock.Advance(0.1);
            _scripted.Tick();
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.Five));

            _clock.Advance(2.0);
            _scripted.Tick();
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.One));
        }

        [Test]
        public void ATimelineStartedInTheFuture_SelectsNothingUntilThen()
        {
            _scripted.Start(IiVI(), startDspTime: 2.0);

            Assert.That(_harmony.HeldFunction, Is.Null);

            _clock.Advance(1.9);
            _scripted.Tick();
            Assert.That(_harmony.HeldFunction, Is.Null);

            _clock.Advance(0.1);
            _scripted.Tick();
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.Two));
        }

        // A pose the learner's left hand held on the way into a right-hand lesson must not stay selected through
        // the lead-in: the melody would be offered that chord's tones, and a label would name it, until the first scripted chord.
        [Test]
        public void StartingATimeline_ReleasesASelectionLeftOverFromBefore()
        {
            _harmony.SetHeldFunction(ChordFunction.Five); // the learner's own pose, or the last lesson's last chord
            _changes.Clear();

            _scripted.Start(IiVI(), startDspTime: 2.0);

            Assert.That(_harmony.HeldFunction, Is.Null, "nothing is selected until the script's first chord is due");
            Assert.That(_changes, Has.Count.EqualTo(1));
            Assert.That(_changes[0].CurrentChord, Is.Null);
        }

        [Test]
        public void StartingATimeline_WithNothingSelected_RaisesNoChange()
        {
            _scripted.Start(IiVI(), startDspTime: 2.0);

            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void ALateTick_CatchesUpThroughEveryDueChord_InOrder()
        {
            _scripted.Start(IiVI(), startDspTime: 0.0);
            _changes.Clear();

            _clock.Advance(10.0); // a hitch spanning both later changes
            _scripted.Tick();

            Assert.That(_changes, Has.Count.EqualTo(2));
            Assert.That(_changes[0].CurrentFunction, Is.EqualTo(ChordFunction.Five));
            Assert.That(_changes[1].CurrentFunction, Is.EqualTo(ChordFunction.One));
        }

        [Test]
        public void ItSendsNoNoteEvents_ItOnlySelects()
        {
            _scripted.Start(IiVI(), startDspTime: 0.0);
            for (int i = 0; i < 100; i++)
            {
                _clock.Advance(0.1);
                _scripted.Tick();
                _harmony.Tick();
            }

            _scripted.Stop();

            Assert.That(_sink.Events, Is.Empty, "selection is silent by construction (ADR-0025)");
        }

        [Test]
        public void AFixedChordAlsoSendsNothing()
        {
            _scripted.StartFixed(ChordFunction.Five);
            _scripted.Stop();

            Assert.That(_sink.Events, Is.Empty);
        }

        [Test]
        public void SelectionRaisesChordChanged_SoTheMelodyCanFollow()
        {
            _scripted.StartFixed(ChordFunction.One);

            Assert.That(_changes, Has.Count.EqualTo(1));
            Assert.That(_changes[0].CurrentFunction, Is.EqualTo(ChordFunction.One));
        }

        [Test]
        public void Stop_ReleasesTheSelection()
        {
            _scripted.StartFixed(ChordFunction.One);

            _scripted.Stop();

            Assert.That(_harmony.HeldFunction, Is.Null);
            Assert.That(_scripted.IsRunning, Is.False);
        }

        [Test]
        public void Stop_WhenNotRunning_LeavesTheLearnersOwnSelectionAlone()
        {
            _harmony.SetHeldFunction(ChordFunction.Five); // the learner's left hand

            _scripted.Stop();

            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.Five));
        }

        [Test]
        public void AfterStop_TickDoesNothing()
        {
            _scripted.Start(IiVI(), 0.0);
            _scripted.Stop();
            _changes.Clear();

            _clock.Advance(100.0);
            _scripted.Tick();

            Assert.That(_changes, Is.Empty);
        }

        [Test]
        public void StartingAgain_RestartsFromTheTop()
        {
            _scripted.Start(IiVI(), 0.0);
            _clock.Advance(10.0);
            _scripted.Tick();
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.One));

            _scripted.Start(IiVI(), startDspTime: _clock.Now);

            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.Two));
        }

        [Test]
        public void StartingNullOrNonFinite_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _scripted.Start(null!, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _scripted.Start(IiVI(), double.NaN));
        }

        // ADR-0077: 120 BPM (0.5 s a beat); beats 0-8 of every 16 are the system's.
        private static LessonTimeline CallAndResponse() =>
            new LessonTimelineBuilder()
                .WithTempo(Tempo.Bpm(120))
                .WithTurns(new TurnWindow(16.0, 8.0))
                .Chord(0.0, ChordFunction.Two)
                .Chord(4.0, ChordFunction.Five)
                .Chord(8.0, ChordFunction.One) // the learner's to choose
                .Chord(16.0, ChordFunction.Two)
                .Build();

        [Test]
        public void WithTurns_TheScriptSelectsOnlyInTheSystemsTurn()
        {
            _scripted.Start(CallAndResponse(), startDspTime: 0.0);

            Assert.That(_scripted.IsSelecting, Is.True);
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.Two));

            _clock.Advance(2.0); // beat 4
            _scripted.Tick();
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.Five));

            _clock.Advance(2.0); // beat 8: the learner's turn - the I authored here is not selected for them
            _scripted.Tick();
            Assert.That(_scripted.IsSelecting, Is.False);
            Assert.That(_scripted.IsRunning, Is.True);
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.Five), "the runner hands the selection back, not the script");

            _clock.Advance(4.0); // beat 16: the system's again
            _scripted.Tick();
            Assert.That(_scripted.IsSelecting, Is.True);
            Assert.That(_harmony.HeldFunction, Is.EqualTo(ChordFunction.Two));
        }

        [Test]
        public void WithTurns_TheLeftHandsStrikesAreBlockedOnlyInTheSystemsTurn()
        {
            _scripted.Start(CallAndResponse(), startDspTime: 1.0);

            Assert.That(_scripted.BlocksLeftHandStrikes, Is.True, "the lead-in is the system's");

            _clock.Advance(1.0 + 7.9 * 0.5);
            Assert.That(_scripted.BlocksLeftHandStrikes, Is.True);

            _clock.Advance(0.2); // past beat 8
            Assert.That(_scripted.BlocksLeftHandStrikes, Is.False);

            _scripted.Stop();
            Assert.That(_scripted.BlocksLeftHandStrikes, Is.False);
        }

        [Test]
        public void WithoutTurns_ScriptingBlocksNoStrikes_AndSelectsForAsLongAsItRuns()
        {
            _scripted.Start(IiVI(), startDspTime: 0.0);
            _clock.Advance(100.0);

            Assert.That(_scripted.IsSelecting, Is.True);
            Assert.That(_scripted.BlocksLeftHandStrikes, Is.False, "a right-hand lesson or drill never relied on this");

            _scripted.StartFixed(ChordFunction.One);
            Assert.That(_scripted.IsSelecting, Is.True);
            Assert.That(_scripted.BlocksLeftHandStrikes, Is.False);
        }
    }
}
