using Jazztures.Config;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Music;
using Jazztures.Events;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.HandGrab.Visuals;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The lesson's ghost <b>right</b> hand (ADR-0055): with the right hand playing by bending fingers
    /// (ADR-0050, an exploration), a note is a finger press, so the ghost hand presses the finger that
    /// plays the cued note. A pure subscriber to <see cref="GhostFrameChannel"/> (§2.3) — it reads
    /// <see cref="GhostFrame.PressFinger"/> and <see cref="GhostFrame.PressProgress"/> and writes to nothing.
    ///
    /// <para>
    /// The hand is an open palm-down hand held out in front of the learner, the fingers a little relaxed.
    /// The pressing finger bends from that rest toward the fist; the others stay put. The finger is fully
    /// down on the note's onset - the lesson schedules it that way - so the fingertip meets the key as the
    /// note sounds, and in Try Yourself, where nothing sounds, the press is the cue to do it now.
    /// </para>
    ///
    /// <para>
    /// <b>No new poses were recorded.</b> As the tutorial's ghost (<see cref="TutorialGhostView"/>) does, it
    /// reuses the left-hand ghost captures: the <i>I</i> capture (open palm facing down) is the hand at rest,
    /// the <i>V</i> capture (fist) the bend. They are mirrored to the right hand first
    /// (<see cref="GhostPoseAsset.CreateRightHandPose"/>, ADR-0057). A finger's joints are blended from one
    /// toward the other, so a recapture of either follows here.
    /// </para>
    ///
    /// <para>
    /// Like the left ghost (ADR-0052) it is not tied to the learner's hand: it hovers at a fixed place in
    /// the learner's heading frame, so it neither jumps nor vanishes when the real hand is lost.
    /// </para>
    ///
    /// <para>
    /// <b>Labels (ADR-0058).</b> Each ghost fingertip carries the letter of the note that finger plays, as the
    /// learner's own fingertips do (<see cref="FingerNoteLabels"/>): it re-pitches with the chord the demonstrated
    /// notes belong to (<see cref="GhostFrame.MelodyPose"/>) and lights up as the finger lands on its key.
    /// </para>
    /// </summary>
    public sealed class RightGhostHandView : MonoBehaviour
    {
        [Header("Channel")]
        [SerializeField] private GhostFrameChannel _ghostChannel;

        [Header("The ghost")]
        [Tooltip("HandPuppet on the right ghost hand (SDK prefab OpenXRGhost-HandRight).")]
        [SerializeField] private HandPuppet _puppet;

        [Tooltip("The centre-eye / head transform. The ghost is placed and oriented in the learner's heading (§3.4).")]
        [SerializeField] private Transform _head;

        [Tooltip("Optional. Show the ghost only while this component is enabled - assign the finger-bend input, " +
                 "which the lesson flow switches on for a lesson that uses the right hand and off on the menu. " +
                 "Unassigned, the ghost shows whenever the lesson shows its ghosts.")]
        [SerializeField] private Behaviour _follow;

        [Header("Poses (reused from the left-hand ghost - Jazztures ▸ Ghost Pose Recorder)")]
        [Tooltip("The hand at rest: the I capture, open palm facing down.")]
        [SerializeField] private GhostPoseAsset _openPose;

        [Tooltip("The bend: the V capture, a fist. A pressing finger moves from the open pose toward this one.")]
        [SerializeField] private GhostPoseAsset _fistPose;

        [Header("Where it hovers (heading frame, from the head: x right, y up, z forward; metres)")]
        [SerializeField] private Vector3 _stationMetres = new Vector3(0.25f, -0.30f, 0.45f);

        [Header("Labels (ADR-0058)")]
        [Tooltip("Label size and colours come from here - the same settings as the learner's finger letters. " +
                 "Left empty, the built-in defaults apply. Jazztures ▸ Labels ▸ Set Up Ghost Hand Labels fills it in.")]
        [SerializeField] private MelodyConfig _config;

        [Header("Fingers")]
        [Tooltip("How far every finger rests toward the fist, 0..1, so a still hand reads as relaxed over the keys " +
                 "rather than rigid.")]
        [Range(0f, 1f)] [SerializeField] private float _restCurl = 0.12f;

        [Tooltip("How far a finger bends toward the fist when its key is fully down, 0..1. A key press is a modest " +
                 "bend, not a fist (ADR-0050).")]
        [Range(0f, 1f)] [SerializeField] private float _pressCurl = 0.6f;

        [Tooltip("Seconds a finger takes to come back up when another finger has taken over the cue before it " +
                 "finished lifting. A finger that is the cue follows the lesson's own timing instead.")]
        [Min(0.01f)] [SerializeField] private float _handOverSeconds = 0.2f;

        private const int FingerCount = 5;

        // How the labels are drawn and when they light, not how anything behaves - cosmetic, so not [TUNABLE].
        private const float HoverAboveTipMetres = 0.035f;  // as FingerNoteLabels hangs them over the real fingertips
        private const float TipBeyondJointFraction = 0.75f; // the tip is about this much of the last bone beyond its joint
        private const float LandedAt = 0.9f;                // a finger has "landed" on its key when it is this far down

        private readonly float[] _depth = new float[FingerCount];
        private readonly bool[] _wasLanded = new bool[FingerCount];
        private readonly GhostLabel[] _labels = new GhostLabel[FingerCount];
        private readonly Transform[] _distal = new Transform[FingerCount];
        private readonly Transform[] _intermediate = new Transform[FingerCount];
        private ChordFunction? _lettersFor;
        private bool _hasLetters;
        private HandPose _open;
        private HandPose _fist;
        private HandPose _scratch;
        private Quaternion _wrist = Quaternion.identity;
        private Renderer[] _renderers;
        private bool _ready;
        private bool _visible;

        private void Awake()
        {
            if (_puppet == null)
            {
                Debug.LogError($"{nameof(RightGhostHandView)}: assign the right ghost hand's HandPuppet.", this);
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
                    $"{nameof(RightGhostHandView)}: assign the open-hand (I) and fist (V) ghost poses.", this);
            }
            else
            {
                // The captures are of the left hand: mirror them, joints and wrist, for the right (ADR-0057).
                _open = _openPose.CreateRightHandPose();
                _fist = _fistPose.CreateRightHandPose();
                _scratch = new HandPose(Handedness.Right);
                _scratch.CopyFrom(_open, mirrorHandedness: true);

                _wrist = _openPose.RightWristRotationHeadLocal;
                _ready = _head != null;
            }

            for (int f = 0; f < FingerCount; f++)
            {
                _labels[f] = new GhostLabel(transform, $"Ghost Finger Label {f}", _config);

                // The two joints that point along the fingertip: the tip itself is not a rig joint.
                HandJointId[] joints = FingersMetadata.FINGER_TO_JOINTS[f];
                if (joints.Length >= 2)
                {
                    _distal[f] = JointTransform(joints[joints.Length - 1]);
                    _intermediate[f] = JointTransform(joints[joints.Length - 2]);
                }
            }

            SetVisible(false);
        }

        private Transform JointTransform(HandJointId id)
        {
            foreach (HandJointMap map in _puppet.JointMaps)
            {
                if (map.id == id)
                {
                    return map.transform;
                }
            }

            return null;
        }

        private void OnEnable() => _ghostChannel?.Register(OnGhost);

        private void OnDisable()
        {
            _ghostChannel?.Unregister(OnGhost);
            Rest();
        }

        private void OnGhost(GhostFrame frame)
        {
            bool shown = _ready && frame.Visible && (_follow == null || _follow.isActiveAndEnabled);
            SetVisible(shown);

            float dt = Time.deltaTime;
            if (!shown)
            {
                Rest();
                PlaceLabels(false, dt);
                return;
            }

            float handOver = dt / _handOverSeconds;
            for (int f = 0; f < FingerCount; f++)
            {
                _depth[f] = f == frame.PressFinger
                    ? (float)frame.PressProgress
                    : Mathf.MoveTowards(_depth[f], 0f, handOver);

                // A finger lights as it lands on its key - the moment the note sounds, or would.
                bool landed = _depth[f] >= LandedAt;
                if (landed && !_wasLanded[f])
                {
                    _labels[f].Flash();
                }

                _wasLanded[f] = landed;
            }

            Apply();

            if (!_hasLetters || frame.MelodyPose != _lettersFor)
            {
                _lettersFor = frame.MelodyPose;
                _hasLetters = true;
                SetLetters(frame.MelodyPose);
            }

            PlaceLabels(true, dt);
        }

        /// <summary>
        /// Name each finger's note for the chord the demonstrated notes belong to: finger <c>f</c> plays slot
        /// <c>f</c> of an octave (thumb = root … pinky = 9th), so "finger 3 is the 7th" whatever the chord.
        /// </summary>
        private void SetLetters(ChordFunction? function)
        {
            if (function is not { } fn)
            {
                for (int f = 0; f < FingerCount; f++)
                {
                    _labels[f].Text = string.Empty;
                }

                return;
            }

            ChordToneSet set = ChordToneSet.For(Progression.ChordFor(fn));
            for (int f = 0; f < FingerCount; f++)
            {
                _labels[f].Text = PitchNames.Of(set[f].Pitch.Class);
            }
        }

        private void PlaceLabels(bool visible, float deltaSeconds)
        {
            Vector3 viewer = _head != null ? _head.position : Vector3.zero;
            for (int f = 0; f < FingerCount; f++)
            {
                Transform distal = _distal[f];
                Transform intermediate = _intermediate[f];
                bool hasTip = distal != null && intermediate != null;

                // Straight up from the fingertip, as the letters over the learner's own hand are.
                Vector3 tip = hasTip
                    ? distal.position + (distal.position - intermediate.position) * TipBeyondJointFraction
                    : Vector3.zero;
                _labels[f].Place(visible && hasTip, tip + Vector3.up * HoverAboveTipMetres, viewer, deltaSeconds);
            }
        }

        private void Apply()
        {
            // Every finger joint is rewritten each frame from the open pose, so nothing is left bent from
            // an earlier press. Joints that belong to no finger keep the open pose's rotation.
            for (int f = 0; f < FingerCount; f++)
            {
                float curl = Mathf.Lerp(_restCurl, _pressCurl, _depth[f]);
                int[] joints = FingersMetadata.FINGER_TO_JOINT_INDEX[f];
                for (int j = 0; j < joints.Length; j++)
                {
                    int joint = joints[j];
                    _scratch.JointRotations[joint] = Quaternion.SlerpUnclamped(
                        _open.JointRotations[joint], _fist.JointRotations[joint], curl);
                }
            }

            _puppet.SetJointRotations(_scratch.JointRotations);

            Quaternion yaw = GhostHandView.YawOnly(_head);
            _puppet.SetRootPose(new Pose(_head.position + yaw * _stationMetres, yaw * _wrist));
        }

        /// <summary>Every finger up, ready for the next phrase.</summary>
        private void Rest()
        {
            for (int f = 0; f < FingerCount; f++)
            {
                _depth[f] = 0f;
                _wasLanded[f] = false;
                _labels[f]?.Reset();
            }
        }

        private void SetVisible(bool visible)
        {
            if (_visible == visible || _renderers == null)
            {
                return;
            }

            _visible = visible;
            foreach (Renderer r in _renderers)
            {
                if (r != null)
                {
                    r.enabled = visible;
                }
            }
        }
    }
}
