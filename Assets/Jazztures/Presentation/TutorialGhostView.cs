using Jazztures.Core.Tutorial;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.HandGrab.Visuals;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The tutorial's ghost <b>right</b> hand (ADR-0053): a translucent hand held out in front of the
    /// learner that does the thing the current step asks for — closes into a fist, holds an open hand
    /// thumb up, flicks sideways, strikes down — on a loop until the learner does it. A pure
    /// subscriber's view: it is told which demo to play and reads nothing.
    ///
    /// <para>
    /// <b>No new poses were recorded.</b> The SDK ships a right ghost hand (<c>OpenXRGhost-HandRight</c>),
    /// so the left-hand ghost poses already authored through the Ghost Pose Recorder are reused: the <i>ii</i>
    /// capture (open palm, thumb up) is the open hand, the <i>V</i> capture the fist. They are mirrored to the
    /// right hand - every joint rotation and the wrist (<see cref="GhostPoseAsset.CreateRightHandPose"/>,
    /// ADR-0057); copied unchanged, the thumb comes out badly distorted. Re-capture those two poses and the
    /// tutorial follows.
    /// </para>
    ///
    /// <para>
    /// Like the left ghost (ADR-0052) it is <b>not</b> tied to the learner's own hand: it hovers at a fixed
    /// place in the learner's heading frame, to the right and below the menu, so it neither jumps nor
    /// vanishes when the real hand does — which is exactly what happens in the first steps.
    /// </para>
    /// </summary>
    public sealed class TutorialGhostView : MonoBehaviour
    {
        [Header("The ghost")]
        [Tooltip("HandPuppet on the right ghost hand (SDK prefab OpenXRGhost-HandRight).")]
        [SerializeField] private HandPuppet _puppet;

        [Tooltip("The centre-eye / head transform. The ghost is placed and oriented in the learner's heading (§3.4).")]
        [SerializeField] private Transform _head;

        [Header("Poses (reused from the left-hand ghost — Jazztures ▸ Ghost Pose Recorder)")]
        [Tooltip("The open hand with the thumb up: the ii capture.")]
        [SerializeField] private GhostPoseAsset _openPose;

        [Tooltip("The fist: the V capture.")]
        [SerializeField] private GhostPoseAsset _fistPose;

        [Header("Where it hovers (heading frame, from the head: x right, y up, z forward; metres)")]
        [SerializeField] private Vector3 _stationMetres = new Vector3(0.30f, -0.24f, 0.45f);

        [Header("Motion")]
        [Tooltip("How far each side of the station the flick snaps, metres. A flick is short: a snap of the wrist, " +
                 "not a sweep of the arm.")]
        [Min(0f)] [SerializeField] private float _flickWidthMetres = 0.09f;

        [Tooltip("How far the wrist turns toward the flick, degrees. The turn is what makes it read as a flick.")]
        [Range(0f, 45f)] [SerializeField] private float _flickYawDegrees = 22f;

        [Tooltip("How far above the station the hand waits before a strike, metres.")]
        [Min(0f)] [SerializeField] private float _strikeHoverMetres = 0.06f;

        [Tooltip("How far below the station the strike drops, metres.")]
        [Min(0f)] [SerializeField] private float _strikeDropMetres = 0.20f;

        [Tooltip("A slow bob while the hand is held up, metres, so a still hand still reads as alive. 0 = none.")]
        [Min(0f)] [SerializeField] private float _holdBobMetres = 0.008f;

        [Header("Easing")]
        [Tooltip("The curve every movement follows from its start (0) to its end (1): the hand leaves quickly and " +
                 "settles into place, as a hand does, instead of starting and stopping like a machine. Edit the " +
                 "curve to taste - a flatter ending is a softer landing; a bump past 1 is a little overshoot.")]
        [SerializeField] private AnimationCurve _moveCurve = EaseOutCurve();

        [Tooltip("The same for the strike's drop. An ease-out drop leaves at speed and lands softly; an ease-in curve " +
                 "(slow start, fast finish) makes the strike hit harder.")]
        [SerializeField] private AnimationCurve _strikeDropCurve = EaseOutCurve();

        [Tooltip("When the demo changes (the learner has done the last one), the hand glides from where it is to the " +
                 "new demo's motion over this long, on the movement curve, rather than jumping. 0 = jump.")]
        [Min(0f)] [SerializeField] private float _handoverSeconds = 0.4f;

        private const float FistPeriod = 3.6f;
        private const float FlickPeriod = 3.2f;
        private const float StrikePeriod = 2.6f;

        private HandPose _open;
        private HandPose _fist;
        private HandPose _scratch;
        private Quaternion _openWrist = Quaternion.identity;
        private Quaternion _fistWrist = Quaternion.identity;
        private Renderer[] _renderers;
        private TutorialDemo _demo;
        private float _time;
        private bool _ready;
        private bool _visible;

        // Where the hand was drawn last frame, so a new demo can glide on from there.
        private Vector3 _shownOffset;
        private float _shownBlend;
        private float _shownTurn;
        private Vector3 _fromOffset;
        private float _fromBlend;
        private float _fromTurn;
        private float _handoverT = float.MaxValue;

        private void Awake()
        {
            if (_puppet == null)
            {
                Debug.LogError($"{nameof(TutorialGhostView)}: assign the right ghost hand's HandPuppet.", this);
                enabled = false;
                return;
            }

            _renderers = _puppet.GetComponentsInChildren<Renderer>(includeInactive: true);

            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform;
            }

            if (_openPose == null || _fistPose == null)
            {
                Debug.LogError(
                    $"{nameof(TutorialGhostView)}: assign the open-hand (ii) and fist (V) ghost poses.", this);
            }
            else
            {
                // The captures are of the left hand: mirror them, joints and wrist, for the right (ADR-0057).
                _open = _openPose.CreateRightHandPose();
                _fist = _fistPose.CreateRightHandPose();
                _scratch = new HandPose(Handedness.Right);
                _scratch.CopyFrom(_open, mirrorHandedness: true);

                _openWrist = _openPose.RightWristRotationHeadLocal;
                _fistWrist = _fistPose.RightWristRotationHeadLocal;
                _ready = _head != null;
            }

            SetVisible(false);
        }

        /// <summary>Start a demo on a loop, from its beginning. <see cref="TutorialDemo.None"/> hides the ghost.</summary>
        public void Play(TutorialDemo demo)
        {
            if (demo == _demo)
            {
                return;
            }

            // Glide on from wherever the hand is - unless it was not on screen, in which case it simply appears.
            if (_visible && demo != TutorialDemo.None)
            {
                _fromOffset = _shownOffset;
                _fromBlend = _shownBlend;
                _fromTurn = _shownTurn;
                _handoverT = 0f;
            }
            else
            {
                _handoverT = float.MaxValue;
            }

            _demo = demo;
            _time = 0f;
            SetVisible(demo != TutorialDemo.None && _ready);
        }

        /// <summary>Hide the ghost.</summary>
        public void Stop() => Play(TutorialDemo.None);

        private void Update()
        {
            if (!_visible)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            _time += dt;
            Evaluate(_demo, _time, out Vector3 offset, out float blend, out float turnDegrees);

            if (_handoverSeconds > 0f && _handoverT < _handoverSeconds)
            {
                _handoverT += dt;
                float k = Move(_handoverT / _handoverSeconds);
                offset = Vector3.LerpUnclamped(_fromOffset, offset, k);
                blend = Mathf.LerpUnclamped(_fromBlend, blend, k);
                turnDegrees = Mathf.LerpUnclamped(_fromTurn, turnDegrees, k);
            }

            _shownOffset = offset;
            _shownBlend = blend;
            _shownTurn = turnDegrees;

            HandPose.Lerp(_open, _fist, blend, ref _scratch);
            _puppet.SetJointRotations(_scratch.JointRotations);

            Quaternion yaw = GhostHandView.YawOnly(_head);
            Quaternion wrist = Quaternion.Slerp(_openWrist, _fistWrist, blend);
            Vector3 position = _head.position + yaw * (_stationMetres + offset);
            _puppet.SetRootPose(new Pose(position, yaw * Quaternion.Euler(0f, turnDegrees, 0f) * wrist));
        }

        /// <summary>
        /// Where the hand is, relative to the station, and how far it has closed (0 open .. 1 fist),
        /// <paramref name="time"/> seconds into the loop.
        /// </summary>
        private void Evaluate(TutorialDemo demo, float time, out Vector3 offset, out float blend, out float turnDegrees)
        {
            offset = Vector3.zero;
            blend = 0f;
            turnDegrees = 0f;

            switch (demo)
            {
                case TutorialDemo.Fist:
                {
                    // open, close, hold, open, rest
                    float p = Mathf.Repeat(time, FistPeriod);
                    blend = p < 1.0f ? 0f
                          : p < 1.6f ? Move((p - 1.0f) / 0.6f)
                          : p < 2.8f ? 1f
                          : p < 3.4f ? 1f - Move((p - 2.8f) / 0.6f)
                          : 0f;
                    break;
                }

                case TutorialDemo.ThumbUp:
                    offset = new Vector3(0f, _holdBobMetres * Mathf.Sin(time * 2.2f), 0f);
                    break;

                case TutorialDemo.Flick:
                {
                    // Rest; snap right (0.12 s, fast out of the blocks), hold a beat, relax back; rest;
                    // snap left; hold; relax back. The wrist turns with it. A flick is quick OUT and slow BACK.
                    float p = Mathf.Repeat(time, FlickPeriod);
                    float s = p < 0.60f ? 0f
                            : p < 0.72f ? Move((p - 0.60f) / 0.12f)
                            : p < 1.05f ? 1f
                            : p < 1.55f ? 1f - Move((p - 1.05f) / 0.5f)
                            : p < 2.20f ? 0f
                            : p < 2.32f ? -Move((p - 2.20f) / 0.12f)
                            : p < 2.65f ? -1f
                            : p < 3.15f ? -(1f - Move((p - 2.65f) / 0.5f))
                            : 0f;
                    offset = new Vector3(s * _flickWidthMetres, 0f, 0f);
                    turnDegrees = s * _flickYawDegrees;
                    break;
                }

                case TutorialDemo.Strike:
                {
                    // wait up, drop fast, rest, lift slowly
                    float p = Mathf.Repeat(time, StrikePeriod);
                    float up = _strikeHoverMetres;
                    float down = -_strikeDropMetres;
                    float y = p < 0.7f ? up
                            : p < 0.95f ? Mathf.LerpUnclamped(up, down, Drop((p - 0.7f) / 0.25f))
                            : p < 1.25f ? down
                            : p < 2.05f ? Mathf.LerpUnclamped(down, up, Move((p - 1.25f) / 0.8f))
                            : up;
                    offset = new Vector3(0f, y, 0f);
                    break;
                }
            }
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (_renderers == null)
            {
                return;
            }

            foreach (Renderer r in _renderers)
            {
                if (r != null)
                {
                    r.enabled = visible;
                }
            }
        }

        /// <summary>How far through a movement the hand is, <paramref name="u"/> being how far through the time it is (0..1).</summary>
        private float Move(float u) => Follow(_moveCurve, u);

        /// <summary>As <see cref="Move"/>, for the strike's drop.</summary>
        private float Drop(float u) => Follow(_strikeDropCurve, u);

        private static float Follow(AnimationCurve curve, float u)
        {
            u = Mathf.Clamp01(u);
            return curve != null && curve.length >= 2 ? curve.Evaluate(u) : EaseOutCubic(u);
        }

        /// <summary>The fallback if a curve is emptied in the Inspector: fast at first, settling.</summary>
        private static float EaseOutCubic(float t)
        {
            t = 1f - Mathf.Clamp01(t);
            return 1f - t * t * t;
        }

        /// <summary>
        /// Ease-out as an editable curve: leaves at three times the average speed and arrives at rest. The cubic
        /// 1 - (1 - t)^3, which is exactly what a Hermite curve with these two tangents is.
        /// </summary>
        private static AnimationCurve EaseOutCurve() => new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 3f),
            new Keyframe(1f, 1f, 0f, 0f));
    }
}
