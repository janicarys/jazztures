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
    /// The temporal values (top group) are read by <see cref="GestureInterpreter"/> via
    /// <see cref="ToThresholds"/>. The SDK-side values (bottom group) are recorded here
    /// for the thesis but are consumed by the <c>ShapeRecognizer</c> /
    /// <c>TransformRecognizer</c> assets — keep those assets configured to match.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Config/Gesture Thresholds", fileName = "GestureThresholds")]
    public sealed class GestureThresholdsConfig : ScriptableObject
    {
        [Header("Temporal — consumed by GestureInterpreter")]
        [Tooltip("How long a pose must be held before it confirms.")]
        [Min(0f)] [SerializeField] private float _poseHoldSeconds = 0.120f;

        [Tooltip("Consecutive matching frames before a pose confirms (~60 Hz hand update).")]
        [Min(1)] [SerializeField] private int _confirmingFrames = 3;

        [Tooltip("Minimum gap between two confirmed chord changes.")]
        [Min(0f)] [SerializeField] private float _minInterChordSeconds = 0.100f;

        [Tooltip("Consecutive High-quality frames required to resume input after a tracking loss.")]
        [Min(1)] [SerializeField] private int _highFramesToResumeAfterLoss = 3;

        [Tooltip("How long tracking must be lost before the non-modal desaturation cue shows.")]
        [Min(0f)] [SerializeField] private float _trackingLossCueSeconds = 0.200f;

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
            _trackingLossCueSeconds);

        public float FingerExtendedCurl => _fingerExtendedCurl;

        public float FingerCurledCurl => _fingerCurledCurl;

        public float PalmConeEnterDegrees => _palmConeEnterDegrees;

        public float PalmConeExitDegrees => _palmConeExitDegrees;
    }
}
