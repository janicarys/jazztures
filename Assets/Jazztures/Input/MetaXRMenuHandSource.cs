using Jazztures.Core.Gesture;
using Jazztures.Core.Ports;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using UnityEngine;

namespace Jazztures.Input
{
    /// <summary>
    /// The Quest adapter for the menu's view of the right hand (ADR-0051): how fast the hand is
    /// moving left, right and down, and how tightly it is closed. All the deciding — thresholds,
    /// hysteresis, settling, the fist hold — is downstream in <c>MenuGestureRecognizer</c>; this
    /// class only measures.
    ///
    /// <para>
    /// <b>Strikes are sensed at the palm; flicks a little further out</b> (see <c>_flickReachMetres</c>), and neither at a fingertip. Closing a fist swings the fingertips
    /// tens of centimetres in a fraction of a second, which a fingertip would report as a strike
    /// just as the learner means to go back. The palm barely moves when the fingers curl. (The
    /// left-hand strike reads a fingertip for the opposite reason, ADR-0028: a wrist flick barely
    /// translates the wrist. A menu strike is a whole-hand motion, so the palm is enough — and if
    /// it proves too insensitive on device, this is the line to change.)
    /// </para>
    ///
    /// <para>
    /// <b>"Left" and "right" are the learner's.</b> The hand's world-space displacement each frame
    /// is projected onto the head's flattened right vector, then accumulated into a coordinate the
    /// speed windows differentiate. Projecting the <i>velocity</i> rather than the position means
    /// turning the head — which swings the head's right vector round the stationary hand —
    /// produces no speed, where a position measured in the head's frame would read it as a flick
    /// (§3.4: the user turns; gestures must not break).
    /// </para>
    ///
    /// <para>
    /// <b>The fist is the SDK's own finger curl.</b> The four long fingers' <c>FingerShapes</c> curl
    /// blended with knuckle flexion, normalised exactly as the SDK's own grab detection does and as
    /// <see cref="FingerBendBinder"/> does — no hand-rolled joint maths (§3.4). The least-closed
    /// finger decides, so one finger left straight is not a fist.
    /// </para>
    ///
    /// <para>
    /// <b>For the tutorial</b> (ADR-0053) it also reports the <i>left</i> hand's tracking quality, how
    /// closed the <i>most</i> closed finger is (an open hand needs none curled), and the angle between
    /// the right thumb and straight up. That angle is not hand-rolled joint maths: it is the SDK's own
    /// <c>TransformFeature.WristUp</c> value, called through <c>TransformFeatureValueProvider</c> with the
    /// same gravity-up reference the left hand's ii recogniser uses (ADR-0047), so "thumb up" means here
    /// what it means there.
    /// </para>
    ///
    /// <para>
    /// <see cref="CurrentFrame"/> memoizes per Unity frame: the speeds are numeric derivatives, and
    /// reading them twice in one frame would corrupt the next frame's baseline.
    /// </para>
    /// </summary>
    public sealed class MetaXRMenuHandSource : MonoBehaviour, IMenuHandSource
    {
        [Tooltip("The right Interaction SDK Hand component.")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Tooltip("The left Interaction SDK Hand component. Optional: only the tutorial uses it, to tell whether " +
                 "the learner's left hand is in view.")]
        [SerializeField] private MonoBehaviour _leftHand;

        [Tooltip("The centre-eye / head transform — defines which way is the learner's right. Falls back to Camera.main.")]
        [SerializeField] private Transform _head;

        [Tooltip("How far past the wrist, along the hand, the point that senses a FLICK sits, metres. A flick is a snap " +
                 "of the wrist, which barely moves the palm (it is a few centimetres from the pivot) but throws a point " +
                 "further out; sensing there lets a small, quick flick register as readily as a sweep of the arm. The point " +
                 "comes from the wrist and the middle knuckle only, so it ignores the fingers curling (a fist closing must " +
                 "not flick). 0 = sense at the palm, as before. Strikes still use the palm.")]
        [Min(0f)] [SerializeField] private float _flickReachMetres = 0.16f;

        [Tooltip("Recent frames to take the peak speed over (ADR-0028): a stroke that decelerates right as it " +
                 "crosses the threshold still registers.")]
        [Min(1)] [SerializeField] private int _speedSampleFrames = 3;

        // Index, middle, ring, pinky. The SDK's own PalmGrabAPI.CURL_RANGE, as FingerBendBinder copies it.
        private static readonly HandFinger[] LongFingers =
        {
            HandFinger.Index, HandFinger.Middle, HandFinger.Ring, HandFinger.Pinky,
        };

        private static readonly float[] BendFlat = { 180f, 180f, 180f, 180f };
        private static readonly float[] BendFull = { 250f, 250f, 250f, 245f };

        private readonly FingerShapes _shapes = new FingerShapes();

        private IHand _hand;
        private IHand _left;
        private TransformJointData _thumbData;
        private TransformConfig _thumbConfig;
        private DownwardSpeedWindow _leftward;
        private DownwardSpeedWindow _rightward;
        private DownwardSpeedWindow _downward;

        private bool _hasPrevious;
        private Vector3 _previousFlickPoint;
        private float _lateral;   // accumulated sideways displacement, metres, + = learner's right

        private MenuHandFrame _cachedFrame = MenuHandFrame.Untracked;
        private int _cachedFrameNumber = -1;

        private void Awake()
        {
            _hand = _rightHand as IHand;
            if (_hand == null)
            {
                Debug.LogError(
                    $"{nameof(MetaXRMenuHandSource)}: '{(_rightHand == null ? "<none>" : _rightHand.GetType().Name)}' " +
                    $"is not an {nameof(IHand)} — the menu cannot read the right hand.", this);
            }

            _left = _leftHand as IHand;
            if (_left == null)
            {
                Debug.LogWarning(
                    $"{nameof(MetaXRMenuHandSource)}: no left hand assigned, so the tutorial cannot see whether the " +
                    "learner's left hand is in view. It will take the right hand's tracking for both rather than " +
                    "wait forever (Jazztures ▸ Menu ▸ Set Up Gesture Menu + Tutorial assigns it).", this);
            }

            // Reused every frame - the SDK's WristUp value takes these two objects, so allocate them once.
            _thumbData = new TransformJointData
            {
                IsValid = true,
                Handedness = Oculus.Interaction.Input.Handedness.Right,
                TrackingSystemUp = Vector3.up,
                TrackingSystemForward = Vector3.forward,
            };
            _thumbConfig = new TransformConfig { UpVectorType = UpVectorType.Tracking };

            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform;
            }

            int frames = Mathf.Max(1, _speedSampleFrames);
            _leftward = new DownwardSpeedWindow(frames);
            _rightward = new DownwardSpeedWindow(frames);
            _downward = new DownwardSpeedWindow(frames);
        }

        private void OnDisable() => ResetMotion();

        public MenuHandFrame CurrentFrame
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

        private MenuHandFrame ComputeFrame()
        {
            TrackingQuality tracking = ReadTracking(_hand);
            // Without a left hand to read, borrow the right's rather than leave the tutorial waiting for a
            // hand it can never see.
            TrackingQuality leftTracking = _left != null ? ReadTracking(_left) : tracking;

            // Where each hand is relative to the learner's gaze - what "in front of you" and "out of view" mean.
            // Tracking quality is no help: the Quest keeps tracking hands hanging at the sides.
            float viewAngle = ViewAngleOf(_hand, tracking);
            float leftViewAngle = _left != null ? ViewAngleOf(_left, leftTracking) : viewAngle;

            if (tracking != TrackingQuality.High)
            {
                ResetMotion();
                return new MenuHandFrame(
                    tracking, leftTracking: leftTracking, viewAngleDegrees: viewAngle, leftViewAngleDegrees: leftViewAngle);
            }

            // A failed joint read makes the SDK's finger features report 0 — "flat" — which would
            // look like the hand snapping open. Never feed those frames in.
            if (!_hand.GetJointPosesFromWrist(out _) || !TryReadPoint(out Vector3 point))
            {
                ResetMotion();
                return new MenuHandFrame(
                    TrackingQuality.Low, leftTracking: leftTracking,
                    viewAngleDegrees: viewAngle, leftViewAngleDegrees: leftViewAngle);
            }

            float dt = Time.deltaTime;

            float down = _downward.Update(point.y, dt);

            // Sideways is sensed further out along the hand than downward is, so a wrist flick counts.
            Vector3 flickPoint = FlickPoint(point);
            if (_hasPrevious && _head != null)
            {
                Vector3 right = Vector3.ProjectOnPlane(_head.right, Vector3.up);
                if (right.sqrMagnitude > 1e-6f)
                {
                    _lateral += Vector3.Dot(flickPoint - _previousFlickPoint, right.normalized);
                }
            }

            _previousFlickPoint = flickPoint;
            _hasPrevious = true;

            float leftward = _leftward.Update(_lateral, dt);    // decreasing lateral = leftward
            float rightward = _rightward.Update(-_lateral, dt); // decreasing -lateral = rightward

            float fist = ReadClosure(out float mostClosed);
            return new MenuHandFrame(
                TrackingQuality.High, leftward, rightward, down, fist,
                leftTracking, mostClosed, ReadThumbUpAngle(), viewAngle, leftViewAngle);
        }

        /// <summary>
        /// A point <see cref="_flickReachMetres"/> beyond the wrist along the line from the wrist to the middle
        /// knuckle, where a flick of the wrist throws it. Built from those two joints alone, so finger curl cannot
        /// move it, and from their positions rather than an SDK axis constant (the OpenXR and OVR hand data use
        /// different conventions for which way "along the hand" is). Falls back to the palm.
        /// </summary>
        private Vector3 FlickPoint(Vector3 palm)
        {
            if (_flickReachMetres <= 0f
                || !_hand.GetRootPose(out Pose wrist)
                || !_hand.GetJointPose(HandJointId.HandMiddle1, out Pose knuckle))
            {
                return palm;
            }

            Vector3 along = knuckle.position - wrist.position;
            return along.sqrMagnitude < 1e-6f ? palm : wrist.position + along.normalized * _flickReachMetres;
        }

        /// <summary>
        /// The angle between where the learner is looking and the direction from the head to a hand's wrist,
        /// degrees; 180 if the hand is not tracked at all or there is no head or wrist pose to measure.
        /// </summary>
        private float ViewAngleOf(IHand hand, TrackingQuality tracking)
        {
            if (tracking == TrackingQuality.NotTracked || _head == null || !hand.GetRootPose(out Pose wrist))
            {
                return 180f;
            }

            Vector3 toHand = wrist.position - _head.position;
            return toHand.sqrMagnitude < 1e-6f ? 0f : Vector3.Angle(_head.forward, toHand);
        }

        /// <summary>
        /// The angle between the right thumb's side of the wrist and straight up, degrees: the SDK's own
        /// <c>WristUp</c> feature. 180 if the wrist pose is unavailable.
        /// </summary>
        private float ReadThumbUpAngle()
        {
            if (!_hand.GetRootPose(out Pose wrist))
            {
                return 180f;
            }

            _thumbData.WristPose = wrist;
            _thumbData.CenterEyePose = _head != null ? new Pose(_head.position, _head.rotation) : Pose.identity;
            return TransformFeatureValueProvider.GetValue(TransformFeature.WristUp, _thumbData, _thumbConfig);
        }

        /// <summary>The palm; failing that the middle knuckle, which does not move when the finger curls; failing that the wrist.</summary>
        private bool TryReadPoint(out Vector3 point)
        {
            if (_hand.GetJointPose(HandJointId.HandPalm, out Pose pose)
                || _hand.GetJointPose(HandJointId.HandMiddle1, out pose)
                || _hand.GetRootPose(out pose))
            {
                point = pose.position;
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>
        /// 0 (open) .. 1 (tight fist): the least-closed of the four long fingers, which is what a fist
        /// needs every finger to exceed. <paramref name="mostClosed"/> is the other end: the most-closed,
        /// which an open hand needs every finger to be under.
        /// </summary>
        private float ReadClosure(out float mostClosed)
        {
            float least = 1f;
            mostClosed = 0f;
            for (int f = 0; f < LongFingers.Length; f++)
            {
                float degrees = (_shapes.GetCurlValue(LongFingers[f], _hand) * 2f
                                 + _shapes.GetFlexionValue(LongFingers[f], _hand)) / 3f;
                float closure = Mathf.Clamp01((degrees - BendFlat[f]) / (BendFull[f] - BendFlat[f]));
                if (closure < least)
                {
                    least = closure;
                }

                if (closure > mostClosed)
                {
                    mostClosed = closure;
                }
            }

            return least;
        }

        /// <summary>
        /// Forget the baseline, so the first frame after a dropout cannot difference against a
        /// stale position and report a spurious spike (§3.5).
        /// </summary>
        private void ResetMotion()
        {
            _hasPrevious = false;
            _lateral = 0f;
            _leftward?.Reset();
            _rightward?.Reset();
            _downward?.Reset();
        }

        private static TrackingQuality ReadTracking(IHand hand)
        {
            if (hand == null || !hand.IsConnected || !hand.IsTrackedDataValid)
            {
                return TrackingQuality.NotTracked;
            }

            return hand.IsHighConfidence ? TrackingQuality.High : TrackingQuality.Low;
        }
    }
}
