using Jazztures.Core.Melody;
using UnityEngine;

namespace Jazztures.Config
{
    /// <summary>
    /// Every `[TUNABLE]` for the palm-swing right-hand prototype (ADR-0078): hand pose gate, direction angles,
    /// the trigger line, phrase reset and hand-height register zones. All
    /// engineering starting points, unmeasured on a headset — pilot-calibrate before any number here is written
    /// up (Docs/CALIBRATION.md). Core consumes them through <see cref="ToParameters"/>; nothing is hard-coded in
    /// the adapter.
    ///
    /// <para>
    /// <b>explore/swing-slices:</b> the note is now chosen by palm height (regions), not tilt. The Direction, Phrase reset
    /// and Register groups below are <b>unused</b> there; they stay serialised so the asset keeps its data and the
    /// palm-swing branch's behaviour can be recovered. Swing-slice values are in the "Swing slices" group.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Config/Palm Swing", fileName = "PalmSwingSettings")]
    public sealed class PalmSwingSettings : ScriptableObject
    {
        [Header("UNUSED on swing-slices — Direction (tilt). Hand pitch angle in degrees, + = fingers up, measured from how the hand RESTS")]
        [Tooltip("Fingers this far above their resting angle read as Up. Lower than Down because the wrist bends less far toward the thumb. The resting angle is taken when the hand has been in the pose for Rest Settle Seconds, so 'neutral' is wherever the hand naturally sits.")]
        [SerializeField] private float _upDegrees = 10f;

        [Tooltip("Fingers this far below their resting angle (negative) read as Down.")]
        [SerializeField] private float _downDegrees = -14f;

        [Tooltip("How far back from a threshold the hand must come before the direction is left (Schmitt trigger).")]
        [Min(0f)] [SerializeField] private float _directionHysteresisDegrees = 4f;

        [Tooltip("The hand must hold the pose this long before its resting position and angle are taken as neutral, seconds. Re-taken on re-centre and whenever the input is switched off and on.")]
        [Min(0f)] [SerializeField] private float _restSettleSeconds = 0.4f;

        [Tooltip("The palm's height region is read this long BEFORE the crossing, because a fast swing drags the hand's height (and, on the palm-swing branch, its tilt).")]
        [Min(0f)] [SerializeField] private float _lookBackSeconds = 0.05f;

        [Header("Smoothing")]
        [Tooltip("Low-pass time constant on the tilt, swing and height signals, seconds. Removes tracking jitter (stray notes at the line, a flickering arrow) at the cost of about this much lag. 0 = off.")]
        [Range(0f, 0.2f)] [SerializeField] private float _motionSmoothingSeconds = 0.04f;

        [Header("Trigger line")]
        [Tooltip("What crosses the line. Palm Position: the palm centre must cross it in space. Finger Ray: a ray along the " +
                 "fingers is cast at a vertical plane in front of you and the line is on that plane, so pointing the fingers " +
                 "across it (a wrist movement) is enough. With Finger Ray, the dead band below is on that plane, in metres " +
                 "(2 cm at 0.55 m is about 2 degrees).")]
        [SerializeField] private PalmSwingCrossingSource _crossingSource = PalmSwingCrossingSource.FingerRay;

        [Tooltip("Finger Ray only: how far beyond the resting hand the vertical plane stands, metres. The further it is, " +
                 "the more distance the ray's hit point travels per degree of wrist movement (about 1 cm per degree at 0.55 m): " +
                 "raise it for a more sensitive swing, lower it for a calmer one.")]
        [Min(0.1f)] [SerializeField] private float _pointingPlaneDistanceMetres = 0.55f;

        [Tooltip("Finger Ray only: the fingers must point at least this far toward the front (cosine of the angle off " +
                 "the user's forward direction) or the gate closes: a ray that is nearly sideways or vertical hits the plane far away.")]
        [Range(0.05f, 0.9f)] [SerializeField] private float _minPointingFacing = 0.2f;

        [Tooltip("Total width of the dead band around the vertical trigger line, metres. A crossing is from beyond one edge to beyond the other.")]
        [Min(0f)] [SerializeField] private float _deadzoneMetres = CrossingThresholds.DefaultDeadzoneMetres;

        [Tooltip("After a crossing, how long before another can sound, seconds.")]
        [Min(0f)] [SerializeField] private float _lockoutSeconds = (float)CrossingThresholds.DefaultLockoutSeconds;

        [Header("Swing slices — palm height picks the note")]
        [Tooltip("How many height regions are stacked on the trigger line, each one a note. Region 0 is the lowest, the middle one sits at the height the palm rested at when the pose settled. With chord tones from MIDI 72, 15 is the fewest odd count that reaches every pitch both lesson octaves use (9 reached one octave; 14 is the fewest outright but only works from exactly 72, and even counts put the resting height on a band boundary).")]
        [Range(2, 16)] [SerializeField] private int _regionCount = HeightRegionLayout.DefaultRegionCount;

        [Tooltip("Height of each region, centimetres. Smaller means more notes in reach but less margin for tracking jitter.")]
        [Min(1f)] [SerializeField] private float _regionHeightCm = HeightRegionLayout.DefaultRegionHeightMetres * 100f;

        [Tooltip("Near a region boundary the palm keeps its current region until it is this far (cm) past the edge, so a hand resting on a boundary does not flicker.")]
        [Min(0f)] [SerializeField] private float _boundaryHysteresisCm = HeightRegionLayout.DefaultHysteresisMetres * 100f;

        [Tooltip("Lesson circles: diameter, centimetres. About one region height, so a circle's height reads clearly against the line.")]
        [Min(0.5f)] [SerializeField] private float _targetDiameterCm = HeightRegionLayout.DefaultRegionHeightMetres * 100f;

        [Tooltip("DEVELOPER TUNING ONLY. Draw faint lines at the region boundaries on the trigger line. No note names, even when on.")]
        [SerializeField] private bool _showRegionGuidesDebug;

        [Header("Pitch set and range (Swing slices: lowPitch and pitchSetType)")]
        [Tooltip("[OPEN] pitchSetType: what the regions play, ascending: the active chord's tones (root/3rd/5th/7th/9th repeated upward), or the C major scale. Neither is decided (ADR-0078, ADR-0080).")]
        [SerializeField] private PitchSetKind _pitchSet = PitchSetKind.ChordTones;

        [Tooltip("lowPitch: region 0 plays the first pitch of the set at or above this, MIDI. 72 matches the existing right-hand floor, and with 15 chord-tone regions every value of 70-72 covers both lesson octaves (14 regions allow only 72, 9 only reached one octave).")]
        [Range(36, 96)] [SerializeField] private int _lowestMidi = 72;

        [Tooltip("UNUSED on swing-slices (the range is set by Region Count). Size of the cursor's range in semitones.")]
        [Range(7, 36)] [SerializeField] private int _spanSemitones = 18;

        [Tooltip("The smaller range used while Register Source is Hand Height Zones: one octave plus room for extensions.")]
        [Range(7, 36)] [SerializeField] private int _spanSemitonesWithZones = 16;

        [Tooltip("Where a fresh cursor starts in the range: 0 = bottom, 1 = top. Drills and tasks give their own start pitch.")]
        [Range(0f, 1f)] [SerializeField] private float _startIndexFraction = 0.5f;

        [Header("UNUSED on swing-slices — Phrase reset (separates shape from register)")]
        [Tooltip("When the cursor jumps back to the phrase's start pitch without sounding. Combine freely. Default: On Task | On Rest.")]
        [SerializeField] private PhraseResetPolicy _resetPolicy = PhraseResetPolicy.OnTask | PhraseResetPolicy.OnRest;

        [Tooltip("On Rest: reset when no crossing has happened for this long, seconds.")]
        [Min(0.1f)] [SerializeField] private float _restResetSeconds = 1.5f;

        [Header("UNUSED on swing-slices — Register (hand-height zones)")]
        [Tooltip("None: the cursor alone decides the pitch. Hand Height Zones: the palm's height picks an octave, read between notes only.")]
        [SerializeField] private RegisterSource _registerSource = RegisterSource.None;

        [Tooltip("Number of height zones, 2 or 3.")]
        [Range(2, 3)] [SerializeField] private int _zoneCount = RegisterZoneThresholds.DefaultZoneCount;

        [Tooltip("Height of each zone, centimetres. Zones are centred on the lane origin.")]
        [Min(1f)] [SerializeField] private float _zoneHeightCm = RegisterZoneThresholds.DefaultZoneHeightMetres * 100f;

        [Tooltip("Hysteresis at a zone boundary, centimetres.")]
        [Min(0f)] [SerializeField] private float _zoneHysteresisCm = RegisterZoneThresholds.DefaultHysteresisMetres * 100f;

        [Tooltip("The final pitch is clamped to this range once an octave shift is added, MIDI. Piano samples reach 108.")]
        [Range(21, 108)] [SerializeField] private int _safeMinMidi = 48;

        [Range(21, 108)] [SerializeField] private int _safeMaxMidi = 103;

        [Header("Hand pose gate — the input is live only while all three hold")]
        [Tooltip("The palm normal's component along the user's right vector must be below this (negative = palm faces left).")]
        [Range(-1f, 0f)] [SerializeField] private float _palmFacingLeftDot = -0.6f;

        [Tooltip("Every finger's curl (0 flat, 1 fully bent) must be below this for the hand to count as open.")]
        [Range(0.05f, 1f)] [SerializeField] private float _openHandMaxCurl = 0.35f;

        [Header("Lane (visuals)")]
        [Tooltip("The lane's origin is the palm's resting position when the gate first opens. The visuals sit this far to the user's right of the body centre line, so a leftward swing does not run into the left hand's strike plane. Metres.")]
        [SerializeField] private float _laneOffsetRightMetres = 0.25f;

        [Tooltip("Where the trigger line and note marker sit, metres in front of the head.")]
        [Min(0.2f)] [SerializeField] private float _laneDistanceMetres = 0.55f;

        public bool ShowRegionGuidesDebug => _showRegionGuidesDebug;

        public float TargetDiameterMetres => _targetDiameterCm / 100f;

        public RegisterSource RegisterSource
        {
            get => _registerSource;
            set => _registerSource = value;
        }

        public PalmSwingCrossingSource CrossingSource => _crossingSource;

        public float PointingPlaneDistanceMetres => _pointingPlaneDistanceMetres;

        public float MinPointingFacing => _minPointingFacing;

        public float RestSettleSeconds => _restSettleSeconds;

        public float PalmFacingLeftDot => _palmFacingLeftDot;

        public float OpenHandMaxCurl => _openHandMaxCurl;

        public float LaneOffsetRightMetres => _laneOffsetRightMetres;

        public float LaneDistanceMetres => _laneDistanceMetres;

        /// <summary>Build the pure-C# parameter bag the Core pipeline reads.</summary>
        public PalmSwingParameters ToParameters()
        {
            // Keep the pairs consistent rather than throw at a bad inspector value.
            float hysteresis = Mathf.Max(0f, _directionHysteresisDegrees);
            float down = Mathf.Min(_downDegrees, _upDegrees - 2f * hysteresis - 1f);

            return new PalmSwingParameters
            {
                Direction = new DirectionThresholds(_upDegrees, down, hysteresis),
                Crossing = new CrossingThresholds(Mathf.Max(0f, _deadzoneMetres), Mathf.Max(0f, _lockoutSeconds)),
                Zones = new RegisterZoneThresholds(
                    Mathf.Clamp(_zoneCount, 2, 3), Mathf.Max(0.01f, _zoneHeightCm / 100f), Mathf.Max(0f, _zoneHysteresisCm / 100f)),
                RegionCount = Mathf.Clamp(_regionCount, 2, 16),
                RegionHeightMetres = Mathf.Max(0.01f, _regionHeightCm / 100f),
                BoundaryHysteresisMetres = Mathf.Max(0f, _boundaryHysteresisCm / 100f),
                SmoothingSeconds = Mathf.Max(0f, _motionSmoothingSeconds),
                LookBackSeconds = Mathf.Max(0f, _lookBackSeconds),
                Register = _registerSource,
                PitchSet = _pitchSet,
                LowestMidi = _lowestMidi,
                SpanSemitones = _spanSemitones,
                SpanSemitonesWithZones = _spanSemitonesWithZones,
                StartIndexFraction = _startIndexFraction,
                ResetPolicy = _resetPolicy,
                RestResetSeconds = _restResetSeconds,
                SafeMinMidi = Mathf.Min(_safeMinMidi, _safeMaxMidi),
                SafeMaxMidi = Mathf.Max(_safeMinMidi, _safeMaxMidi),
            };
        }
    }
}
