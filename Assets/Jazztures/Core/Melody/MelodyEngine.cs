using System;
using Jazztures.Core.Harmony;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The right hand's melody (CLAUDE.md §3.3). Holds the <see cref="ActiveChordToneSet"/>
    /// — derived state, recomputed on every chord change and never cached across one —
    /// and turns a fingertip entering a target into a note.
    ///
    /// <para>
    /// Struck-piano model: a note-on is always paired with a note-off a fixed sustain
    /// later, so a stuck note is impossible by construction, and a dropped tracking
    /// frame cannot leave one ringing (§3.5). Chord changes do <b>not</b> cut notes that
    /// are already sounding — they decay on their own timer.
    /// </para>
    ///
    /// <para>Call <see cref="Tick"/> every frame to flush due note-offs.</para>
    /// </summary>
    public sealed class MelodyEngine
    {
        /// <summary>
        /// Fixed note length in seconds. `[OPEN]` (CLAUDE.md §3.3 / CALIBRATION.md) — the
        /// thesis does not specify it; this is a placeholder to be pilot-calibrated.
        /// </summary>
        // TODO(OPEN): fixed melody sustain — measure a musically sensible value at M8.
        public const double DefaultSustainSeconds = 0.5;

        /// <summary>Minimum fingertip speed to fire a note. `[TUNABLE]` (§3.3).</summary>
        public const float EntryVelocityGateMetresPerSecond = VelocityCurve.MinSpeed;

        /// <summary>Per-target minimum interval between triggers. `[TUNABLE]` (§3.3).</summary>
        public const double RetriggerCooldownSeconds = 0.080;

        /// <summary>
        /// Most notes that may sound at once. Well under the audio voice pool (§4.2). If
        /// exceeded, the oldest note is released early to make room.
        /// </summary>
        public const int MaxPolyphony = 16;

        private readonly IMusicalClock _clock;
        private readonly INoteSink _sink;
        private readonly double _sustainSeconds;

        private readonly double[] _lastTriggerTime = new double[ChordToneSet.TargetCount];
        private readonly PendingOff[] _pending = new PendingOff[MaxPolyphony];

        private ChordToneSet? _activeSet;

        public MelodyEngine(
            IMusicalClock clock,
            INoteSink sink,
            double sustainSeconds = DefaultSustainSeconds)
        {
            if (double.IsNaN(sustainSeconds) || double.IsInfinity(sustainSeconds) || sustainSeconds <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sustainSeconds), sustainSeconds, "Must be finite and positive.");
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _sustainSeconds = sustainSeconds;
            ClearCooldowns();
        }

        /// <summary>
        /// The current right-hand targets, or null when no chord is held. Recomputed from
        /// each chord change and from nothing else (§3.3).
        /// </summary>
        public ChordToneSet? ActiveChordToneSet => _activeSet;

        /// <summary>Notes currently sounding and awaiting their scheduled note-off.</summary>
        public int PendingNoteCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _pending.Length; i++)
                {
                    if (_pending[i].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Wire this to <see cref="HarmonyEngine.ChordChanged"/>.</summary>
        public void OnChordChanged(ChordChange change)
        {
            _activeSet = change.CurrentChord.HasValue
                ? ChordToneSet.For(change.CurrentChord.Value)
                : (ChordToneSet?)null;

            // New chord -> new pitch at every slot; a rapid re-hit is a genuinely new note.
            ClearCooldowns();
        }

        /// <summary>
        /// A right-hand fingertip has entered target <paramref name="targetIndex"/> at
        /// <paramref name="fingertipSpeedMetresPerSecond"/>. Returns true if this produced
        /// a note; false if it was gated (no chord held, too slow, or still cooling down).
        /// </summary>
        public bool TriggerTarget(int targetIndex, float fingertipSpeedMetresPerSecond)
        {
            if (targetIndex is < 0 or >= ChordToneSet.TargetCount)
            {
                throw new ArgumentOutOfRangeException(nameof(targetIndex), targetIndex, null);
            }

            if (_activeSet is not { } set)
            {
                return false;
            }

            if (fingertipSpeedMetresPerSecond < EntryVelocityGateMetresPerSecond)
            {
                return false;
            }

            double now = _clock.Now;
            if (now - _lastTriggerTime[targetIndex] < RetriggerCooldownSeconds)
            {
                return false;
            }

            _lastTriggerTime[targetIndex] = now;

            Pitch pitch = set[targetIndex].Pitch;
            byte velocity = VelocityCurve.FromSpeed(fingertipSpeedMetresPerSecond);
            _sink.Send(NoteEvent.On(pitch, velocity, now, MidiChannel.Melody, Handedness.Right));
            Register(pitch, now + _sustainSeconds, now);
            return true;
        }

        /// <summary>Send note-offs for every note whose sustain has elapsed.</summary>
        public void Tick()
        {
            double now = _clock.Now;
            for (int i = 0; i < _pending.Length; i++)
            {
                if (_pending[i].Active && _pending[i].DueTime <= now)
                {
                    SendOff(i, now);
                }
            }
        }

        private void Register(Pitch pitch, double dueTime, double now)
        {
            for (int i = 0; i < _pending.Length; i++)
            {
                if (!_pending[i].Active)
                {
                    _pending[i] = new PendingOff(pitch, dueTime, startedAt: now);
                    return;
                }
            }

            // Full: release the oldest note early and take its slot.
            int oldest = 0;
            for (int i = 1; i < _pending.Length; i++)
            {
                if (_pending[i].StartedAt < _pending[oldest].StartedAt)
                {
                    oldest = i;
                }
            }

            SendOff(oldest, now);
            _pending[oldest] = new PendingOff(pitch, dueTime, startedAt: now);
        }

        private void SendOff(int slot, double now)
        {
            _sink.Send(NoteEvent.Off(_pending[slot].Pitch, now, MidiChannel.Melody, Handedness.Right));
            _pending[slot] = default;
        }

        private void ClearCooldowns()
        {
            for (int i = 0; i < _lastTriggerTime.Length; i++)
            {
                _lastTriggerTime[i] = double.NegativeInfinity;
            }
        }

        private readonly struct PendingOff
        {
            public PendingOff(Pitch pitch, double dueTime, double startedAt)
            {
                Pitch = pitch;
                DueTime = dueTime;
                StartedAt = startedAt;
                Active = true;
            }

            public bool Active { get; }

            public Pitch Pitch { get; }

            public double DueTime { get; }

            public double StartedAt { get; }
        }
    }
}
