using Jazztures.Core.Gesture;
using UnityEngine;

namespace Jazztures.Config
{
    /// <summary>
    /// The single tuning asset for the continuous harmonic-field selection and pinch
    /// articulation (Design A, ADR-0038). `[TUNABLE]` — every value here is a pure
    /// engineering guess, none pilot-calibrated yet; mirror any change in
    /// <c>Docs/CALIBRATION.md</c>.
    ///
    /// <para>
    /// The landmark/hysteresis and pinch-rate values are read via
    /// <see cref="ToThresholds"/>/<see cref="ToPinchThresholds"/> — by
    /// <see cref="HarmonicField"/> and <see cref="ChordPinchDetector"/> respectively. The
    /// body-anchor group (bottom) is consumed directly by
    /// <c>MetaXRHandPostureSource</c> to turn a wrist world position into the normalised
    /// height <see cref="HarmonicField"/> expects — it stays here rather than in
    /// <see cref="HarmonicFieldThresholds"/> because that struct is deliberately unitless.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Config/Harmonic Field", fileName = "HarmonicFieldConfig")]
    public sealed class HarmonicFieldConfig : ScriptableObject
    {
        [Header("Landmarks (normalised height / openness) — consumed by HarmonicField")]
        [Tooltip("ii — up and open.")]
        [SerializeField] private float _iiHeight = 0.80f;
        [Range(0f, 1f)] [SerializeField] private float _iiOpenness = 0.85f;

        [Tooltip("V — mid-height, closed: closing is tension.")]
        [SerializeField] private float _vHeight = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float _vOpenness = 0.10f;

        [Tooltip("I — low and open.")]
        [SerializeField] private float _iHeight = 0.20f;
        [Range(0f, 1f)] [SerializeField] private float _iOpenness = 0.85f;

        [Header("Floor + hysteresis — consumed by HarmonicField")]
        [Tooltip("Height above which a landmark can first be latched onto from nothing.")]
        [SerializeField] private float _floorHeight = 0.05f;

        [Tooltip("Height below which an already-latched landmark releases. Always < Floor Height (Schmitt exit).")]
        [SerializeField] private float _floorReleaseHeight = -0.05f;

        [Tooltip("Distance to a landmark needed to latch onto it (Schmitt enter).")]
        [Min(0.001f)] [SerializeField] private float _lockRadius = 0.22f;

        [Tooltip("Distance an already-latched landmark must exceed before a different one can take over. Always > Lock Radius (Schmitt exit).")]
        [Min(0.001f)] [SerializeField] private float _unlockRadius = 0.34f;

        [Header("Pinch commit — consumed by ChordPinchDetector")]
        [Tooltip("Minimum gap between two pinch articulations (retrigger cooldown). Inherited from Min Inter Strike Seconds's value, not re-guessed.")]
        [Min(0f)] [SerializeField] private float _minInterPinchSeconds = 0.12f;

        [Tooltip("Pinch-closing rate (per second) mapping to the minimum MIDI velocity.")]
        [Min(0f)] [SerializeField] private float _pinchRateAtMinVelocityPerSecond = 3.0f;

        [Tooltip("Pinch-closing rate (per second) mapping to the maximum MIDI velocity. Always > the min-velocity rate.")]
        [Min(0f)] [SerializeField] private float _pinchRateAtMaxVelocityPerSecond = 14.0f;

        [Header("Body anchor (metres) — consumed by MetaXRHandPostureSource")]
        [Tooltip("Left shoulder height relative to the head, metres (negative = below). "
            + "Mirrors MelodyConfig's right-hand equivalent (ADR-0015), not re-guessed.")]
        [SerializeField] private float _shoulderHeightOffsetMetres = -0.25f;

        [Tooltip("Wrist height relative to the shoulder anchor that normalises to 0 (the field's floor).")]
        [SerializeField] private float _floorOffsetMetres = -0.35f;

        [Tooltip("Wrist height relative to the shoulder anchor that normalises to 1 (the field's ceiling). "
            + "Together with Floor Offset Metres this sets the whole height normalisation's span.")]
        [SerializeField] private float _ceilingOffsetMetres = 0.15f;

        public HarmonicFieldThresholds ToThresholds() => new HarmonicFieldThresholds(
            _iiHeight, _iiOpenness,
            _vHeight, _vOpenness,
            _iHeight, _iOpenness,
            _floorHeight, _floorReleaseHeight,
            _lockRadius, _unlockRadius);

        public PinchCommitThresholds ToPinchThresholds() => new PinchCommitThresholds(
            _minInterPinchSeconds,
            _pinchRateAtMinVelocityPerSecond,
            _pinchRateAtMaxVelocityPerSecond);

        public float ShoulderHeightOffsetMetres => _shoulderHeightOffsetMetres;

        public float FloorOffsetMetres => _floorOffsetMetres;

        public float CeilingOffsetMetres => _ceilingOffsetMetres;
    }
}
