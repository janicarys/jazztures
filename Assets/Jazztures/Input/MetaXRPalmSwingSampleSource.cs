using Jazztures.Config;
using Jazztures.Core.Melody;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
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
    /// space, not relative to the palm, so wrist, elbow and shoulder tilt all count. It is reported <i>relative to the
    /// angle the hand rested at</i> once settled in the pose, so neutral is wherever the hand naturally sits.
    /// </para>
    /// <para>
    /// <b>Lane frame.</b> Origin = the palm centre once the pose has settled (or on <see cref="Recenter"/>); left-right
    /// axis = the body's right, from the head's yaw only. The lane is then <b>body-anchored</b>, as the left hand's strike
    /// plate is (ADR-0040): it follows the head's position every frame and its yaw lazily (the same <c>LazyRecenter</c>),
    /// horizontally only, so walking or turning carries the trigger line and the plane the ray is cast at along with the
    /// learner, and the trigger's lateral position is measured against the lane as it now stands.
    /// Lateral position is, by the <c>Crossing Source</c> setting, the palm centre's offset along it
    /// (<c>Palm Position</c>) or the hit point of a ray along the fingers on a vertical plane in front of the user
    /// (<c>Finger Ray</c>, where the origin is the resting ray's hit, so the line stands where the fingers pointed). Height
    /// is always the palm centre's vertical offset from where it rested.
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
        private Vector3 _laneOrigin;            // where the trigger line stands: the resting palm, or the resting ray's hit
        private float _restPalmY;               // the palm centre's height when the lane was set: the origin of "height"
        private LazyRecenter _recenter;         // follows the head's yaw lazily, as the strike plate and melody arc do (ADR-0040)
        private float _lastYaw;
        private Quaternion _facing = Quaternion.identity;
        private Vector3 _originBody;            // the line's origin and the plane's point in the body frame: x, z relative to the
        private Vector3 _planeBody;             // head and turned by the committed facing; y is a world height (no vertical follow)
        private Vector3 _laneRight = Vector3.right;
        private Vector3 _laneForward = Vector3.forward;
        private Vector3 _planePoint;            // Finger Ray: a point on the vertical plane the ray is cast at
        private float _restPitch;               // the hand's pitch angle when the lane was set: "neutral"
        private double _openSince = -1.0;       // when the gate (pose) last became continuously open, for the rest settle
        private bool _recenterRequested;

        /// <summary>For the visuals and diagnostics: why the gate is closed, or null while it is open.</summary>
        public string GateClosedReason { get; private set; } = "no hand";

        /// <summary>The lane's origin in world space (valid once the gate has opened).</summary>
        public Vector3 LaneOrigin => _laneOrigin;

        /// <summary>The world height of the middle region: the palm's height when the lane was set (swing slices).</summary>
        public float RegionOriginY => _restPalmY;

        /// <summary>The lane's left-right axis in world space.</summary>
        public Vector3 LaneRight => _laneRight;

        public bool LaneSet => _laneSet;

        /// <summary>
        /// Where the hand is pointing now, in world space: the ray's hit on the plane (Finger Ray) or the palm centre
        /// (Palm Position). For the view's cursor. Valid only while <see cref="PointerValid"/>.
        /// </summary>
        public Vector3 PointerWorld { get; private set; }

        public bool PointerValid { get; private set; }

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
            _recenter = null;
        }

        public void Recenter() => _recenterRequested = true;

        public PalmSwingSample Read()
        {
            if (_hand == null || _head == null || _settings == null)
            {
                GateClosedReason = "not wired";
                return Closed();
            }

            UpdateFrame();

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

            Vector3 userRight = _facing * Vector3.right; // the body's right: the lane's frame, not the head's instantaneous one

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

            Vector3 palmCentre = (wrist.position + knuckle.position) * 0.5f;
            Vector3 along = (knuckle.position - wrist.position).normalized;
            bool ray = _settings.CrossingSource == PalmSwingCrossingSource.FingerRay;
            Vector3 forward = _facing * Vector3.forward;

            // Finger Ray: the fingers must point toward the front, or the ray meets the plane far away or behind.
            if (ray && Vector3.Dot(along, forward) < _settings.MinPointingFacing)
            {
                GateClosedReason = "fingers not pointing forward";
                return Closed();
            }

            float rawPitch = Mathf.Asin(Mathf.Clamp(along.y, -1f, 1f)) * Mathf.Rad2Deg;

            if (!_laneSet || _recenterRequested)
            {
                // Neutral is where the hand sits once it has settled into the pose, not the horizontal: a relaxed
                // handshake pose points a little downward, and a threshold measured from the horizontal made "Same" unreachable.
                double now = Time.unscaledTimeAsDouble;
                if (_openSince < 0.0)
                {
                    _openSince = now;
                }

                if (!_recenterRequested && now - _openSince < _settings.RestSettleSeconds)
                {
                    GateClosedReason = "settling";
                    return Closed(resetSettle: false);
                }

                _restPitch = rawPitch;
                _laneRight = userRight;
                _laneForward = forward;
                _restPalmY = palmCentre.y;
                _planePoint = palmCentre + forward * _settings.PointingPlaneDistanceMetres;
                _laneOrigin = ray ? RayHit(palmCentre, along) : palmCentre;
                _laneOrigin.y = _restPalmY; // swing slices: the line, its regions and the lesson circles are centred at the palm's resting height
                _originBody = ToBody(_laneOrigin);
                _planeBody = ToBody(_planePoint);
                _laneSet = true;
                _recenterRequested = false;
            }

            GateClosedReason = null;
            Vector3 position = ray ? RayHit(palmCentre, along) : palmCentre;
            PointerWorld = new Vector3(position.x, palmCentre.y, position.z); // swing slices: the dot sits at the PALM's height, the line's lateral
            PointerValid = true;
            float lateral = Vector3.Dot(position - _laneOrigin, _laneRight);
            float height = palmCentre.y - _restPalmY;
            return new PalmSwingSample(0.0, true, rawPitch - _restPitch, lateral, height);
        }

        // The body frame: the head's position, turned by the lazily-followed facing, like the strike plate. The lane
        // (the trigger line, and the plane the finger ray is cast at) is stored in it, so walking or turning carries both
        // the visual line and the actual target along. Horizontal only: the stored y is a world height.
        private void UpdateFrame()
        {
            Vector3 flat = _head.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude > 1e-6f)
            {
                _lastYaw = Mathf.Atan2(flat.x, flat.z); // looking straight up or down: hold the last heading
            }

            if (_recenter == null)
            {
                _recenter = new LazyRecenter(LazyRecenterSettings.Default, _lastYaw);
            }
            else if (_recenterRequested)
            {
                _recenter.Reset(_lastYaw);
            }

            float committed = (float)_recenter.Update(_lastYaw, Time.unscaledDeltaTime);
            _facing = Quaternion.AngleAxis(committed * Mathf.Rad2Deg, Vector3.up);

            if (_laneSet)
            {
                _laneRight = _facing * Vector3.right;
                _laneForward = _facing * Vector3.forward;
                _laneOrigin = FromBody(_originBody);
                _planePoint = FromBody(_planeBody);
            }
        }

        private Vector3 ToBody(Vector3 world)
        {
            Vector3 local = Quaternion.Inverse(_facing) * new Vector3(world.x - _head.position.x, 0f, world.z - _head.position.z);
            return new Vector3(local.x, world.y, local.z);
        }

        private Vector3 FromBody(Vector3 body)
        {
            Vector3 flat = _facing * new Vector3(body.x, 0f, body.z);
            return new Vector3(_head.position.x + flat.x, body.y, _head.position.z + flat.z);
        }

        // Where a ray from `origin` along `direction` meets the vertical plane through _planePoint. The caller has
        // checked the direction points toward the front, so the divisor is not near zero.
        private Vector3 RayHit(Vector3 origin, Vector3 direction)
        {
            float t = Vector3.Dot(_planePoint - origin, _laneForward) / Vector3.Dot(direction, _laneForward);
            return origin + direction * t;
        }

        // `resetSettle` is false only while the hand is still settling into the pose: any other reason to close the gate
        // means the pose was lost, so the settle starts again.
        private PalmSwingSample Closed(bool resetSettle = true)
        {
            if (resetSettle)
            {
                _openSince = -1.0;
            }

            PointerValid = false;
            return new PalmSwingSample(0.0, false, 0f, 0f, 0f);
        }
    }
}
