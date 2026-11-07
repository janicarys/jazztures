using System;
using Jazztures.Core.Music;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The swing-slice right hand, end to end (explore/swing-slices, built on the palm swing of ADR-0078): hand samples
    /// in, notes out. Palm height picks one of a stack of regions and is silent; a sideways swing across the vertical
    /// trigger line, either way, plays that region's pitch for the active chord. Swinging back and forth at one height
    /// repeats the note. The tilt, melody cursor, phrase reset and register zones of <see cref="PalmSwingPipeline"/> are not used.
    ///
    /// <para>
    /// The region is read from <c>LookBackSeconds</c> <i>before</i> the crossing (from <see cref="DirectionHistory"/>,
    /// whose zone field carries the region), because the swing itself drags the hand's height. A chord change retunes
    /// the regions at once; a note already sounding keeps its pitch (the melody engine holds it). With no chord ever held
    /// the fallback chord is used, so the right hand plays before the left has struck. Pure and allocation-free per
    /// frame (the pitch set is rebuilt only on a chord change).
    /// </para>
    /// </summary>
    public sealed class SwingSlicesPipeline
    {
        private readonly PalmSwingParameters _parameters;
        private readonly CrossingTrigger _trigger;
        private readonly HeightRegionLayout _layout;
        private readonly DirectionHistory _history = new DirectionHistory();
        private readonly MotionSmoother _lateralSmoother;
        private readonly MotionSmoother _heightSmoother;

        private Chord _chord;
        private RegionPitchSet _pitches;

        public SwingSlicesPipeline(PalmSwingParameters parameters, Chord fallbackChord)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            _trigger = new CrossingTrigger(parameters.Crossing);
            _layout = new HeightRegionLayout(
                parameters.RegionCount, parameters.RegionHeightMetres, parameters.BoundaryHysteresisMetres);
            _lateralSmoother = new MotionSmoother(parameters.SmoothingSeconds);
            _heightSmoother = new MotionSmoother(parameters.SmoothingSeconds);
            _chord = fallbackChord;
            _pitches = BuildSet(fallbackChord);
        }

        public bool GateOpen { get; private set; }

        public HeightRegionLayout Layout => _layout;

        public RegionPitchSet Pitches => _pitches;

        public Chord Chord => _chord;

        /// <summary>The region the palm is in now (for developer guides only; never shown by name).</summary>
        public int Region => _layout.Current;

        /// <summary>Wire to <c>HarmonyEngine.ChordChanged</c>. Releasing to no chord keeps the last one.</summary>
        public void OnChordChanged(Chord? chord)
        {
            if (!chord.HasValue || chord.Value == _chord)
            {
                return;
            }

            _chord = chord.Value;
            _pitches = BuildSet(_chord);
        }

        /// <summary>The gate closed or tracking was lost: forget the hand. Notes cannot fire by re-arriving.</summary>
        public void OnGateClosed()
        {
            GateOpen = false;
            _trigger.Reset();
            _layout.Reset();
            _lateralSmoother.Reset();
            _heightSmoother.Reset();
            _history.Clear();
        }

        /// <summary>Feed one frame. True if it produced a note.</summary>
        public bool Update(in PalmSwingSample sample, out SwingSlicesNote note)
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

            float lateral = _lateralSmoother.Update(sample.Time, sample.LateralMetres);
            float height = _heightSmoother.Update(sample.Time, sample.HeightMetres);

            int region = _layout.Update(height);
            _history.Record(sample.Time, StepDirection.Same, region);

            if (!_trigger.Update(sample.Time, lateral))
            {
                return false;
            }

            int held = _history.At(_trigger.LastTime - _parameters.LookBackSeconds).Zone;
            held = Math.Max(0, Math.Min(_pitches.Count - 1, held));
            note = new SwingSlicesNote(
                _pitches[held], held, _trigger.LastDirection, _trigger.LastTime, _trigger.LastSpeedMetresPerSecond);
            return true;
        }

        private RegionPitchSet BuildSet(Chord chord) => RegionPitchSet.Build(
            _parameters.PitchSet, chord, _parameters.LowestMidi, _parameters.RegionCount);
    }
}
