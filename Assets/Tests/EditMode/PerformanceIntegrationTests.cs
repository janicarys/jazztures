using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Tests.EditMode.TestSupport;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode
{
    /// <summary>
    /// Phase 2 / M1 "done when": a scripted gesture + fingertip stream driven through
    /// <see cref="HarmonyEngine"/> + <see cref="MelodyEngine"/> on a
    /// <see cref="VirtualClock"/> produces an exact, ordered <see cref="NoteEvent"/>
    /// stream. Nothing here touches Unity.
    /// </summary>
    public class PerformanceIntegrationTests
    {
        [Test]
        public void ScriptedIiViWithMelody_ProducesTheExactNoteStream()
        {
            var clock = new VirtualClock();
            var sink = new RecordingNoteSink();
            var harmony = new HarmonyEngine(clock, sink);
            var melody = new MelodyEngine(clock, sink, sustainSeconds: 0.5);
            harmony.ChordChanged += melody.OnChordChanged;

            const byte h = HarmonyEngine.DefaultVoicingVelocity;
            byte m = VelocityCurve.FromSpeed(1.0f);

            // t=0.0 — hold ii (Dm7)
            harmony.SetHeldFunction(ChordFunction.Two);

            // t=0.5 — strike the lower-octave root (Dm7 slot 0 = D5 = 74)
            clock.SetNow(0.5);
            melody.TriggerTarget(0, 1.0f);

            // t=1.0 — the D5 note's sustain elapses; strike the upper-octave root (86)
            clock.SetNow(1.0);
            melody.Tick();
            melody.TriggerTarget(5, 1.0f);

            // t=2.0 — flush the 86, then move to V (G7)
            clock.SetNow(2.0);
            melody.Tick();
            harmony.SetHeldFunction(ChordFunction.Five);

            // t=2.5 — strike G7 slot 0 (G5 = 79)
            clock.SetNow(2.5);
            melody.TriggerTarget(0, 1.0f);

            // t=3.0 — flush the 79, then release everything
            clock.SetNow(3.0);
            melody.Tick();
            harmony.SetHeldFunction(null);

            var expected = new List<NoteEvent>
            {
                NoteEvent.On(new Pitch(50), h, 0.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.On(new Pitch(53), h, 0.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.On(new Pitch(57), h, 0.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.On(new Pitch(60), h, 0.0, MidiChannel.Harmony, Handedness.Left),

                NoteEvent.On(new Pitch(74), m, 0.5, MidiChannel.Melody, Handedness.Right),

                NoteEvent.Off(new Pitch(74), 1.0, MidiChannel.Melody, Handedness.Right),
                NoteEvent.On(new Pitch(86), m, 1.0, MidiChannel.Melody, Handedness.Right),

                NoteEvent.Off(new Pitch(86), 2.0, MidiChannel.Melody, Handedness.Right),

                NoteEvent.Off(new Pitch(50), 2.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.Off(new Pitch(53), 2.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.Off(new Pitch(57), 2.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.Off(new Pitch(60), 2.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.On(new Pitch(55), h, 2.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.On(new Pitch(59), h, 2.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.On(new Pitch(62), h, 2.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.On(new Pitch(65), h, 2.0, MidiChannel.Harmony, Handedness.Left),

                NoteEvent.On(new Pitch(79), m, 2.5, MidiChannel.Melody, Handedness.Right),

                NoteEvent.Off(new Pitch(79), 3.0, MidiChannel.Melody, Handedness.Right),

                NoteEvent.Off(new Pitch(55), 3.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.Off(new Pitch(59), 3.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.Off(new Pitch(62), 3.0, MidiChannel.Harmony, Handedness.Left),
                NoteEvent.Off(new Pitch(65), 3.0, MidiChannel.Harmony, Handedness.Left),
            };

            CollectionAssert.AreEqual(expected, sink.Events);
        }

        [Test]
        public void MelodyStaysConstrainedToTheHeldChordsTones_AcrossEveryTransition()
        {
            var clock = new VirtualClock();
            var sink = new RecordingNoteSink();
            var harmony = new HarmonyEngine(clock, sink);
            var melody = new MelodyEngine(clock, sink);
            harmony.ChordChanged += melody.OnChordChanged;

            foreach (ChordFunction function in new[]
            {
                ChordFunction.One, ChordFunction.Two, ChordFunction.Five,
                ChordFunction.One, ChordFunction.Five, ChordFunction.Two,
            })
            {
                harmony.SetHeldFunction(function);
                Chord chord = harmony.ActiveChord!.Value;

                for (int slot = 0; slot < ChordToneSet.TargetCount; slot++)
                {
                    clock.Advance(0.1);
                    sink.Clear();
                    melody.TriggerTarget(slot, 1.0f);

                    NoteEvent on = sink.On(MidiChannel.Melody)[0];
                    Assert.That(
                        chord.ClassOf(ChordToneSet.DegreeAt(slot)),
                        Is.EqualTo(on.Pitch.Class),
                        $"{chord} slot {slot}");
                }
            }
        }
    }
}
