using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Events;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.HandGrab.Visuals;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The ghost-hand demonstration renderer (ADR-0012) — the M6 replacement for the text
    /// prompt. A pure subscriber to <see cref="GhostFrameChannel"/> (§2.3): it never
    /// raises anything, it just shows what the lesson is asking for.
    ///
    /// <list type="bullet">
    ///   <item><b>Left hand</b> — a translucent articulated mesh (<see cref="HandPuppet"/>)
    ///   superimposed on the learner's own wrist, morphing between the ii / V / I target
    ///   poses. The fingers show the delta between where the hand is and where it should be.</item>
    ///   <item><b>Right hand</b> — no ghost fingertip. The demonstrated melody stop lights
    ///   (<see cref="TouchTarget.SetDemonstrated"/>); the learner chooses the reach.</item>
    /// </list>
    ///
    /// <para>
    /// Visible only in ghost-hand modes — the runner sends <see cref="GhostFrame.Hidden"/>
    /// in Test Yourself / Compose (<c>ModePolicy.GhostHandsVisible</c>).
    /// </para>
    /// </summary>
    public sealed class GhostHandView : MonoBehaviour
    {
        [Header("Channel")]
        [SerializeField] private GhostFrameChannel _ghostChannel;

        [Header("Left-hand ghost")]
        [Tooltip("HandPuppet on the ghost mesh — sibling of OVRLeftHandVisual under HandVisualsLeft.")]
        [SerializeField] private HandPuppet _puppet;

        [Tooltip("The left Interaction SDK Hand — the ghost sits at the learner's own wrist (ADR-0012).")]
        [SerializeField] private MonoBehaviour _leftHand;

        [Tooltip("The centre-eye / head transform. The demonstrated wrist orientation is " +
                 "relative to head yaw (§3.4), so ii vs I reads correctly however the learner turns.")]
        [SerializeField] private Transform _head;

        [Tooltip("Beats to morph between poses. ADR-0012: the ghost conveys the movement, so ease, never snap.")]
        [Min(0.1f)] [SerializeField] private float _morphBeats = 1.5f;

        [Header("Target poses (authored via Jazztures ▸ Ghost Pose Recorder)")]
        [SerializeField] private GhostPoseAsset _ii;
        [SerializeField] private GhostPoseAsset _v;
        [SerializeField] private GhostPoseAsset _i;

        [Header("Right-hand demo lights")]
        [Tooltip("Optional. The touch-target rig whose stops light while the demo plays a note.")]
        [SerializeField] private TouchTargetRig _rig;

        private IHand _hand;
        private Renderer[] _ghostRenderers;
        private HandPose _previous;
        private HandPose _target;
        private HandPose _scratch;
        private Quaternion _previousWrist = Quaternion.identity;
        private Quaternion _targetWrist = Quaternion.identity;
        private bool _haveTargetWrist;
        private ChordFunction? _lastPose;
        private bool _seededPose;
        private double _morphStartBeat;
        private float _morphT;
        private int _litIndex = -1;
        private bool _ghostVisible;

        private void Awake()
        {
            _hand = _leftHand as IHand;
            _previous = new HandPose(Handedness.Left);
            _target = new HandPose(Handedness.Left);
            _scratch = new HandPose(Handedness.Left);

            if (_puppet != null)
            {
                _ghostRenderers = _puppet.GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            if (_puppet == null)
            {
                Debug.LogError($"{nameof(GhostHandView)}: assign {nameof(_puppet)} (the ghost HandPuppet).", this);
                enabled = false;
                return;
            }

            if (_hand == null)
            {
                Debug.LogWarning(
                    $"{nameof(GhostHandView)}: no left {nameof(IHand)} — the ghost will morph in place " +
                    "but won't ride the learner's wrist (ADR-0012 superimposition).", this);
            }

            SetGhostVisible(false);
        }

        private void OnEnable() => _ghostChannel?.Register(OnGhost);

        private void OnDisable()
        {
            _ghostChannel?.Unregister(OnGhost);
            ClearDemoLight();
        }

        private void OnGhost(GhostFrame frame)
        {
            SetGhostVisible(frame.Visible);

            if (!frame.Visible)
            {
                ClearDemoLight();
                return;
            }

            LightTarget(frame.LitTargetIndex);

            GhostPoseAsset wantedAsset = AssetFor(frame.DemonstratedPose);
            HandPose wanted = wantedAsset != null ? wantedAsset.Pose : null;
            if (wanted == null)
            {
                // null = "relaxed hand" (GhostFrame doc). No relaxed pose authored yet —
                // hold the last shape rather than snapping to identity.
                // TODO(OPEN): author a relaxed ghost pose.
                ApplyScratch();
                return;
            }

            if (!_seededPose)
            {
                _previous.CopyFrom(wanted);
                _target.CopyFrom(wanted);
                _scratch.CopyFrom(wanted);
                _previousWrist = _targetWrist = WristFor(wantedAsset);
                _haveTargetWrist = wantedAsset.HasWrist;
                _lastPose = frame.DemonstratedPose;
                _morphStartBeat = frame.PoseChangedAtBeat;
                _seededPose = true;
            }
            else if (frame.DemonstratedPose != _lastPose)
            {
                _previous.CopyFrom(_scratch);        // morph on from where we visually are
                _target.CopyFrom(wanted);
                _previousWrist = _haveTargetWrist
                    ? Quaternion.SlerpUnclamped(_previousWrist, _targetWrist, _morphT)
                    : _targetWrist;
                _targetWrist = WristFor(wantedAsset);
                _haveTargetWrist = wantedAsset.HasWrist;
                _lastPose = frame.DemonstratedPose;
                _morphStartBeat = frame.PoseChangedAtBeat;
            }

            _morphT = _morphBeats > 0f
                ? Mathf.Clamp01((float)((frame.Beat - _morphStartBeat) / _morphBeats))
                : 1f;
            HandPose.Lerp(_previous, _target, _morphT, ref _scratch);
            ApplyScratch();
        }

        private void ApplyScratch()
        {
            if (_hand != null && _hand.GetRootPose(out Pose wrist))
            {
                // Position rides the learner's wrist (superimposition); orientation is the
                // demonstrated one, re-expressed in world via current head yaw (§3.4). That
                // difference is what shows ii vs I — same finger shape, wrist turned.
                Quaternion rotation = wrist.rotation;
                if (_haveTargetWrist && _head != null)
                {
                    Quaternion headYaw = Quaternion.Euler(0f, _head.eulerAngles.y, 0f);
                    Quaternion targetWorld = headYaw * Quaternion.SlerpUnclamped(_previousWrist, _targetWrist, _morphT);
                    rotation = targetWorld;
                }

                _puppet.SetRootPose(new Pose(wrist.position, rotation));
            }

            _puppet.SetJointRotations(_scratch.JointRotations);
        }

        private GhostPoseAsset AssetFor(ChordFunction? function) => function switch
        {
            ChordFunction.Two => _ii,
            ChordFunction.Five => _v,
            ChordFunction.One => _i,
            _ => null,
        };

        private static Quaternion WristFor(GhostPoseAsset asset) =>
            asset != null && asset.HasWrist ? asset.WristRotationHeadLocal : Quaternion.identity;

        private void LightTarget(int index)
        {
            if (_rig == null || index == _litIndex)
            {
                return;
            }

            var targets = _rig.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].SetDemonstrated(targets[i].Index == index);
            }

            _litIndex = index;
        }

        private void ClearDemoLight() => LightTarget(-1);

        private void SetGhostVisible(bool visible)
        {
            if (_ghostVisible == visible || _ghostRenderers == null)
            {
                return;
            }

            _ghostVisible = visible;
            foreach (Renderer r in _ghostRenderers)
            {
                if (r != null)
                {
                    r.enabled = visible;
                }
            }
        }
    }
}
