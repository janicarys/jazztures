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
    ///
    /// <para>
    /// <b>A hand the lesson does not use is never heard.</b> <see cref="SetActiveHands"/> says which hands the
    /// running lesson is about (§3.9); the learner's notes from the other — the left hand's chord voicing, or the
    /// right hand's melody — are dropped from the audible sink in every mode, exactly as the mode drops them, and
    /// still reach the unconditional sink. Recognition is untouched (§3.8): the hand is still seen and its strikes
    /// are still logged, they are only silent. The system's own playback (<see cref="MidiChannel.Accompaniment"/>)
    /// is never the learner's and is never gated.
    /// </para>
    /// </summary>
    public sealed class ModeGatedNoteSink : INoteSink
    {
        private readonly INoteSink _audible;
        private readonly INoteSink _unconditional;
        private readonly IMusicalClock _clock;

        private LearningMode _mode = LearningMode.ComposeOnTheFly;
        private ActiveHands _hands = ActiveHands.Both; // no lesson: the free instrument, both hands heard
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

        /// <summary>The hands the learner is heard on. <see cref="ActiveHands.Both"/> unless a lesson says otherwise.</summary>
        public ActiveHands Hands => _hands;

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
        /// Which hands the running lesson uses (§3.9). The learner's notes from a hand not in
        /// <paramref name="hands"/> stop sounding at once — including a chord already ringing — and sound again
        /// if the hand is switched back on. Set <see cref="ActiveHands.Both"/> when no lesson is running.
        /// </summary>
        public void SetActiveHands(ActiveHands hands)
        {
            if (_hands == hands)
            {
                return;
            }

            _hands = hands;
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
                TrackHarmony(note, open && HandInUse(ActiveHands.Left));
            }
            else if (open && (note.Channel != MidiChannel.Melody || HandInUse(ActiveHands.Right)))
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
            if (GateOpen() && HandInUse(ActiveHands.Left))
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

        private bool HandInUse(ActiveHands hand) => (_hands & hand) != 0;

        private bool GateOpen() => ModePolicy.For(_mode).UserAudio switch
        {
            UserAudioGate.Always => true,
            UserAudioGate.Never => false,
            UserAudioGate.OnlyWhenGestureCorrect => _gestureCorrect,
            _ => false,
        };
    }
}
