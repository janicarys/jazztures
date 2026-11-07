using System;
using System.Collections.Generic;
using System.Linq;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    // Cmaj7 is the fallback chord throughout. Its set over 72..90 is 72 74 76 79 83 84 86 88; a fresh cursor
    // starts at index 4 (83). With height zones the range is 72..88, which holds the same eight pitches.
    public class PalmSwingPipelineTests
    {
        private const double Frame = 1.0 / 90.0;

        // Drives a pipeline with a scripted hand: holds, and linear swings, at 90 Hz.
        private sealed class Hand
        {
            private readonly PalmSwingPipeline _pipeline;

            public Hand(PalmSwingPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            public double T;
            public float Angle;
            public float X = -0.08f;
            public float Height;
            public bool Gate = true;
            public bool JitterHeight;
            public List<PalmSwingNote> Notes { get; } = new List<PalmSwingNote>();

            public int[] Pitches => Notes.Select(n => n.Pitch.Midi).ToArray();

            public void Tick()
            {
                float height = JitterHeight ? (Notes.Count + (int)(T / Frame)) % 2 == 0 ? 0.3f : -0.3f : Height;
                if (_pipeline.Update(new PalmSwingSample(T, Gate, Angle, X, height), out PalmSwingNote note))
                {
                    Notes.Add(note);
                }

                T += Frame;
            }

            public void Hold(double seconds)
            {
                int frames = (int)Math.Round(seconds / Frame);
                for (int i = 0; i < frames; i++)
                {
                    Tick();
                }
            }

            public void HoldUntil(double time)
            {
                while (T < time)
                {
                    Tick();
                }
            }

            public void Swing(float toX, double seconds)
            {
                float from = X;
                int frames = Math.Max(1, (int)Math.Round(seconds / Frame));
                for (int i = 1; i <= frames; i++)
                {
                    X = from + (toX - from) * i / frames;
                    Tick();
                }
            }
        }

        private static PalmSwingPipeline Make(Action<PalmSwingParameters>? tweak = null)
        {
            var parameters = new PalmSwingParameters();
            tweak?.Invoke(parameters);
            return new PalmSwingPipeline(parameters, Chord.Cmaj7);
        }

        private static void Zones(PalmSwingParameters p) => p.Register = RegisterSource.HandHeightZones;

        // ---- the whole chain ----

        [Test]
        public void ScriptedHandPath_TiltUp_SwingRight_SwingLeft_TiltDown_SwingRight_PlaysTheExpectedPitches()
        {
            var hand = new Hand(Make());

            hand.Angle = 30f;       // fingers up
            hand.Hold(0.3);
            hand.Swing(0.08f, 0.3); // right: up
            hand.Swing(-0.08f, 0.3); // left: up again
            hand.Angle = -35f;      // fingers down
            hand.Hold(0.3);
            hand.Swing(0.08f, 0.3); // right: down

            Assert.That(hand.Pitches, Is.EqualTo(new[] { 84, 86, 84 }));
            Assert.That(hand.Notes.Select(n => n.Swing), Is.EqualTo(new[]
            {
                SwingDirection.LeftToRight, SwingDirection.RightToLeft, SwingDirection.LeftToRight,
            }));
            Assert.That(hand.Notes.Select(n => n.SampledDirection), Is.EqualTo(new[]
            {
                StepDirection.Up, StepDirection.Up, StepDirection.Down,
            }));
        }

        [Test]
        public void HoveringAtTheTriggerLine_SoundsNothing()
        {
            var hand = new Hand(Make());
            hand.Angle = 30f;
            hand.Hold(0.2);
            for (int i = 0; i < 180; i++)
            {
                hand.X = i % 2 == 0 ? 0.006f : -0.006f;
                hand.Tick();
            }

            Assert.That(hand.Notes, Is.Empty);
        }

        [Test]
        public void TheDirectionIsReadFromBeforeTheSwing_NotFromTheDraggedTilt()
        {
            // The hand is tilted up, then flattens in the 40 ms before it crosses (the swing dragged it).
            var pipeline = Make();
            var notes = new List<PalmSwingNote>();
            for (int i = 0; i <= 55; i++)
            {
                bool dragged = i >= 51;
                float x = i <= 50 ? -0.08f : i == 55 ? 0.04f : -0.08f + (i - 50) * 0.02f;
                if (pipeline.Update(new PalmSwingSample(i * 0.01, true, dragged ? 0f : 30f, x, 0f), out PalmSwingNote n))
                {
                    notes.Add(n);
                }
            }

            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].SampledDirection, Is.EqualTo(StepDirection.Up));
            Assert.That(notes[0].Pitch.Midi, Is.EqualTo(84));
        }

        [Test]
        public void AClosedGate_EmitsNothing_AndAHandThatReopensOnTheOtherSideDoesNotFireByArriving()
        {
            var hand = new Hand(Make());
            hand.Angle = 30f;
            hand.Hold(0.2);

            hand.Gate = false;
            hand.Swing(0.08f, 0.3);
            hand.Gate = true;
            hand.Hold(0.2);
            Assert.That(hand.Notes, Is.Empty);

            hand.Swing(-0.08f, 0.3);
            Assert.That(hand.Notes, Has.Count.EqualTo(1));
        }

        // ---- the chord ----

        [Test]
        public void WithNoChordEverHeld_TheRightHandStillPlays_FromTheFallbackChord()
        {
            var hand = new Hand(Make());
            hand.Angle = 30f;
            hand.Hold(0.2);
            hand.Swing(0.08f, 0.3);

            Assert.That(hand.Notes, Has.Count.EqualTo(1));
            Assert.That(hand.Notes[0].Pitch.Midi, Is.EqualTo(84)); // a Cmaj7 tone
        }

        [Test]
        public void ReleasingToNoChord_KeepsTheLastChord()
        {
            PalmSwingPipeline pipeline = Make();
            pipeline.OnChordChanged(Chord.Dm7);
            pipeline.OnChordChanged(null);

            Assert.That(pipeline.Chord, Is.EqualTo(Chord.Dm7));
        }

        [Test]
        public void AChordChange_RepitchesTheNextNote_ToTheNewChordsTones()
        {
            PalmSwingPipeline pipeline = Make();
            var hand = new Hand(pipeline);
            hand.Angle = 30f;
            hand.Hold(0.2);
            hand.Swing(0.08f, 0.3);       // 84

            pipeline.OnChordChanged(Chord.G7); // 84 is not a G7 tone: snaps to 83, so Up is 86 (D)
            hand.Swing(-0.08f, 0.3);

            Assert.That(hand.Pitches, Is.EqualTo(new[] { 84, 86 }));
        }

        // ---- phrase reset ----

        [Test]
        public void BeginTask_ResetsWithoutPlaying_AndTheNextSwingStartsFromTheStartPitch()
        {
            PalmSwingPipeline pipeline = Make();
            var resets = new List<(PhraseResetReason, int)>();
            pipeline.PhraseReset += (reason, pitch) => resets.Add((reason, pitch.Midi));
            var hand = new Hand(pipeline);
            hand.Angle = 30f;
            hand.Hold(0.2);

            pipeline.BeginTask(PhraseStart.FromMidi(72));

            Assert.That(hand.Notes, Is.Empty);
            Assert.That(resets, Is.EqualTo(new[] { (PhraseResetReason.Task, 72) }));
            hand.Swing(0.08f, 0.3);
            Assert.That(hand.Pitches, Is.EqualTo(new[] { 74 }));
        }

        [Test]
        public void ATaskWithNoStartPitch_ResetsToTheChordRootNearTheMiddle()
        {
            PalmSwingPipeline pipeline = Make();
            pipeline.BeginTask(PhraseStart.ChordRoot);

            Assert.That(pipeline.CursorPitch.Midi, Is.EqualTo(84));
        }

        [Test]
        public void ATaskStartingOutsideTheSet_SnapsToTheNearestMember()
        {
            PalmSwingPipeline pipeline = Make();

            pipeline.BeginTask(PhraseStart.FromMidi(75));
            Assert.That(pipeline.CursorPitch.Midi, Is.EqualTo(74)); // 74 and 76 tie: the lower

            pipeline.BeginTask(PhraseStart.FromMidi(100));
            Assert.That(pipeline.CursorPitch.Midi, Is.EqualTo(88));
        }

        [Test]
        public void OnRest_FiresAfterTheRestTime_AndNotBefore_AndOnlyOnce()
        {
            PalmSwingPipeline pipeline = Make();
            var reasons = new List<PhraseResetReason>();
            pipeline.PhraseReset += (reason, _) => reasons.Add(reason);
            var hand = new Hand(pipeline);
            hand.Angle = 30f;
            hand.Hold(0.2);
            hand.Swing(0.08f, 0.3); // a note: 84
            double crossing = hand.Notes[0].Time;

            hand.HoldUntil(crossing + 1.45);
            Assert.That(reasons, Is.Empty);

            hand.HoldUntil(crossing + 1.55);
            Assert.That(reasons, Is.EqualTo(new[] { PhraseResetReason.Rest }));
            Assert.That(pipeline.CursorPitch.Midi, Is.EqualTo(84));

            hand.HoldUntil(crossing + 6.0);
            Assert.That(reasons, Has.Count.EqualTo(1));
        }

        [Test]
        public void OnRest_ReturnsToTheTasksStartPitch_NotJustTheRoot()
        {
            PalmSwingPipeline pipeline = Make();
            var hand = new Hand(pipeline);
            pipeline.BeginTask(PhraseStart.FromMidi(72));
            hand.Angle = 30f;
            hand.Hold(0.2);
            hand.Swing(0.08f, 0.3); // 74
            hand.HoldUntil(hand.Notes[0].Time + 1.6);

            Assert.That(pipeline.CursorPitch.Midi, Is.EqualTo(72));
        }

        [Test]
        public void OnBar_ResetsAtTheBarLine_ToTheTasksStart()
        {
            PalmSwingPipeline pipeline = Make(p => p.ResetPolicy = PhraseResetPolicy.OnBar);
            var reasons = new List<PhraseResetReason>();
            pipeline.PhraseReset += (reason, _) => reasons.Add(reason);
            var hand = new Hand(pipeline);
            pipeline.BeginTask(PhraseStart.FromMidi(76));
            Assert.That(reasons, Is.Empty); // OnTask is not in this policy
            hand.Angle = 30f;
            hand.Hold(0.2);
            hand.Swing(0.08f, 0.3); // 79

            pipeline.NotifyBarLine();

            Assert.That(reasons, Is.EqualTo(new[] { PhraseResetReason.Bar }));
            Assert.That(pipeline.CursorPitch.Midi, Is.EqualTo(76));
        }

        [Test]
        public void ThePolicyFlags_GateEachTrigger()
        {
            foreach ((PhraseResetPolicy policy, int onTask, int onBar) in new[]
            {
                (PhraseResetPolicy.Never, 0, 0),
                (PhraseResetPolicy.OnTask, 1, 0),
                (PhraseResetPolicy.OnBar, 0, 1),
                (PhraseResetPolicy.OnTask | PhraseResetPolicy.OnBar, 1, 1),
            })
            {
                PalmSwingPipeline pipeline = Make(p => p.ResetPolicy = policy);
                int taskResets = 0, barResets = 0;
                pipeline.PhraseReset += (reason, _) =>
                {
                    if (reason == PhraseResetReason.Task)
                    {
                        taskResets++;
                    }
                    else if (reason == PhraseResetReason.Bar)
                    {
                        barResets++;
                    }
                };

                pipeline.BeginTask(PhraseStart.FromMidi(72));
                pipeline.NotifyBarLine();

                Assert.That((taskResets, barResets), Is.EqualTo((onTask, onBar)), policy.ToString());
            }
        }

        [Test]
        public void RepeatedUpUp_AcrossTasks_NeverReachesTheLimit_WhenEachTaskStartsLow()
        {
            PalmSwingPipeline pipeline = Make();
            var hand = new Hand(pipeline);
            hand.Angle = 30f;
            for (int task = 0; task < 5; task++)
            {
                pipeline.BeginTask(PhraseStart.FromMidi(72));
                hand.Hold(0.2);
                hand.Swing(0.08f, 0.3);
                hand.Swing(-0.08f, 0.3);
            }

            Assert.That(hand.Notes, Has.Count.EqualTo(10));
            Assert.That(hand.Notes.Any(n => n.AtLimit), Is.False);
            Assert.That(hand.Pitches, Is.EqualTo(new[] { 74, 76, 74, 76, 74, 76, 74, 76, 74, 76 }));
        }

        [Test]
        public void WithoutResets_RepeatedUpUp_ClimbsToTheLimit()
        {
            PalmSwingPipeline pipeline = Make(p => p.ResetPolicy = PhraseResetPolicy.Never);
            var hand = new Hand(pipeline);
            hand.Angle = 30f;
            hand.Hold(0.2);
            for (int i = 0; i < 5; i++)
            {
                hand.Swing(0.08f, 0.3);
                hand.Swing(-0.08f, 0.3);
            }

            Assert.That(hand.Notes.Any(n => n.AtLimit), Is.True);
        }

        // ---- register zones ----

        private static int[] TwoUpSwingsAtHeight(float height)
        {
            PalmSwingPipeline pipeline = Make(Zones);
            var hand = new Hand(pipeline);
            pipeline.BeginTask(PhraseStart.FromMidi(76));
            hand.Angle = 30f;
            hand.Height = height;
            hand.Hold(0.3);
            hand.Swing(0.08f, 0.3);
            hand.Swing(-0.08f, 0.3);
            return hand.Pitches;
        }

        [Test]
        public void HeightZones_ShiftTheSameShapeByOctaves_TwentyFourSemitonesBetweenLowAndHigh()
        {
            int[] middle = TwoUpSwingsAtHeight(0f);
            int[] low = TwoUpSwingsAtHeight(-0.12f);
            int[] high = TwoUpSwingsAtHeight(0.12f);

            Assert.That(middle, Is.EqualTo(new[] { 79, 83 }));
            Assert.That(low, Is.EqualTo(middle.Select(m => m - 12)));
            Assert.That(high, Is.EqualTo(middle.Select(m => m + 12)));
            Assert.That(high[0] - low[0], Is.EqualTo(24));
            Assert.That(high[1] - low[1], Is.EqualTo(24));
        }

        [Test]
        public void AZoneChangeJustBeforeTheCrossing_DoesNotRepitchTheNote_AndNothingSoundsOnItsOwn()
        {
            PalmSwingPipeline pipeline = Make(Zones);
            var notes = new List<PalmSwingNote>();
            for (int i = 0; i <= 55; i++)
            {
                float x = i <= 50 ? -0.08f : i == 55 ? 0.04f : -0.08f + (i - 50) * 0.02f;
                float height = i >= 51 ? 0.12f : 0f; // the hand rises into the high zone as it swings
                if (pipeline.Update(new PalmSwingSample(i * 0.01, true, 30f, x, height), out PalmSwingNote n))
                {
                    notes.Add(n);
                }
            }

            Assert.That(notes, Has.Count.EqualTo(1));
            Assert.That(notes[0].Zone, Is.EqualTo(1));          // read from before the rise
            Assert.That(notes[0].Pitch.Midi, Is.EqualTo(84));   // no octave shift

            for (int i = 56; i < 100; i++) // the hand stays high and still: no note, nothing re-pitched
            {
                Assert.That(pipeline.Update(new PalmSwingSample(i * 0.01, true, 30f, 0.04f, 0.12f), out _), Is.False);
            }

            Assert.That(pipeline.Zone, Is.EqualTo(2));
        }

        [Test]
        public void WithRegisterSourceNone_HandHeightHasNoEffectAtAll()
        {
            int[] Run(bool jitter)
            {
                var hand = new Hand(Make()) { JitterHeight = jitter };
                hand.Angle = 30f;
                hand.Hold(0.3);
                hand.Swing(0.08f, 0.3);
                hand.Swing(-0.08f, 0.3);
                Assert.That(hand.Notes.All(n => n.Zone == -1), Is.True);
                return hand.Pitches;
            }

            Assert.That(Run(jitter: true), Is.EqualTo(Run(jitter: false)));
        }

        [Test]
        public void TheOutputPitch_IsClampedToTheSafeRange()
        {
            PalmSwingPipeline pipeline = Make(p =>
            {
                p.Register = RegisterSource.HandHeightZones;
                p.SafeMaxMidi = 90;
            });
            var hand = new Hand(pipeline);
            pipeline.BeginTask(PhraseStart.FromMidi(76));
            hand.Angle = 30f;
            hand.Height = 0.12f; // +12 would give 91
            hand.Hold(0.3);
            hand.Swing(0.08f, 0.3);

            Assert.That(hand.Pitches, Is.EqualTo(new[] { 90 }));
        }

        // ---- smoothing ----

        [Test]
        public void WithSmoothing_TheScriptedPathStillPlaysTheSamePitches()
        {
            var hand = new Hand(Make(p => p.SmoothingSeconds = 0.04));

            hand.Angle = 30f;
            hand.Hold(0.3);
            hand.Swing(0.08f, 0.3);
            hand.Swing(-0.08f, 0.3);
            hand.Angle = -35f;
            hand.Hold(0.3);
            hand.Swing(0.08f, 0.3);

            Assert.That(hand.Pitches, Is.EqualTo(new[] { 84, 86, 84 }));
        }

        [Test]
        public void ASingleFrameSpikeAcrossTheLine_SoundsWithoutSmoothing_ButNotWithIt()
        {
            int Run(double smoothing)
            {
                var hand = new Hand(Make(p => p.SmoothingSeconds = smoothing));
                hand.Angle = 30f;
                hand.Hold(0.3);
                hand.X = 0.08f;
                hand.Tick(); // one bad tracking frame on the far side
                hand.X = -0.08f;
                hand.Hold(0.3);
                return hand.Notes.Count;
            }

            Assert.That(Run(0.0), Is.EqualTo(1));
            Assert.That(Run(0.04), Is.Zero);
        }

        [Test]
        public void ASingleFrameTiltSpike_ReadsUpWithoutSmoothing_ButNotWithIt()
        {
            StepDirection Run(double smoothing)
            {
                PalmSwingPipeline pipeline = Make(p => p.SmoothingSeconds = smoothing);
                var hand = new Hand(pipeline);
                hand.Angle = 0f;
                hand.Hold(0.3);
                hand.Angle = 40f;
                hand.Tick(); // one bad tracking frame
                return pipeline.HeldDirection;
            }

            Assert.That(Run(0.0), Is.EqualTo(StepDirection.Up));
            Assert.That(Run(0.04), Is.EqualTo(StepDirection.Same));
        }
    }
}
