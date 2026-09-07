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
        [Tooltip("Radius of each target's trigger sphere, metres.")]
        [Min(0.001f)] [SerializeField] private float _targetRadiusMetres = 0.035f;

        [Tooltip("Centre-to-centre spacing between adjacent targets, metres. Must exceed " +
                 "2×radius plus tracking jitter or targets bleed (§3.3).")]
        [Min(0.001f)] [SerializeField] private float _interTargetSpacingMetres = 0.08f;

        [Header("Body anchor — consumed by the touch-target rig (ADR-0015)")]
        [Tooltip("Forward distance from the anchor to the target grid, metres — a comfortable reach.")]
        [Min(0.05f)] [SerializeField] private float _reachDistanceMetres = 0.45f;

        [Tooltip("Vertical offset of the anchor from head height, metres. Negative = below " +
                 "the head, toward chest height.")]
        [SerializeField] private float _anchorHeightOffsetMetres = -0.25f;

        [Tooltip("Head-yaw divergence from the rig's facing before a recenter begins, degrees.")]
        [Range(0f, 90f)] [SerializeField] private float _recenterAngleDegrees = 35f;

        [Tooltip("How long the divergence must be sustained before the recenter starts, seconds.")]
        [Min(0f)] [SerializeField] private float _recenterDwellSeconds = 0.6f;

        [Tooltip("Time for the rig to ease to the new facing once a recenter starts, seconds.")]
        [Min(0.01f)] [SerializeField] private float _recenterEaseSeconds = 0.5f;

        [Header("Melody engine — mirrors Jazztures.Core.Melody consts, kept in sync by hand")]
        [Tooltip("Minimum fingertip speed to fire a note, m/s. Mirrors VelocityCurve.MinSpeed.")]
        [Min(0f)] [SerializeField] private float _entryVelocityGateMetresPerSecond = 0.15f;

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

        public float InterTargetSpacingMetres => _interTargetSpacingMetres;

        public float ReachDistanceMetres => _reachDistanceMetres;

        public float AnchorHeightOffsetMetres => _anchorHeightOffsetMetres;

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
