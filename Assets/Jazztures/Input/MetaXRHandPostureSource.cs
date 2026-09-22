using Jazztures.Config;
using Jazztures.Core.Gesture;
using Jazztures.Core.Ports;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using UnityEngine;

namespace Jazztures.Input
{
    /// <summary>
    /// The continuous-selection, pinch-articulation Quest hand-tracking adapter (Design A,
    /// ADR-0038) — an alternative to <see cref="MetaXRHandPoseSource"/>, not a replacement
    /// for it. Turns the left hand's wrist height and finger openness into a
    /// <see cref="HandPoseCandidate"/> via <see cref="HarmonicField"/>, and its
    /// index-to-thumb pinch into the articulation signal <see cref="ChordPinchDetector"/>
    /// consumes — so everything downstream (<c>GestureInterpreter</c>,
    /// <c>PerformanceCompositionRoot</c>, the tension-colour UI) is exactly the same code
    /// the discrete pose path already uses.
    /// </summary>
    /// <remarks>
    /// Assign the same left/right <see cref="IHand"/> components <see cref="MetaXRHandPoseSource"/>
    /// uses, the <see cref="HarmonicFieldConfig"/> asset, and the centre-eye head transform
    /// (the same one <c>TouchTargetRig</c> anchors against). <see cref="CurrentFrame"/>
    /// memoizes per <see cref="Time.frameCount"/>, for the identical reason
    /// <see cref="MetaXRHandPoseSource.CurrentFrame"/> does: multiple readers in one frame,
    /// unguaranteed order, and both the pinch rate and the field's own latch state are
    /// only correct if computed exactly once per frame.
    /// </remarks>
    public sealed class MetaXRHandPostureSource : MonoBehaviour, IHandPoseSource
    {
        [Tooltip("Tuning for the continuous field, the pinch commit, and the body anchor.")]
        [SerializeField] private HarmonicFieldConfig _config;

        [Tooltip("The centre-eye / head transform — the same one TouchTargetRig anchors against.")]
        [SerializeField] private Transform _head;

        [Tooltip("The left Interaction SDK Hand component.")]
        [SerializeField] private MonoBehaviour _leftHand;

        [Tooltip("The right Interaction SDK Hand component (tracking quality only).")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Tooltip("Which finger's pinch commits a chord. Index by default; an Inspector-only "
            + "fallback to Middle if index-pinch proves unreliable from a closed fist at the "
            + "V landmark — a real, on-device-only unknown (ADR-0038) — with no code change.")]
        [SerializeField] private HandFinger _pinchFinger = HandFinger.Index;

        [Tooltip("Recent frames to take the peak pinch-closing rate over — mirrors "
            + "MetaXRHandPoseSource's strike-speed sampling (ADR-0028/-0035).")]
        [Min(1)] [SerializeField] private int _pinchRateSampleFrames = 3;

        [Tooltip("Log height/openness/candidate/pinch every frame — essential for the first "
            + "device session, since every landmark and radius is an unmeasured guess.")]
        [SerializeField] private bool _logPosture;

        private IHand _left;
        private IHand _right;
        private HarmonicField _field;
        private readonly FingerShapes _shapes = new FingerShapes();

        private HandPoseFrame _cachedFrame = HandPoseFrame.Untracked;
        private int _cachedFrameNumber = -1;

        private bool _reportedPinch;
        private bool _hasPreviousPinchStrength;
        private float _previousPinchStrength;
        private float[] _pinchRateSamples;
        private int _pinchRateCursor;

        /// <summary>The last computed normalised height, for external debugging/visualisation.</summary>
        public float DebugHeight { get; private set; }

        /// <summary>The last computed openness (1 = open, 0 = curled), for external debugging/visualisation.</summary>
        public float DebugOpenness { get; private set; }

        private void Awake()
        {
            _left = Resolve<IHand>(_leftHand, nameof(_leftHand));
            _right = Resolve<IHand>(_rightHand, nameof(_rightHand));
            _field = new HarmonicField(_config != null ? _config.ToThresholds() : HarmonicFieldThresholds.Default);
            _pinchRateSamples = new float[Mathf.Max(1, _pinchRateSampleFrames)];

            if (_config == null)
            {
                Debug.LogError($"{nameof(MetaXRHandPostureSource)}: no {nameof(HarmonicFieldConfig)} assigned; using engineering defaults.", this);
            }

            if (_head == null)
            {
                Debug.LogError($"{nameof(MetaXRHandPostureSource)}: '{nameof(_head)}' is not assigned.", this);
            }
        }

        public HandPoseFrame CurrentFrame
        {
            get
            {
                int frame = Time.frameCount;
                if (frame != _cachedFrameNumber)
                {
                    _cachedFrame = ComputeFrame();
                    _cachedFrameNumber = frame;
                }

                return _cachedFrame;
            }
        }

        private HandPoseFrame ComputeFrame()
        {
            TrackingQuality left = ReadTracking(_left);
            TrackingQuality right = ReadTracking(_right);

            if (left != TrackingQuality.High || _head == null)
            {
                // A degraded or failed read cannot be trusted for a continuous signal — a
                // failed FingerShapes read normalises to "fully open," which would
                // otherwise misread as ii. GestureInterpreter's own tracking-loss handling
                // (§3.5) already sustains whatever HarmonicField last latched onto — this
                // adapter does not need to, and must not, touch _field itself here.
                _hasPreviousPinchStrength = false;
                return new HandPoseFrame(HandPoseCandidate.None, left, right, 0f, _reportedPinch, 0f);
            }

            float height = ReadNormalizedHeight();
            float openness = ReadOpenness();
            DebugHeight = height;
            DebugOpenness = openness;
            HandPoseCandidate candidate = _field.Update(height, openness);

            UpdatePinchState();
            float pinchRate = ReadPinchClosingRate();

            if (_logPosture)
            {
                Debug.Log($"[POSTURE] h={height:0.00} o={openness:0.00} → {candidate} pinch={_reportedPinch} rate={pinchRate:0.0}", this);
            }

            return new HandPoseFrame(candidate, left, right, 0f, _reportedPinch, pinchRate);
        }

        /// <summary>
        /// The left wrist's height relative to a shoulder anchor, normalised so
        /// <see cref="HarmonicFieldConfig.FloorOffsetMetres"/> maps to 0 and
        /// <see cref="HarmonicFieldConfig.CeilingOffsetMetres"/> maps to 1 — unclamped, so a
        /// hand-drop below the floor reads as negative, exactly as <see cref="HarmonicField"/>
        /// expects. Reads <see cref="HandJointId.HandWristRoot"/>, never a fingertip: a
        /// fingertip's height co-varies with finger curl, which would contaminate this axis
        /// against the openness axis. Gravity does not rotate with the user, so — like
        /// <see cref="MetaXRHandPoseSource.LeftVerticalSpeedMetresPerSecond"/> before it —
        /// this needs no head-forward correction.
        /// </summary>
        private float ReadNormalizedHeight()
        {
            if (!_left.GetJointPose(HandJointId.HandWristRoot, out Pose wrist))
            {
                return 0f;
            }

            float shoulderY = _head.position.y + (_config != null ? _config.ShoulderHeightOffsetMetres : -0.25f);
            float relative = wrist.position.y - shoulderY;

            float floor = _config != null ? _config.FloorOffsetMetres : -0.35f;
            float ceiling = _config != null ? _config.CeilingOffsetMetres : 0.15f;
            return (relative - floor) / (ceiling - floor);
        }

        /// <summary>
        /// 1.0 = fully open/extended, 0.0 = fully curled — averaged over the middle, ring
        /// and pinky fingers only. The index is deliberately excluded: pinching curls it by
        /// roughly a quarter of its range, and if it counted toward openness, the act of
        /// committing a chord would itself drag the selection point toward V (ADR-0038 §2).
        /// Curl comes from <c>FingerShapes</c> — the Interaction SDK's own feature
        /// extractor, the same one <c>ShapeRecognizerActiveState</c> uses internally, so
        /// this remains compliant with §3.4's "do not hand-roll joint-angle math." Its raw
        /// units are degrees (~180 = flat, ~250 = curled, per finger); the normalisation
        /// bounds below are copied verbatim from the SDK's own <c>PalmGrabAPI.CURL_RANGE</c>
        /// — not a new tunable.
        /// </summary>
        private float ReadOpenness()
        {
            float middle = OpennessOf(HandFinger.Middle, 180f, 250f);
            float ring = OpennessOf(HandFinger.Ring, 180f, 250f);
            float pinky = OpennessOf(HandFinger.Pinky, 180f, 245f);
            return (middle + ring + pinky) / 3f;
        }

        private float OpennessOf(HandFinger finger, float curlAtFullyOpenDegrees, float curlAtFullyCurledDegrees)
        {
            float curlDegrees = _shapes.GetCurlValue(finger, _left);
            float normalizedCurl = Mathf.Clamp01(
                (curlDegrees - curlAtFullyOpenDegrees) / (curlAtFullyCurledDegrees - curlAtFullyOpenDegrees));
            return 1f - normalizedCurl;
        }

        /// <summary>
        /// Holds <see cref="_reportedPinch"/> through a per-finger confidence blip rather
        /// than forcing it false (ADR-0031's "sustain, do not release" principle, one level
        /// down at finger granularity) — otherwise a confidence blip mid-pinch could read
        /// as release-then-re-pinch, manufacturing a phantom rising edge in
        /// <see cref="ChordPinchDetector"/>.
        /// </summary>
        private void UpdatePinchState()
        {
            bool confident = _left.GetFingerIsHighConfidence(_pinchFinger) && _left.GetFingerIsHighConfidence(HandFinger.Thumb);
            if (confident)
            {
                _reportedPinch = _left.GetFingerIsPinching(_pinchFinger);
            }
        }

        /// <summary>
        /// Peak d(pinch strength)/dt over the last <see cref="_pinchRateSampleFrames"/>
        /// frames while closing, per second — the pinch-commit counterpart of
        /// <see cref="MetaXRHandPoseSource.LeftVerticalSpeedMetresPerSecond"/>, using the
        /// identical ADR-0035 backward-walk (stop at the first non-closing sample, so a
        /// finished close can never leak into a frame where the pinch has already reversed
        /// or stopped) with the sign flipped: here we track the peak <i>positive</i>
        /// (closing) rate, not the peak negative (downward) one.
        /// </summary>
        private float ReadPinchClosingRate()
        {
            float strength = _left.GetFingerPinchStrength(_pinchFinger);
            float deltaTime = Time.deltaTime;

            // Time.deltaTime, not a musical clock's dsp time: DspMusicalClock reads
            // AudioSettings.dspTime, which advances per audio buffer, not per Update() —
            // two consecutive frames could read the same value and divide by zero.
            float instant = _hasPreviousPinchStrength && deltaTime > 0f
                ? (strength - _previousPinchStrength) / deltaTime
                : 0f;

            _previousPinchStrength = strength;
            _hasPreviousPinchStrength = true;

            _pinchRateCursor = (_pinchRateCursor + 1) % _pinchRateSamples.Length;
            _pinchRateSamples[_pinchRateCursor] = instant;

            float peakClosing = 0f; // 0 = "not closing right now" is the safe neutral value
            int sampleCount = _pinchRateSamples.Length;
            for (int steps = 0; steps < sampleCount; steps++)
            {
                int index = (_pinchRateCursor - steps + sampleCount) % sampleCount;
                float sample = _pinchRateSamples[index];
                if (sample <= 0f)
                {
                    break; // direction has reversed (or opening, or a never-written slot) — stop
                }

                if (sample > peakClosing)
                {
                    peakClosing = sample;
                }
            }

            return peakClosing;
        }

        private static TrackingQuality ReadTracking(IHand hand)
        {
            if (hand == null || !hand.IsConnected || !hand.IsTrackedDataValid)
            {
                return TrackingQuality.NotTracked;
            }

            return hand.IsHighConfidence ? TrackingQuality.High : TrackingQuality.Low;
        }

        private T Resolve<T>(MonoBehaviour behaviour, string field) where T : class
        {
            if (behaviour == null)
            {
                Debug.LogError($"{nameof(MetaXRHandPostureSource)}: '{field}' is not assigned.", this);
                return null;
            }

            if (behaviour is T typed)
            {
                return typed;
            }

            Debug.LogError(
                $"{nameof(MetaXRHandPostureSource)}: '{field}' ({behaviour.GetType().Name}) is not an {typeof(T).Name}.",
                this);
            return null;
        }
    }
}
