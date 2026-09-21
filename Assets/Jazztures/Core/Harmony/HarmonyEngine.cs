using System;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Harmony
{
    /// <summary>
    /// Turns the left hand's held <see cref="ChordFunction"/> into sounding harmony
    /// (CLAUDE.md §3.2, revised by ADR-0025). <see cref="SetHeldFunction"/> only
    /// <b>selects</b> — it updates <see cref="ActiveChord"/> (so the melody engine can
    /// recompute its chord-tone set and presentation can react) but is silent by itself.
    /// <see cref="Strike"/> is the separate, explicit event that actually sounds the
    /// selected chord, struck-piano style with a fixed sustain — the same model
    /// <c>MelodyEngine</c> already uses (§3.3), now applied symmetrically to the left hand.
    ///
    /// <para>
    /// <b>Why not sound on selection:</b> a comping pianist re-articulates a held chord on
    /// the beat — the same function, struck again — which the pre-ADR-0025 design (sound
    /// on <see cref="ProgressionState.Changed"/>) could not express at all, since holding
    /// the same function twice is a documented no-op (<see cref="ProgressionState.Hold"/>).
    /// That gap is the measured root cause of the left-hand timing/fluidity problem
    /// ADR-0025 fixes: every re-articulation had to release-then-reselect, paying a full
    /// gesture-confirmation round trip per strike.
    /// </para>
    ///
    /// <para>
    /// Re-striking cuts whatever is still ringing before sounding the new voicing — a
    /// deliberate re-attack — but merely <i>selecting</i> a different function does not
    /// cut a chord already ringing from an earlier strike; like a melody note, it decays on
    /// its own timer (§3.3). An explicit release (selecting no function) does cut it
    /// immediately, mirroring a pianist lifting the hand off the keys.
    /// </para>
    ///
    /// <para>
    /// The input to both is already-confirmed/-detected state — debounce, hysteresis,
    /// confidence gating and the tracking-loss "sustain, do not release" policy (§3.4,
    /// §3.5) live upstream in <see cref="Jazztures.Core.Gesture.GestureInterpreter"/> and
    /// <see cref="Jazztures.Core.Gesture.ChordStrikeDetector"/>.
    /// </para>
    /// </summary>
    public sealed class HarmonyEngine
    {
        /// <summary>
        /// Fixed velocity for chord voicings when no fingertip/strike speed is available
        /// (kept for callers that still want a constant-velocity chord). `[TUNABLE]` — not
        /// called out in the thesis; mirror any change in <c>Docs/CALIBRATION.md</c>.
        /// </summary>
        public const byte DefaultVoicingVelocity = 80;

        /// <summary>
        /// Fixed chord ring length in seconds — the harmony-side counterpart of
        /// <c>MelodyEngine.DefaultSustainSeconds</c>. `[OPEN]` (ADR-0025 / CALIBRATION.md):
        /// the thesis does not specify it; long enough to read as a sustained chord at the
        /// §3.6 tempo range, short enough to have decayed before the same chord is
        /// typically re-struck.
        /// </summary>
        // TODO(OPEN): fixed harmony sustain — measure a musically sensible value at M8.
        public const double DefaultSustainSeconds = 1.5;

        private readonly IMusicalClock _clock;
        private readonly INoteSink _sink;
        private readonly ProgressionState _progression = new ProgressionState();
        private readonly double _sustainSeconds;

        private ChordVoicing? _sounding;
        private double _soundingDueOff;

        public HarmonyEngine(IMusicalClock clock, INoteSink sink, double sustainSeconds = DefaultSustainSeconds)
        {
            if (double.IsNaN(sustainSeconds) || double.IsInfinity(sustainSeconds) || sustainSeconds <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sustainSeconds), sustainSeconds, "Must be finite and positive.");
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _sustainSeconds = sustainSeconds;
            _progression.Changed += OnProgressionChanged;
        }

        /// <summary>The function currently selected, or null.</summary>
        public ChordFunction? HeldFunction => _progression.Active;

        /// <summary>The chord currently selected (may or may not be sounding), or null.</summary>
        public Chord? ActiveChord => _progression.ActiveChord;

        /// <summary>The voicing currently ringing from the last strike, or null.</summary>
        public ChordVoicing? SoundingVoicing => _sounding;

        /// <summary>Raised on every selection change. Carries no timing guarantee about note events — see the type docs.</summary>
        public event Action<ChordChange>? ChordChanged;

        /// <summary>
        /// Select the held function (null = release). Silent by itself — updates
        /// <see cref="ActiveChord"/> and raises <see cref="ChordChanged"/>, but sounds
        /// nothing (see type docs). A no-op if it matches the current selection. Returns
        /// true if the selection changed.
        /// </summary>
        public bool SetHeldFunction(ChordFunction? function) =>
            function.HasValue ? _progression.Hold(function.Value) : _progression.Release();

        /// <summary>
        /// Sound the currently selected chord: cut whatever is still ringing, then sound
        /// this voicing at <paramref name="velocity"/> with a fixed sustain. A no-op — no
        /// notes, returns false — if nothing is selected; you cannot strike silence.
        /// </summary>
        public bool Strike(byte velocity)
        {
            if (_progression.ActiveChord is not { } chord)
            {
                return false;
            }

            double now = _clock.Now;
            StopSounding(now);

            ChordVoicing voicing = Voicing.Close(chord);
            foreach (Pitch pitch in voicing)
            {
                _sink.Send(NoteEvent.On(pitch, velocity, now, MidiChannel.Harmony, Handedness.Left));
            }

            _sounding = voicing;
            _soundingDueOff = now + _sustainSeconds;
            return true;
        }

        /// <summary>Send the note-off for the sounding voicing once its sustain has elapsed. Call every frame.</summary>
        public void Tick()
        {
            if (_sounding.HasValue && _clock.Now >= _soundingDueOff)
            {
                StopSounding(_clock.Now);
            }
        }

        private void OnProgressionChanged(ChordChange change)
        {
            if (change.CurrentChord is null)
            {
                // An explicit release is a deliberate stop — lifting the hand off the keys
                // — unlike merely selecting a different chord, which lets a still-ringing
                // strike decay on its own timer (§3.3's rule, applied here too).
                StopSounding(_clock.Now);
            }

            ChordChanged?.Invoke(change);
        }

        private void StopSounding(double now)
        {
            if (_sounding is { } outgoing)
            {
                foreach (Pitch pitch in outgoing)
                {
                    _sink.Send(NoteEvent.Off(pitch, now, MidiChannel.Harmony, Handedness.Left));
                }

                _sounding = null;
            }
        }
    }
}
