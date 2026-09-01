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
    /// <see cref="IHand"/> for tracking quality. All temporal logic and the ii/I
    /// ambiguity rule live downstream in <c>GestureInterpreter</c>; this class only
    /// reports what the SDK matches this frame.
    /// </summary>
    /// <remarks>
    /// Assign the pose-state and hand components in the inspector. The recogniser assets
    /// themselves are authored in the editor against the palm-right / fist / palm-down
    /// poses — see <c>Docs/DECISIONS.md</c> ADR-0006.
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

        private IActiveState _ii;
        private IActiveState _v;
        private IActiveState _i;
        private IHand _left;
        private IHand _right;

        private void Awake()
        {
            _ii = Resolve<IActiveState>(_iiPoseState, nameof(_iiPoseState));
            _v = Resolve<IActiveState>(_vPoseState, nameof(_vPoseState));
            _i = Resolve<IActiveState>(_iPoseState, nameof(_iPoseState));
            _left = Resolve<IHand>(_leftHand, nameof(_leftHand));
            _right = Resolve<IHand>(_rightHand, nameof(_rightHand));
        }

        public HandPoseFrame CurrentFrame => new HandPoseFrame(
            ReadCandidate(),
            ReadTracking(_left),
            ReadTracking(_right));

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
