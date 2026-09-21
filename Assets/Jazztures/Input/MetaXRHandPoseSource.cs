using Jazztures.Core.Ports;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Input
{
    /// <summary>
    /// The Quest hand-tracking adapter (CLAUDE.md §2.2, §3.4). Reads three composed Meta
    /// XR Interaction SDK recognisers — one <see cref="IActiveState"/> per left-hand pose,
    /// each a <c>ShapeRecognizer</c> (finger curl) + <c>TransformRecognizer</c> (palm
    /// orientation) grouped through an <c>ActiveStateGroup</c> — plus the left
    /// <see cref="IHand"/> for tracking quality and fingertip strike velocity (ADR-0025 /
    /// ADR-0028). All temporal logic, the ii/I ambiguity rule, and strike detection live
    /// downstream in <c>GestureInterpreter</c> / <c>ChordStrikeDetector</c>; this class only
    /// reports what the SDK matches this frame.
    /// </summary>
    /// <remarks>
    /// Assign the pose-state and hand components in the inspector. The recogniser assets
    /// themselves are authored in the editor against the palm-right / fist / palm-down
    /// poses — see <c>Docs/DECISIONS.md</c> ADR-0006.
    ///
    /// <para>
    /// <see cref="CurrentFrame"/> memoizes its result per Unity frame
    /// (<see cref="Time.frameCount"/>) rather than recomputing on every access:
    /// <c>DomainEventBridge</c> and <c>PerformanceCompositionRoot</c> both read it from
    /// their own <c>Update</c>, in an order Unity does not guarantee, and fingertip
    /// velocity is a numeric derivative over <see cref="Time.deltaTime"/> — computing it
    /// twice in one frame would corrupt the next frame's baseline. Keying on the frame
    /// number (rather than computing in this component's own <c>Update</c>) makes the
    /// result correct regardless of which caller reaches it first each frame.
    /// </para>
    /// </remarks>
    public sealed class MetaXRHandPoseSource : MonoBehaviour, IHandPoseSource
    {
        [Tooltip("Composed recogniser for ii — open palm facing the user's right.")]
        [SerializeField] private MonoBehaviour _iiPoseState;

        [Tooltip("Composed recogniser for V — fist.")]
        [SerializeField] private MonoBehaviour _vPoseState;

        [Tooltip("Composed recogniser for I — open palm facing down.")]
        [SerializeField] private MonoBehaviour _iPoseState;

        [Tooltip("The left Interaction SDK Hand component.")]
        [SerializeField] private MonoBehaviour _leftHand;

        [Tooltip("The right Interaction SDK Hand component (tracking quality only).")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Tooltip("Recent frames to take the peak downward speed over (ADR-0028) — mirrors "
            + "TouchTargetBinder's fingertip speed sampling, so a strike that decelerates right "
            + "as it crosses the threshold still registers.")]
        [Min(1)] [SerializeField] private int _speedSampleFrames = 3;

        private IActiveState _ii;
        private IActiveState _v;
        private IActiveState _i;
        private IHand _left;
        private IHand _right;

        private HandPoseFrame _cachedFrame = HandPoseFrame.Untracked;
        private int _cachedFrameNumber = -1;
        private bool _hasPreviousTipHeight;
        private float _previousTipHeight;
        private float[] _verticalSpeedSamples;
        private int _speedCursor;

        private void Awake()
        {
            _ii = Resolve<IActiveState>(_iiPoseState, nameof(_iiPoseState));
            _v = Resolve<IActiveState>(_vPoseState, nameof(_vPoseState));
            _i = Resolve<IActiveState>(_iPoseState, nameof(_iPoseState));
            _left = Resolve<IHand>(_leftHand, nameof(_leftHand));
            _right = Resolve<IHand>(_rightHand, nameof(_rightHand));
            _verticalSpeedSamples = new float[Mathf.Max(1, _speedSampleFrames)];
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

            // The SDK's recognisers dereference per-finger state that only exists once
            // hand data has arrived, so querying them on an untracked hand throws rather
            // than reporting "no match". Nothing is lost by skipping them: the
            // interpreter discards the candidate whenever tracking is unusable (§3.5).
            HandPoseCandidate candidate = left == TrackingQuality.NotTracked
                ? HandPoseCandidate.None
                : ReadCandidate();

            float verticalSpeed = ReadVerticalSpeed(left);

            return new HandPoseFrame(candidate, left, right, verticalSpeed);
        }

        /// <summary>
        /// Signed peak-downward fingertip speed over the last <see cref="_speedSampleFrames"/>
        /// frames, m/s, negative = downward (ADR-0025 / ADR-0028) — the raw signal
        /// <see cref="Jazztures.Core.Gesture.ChordStrikeDetector"/> turns into a chord
        /// articulation. Gravity does not rotate with the user, so unlike the pose
        /// recognisers this needs no head-relative correction (§3.4 concerns the lateral
        /// axes, not vertical).
        ///
        /// <para>
        /// Reads <see cref="HandJointId.HandMiddleTip"/>, not the wrist: comping from the
        /// ADR-0026 horizontal ii pose is naturally a wrist-flick — a rotation about the
        /// wrist joint itself, which barely translates it — while the fingertip, farther
        /// from that pivot, moves substantially. Measuring at the wrist under-reported
        /// exactly the motion learners actually make (ADR-0028).
        /// </para>
        ///
        /// <para>
        /// Reports the <b>peak</b> downward speed over the last few frames, not the
        /// instantaneous one — mirroring <c>TouchTargetBinder</c>'s fingertip-speed sampling
        /// (ADR-0018) for the same reason: a real strike decelerates near the bottom of its
        /// arc, and without this a motion that peaked one frame before crossing the enter
        /// threshold would under-report its own speed right when it matters.
        /// </para>
        ///
        /// <para>
        /// Only accumulates while tracking is High: the baseline resets on any dip, so the
        /// first frame after regaining tracking always reports 0 rather than a spurious
        /// spike computed against a stale position (§3.5 — degrade gracefully). A stale
        /// sample left in the window during a brief dip is harmless — every consumer already
        /// gates on <see cref="Jazztures.Core.Gesture.GestureInterpreter.TrackingUsable"/>.
        /// </para>
        /// </summary>
        private float ReadVerticalSpeed(TrackingQuality left)
        {
            if (left != TrackingQuality.High || !_left.GetJointPose(HandJointId.HandMiddleTip, out Pose tipPose))
            {
                _hasPreviousTipHeight = false;
                return 0f;
            }

            float height = tipPose.position.y;
            float deltaTime = Time.deltaTime;

            float instant = _hasPreviousTipHeight && deltaTime > 0f
                ? (height - _previousTipHeight) / deltaTime
                : 0f;

            _previousTipHeight = height;
            _hasPreviousTipHeight = true;

            _speedCursor = (_speedCursor + 1) % _verticalSpeedSamples.Length;
            _verticalSpeedSamples[_speedCursor] = instant;

            float peakDownward = 0f; // 0 = "no downward motion in this window" is the safe neutral value
            for (int i = 0; i < _verticalSpeedSamples.Length; i++)
            {
                if (_verticalSpeedSamples[i] < peakDownward)
                {
                    peakDownward = _verticalSpeedSamples[i];
                }
            }

            return peakDownward;
        }

        private HandPoseCandidate ReadCandidate()
        {
            bool ii = _ii != null && _ii.Active;
            bool v = _v != null && _v.Active;
            bool i = _i != null && _i.Active;

            int matches = (ii ? 1 : 0) + (v ? 1 : 0) + (i ? 1 : 0);
            if (matches == 0)
            {
                return HandPoseCandidate.None;
            }

            if (matches == 1)
            {
                return ii ? HandPoseCandidate.Ii : v ? HandPoseCandidate.V : HandPoseCandidate.I;
            }

            // More than one pose matches — never guess (§3.4).
            return HandPoseCandidate.Ambiguous;
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
                Debug.LogError($"{nameof(MetaXRHandPoseSource)}: '{field}' is not assigned.", this);
                return null;
            }

            if (behaviour is T typed)
            {
                return typed;
            }

            Debug.LogError(
                $"{nameof(MetaXRHandPoseSource)}: '{field}' ({behaviour.GetType().Name}) is not an {typeof(T).Name}.",
                this);
            return null;
        }
    }
}
