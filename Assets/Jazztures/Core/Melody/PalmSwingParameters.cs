namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Every `[TUNABLE]` the palm-swing pipeline reads, in one bag (ADR-0078). A plain mutable class so the
    /// Unity <c>PalmSwingSettings</c> asset can fill it; the defaults here are the engineering starting points
    /// from the brief and are <b>not</b> findings — pilot-calibrate them (CALIBRATION.md) before they are written
    /// up.
    /// </summary>
    public sealed class PalmSwingParameters
    {
        public DirectionThresholds Direction { get; set; } = DirectionThresholds.Default;

        public CrossingThresholds Crossing { get; set; } = CrossingThresholds.Default;

        public RegisterZoneThresholds Zones { get; set; } = RegisterZoneThresholds.Default;

        /// <summary>
        /// Time constant of the low-pass filter on the tilt angle, lateral position and height, seconds. 0 = off (Core's
        /// default, so the pure tests see raw samples); the Unity settings asset supplies the real value.
        /// </summary>
        public double SmoothingSeconds { get; set; } = 0.0;

        /// <summary>How long before a crossing the held direction (and zone) is read, seconds.</summary>
        public double LookBackSeconds { get; set; } = 0.050;

        /// <summary>Swing slices: how many height regions, each one a note. `[TUNABLE]`.</summary>
        public int RegionCount { get; set; } = HeightRegionLayout.DefaultRegionCount;

        /// <summary>Swing slices: height of each region, metres. `[TUNABLE]`.</summary>
        public float RegionHeightMetres { get; set; } = HeightRegionLayout.DefaultRegionHeightMetres;

        /// <summary>Swing slices: how far past a region's edge the hand keeps it, metres. `[TUNABLE]`.</summary>
        public float BoundaryHysteresisMetres { get; set; } = HeightRegionLayout.DefaultHysteresisMetres;

        public RegisterSource Register { get; set; } = RegisterSource.None;

        /// <summary>`[OPEN]`: chord tones or the C major scale.</summary>
        public PitchSetKind PitchSet { get; set; } = PitchSetKind.ChordTones;

        /// <summary>Bottom of the cursor's range, MIDI. 72 matches the existing right-hand floor (§3.1).</summary>
        public int LowestMidi { get; set; } = 72;

        /// <summary>Size of the range, semitones: about an octave and a half.</summary>
        public int SpanSemitones { get; set; } = 18;

        /// <summary>The smaller range used with height zones, which add the octaves: an octave plus room.</summary>
        public int SpanSemitonesWithZones { get; set; } = 16;

        /// <summary>Where in the range a fresh cursor starts: 0 = bottom, 1 = top.</summary>
        public float StartIndexFraction { get; set; } = 0.5f;

        public PhraseResetPolicy ResetPolicy { get; set; } = PhraseResetPolicy.OnTask | PhraseResetPolicy.OnRest;

        public double RestResetSeconds { get; set; } = 1.5;

        /// <summary>The final pitch is clamped to this range once an octave shift is added.</summary>
        public int SafeMinMidi { get; set; } = 48;

        public int SafeMaxMidi { get; set; } = 103;
    }
}
