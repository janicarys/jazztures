using Oculus.Interaction.HandGrab;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// One target left-hand pose for the ghost hand (ADR-0012) — the joint rotations for
    /// ii ("open palm, facing right"), V ("fist"), or I ("open palm, facing down").
    ///
    /// <para>
    /// The <c>ShapeRecognizer</c> assets under <c>Input/Poses/</c> cannot drive a mesh:
    /// they hold symbolic curl/flexion constraints, not rotations, and one is shared by
    /// both ii and I (ADR-0014). So the ghost's target poses are authored separately —
    /// captured from the developer's own tracked hand in Play Mode via
    /// <c>Jazztures ▸ Ghost Pose Recorder</c>.
    /// </para>
    ///
    /// <para>
    /// These are three reference poses shipped as app content, authored once at edit time.
    /// §4.1's ban on persisting raw joint transforms off-device governs <b>participant
    /// telemetry</b>; it does not touch authored reference content.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Ghost Pose", fileName = "GhostPose")]
    public sealed class GhostPoseAsset : ScriptableObject
    {
        [SerializeField] private HandPose _pose = new HandPose(Oculus.Interaction.Input.Handedness.Left);

        [Tooltip("Wrist orientation at capture, relative to head yaw (§3.4). This is what " +
                 "distinguishes ii from I — same finger shape, different wrist (ADR-0014).")]
        [SerializeField] private Quaternion _wristRotationHeadLocal = Quaternion.identity;

        /// <summary>The captured joint rotations. Never null once authored.</summary>
        public HandPose Pose => _pose;

        /// <summary>The target wrist orientation, relative to head yaw. Identity if not captured.</summary>
        public Quaternion WristRotationHeadLocal => _wristRotationHeadLocal;

        /// <summary>True when a wrist orientation was captured (distinguishes the palm-facing poses).</summary>
        public bool HasWrist => _wristRotationHeadLocal.x * _wristRotationHeadLocal.x
                                + _wristRotationHeadLocal.y * _wristRotationHeadLocal.y
                                + _wristRotationHeadLocal.z * _wristRotationHeadLocal.z
                                + _wristRotationHeadLocal.w * _wristRotationHeadLocal.w > 0.01f;

#if UNITY_EDITOR
        /// <summary>Editor only — the Ghost Pose Recorder writes a fresh capture here.</summary>
        public void SetPose(HandPose pose, Quaternion wristRotationHeadLocal)
        {
            _pose = new HandPose(pose);
            _wristRotationHeadLocal = wristRotationHeadLocal;
        }
#endif
    }
}
