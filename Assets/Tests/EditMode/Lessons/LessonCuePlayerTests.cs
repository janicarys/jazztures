using System;
using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Lessons
{
    public class LessonCuePlayerTests
    {
        private static LessonTimeline Timeline() =>
            new LessonTimelineBuilder()
                .Chord(0.0, ChordFunction.Two)
                .Chord(4.0, ChordFunction.One)
                .Note(0.0, 0)
                .Marker(4.0, "resolution")
                .Build();

        private LessonCuePlayer _player = null!;
        private List<CueAction> _fired = null!;

        private void Start(LessonScript script)
        {
            _player = new LessonCuePlayer(script, Timeline());
            _fired = new List<CueAction>();
            _player.ActionFired += _fired.Add;
        }

        [Test]
        public void BeatCue_FiresWhenTheClockReachesIt_AndOnlyOnce()
        {
            Start(new LessonScriptBuilder()
                .Cue(CueTrigger.AtBeat(2.0), CueAction.ShowText("keep the pulse"))
                .Build());

            _player.AdvanceTo(1.9);
            Assert.That(_fired, Is.Empty);

            _player.AdvanceTo(2.0);
            Assert.That(_fired, Has.Count.EqualTo(1));
            Assert.That(_fired[0].Text, Is.EqualTo("keep the pulse"));

            _player.AdvanceTo(3.0);
            Assert.That(_fired, Has.Count.EqualTo(1), "does not re-fire");
        }

        [Test]
        public void BeatCueAtZero_FiresOnTheFirstAdvance()
        {
            Start(new LessonScriptBuilder()
                .Cue(CueTrigger.AtBeat(0.0), CueAction.ShowText("welcome"))
                .Build());

            _player.AdvanceTo(0.0);
            Assert.That(_fired, Has.Count.EqualTo(1));
        }

        [Test]
        public void MultipleBeatCues_FireInBeatOrder_WhenTheClockJumpsPastThem()
        {
            Start(new LessonScriptBuilder()
                .Cue(CueTrigger.AtBeat(3.0), CueAction.ShowText("c"))
                .Cue(CueTrigger.AtBeat(1.0), CueAction.ShowText("a"))
                .Cue(CueTrigger.AtBeat(2.0), CueAction.ShowText("b"))
                .Build());

            _player.AdvanceTo(5.0);

            Assert.That(_fired.ConvertAll(a => a.Text), Is.EqualTo(new[] { "a", "b", "c" }));
        }

        [Test]
        public void MarkerCue_ResolvesToTheMarkerBeat()
        {
            Start(new LessonScriptBuilder()
                .Cue(CueTrigger.AtMarker("resolution"), CueAction.SetTensionColor(TensionColor.Resolved))
                .Build());

            _player.AdvanceTo(3.99);
            Assert.That(_fired, Is.Empty);

            _player.AdvanceTo(4.0);
            Assert.That(_fired, Has.Count.EqualTo(1));
            Assert.That(_fired[0].Color, Is.EqualTo(TensionColor.Resolved));
        }

        [Test]
        public void UnknownMarker_ThrowsAtConstruction()
        {
            LessonScript script = new LessonScriptBuilder()
                .Cue(CueTrigger.AtMarker("nope"), CueAction.ClearHighlights())
                .Build();

            Assert.Throws<InvalidOperationException>(() => new LessonCuePlayer(script, Timeline()));
        }

        [Test]
        public void LearnerActionCue_FiresOnAMatchingNotify()
        {
            Start(new LessonScriptBuilder()
                .Cue(CueTrigger.WhenChordConfirmed(ChordFunction.One), CueAction.ShowText("that's the tonic"))
                .Build());

            _player.Notify(LearnerAction.ChordConfirmed, ChordFunction.Two);
            Assert.That(_fired, Is.Empty, "wrong function");

            _player.Notify(LearnerAction.ChordConfirmed, ChordFunction.One);
            Assert.That(_fired, Has.Count.EqualTo(1));

            _player.Notify(LearnerAction.ChordConfirmed, ChordFunction.One);
            Assert.That(_fired, Has.Count.EqualTo(1), "fires once");
        }

        [Test]
        public void LearnerActionCue_WithoutNarrowing_MatchesAnyOfThatAction()
        {
            Start(new LessonScriptBuilder()
                .Cue(CueTrigger.WhenNotePlayed(), CueAction.ClearHighlights())
                .Build());

            _player.Notify(LearnerAction.MelodyNotePlayed, targetIndex: 7);
            Assert.That(_fired, Has.Count.EqualTo(1));
        }

        [Test]
        public void AdvanceTo_RejectsGoingBackwards()
        {
            Start(LessonScript.Empty);
            _player.AdvanceTo(3.0);

            Assert.Throws<ArgumentOutOfRangeException>(() => _player.AdvanceTo(2.0));
        }

        [Test]
        public void Reset_RearmsEveryCue()
        {
            Start(new LessonScriptBuilder()
                .Cue(CueTrigger.AtBeat(1.0), CueAction.ShowText("x"))
                .Cue(CueTrigger.WhenAttemptCompleted(), CueAction.AdvancePhase())
                .Build());

            _player.AdvanceTo(2.0);
            _player.Notify(LearnerAction.AttemptCompleted);
            Assert.That(_fired, Has.Count.EqualTo(2));

            _player.Reset();
            Assert.That(_player.CurrentBeat, Is.EqualTo(0.0));

            _player.AdvanceTo(2.0);
            Assert.That(_fired, Has.Count.EqualTo(3));
        }

        [Test]
        public void ControlActions_AreDeliveredLikeAnyOther()
        {
            Start(new LessonScriptBuilder()
                .Cue(CueTrigger.WhenPhraseStarts(), CueAction.SetScoring(true))
                .Cue(CueTrigger.WhenAttemptCompleted(), CueAction.AdvancePhase())
                .Build());

            _player.Notify(LearnerAction.PhraseStarted);
            _player.Notify(LearnerAction.AttemptCompleted);

            Assert.That(_fired[0].Kind, Is.EqualTo(CueActionKind.SetScoring));
            Assert.That(_fired[0].Flag, Is.True);
            Assert.That(_fired[1].Kind, Is.EqualTo(CueActionKind.AdvancePhase));
        }
    }
}
