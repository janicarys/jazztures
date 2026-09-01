using System;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Sits between the domain and the sinks and enforces the §3.8 rule: the learning
    /// mode gates <b>only</b> the audible sink. Everything — every attempt, sounded or
    /// not — always reaches the unconditional sink (telemetry, OSC, presentation
    /// channels). "Silent-but-logged" is a first-class state.
    /// </summary>
    public sealed class ModeGatedNoteSink : INoteSink
    {
        private readonly INoteSink _audible;
        private readonly INoteSink _unconditional;

        private LearningMode _mode = LearningMode.ComposeOnTheFly;
        private bool _gestureCorrect;

        /// <param name="audible">The sink that produces sound the learner hears (the sampler).</param>
        /// <param name="unconditional">
        /// The sink that must see every note regardless of mode — telemetry, OSC, the
        /// presentation channels. Usually a composite.
        /// </param>
        public ModeGatedNoteSink(INoteSink audible, INoteSink unconditional)
        {
            _audible = audible ?? throw new ArgumentNullException(nameof(audible));
            _unconditional = unconditional ?? throw new ArgumentNullException(nameof(unconditional));
        }

        public LearningMode Mode => _mode;

        public void SetMode(LearningMode mode) => _mode = mode;

        /// <summary>
        /// Whether the learner's current gesture is correct — only consulted in
        /// <see cref="LearningMode.TryYourself"/>, where user audio is the reward for a match.
        /// </summary>
        public void SetGestureCorrect(bool correct) => _gestureCorrect = correct;

        public void Send(in NoteEvent note)
        {
            _unconditional.Send(note);

            if (ShouldSound(note))
            {
                _audible.Send(note);
            }
        }

        private bool ShouldSound(in NoteEvent note)
        {
            // System-played demonstration / backing is always audible.
            if (note.Channel == MidiChannel.Accompaniment)
            {
                return true;
            }

            return ModePolicy.For(_mode).UserAudio switch
            {
                UserAudioGate.Always => true,
                UserAudioGate.Never => false,
                UserAudioGate.OnlyWhenGestureCorrect => _gestureCorrect,
                _ => false,
            };
        }
    }
}
