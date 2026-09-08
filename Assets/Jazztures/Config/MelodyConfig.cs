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
        [Min(0.001f)] [SerializeField] private float _targetRadiusMetres = 0.035f;

        [Tooltip("Total depth of each target along the approach axis, metres. Generous on " +
                 "purpose: mid-air VR gives no depth cue and haptics are ruled out, so Z is " +
                 "the axis nobody can aim (ADR-0018). ±half this from the face.")]
        [Min(0.001f)] [SerializeField] private float _targetDepthMetres = 0.15f;

        [Tooltip("Centre-to-centre spacing between adjacent targets, metres. Must exceed " +
                 "2×radius plus tracking jitter or targets bleed (§3.3).")]
        [Min(0.001f)] [SerializeField] private float _interTargetSpacingMetres = 0.08f;

        [Tooltip("Volume multiplier for the 'hovering' glow — a target lights this much " +
                 "bigger than its trigger volume as a fingertip nears, so aim is learnable.")]
        [Min(1f)] [SerializeField] private float _hoverScale = 1.6f;

        [Tooltip("Frames the touch-target binder keeps fingertip speed over — it fires on " +
                 "the peak, so a hand decelerating into a target still reports its approach.")]
        [Min(1)] [SerializeField] private int _speedSampleFrames = 3;

        [Header("Body anchor — consumed by the touch-target rig (ADR-0015)")]
        [Tooltip("Forward distance from the anchor to the target grid, metres — a comfortable reach.")]
        [Min(0.05f)] [SerializeField] private float _reachDistanceMetres = 0.45f;

        [Tooltip("Vertical offset of the anchor from head height, metres. Negative = below " +
                 "the head, toward chest height.")]
        [SerializeField] private float _anchorHeightOffsetMetres = -0.25f;

        [Tooltip("Lateral offset of the grid from the body centreline, metres. Positive = " +
                 "right, toward where the right hand rests. Keep the whole grid inside the " +
                 "~140° tracking FOV (§1.4).")]
        [SerializeField] private float _anchorLateralOffsetMetres = 0.20f;

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

        public float InterTargetSpacingMetres => _interTargetSpacingMetres;

        public float HoverScale => _hoverScale;

        public int SpeedSampleFrames => _speedSampleFrames;

        public float ReachDistanceMetres => _reachDistanceMetres;

        public float AnchorHeightOffsetMetres => _anchorHeightOffsetMetres;

        public float AnchorLateralOffsetMetres => _anchorLateralOffsetMetres;

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
