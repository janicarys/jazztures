using Jazztures.Core.Melody;
using UnityEngine;

namespace Jazztures.Config
{
    /// <summary>
    /// Every `[TUNABLE]` for the palm-swing right-hand prototype (ADR-0078): hand pose gate, direction angles,
    /// the trigger line, phrase reset, hand-height register zones, and the sandbox's scoring window. All
    /// engineering starting points, unmeasured on a headset — pilot-calibrate before any number here is written
    /// up (Docs/CALIBRATION.md). Core consumes them through <see cref="ToParameters"/>; nothing is hard-coded in
    /// the adapter.
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Config/Palm Swing", fileName = "PalmSwingSettings")]
    public sealed class PalmSwingSettings : ScriptableObject
    {
        [Header("Direction — hand pitch angle, degrees above horizontal, + = fingers up")]
        [Tooltip("Fingers at or above this angle read as Up. Lower than Down because the wrist bends less far toward the thumb.")]
        [SerializeField] private float _upDegrees = DirectionThresholds.DefaultUpDegrees;

        [Tooltip("Fingers at or below this angle (negative) read as Down.")]
        [SerializeField] private float _downDegrees = DirectionThresholds.DefaultDownDegrees;

        [Tooltip("How far back from a threshold the hand must come before the direction is left (Schmitt trigger).")]
        [Min(0f)] [SerializeField] private float _directionHysteresisDegrees = DirectionThresholds.DefaultHysteresisDegrees;

        [Tooltip("The direction (and register zone) is read this long BEFORE the crossing, because a fast swing drags the hand's tilt.")]
        [Min(0f)] [SerializeField] private float _lookBackSeconds = 0.05f;

        [Header("Trigger line")]
        [Tooltip("Total width of the dead band around the vertical trigger line, metres. A crossing is from beyond one edge to beyond the other.")]
        [Min(0f)] [SerializeField] private float _deadzoneMetres = CrossingThresholds.DefaultDeadzoneMetres;

        [Tooltip("After a crossing, how long before another can sound, seconds.")]
        [Min(0f)] [SerializeField] private float _lockoutSeconds = (float)CrossingThresholds.DefaultLockoutSeconds;

        [Header("Pitch set and range")]
        [Tooltip("[OPEN] What the swing steps through: the active chord's tones, or the C major scale. Neither is decided (ADR-0078).")]
        [SerializeField] private PitchSetKind _pitchSet = PitchSetKind.ChordTones;

        [Tooltip("Bottom of the cursor's range, MIDI. 72 matches the existing right-hand floor.")]
        [Range(36, 96)] [SerializeField] private int _lowestMidi = 72;

        [Tooltip("Size of the range in semitones. 18 is an octave and a half (8-10 positions with chord tones).")]
        [Range(7, 36)] [SerializeField] private int _spanSemitones = 18;

        [Tooltip("The smaller range used while Register Source is Hand Height Zones: one octave plus room for extensions.")]
        [Range(7, 36)] [SerializeField] private int _spanSemitonesWithZones = 16;

        [Tooltip("Where a fresh cursor starts in the range: 0 = bottom, 1 = top. Drills and tasks give their own start pitch.")]
        [Range(0f, 1f)] [SerializeField] private float _startIndexFraction = 0.5f;

        [Header("Phrase reset (separates shape from register)")]
        [Tooltip("When the cursor jumps back to the phrase's start pitch without sounding. Combine freely. Default: On Task | On Rest.")]
        [SerializeField] private PhraseResetPolicy _resetPolicy = PhraseResetPolicy.OnTask | PhraseResetPolicy.OnRest;

        [Tooltip("On Rest: reset when no crossing has happened for this long, seconds.")]
        [Min(0.1f)] [SerializeField] private float _restResetSeconds = 1.5f;

        [Header("Register (hand-height zones) — OFF by default")]
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

        [Header("Sandbox scoring")]
        [Tooltip("A target beat matches the nearest crossing within this many seconds either side.")]
        [Min(0.01f)] [SerializeField] private float _matchWindowSeconds = 0.15f;

        public RegisterSource RegisterSource
        {
            get => _registerSource;
            set => _registerSource = value;
        }

        public float PalmFacingLeftDot => _palmFacingLeftDot;

        public float OpenHandMaxCurl => _openHandMaxCurl;

        public float LaneOffsetRightMetres => _laneOffsetRightMetres;

        public float LaneDistanceMetres => _laneDistanceMetres;

        public float MatchWindowSeconds => _matchWindowSeconds;

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
