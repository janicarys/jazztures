using System;
using System.Collections.Generic;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Sits between the domain and the sinks and enforces the §3.8 rule: the learning
    /// mode gates <b>only</b> the audible sink. Everything — every attempt, sounded or
    /// not — always reaches the unconditional sink (telemetry, OSC, presentation
    /// channels). "Silent-but-logged" is a first-class state.
    ///
    /// <para>
    /// The left-hand chord voicing is <b>sustained</b> — one note-on that rings until the
    /// next chord change (§3.2). So when the gate opens mid-chord (a <see cref="LearningMode"/>
    /// switch, or the learner's gesture becoming correct in
    /// <see cref="UserAudioGate.OnlyWhenGestureCorrect"/>) this sink re-sounds whatever
    /// harmony is already held — otherwise a note-on that was dropped while the gate was
    /// shut stays silent until the learner changes chord, which reads as broken audio.
    /// Melody is a struck model (§3.3) — transient, not tracked.
    /// </para>
    /// </summary>
    public sealed class ModeGatedNoteSink : INoteSink
    {
        private readonly INoteSink _audible;
        private readonly INoteSink _unconditional;
        private readonly IMusicalClock _clock;

        private LearningMode _mode = LearningMode.ComposeOnTheFly;
        private bool _gestureCorrect;

        // Sustained harmony the learner is holding: MIDI note -> the note-on last seen for
        // it. Small (one close voicing), pre-sized so Send() never allocates.
        private readonly Dictionary<int, NoteEvent> _heldHarmony = new Dictionary<int, NoteEvent>(8);
        private readonly HashSet<int> _harmonyAudible = new HashSet<int>();

        /// <param name="audible">The sink that produces sound the learner hears (the sampler).</param>
        /// <param name="unconditional">
        /// The sink that must see every note regardless of mode — telemetry, OSC, the
        /// presentation channels. Usually a composite.
        /// </param>
        /// <param name="clock">Stamps the re-sounded / released note events for held harmony.</param>
        public ModeGatedNoteSink(INoteSink audible, INoteSink unconditional, IMusicalClock clock)
        {
            _audible = audible ?? throw new ArgumentNullException(nameof(audible));
            _unconditional = unconditional ?? throw new ArgumentNullException(nameof(unconditional));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public LearningMode Mode => _mode;

        public void SetMode(LearningMode mode)
        {
            if (_mode == mode)
            {
                return;
            }

            _mode = mode;
            SyncHeldHarmony();
        }

        /// <summary>
        /// Whether the learner's current gesture is correct — consulted only under
        /// <see cref="UserAudioGate.OnlyWhenGestureCorrect"/> (Try Yourself, and Gesture
        /// Learning when the lesson gates on it), where user audio is the reward for a match.
        /// </summary>
        public void SetGestureCorrect(bool correct)
        {
            if (_gestureCorrect == correct)
            {
                return;
            }

            _gestureCorrect = correct;
            SyncHeldHarmony();
        }

        public void Send(in NoteEvent note)
        {
            _unconditional.Send(note);

            // System-played demonstration / backing is never gated (§3.8).
            if (note.Channel == MidiChannel.Accompaniment)
            {
                _audible.Send(note);
                return;
            }

            bool open = GateOpen();

            if (note.Channel == MidiChannel.Harmony)
            {
                TrackHarmony(note, open);
            }
            else if (open)
            {
                _audible.Send(note);
            }
        }

        private void TrackHarmony(in NoteEvent note, bool open)
        {
            int key = note.Pitch.Midi;

            if (note.Kind == NoteEventKind.On)
            {
                _heldHarmony[key] = note;
                if (open && _harmonyAudible.Add(key))
                {
                    _audible.Send(note);
                }
            }
            else
            {
                _heldHarmony.Remove(key);
                if (_harmonyAudible.Remove(key))
                {
                    _audible.Send(note); // only release what we actually sounded
                }
            }
        }

        /// <summary>Make the audible sink match the gate for every held harmony note.</summary>
        private void SyncHeldHarmony()
        {
            if (GateOpen())
            {
                foreach (KeyValuePair<int, NoteEvent> held in _heldHarmony)
                {
                    if (_harmonyAudible.Add(held.Key))
                    {
                        NoteEvent on = held.Value;
                        _audible.Send(NoteEvent.On(
                            on.Pitch, on.Velocity, _clock.Now, on.Channel, on.Source));
                    }
                }

                return;
            }

            if (_harmonyAudible.Count == 0)
            {
                return;
            }

            foreach (int key in _harmonyAudible)
            {
                if (_heldHarmony.TryGetValue(key, out NoteEvent on))
                {
                    _audible.Send(NoteEvent.Off(on.Pitch, _clock.Now, on.Channel, on.Source));
                }
            }

            _harmonyAudible.Clear();
        }

        private bool GateOpen() => ModePolicy.For(_mode).UserAudio switch
        {
            UserAudioGate.Always => true,
            UserAudioGate.Never => false,
            UserAudioGate.OnlyWhenGestureCorrect => _gestureCorrect,
            _ => false,
        };
    }
}
