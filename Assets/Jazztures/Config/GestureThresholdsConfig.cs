using Jazztures.Core.Gesture;
using UnityEngine;

namespace Jazztures.Config
{
    /// <summary>
    /// The single tuning asset for gesture recognition (CLAUDE.md §3.4 / §3.5 —
    /// "all of the following belong in <c>Config/GestureThresholds.asset</c>"). `[TUNABLE]`,
    /// pilot-calibrated at M8; mirror any change in <c>Docs/CALIBRATION.md</c>.
    ///
    /// <para>
    /// The temporal, confirmation-tolerance, release-tolerance and strike-detection values
    /// are all read via <see cref="ToThresholds"/> — by <see cref="GestureInterpreter"/> and
    /// <see cref="ChordStrikeDetector"/> respectively (ADR-0025 / ADR-0027 / ADR-0029 /
    /// ADR-0030). The SDK-side values (bottom group) are recorded here for the thesis but
    /// are consumed by the <c>ShapeRecognizer</c> / <c>TransformRecognizer</c> assets —
    /// keep those assets configured to match.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Config/Gesture Thresholds", fileName = "GestureThresholds")]
    public sealed class GestureThresholdsConfig : ScriptableObject
    {
        [Header("Temporal — consumed by GestureInterpreter")]
        [Tooltip("How long a pose must be held before it confirms.")]
        [Min(0f)] [SerializeField] private float _poseHoldSeconds = 0.150f;

        [Tooltip("Matching frames needed within the hold window before a pose confirms (~60 Hz hand update).")]
        [Min(1)] [SerializeField] private int _confirmingFrames = 3;

        [Tooltip("Minimum gap between two confirmed chord changes.")]
        [Min(0f)] [SerializeField] private float _minInterChordSeconds = 0.100f;

        [Tooltip("Consecutive High-quality frames required to resume input after a tracking loss.")]
        [Min(1)] [SerializeField] private int _highFramesToResumeAfterLoss = 3;

        [Tooltip("How long tracking must be lost before the non-modal desaturation cue shows.")]
        [Min(0f)] [SerializeField] private float _trackingLossCueSeconds = 0.200f;

        [Header("Confirmation tolerance — consumed by GestureInterpreter (ADR-0025)")]
        [Tooltip("Consecutive non-matching frames (tracking blip / Ambiguous mid-rotation) tolerated "
            + "without restarting the hold window.")]
        [Min(0)] [SerializeField] private int _confirmationMissTolerance = 2;

        [Header("Release tolerance — consumed by GestureInterpreter (ADR-0027)")]
        [Tooltip("How long a pose must read None before releasing an already-confirmed function. "
            + "Always >= Pose Hold Seconds — a false release is audible (cuts the sounding chord), "
            + "and a strike's own motion can disrupt the pose reading for longer than ordinary noise.")]
        [Min(0f)] [SerializeField] private float _releaseHoldSeconds = 0.400f;

        [Tooltip("The release counterpart of Confirmation Miss Tolerance. Always >= it.")]
        [Min(0)] [SerializeField] private int _releaseMissTolerance = 6;

        [Header("Strike detection — consumed by ChordStrikeDetector (ADR-0025)")]
        [Tooltip("Downward hand speed that starts a chord strike.")]
        [Min(0f)] [SerializeField] private float _strikeEnterSpeedMetresPerSecond = 0.25f;

        [Tooltip("The hand must decelerate at or below this speed before another strike can arm (Schmitt exit).")]
        [Min(0f)] [SerializeField] private float _strikeExitSpeedMetresPerSecond = 0.10f;

        [Tooltip("Minimum gap between two strikes (retrigger cooldown).")]
        [Min(0f)] [SerializeField] private float _minInterStrikeSeconds = 0.12f;

        [Tooltip("Consecutive frames at or below the exit speed needed before a strike re-arms. "
            + "A real strike's deceleration often rebounds slightly; requiring more than one "
            + "settled frame stops that rebound from firing a second, unintended strike.")]
        [Min(1)] [SerializeField] private int _strikeSettleFrames = 3;

        [Header("SDK recognisers — keep the ShapeRecognizer / TransformRecognizer assets in sync")]
        [Range(0f, 1f)] [SerializeField] private float _fingerExtendedCurl = 0.25f;
        [Range(0f, 1f)] [SerializeField] private float _fingerCurledCurl = 0.75f;
        [Range(0f, 90f)] [SerializeField] private float _palmConeEnterDegrees = 35f;
        [Range(0f, 90f)] [SerializeField] private float _palmConeExitDegrees = 50f;

        public GestureThresholds ToThresholds() => new GestureThresholds(
            _poseHoldSeconds,
            _confirmingFrames,
            _minInterChordSeconds,
            _highFramesToResumeAfterLoss,
            _trackingLossCueSeconds,
            _confirmationMissTolerance,
            _strikeEnterSpeedMetresPerSecond,
            _strikeExitSpeedMetresPerSecond,
            _minInterStrikeSeconds,
            _releaseHoldSeconds,
            _releaseMissTolerance,
            _strikeSettleFrames);

        public float FingerExtendedCurl => _fingerExtendedCurl;

        public float FingerCurledCurl => _fingerCurledCurl;

        public float PalmConeEnterDegrees => _palmConeEnterDegrees;

        public float PalmConeExitDegrees => _palmConeExitDegrees;
    }
}
