using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Harmony
{
    public class ProgressionStateTests
    {
        [Test]
        public void StartsWithNothingHeld()
        {
            var state = new ProgressionState();

            Assert.That(state.Active, Is.Null);
            Assert.That(state.ActiveChord, Is.Null);
        }

        [Test]
        public void Hold_SetsActive_AndRaisesChangedOnce()
        {
            var state = new ProgressionState();
            var changes = new List<ChordChange>();
            state.Changed += changes.Add;

            bool changed = state.Hold(ChordFunction.Five);

            Assert.That(changed, Is.True);
            Assert.That(state.Active, Is.EqualTo(ChordFunction.Five));
            Assert.That(state.ActiveChord, Is.EqualTo(Chord.G7));
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].PreviousFunction, Is.Null);
            Assert.That(changes[0].CurrentFunction, Is.EqualTo(ChordFunction.Five));
            Assert.That(changes[0].CurrentChord, Is.EqualTo(Chord.G7));
        }

        [Test]
        public void HoldingTheSameFunctionAgain_IsANoOp()
        {
            var state = new ProgressionState();
            state.Hold(ChordFunction.Two);

            int raised = 0;
            state.Changed += _ => raised++;

            bool changed = state.Hold(ChordFunction.Two);

            Assert.That(changed, Is.False);
            Assert.That(raised, Is.Zero);
        }

        [Test]
        public void Release_ClearsActive_AndReportsThePreviousChord()
        {
            var state = new ProgressionState();
            state.Hold(ChordFunction.One);

            ChordChange? seen = null;
            state.Changed += c => seen = c;

            bool changed = state.Release();

            Assert.That(changed, Is.True);
            Assert.That(state.Active, Is.Null);
            Assert.That(seen!.Value.PreviousFunction, Is.EqualTo(ChordFunction.One));
            Assert.That(seen.Value.PreviousChord, Is.EqualTo(Chord.Cmaj7));
            Assert.That(seen.Value.CurrentFunction, Is.Null);
        }

        [Test]
        public void ReleaseWithNothingHeld_IsANoOp()
        {
            var state = new ProgressionState();
            int raised = 0;
            state.Changed += _ => raised++;

            Assert.That(state.Release(), Is.False);
            Assert.That(raised, Is.Zero);
        }

        // §3.2 — the progression is a functional relationship, not a fixed sequence.
        [Test]
        public void Transitions_AreUnordered_ISupportsReverseOrder()
        {
            var state = new ProgressionState();
            var functions = new List<ChordFunction?>();
            state.Changed += c => functions.Add(c.CurrentFunction);

            state.Hold(ChordFunction.One);
            state.Hold(ChordFunction.Five);
            state.Hold(ChordFunction.Two);

            CollectionAssert.AreEqual(
                new ChordFunction?[] { ChordFunction.One, ChordFunction.Five, ChordFunction.Two },
                functions);
        }

        // Phase 1 "done when": scripted function sequence -> exact chord-tone set each step.
        [Test]
        public void ScriptedSequence_YieldsTheExpectedChordToneSetAfterEachChange()
        {
            var state = new ProgressionState();
            var toneSets = new List<ChordToneSet?>();
            state.Changed += c =>
                toneSets.Add(c.CurrentChord.HasValue
                    ? ChordToneSet.For(c.CurrentChord.Value)
                    : (ChordToneSet?)null);

            state.Hold(ChordFunction.Two);   // Dm7
            state.Hold(ChordFunction.Five);  // G7
            state.Hold(ChordFunction.One);   // Cmaj7
            state.Release();                 // -

            Assert.That(toneSets, Has.Count.EqualTo(4));
            AssertToneSetMidi(toneSets[0], new[] { 74, 77, 81, 84, 88, 86, 89, 93, 96, 100 });
            AssertToneSetMidi(toneSets[1], new[] { 79, 83, 86, 89, 93, 91, 95, 98, 101, 105 });
            AssertToneSetMidi(toneSets[2], new[] { 72, 76, 79, 83, 86, 84, 88, 91, 95, 98 });
            Assert.That(toneSets[3], Is.Null);
        }

        private static void AssertToneSetMidi(ChordToneSet? set, int[] expected)
        {
            Assert.That(set.HasValue, Is.True);
            int[] actual = new int[ChordToneSet.TargetCount];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = set!.Value[i].Pitch.Midi;
            }

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}
