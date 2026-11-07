using Jazztures.Config;
using Jazztures.Core.Melody;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Input
{
    /// <summary>
    /// Reads the right Interaction SDK hand for the palm-swing input (ADR-0078) and reduces it to numbers: a
    /// gate (tracking good, palm facing left, hand open), the hand's pitch angle in world space, and the palm
    /// centre's position in a lane frame.
    ///
    /// <para>
    /// <b>Gate.</b> Tracked data valid, index/middle/ring/pinky high-confidence (the thumb flickers and is left
    /// out, as in <c>FingerBendBinder</c>), the palm normal's component along the user's right vector below
    /// <c>Palm Facing Left Dot</c>, and every one of those four fingers' curl below <c>Open Hand Max Curl</c>.
    /// Curl comes from <c>FingerShapes</c>, normalised with the SDK's own <c>CURL_RANGE</c> (CLAUDE.md §3.4: no
    /// hand-rolled joint maths for the pose itself).
    /// </para>
    /// <para>
    /// <b>Pitch angle.</b> The direction wrist → middle knuckle, as an angle above the horizontal in <i>world</i>
    /// space, not relative to the palm, so wrist, elbow and shoulder tilt all count.
    /// </para>
    /// <para>
    /// <b>Lane frame.</b> Origin = the palm centre when the gate first opens (or on <see cref="Recenter"/>);
    /// left-right axis = the head's right vector flattened to the horizontal at that moment (yaw only).
    /// Lateral position and height are the palm centre's offsets from it.
    /// </para>
    /// </summary>
    public sealed class MetaXRPalmSwingSampleSource : MonoBehaviour, IPalmSwingSampleSource
    {
        [Tooltip("The right Interaction SDK Hand component.")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Tooltip("The head (centre eye) transform. Defaults to Camera.main.")]
        [SerializeField] private Transform _head;

        [SerializeField] private PalmSwingSettings _settings;

        private static readonly HandFinger[] OpenFingers =
        {
            HandFinger.Index, HandFinger.Middle, HandFinger.Ring, HandFinger.Pinky,
        };

        // The SDK's own PalmGrabAPI.CURL_RANGE for index..pinky, copied verbatim — flat and fully bent, in the
        // degrees FingerShapes reports. Not new tunables.
        private static readonly float[] CurlFlat = { 180f, 180f, 180f, 180f };
        private static readonly float[] CurlFull = { 250f, 250f, 250f, 245f };

        private readonly FingerShapes _shapes = new FingerShapes();
        private IHand _hand;
        private bool _laneSet;
        private Vector3 _laneOrigin;
        private Vector3 _laneRight = Vector3.right;
        private bool _recenterRequested;

        /// <summary>For the visuals and diagnostics: why the gate is closed, or null while it is open.</summary>
        public string GateClosedReason { get; private set; } = "no hand";

        /// <summary>The lane's origin in world space (valid once the gate has opened).</summary>
        public Vector3 LaneOrigin => _laneOrigin;

        /// <summary>The lane's left-right axis in world space.</summary>
        public Vector3 LaneRight => _laneRight;

        public bool LaneSet => _laneSet;

        private void Awake()
        {
            _hand = _rightHand as IHand;
            if (_hand == null)
            {
                Debug.LogError(
                    $"{nameof(MetaXRPalmSwingSampleSource)}: '{(_rightHand == null ? "<none>" : _rightHand.GetType().Name)}' " +
                    $"is not an {nameof(IHand)}.", this);
                enabled = false;
                return;
            }

            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform;
            }
        }

        private void OnDisable()
        {
            // A new lesson / exercise gets a fresh lane, set where the hand is when it first opens.
            _laneSet = false;
        }

        public void Recenter() => _recenterRequested = true;

        public PalmSwingSample Read()
        {
            if (_hand == null || _head == null || _settings == null)
            {
                GateClosedReason = "not wired";
                return Closed();
            }

            if (!_hand.IsTrackedDataValid
                || !_hand.GetJointPose(HandJointId.HandWristRoot, out Pose wrist)
                || !_hand.GetJointPose(HandJointId.HandMiddle1, out Pose knuckle))
            {
                GateClosedReason = "hand lost";
                return Closed();
            }

            for (int f = 0; f < OpenFingers.Length; f++)
            {
                if (!_hand.GetFingerIsHighConfidence(OpenFingers[f]))
                {
                    GateClosedReason = "low confidence";
                    return Closed();
                }
            }

            Vector3 userRight = Vector3.ProjectOnPlane(_head.right, Vector3.up);
            if (userRight.sqrMagnitude < 1e-6f)
            {
                GateClosedReason = "head looking straight up or down";
                return Closed();
            }

            userRight.Normalize();

            Vector3 palmar = _hand.Handedness == Handedness.Left ? Constants.LeftPalmar : Constants.RightPalmar;
            Vector3 palmNormal = wrist.rotation * palmar;
            if (Vector3.Dot(palmNormal, userRight) >= _settings.PalmFacingLeftDot)
            {
                GateClosedReason = "palm not facing left";
                return Closed();
            }

            for (int f = 0; f < OpenFingers.Length; f++)
            {
                float degrees = _shapes.GetCurlValue(OpenFingers[f], _hand);
                float curl = Mathf.Clamp01((degrees - CurlFlat[f]) / (CurlFull[f] - CurlFlat[f]));
                if (curl >= _settings.OpenHandMaxCurl)
                {
                    GateClosedReason = "hand not open";
                    return Closed();
                }
            }

            GateClosedReason = null;

            Vector3 palmCentre = (wrist.position + knuckle.position) * 0.5f;
            if (!_laneSet || _recenterRequested)
            {
                _laneOrigin = palmCentre;
                _laneRight = userRight;
                _laneSet = true;
                _recenterRequested = false;
            }

            Vector3 along = (knuckle.position - wrist.position).normalized;
            float pitch = Mathf.Asin(Mathf.Clamp(along.y, -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 offset = palmCentre - _laneOrigin;
            return new PalmSwingSample(0.0, true, pitch, Vector3.Dot(offset, _laneRight), offset.y);
        }

        private static PalmSwingSample Closed() => new PalmSwingSample(0.0, false, 0f, 0f, 0f);
    }
}
