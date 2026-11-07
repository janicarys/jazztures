using System;
using Jazztures.Core.Music;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The palm-swing right hand, end to end (ADR-0078): hand samples in, notes out. It chains
    /// <see cref="DirectionClassifier"/> → <see cref="DirectionHistory"/> and <see cref="CrossingTrigger"/> →
    /// <see cref="MelodyCursor"/>, with the optional <see cref="RegisterZoneClassifier"/> octave shift and the
    /// phrase-reset policy. Pure and allocation-free per frame; the Unity adapter feeds it and hands each note to
    /// <c>MelodyEngine.PlayPitch</c>.
    ///
    /// <para>
    /// The cursor always has a pitch set: before any chord has been held it uses the fallback chord given at
    /// construction, and releasing to no chord keeps the last one, so a lesson where the left hand has not played
    /// yet never silences the right (a Session 4 complaint).
    /// </para>
    /// </summary>
    public sealed class PalmSwingPipeline
    {
        private readonly PalmSwingParameters _parameters;
        private readonly DirectionClassifier _classifier;
        private readonly CrossingTrigger _trigger;
        private readonly DirectionHistory _history = new DirectionHistory();
        private readonly RegisterZoneClassifier _zones;
        private readonly MotionSmoother _angleSmoother;
        private readonly MotionSmoother _lateralSmoother;
        private readonly MotionSmoother _heightSmoother;
        private readonly MelodyCursor _cursor;
        private readonly bool _useZones;
        private readonly int _middleZone;

        private Chord _chord;
        private PhraseStart _start = PhraseStart.ChordRoot;
        private bool _dirty;                    // a note has sounded since the last reset
        private double _lastCrossingTime = double.NegativeInfinity;

        public PalmSwingPipeline(PalmSwingParameters parameters, Chord fallbackChord)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            _classifier = new DirectionClassifier(parameters.Direction);
            _trigger = new CrossingTrigger(parameters.Crossing);
            _zones = new RegisterZoneClassifier(parameters.Zones);
            _angleSmoother = new MotionSmoother(parameters.SmoothingSeconds);
            _lateralSmoother = new MotionSmoother(parameters.SmoothingSeconds);
            _heightSmoother = new MotionSmoother(parameters.SmoothingSeconds);
            _middleZone = parameters.Zones.MiddleZone;
            _useZones = parameters.Register == RegisterSource.HandHeightZones;
            _cursor = new MelodyCursor(parameters.StartIndexFraction);
            _chord = fallbackChord;
            _cursor.SetPitchSet(BuildSet(fallbackChord));
        }

        /// <summary>The cursor was reset (reason, and the pitch it is now on). Plain event: no allocation.</summary>
        public event Action<PhraseResetReason, Pitch>? PhraseReset;

        public bool GateOpen { get; private set; }

        /// <summary>The tilt direction currently held — what the next swing will do.</summary>
        public StepDirection HeldDirection => _classifier.Current;

        /// <summary>The register zone now, or -1 when height zones are off.</summary>
        public int Zone => _useZones ? _zones.Current : -1;

        public int ZoneCount => _parameters.Zones.ZoneCount;

        public bool AtLimit => _cursor.AtLimit;

        /// <summary>The pitch the cursor is on (before any octave shift).</summary>
        public Pitch CursorPitch => _cursor.CurrentPitch;

        public PitchSet PitchSet => _cursor.Set!;

        public Chord Chord => _chord;

        /// <summary>Wire to <c>HarmonyEngine.ChordChanged</c>. Releasing to no chord keeps the last one.</summary>
        public void OnChordChanged(Chord? chord)
        {
            if (!chord.HasValue || chord.Value == _chord)
            {
                return;
            }

            _chord = chord.Value;
            _cursor.SetPitchSet(BuildSet(_chord));
        }

        /// <summary>
        /// A drill task, or a Compose on the Fly answer, begins. Remembers <paramref name="start"/> as this
        /// phrase's start pitch and, if the policy has <see cref="PhraseResetPolicy.OnTask"/>, resets to it.
        /// </summary>
        public void BeginTask(PhraseStart start)
        {
            _start = start;
            if ((_parameters.ResetPolicy & PhraseResetPolicy.OnTask) != 0)
            {
                ApplyReset(PhraseResetReason.Task);
            }
        }

        /// <summary>A bar line passed on the audio clock. Resets to the task's start if the policy has OnBar.</summary>
        public void NotifyBarLine()
        {
            if ((_parameters.ResetPolicy & PhraseResetPolicy.OnBar) != 0)
            {
                ApplyReset(PhraseResetReason.Bar);
            }
        }

        /// <summary>Jump to the current start pitch now, whatever the policy (an explicit request, logged as a task).</summary>
        public void ResetNow() => ApplyReset(PhraseResetReason.Task);

        /// <summary>The gate closed or tracking was lost: forget the hand, keep the melody. Notes cannot fire by re-arriving.</summary>
        public void OnGateClosed()
        {
            GateOpen = false;
            _trigger.Reset();
            _classifier.Reset();
            _zones.Reset();
            _angleSmoother.Reset();
            _lateralSmoother.Reset();
            _heightSmoother.Reset();
            _history.Clear();
        }

        /// <summary>Feed one frame. True if it produced a note.</summary>
        public bool Update(in PalmSwingSample sample, out PalmSwingNote note)
        {
            note = default;
            if (!sample.GateOpen)
            {
                if (GateOpen)
                {
                    OnGateClosed();
                }

                return false;
            }

            GateOpen = true;

            if (_dirty
                && (_parameters.ResetPolicy & PhraseResetPolicy.OnRest) != 0
                && sample.Time - _lastCrossingTime >= _parameters.RestResetSeconds)
            {
                ApplyReset(PhraseResetReason.Rest);
            }

            // Smooth the raw hand signals first: tracking jitter would flicker the tilt and put stray crossings on the line.
            float angle = _angleSmoother.Update(sample.Time, sample.PitchAngleDegrees);
            float lateral = _lateralSmoother.Update(sample.Time, sample.LateralMetres);
            float height = _heightSmoother.Update(sample.Time, sample.HeightMetres);

            StepDirection direction = _classifier.Update(angle);
            int zone = _useZones ? _zones.Update(height) : 0;
            _history.Record(sample.Time, direction, zone);

            if (!_trigger.Update(sample.Time, lateral))
            {
                return false;
            }

            // Read the hand from just before the swing: the swing itself drags the tilt and the height.
            HandSnapshot held = _history.At(_trigger.LastTime - _parameters.LookBackSeconds);
            Pitch cursorPitch = _cursor.Step(held.Direction);

            Pitch sounding = cursorPitch;
            if (_useZones)
            {
                int midi = cursorPitch.Midi + (held.Zone - _middleZone) * 12;
                sounding = new Pitch(Math.Max(_parameters.SafeMinMidi, Math.Min(_parameters.SafeMaxMidi, midi)));
            }

            _dirty = true;
            _lastCrossingTime = _trigger.LastTime;
            note = new PalmSwingNote(
                sounding, cursorPitch, held.Direction, _trigger.LastDirection,
                _useZones ? held.Zone : -1, _trigger.LastTime, _trigger.LastSpeedMetresPerSecond, _cursor.AtLimit);
            return true;
        }

        private void ApplyReset(PhraseResetReason reason)
        {
            if (_start.HasIndex)
            {
                _cursor.Reset(_start.Index);
            }
            else if (_start.HasMidi)
            {
                _cursor.Reset(new Pitch(_start.Midi));
            }
            else
            {
                _cursor.ResetToChordRoot();
            }

            _dirty = false;
            PhraseReset?.Invoke(reason, _cursor.CurrentPitch);
        }

        private PitchSet BuildSet(Chord chord) => PitchSet.Build(
            _parameters.PitchSet,
            chord,
            _parameters.LowestMidi,
            _useZones ? _parameters.SpanSemitonesWithZones : _parameters.SpanSemitones);
    }
}
