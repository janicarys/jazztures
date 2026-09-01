using System;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Harmony
{
    /// <summary>
    /// Turns the left hand's held <see cref="ChordFunction"/> into sounding harmony
    /// (CLAUDE.md §3.2). On a chord change it releases the outgoing voicing and sounds
    /// the incoming one, in that order, then announces the change so the melody engine
    /// and presentation can react.
    ///
    /// <para>
    /// The input is already-confirmed function state — debounce, hysteresis, confidence
    /// gating and the tracking-loss "sustain, do not release" policy (§3.4, §3.5) live
    /// upstream in the gesture layer and simply drive <see cref="SetHeldFunction"/>.
    /// </para>
    /// </summary>
    public sealed class HarmonyEngine
    {
        /// <summary>
        /// Fixed velocity for chord voicings. `[TUNABLE]` — not called out in the thesis;
        /// mirror any change in <c>Docs/CALIBRATION.md</c>.
        /// </summary>
        public const byte DefaultVoicingVelocity = 80;

        private readonly IMusicalClock _clock;
        private readonly INoteSink _sink;
        private readonly ProgressionState _progression = new ProgressionState();
        private readonly byte _voicingVelocity;

        private ChordVoicing? _sounding;

        public HarmonyEngine(IMusicalClock clock, INoteSink sink, byte voicingVelocity = DefaultVoicingVelocity)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _voicingVelocity = voicingVelocity;
            _progression.Changed += OnProgressionChanged;
        }

        /// <summary>The function currently held, or null.</summary>
        public ChordFunction? HeldFunction => _progression.Active;

        /// <summary>The chord currently sounding, or null.</summary>
        public Chord? ActiveChord => _progression.ActiveChord;

        /// <summary>The voicing currently sounding, or null.</summary>
        public ChordVoicing? SoundingVoicing => _sounding;

        /// <summary>Raised after the note events for a chord change have been sent.</summary>
        public event Action<ChordChange>? ChordChanged;

        /// <summary>
        /// Set the held function (null = release). A no-op if it matches the current
        /// state. Returns true if the harmony changed.
        /// </summary>
        public bool SetHeldFunction(ChordFunction? function) =>
            function.HasValue ? _progression.Hold(function.Value) : _progression.Release();

        private void OnProgressionChanged(ChordChange change)
        {
            double now = _clock.Now;

            if (_sounding is { } outgoing)
            {
                foreach (Pitch pitch in outgoing)
                {
                    _sink.Send(NoteEvent.Off(pitch, now, MidiChannel.Harmony, Handedness.Left));
                }

                _sounding = null;
            }

            if (change.CurrentChord is { } chord)
            {
                ChordVoicing voicing = Voicing.Close(chord);
                foreach (Pitch pitch in voicing)
                {
                    _sink.Send(NoteEvent.On(
                        pitch, _voicingVelocity, now, MidiChannel.Harmony, Handedness.Left));
                }

                _sounding = voicing;
            }

            ChordChanged?.Invoke(change);
        }
    }
}
