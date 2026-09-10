using Jazztures.Core.Melody;
using UnityEngine;

namespace Jazztures.Config
{
    /// <summary>
    /// The single tuning asset for the right-hand melody (CLAUDE.md §3.3, §3.1) and the
    /// body-anchored touch-target rig (ADR-0015). `[TUNABLE]`, pilot-calibrated at M8;
    /// mirror any change in <c>Docs/CALIBRATION.md</c>.
    ///
    /// <para>
    /// The top group (geometry + anchor) is read live by the touch-target rig in
    /// <c>Presentation</c>. The bottom group mirrors the `[TUNABLE]` constants that still
    /// live in <c>Jazztures.Core.Melody</c> (<c>MelodyEngine</c>, <c>VelocityCurve</c>) —
    /// recorded here for the thesis and for M8 calibration; Core consumes its own
    /// constants until Phase 8 wires this asset to the parameterised overloads
    /// (see <c>Docs/CALIBRATION.md</c> §3.1). Keep the two in sync by hand.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Config/Melody", fileName = "MelodyConfig")]
    public sealed class MelodyConfig : ScriptableObject
    {
        [Header("Target geometry — consumed by the touch-target rig (§3.3)")]
        [Tooltip("Radius of each target's circular face, metres — selects scale degree + octave.")]
        [Min(0.001f)] [SerializeField] private float _targetRadiusMetres = 0.04f;

        [Tooltip("Total depth of each target along the approach axis, metres. Generous on " +
                 "purpose: mid-air VR gives no depth cue and haptics are ruled out, so Z is " +
                 "the axis nobody can aim (ADR-0018). ±half this from the face.")]
        [Min(0.001f)] [SerializeField] private float _targetDepthMetres = 0.15f;

        [Tooltip("Angle between adjacent scale-degree columns, measured at the shoulder " +
                 "pivot (ADR-0019). Chord spacing = 2·reach·sin(angle/2); it must clear " +
                 "2×radius plus tracking jitter with room to spare, or a finger reaching " +
                 "for one target clips its neighbours.")]
        [Range(4f, 40f)] [SerializeField] private float _columnAngleDegrees = 16f;

        [Tooltip("Vertical spacing between the two octave rows, metres. No reach cost — " +
                 "the arm does not stretch to go up — so this can be generous.")]
        [Min(0.001f)] [SerializeField] private float _rowSpacingMetres = 0.14f;

        [Tooltip("Volume multiplier for the 'hovering' glow — a target lights this much " +
                 "bigger than its trigger volume as a fingertip nears, so aim is learnable.")]
        [Min(1f)] [SerializeField] private float _hoverScale = 1.6f;

        [Tooltip("Frames the touch-target binder keeps fingertip speed over — it fires on " +
                 "the peak, so a hand decelerating into a target still reports its approach.")]
        [Min(1)] [SerializeField] private int _speedSampleFrames = 3;

        [Header("Shoulder pivot — consumed by the touch-target rig (ADR-0015/0019)")]
        [Tooltip("Arc radius: the reach from the shoulder pivot to every target, metres. " +
                 "Every column sits at this distance, so the outer degrees are no further " +
                 "away than the centre and a glissando is one shoulder sweep.")]
        [Min(0.05f)] [SerializeField] private float _reachDistanceMetres = 0.45f;

        [Tooltip("Vertical offset of the shoulder pivot from head height, metres. Negative " +
                 "= below the head, toward the real shoulder.")]
        [SerializeField] private float _shoulderHeightOffsetMetres = -0.25f;

        [Tooltip("Lateral offset of the shoulder pivot from the body centreline, metres. " +
                 "Positive = right. Small on purpose (ADR-0020): on a Quest 2 the hand-" +
                 "tracking cone is narrower than §1.4's ~140°, so a big rightward offset " +
                 "makes the learner turn their head to see the melody arc and drop the " +
                 "left hand out of frame. Keep the arc mostly in front.")]
        [SerializeField] private float _shoulderLateralOffsetMetres = 0.08f;

        [Tooltip("Head-yaw divergence from the rig's facing before a recenter begins, degrees.")]
        [Range(0f, 90f)] [SerializeField] private float _recenterAngleDegrees = 35f;

        [Tooltip("How long the divergence must be sustained before the recenter starts, seconds.")]
        [Min(0f)] [SerializeField] private float _recenterDwellSeconds = 0.6f;

        [Tooltip("Time for the rig to ease to the new facing once a recenter starts, seconds.")]
        [Min(0.01f)] [SerializeField] private float _recenterEaseSeconds = 0.5f;

        [Header("Melody engine — mirrors Jazztures.Core.Melody consts, kept in sync by hand")]
        [Tooltip("Minimum fingertip speed to fire a note, m/s — 'was this a deliberate " +
                 "strike?'. Mirrors MelodyEngine.EntryVelocityGateMetresPerSecond. Distinct " +
                 "from the loudness curve's minimum speed (ADR-0018).")]
        [Min(0f)] [SerializeField] private float _entryVelocityGateMetresPerSecond = 0.08f;

        [Tooltip("Fingertip speed mapped to the maximum MIDI velocity, m/s. Mirrors VelocityCurve.MaxSpeed.")]
        [Min(0f)] [SerializeField] private float _velocityCurveMaxSpeedMetresPerSecond = 1.5f;

        [Tooltip("Per-target minimum interval between triggers, seconds. Mirrors MelodyEngine.RetriggerCooldownSeconds.")]
        [Min(0f)] [SerializeField] private float _retriggerCooldownSeconds = 0.080f;

        [Tooltip("MIDI velocity range from fingertip speed, then clamped. Mirrors VelocityCurve.MinVelocity/MaxVelocity.")]
        [Range(1, 127)] [SerializeField] private int _midiVelocityMin = 40;

        [Range(1, 127)] [SerializeField] private int _midiVelocityMax = 110;

        [Tooltip("Hard floor — no note-on is ever quieter than this. Mirrors VelocityCurve.AbsoluteFloor (§3.3).")]
        [Range(1, 127)] [SerializeField] private int _midiVelocityFloor = 30;

        [Tooltip("Fixed note length, seconds. [OPEN] — mirrors MelodyEngine.DefaultSustainSeconds.")]
        [Min(0.01f)] [SerializeField] private float _noteSustainSeconds = 0.5f;

        /// <summary>The recenter parameters for the touch-target rig (ADR-0015).</summary>
        public LazyRecenterSettings ToRecenterSettings() => LazyRecenterSettings.FromDegrees(
            _recenterAngleDegrees, _recenterDwellSeconds, _recenterEaseSeconds);

        public float TargetRadiusMetres => _targetRadiusMetres;

        public float TargetDepthMetres => _targetDepthMetres;

        public float ColumnAngleDegrees => _columnAngleDegrees;

        public float RowSpacingMetres => _rowSpacingMetres;

        public float HoverScale => _hoverScale;

        public int SpeedSampleFrames => _speedSampleFrames;

        public float ReachDistanceMetres => _reachDistanceMetres;

        public float ShoulderHeightOffsetMetres => _shoulderHeightOffsetMetres;

        public float ShoulderLateralOffsetMetres => _shoulderLateralOffsetMetres;

        public float RecenterAngleDegrees => _recenterAngleDegrees;

        public float RecenterDwellSeconds => _recenterDwellSeconds;

        public float RecenterEaseSeconds => _recenterEaseSeconds;

        public float EntryVelocityGateMetresPerSecond => _entryVelocityGateMetresPerSecond;

        public float VelocityCurveMaxSpeedMetresPerSecond => _velocityCurveMaxSpeedMetresPerSecond;

        public float RetriggerCooldownSeconds => _retriggerCooldownSeconds;

        public int MidiVelocityMin => _midiVelocityMin;

        public int MidiVelocityMax => _midiVelocityMax;

        public int MidiVelocityFloor => _midiVelocityFloor;

        public float NoteSustainSeconds => _noteSustainSeconds;
    }
}
