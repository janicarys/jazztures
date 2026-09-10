using System.Linq;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEngine;

namespace Jazztures.Presentation.Editor
{
    /// <summary>
    /// Captures the three ghost target poses (ADR-0012) from the developer's own tracked
    /// left hand. Forked from the SDK's <c>HandGrabPoseWizard.TrackedPose()</c>.
    ///
    /// <para>
    /// Enter Play Mode with hand tracking (Quest Link is fine), hold ii / V / I, and click
    /// Capture. Writes / overwrites <c>Assets/Jazztures/Input/Poses/Ghost/{Ii,V,I}.asset</c>.
    /// Author the pose the learner is <b>taught</b> — a genuine palm-facing-right for ii,
    /// not the machine's <c>FingersUp</c> discriminator (ADR-0014).
    /// </para>
    /// </summary>
    public sealed class GhostPoseRecorderWindow : EditorWindow
    {
        private const string PoseDir = "Assets/Jazztures/Input/Poses/Ghost";

        private GhostPoseAsset _ii;
        private GhostPoseAsset _v;
        private GhostPoseAsset _i;

        [MenuItem("Jazztures/Ghost Pose Recorder")]
        private static void Open() => GetWindow<GhostPoseRecorderWindow>("Ghost Pose Recorder");

        private void OnEnable()
        {
            _ii = Load("Ii");
            _v = Load("V");
            _i = Load("I");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode with the headset on. Hold the pose the learner is taught, then Capture.",
                MessageType.Info);

            _ii = (GhostPoseAsset)EditorGUILayout.ObjectField("ii  (palm right)", _ii, typeof(GhostPoseAsset), false);
            _v = (GhostPoseAsset)EditorGUILayout.ObjectField("V  (fist)", _v, typeof(GhostPoseAsset), false);
            _i = (GhostPoseAsset)EditorGUILayout.ObjectField("I  (palm down)", _i, typeof(GhostPoseAsset), false);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Capture  →  ii")) Capture(ref _ii, "Ii");
                if (GUILayout.Button("Capture  →  V")) Capture(ref _v, "V");
                if (GUILayout.Button("Capture  →  I")) Capture(ref _i, "I");
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.LabelField("(Play Mode required for capture.)", EditorStyles.miniLabel);
            }
        }

        private void Capture(ref GhostPoseAsset slot, string fileName)
        {
            if (!TrackedLeftPose(out HandPose captured, out Quaternion wristHeadLocal))
            {
                Debug.LogError("Ghost Pose Recorder: no tracked left hand / no main camera this frame — hold still and retry.");
                return;
            }

            if (slot == null)
            {
                slot = CreateAssetFile(fileName);
            }

            slot.SetPose(captured, wristHeadLocal);
            EditorUtility.SetDirty(slot);
            AssetDatabase.SaveAssetIfDirty(slot);
            Debug.Log($"Ghost Pose Recorder: captured {fileName} → {AssetDatabase.GetAssetPath(slot)}", slot);
        }

        private static bool TrackedLeftPose(out HandPose pose, out Quaternion wristHeadLocal)
        {
            pose = null;
            wristHeadLocal = Quaternion.identity;

            IHand hand = FindObjectsByType<Hand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(h => h.Handedness == Handedness.Left);
            Camera head = Camera.main;

            if (hand == null || head == null
                || !hand.GetJointPosesLocal(out ReadOnlyHandJointPoses joints)
                || !hand.GetRootPose(out Pose wrist))
            {
                return false;
            }

            pose = new HandPose(Handedness.Left);
            for (int j = 0; j < FingersMetadata.HAND_JOINT_IDS.Length; j++)
            {
                pose.JointRotations[j] = joints[FingersMetadata.HAND_JOINT_IDS[j]].rotation;
            }

            // §3.4: store the wrist orientation relative to head yaw, so it stays right as
            // the learner turns. This is the ii-vs-I discriminator (ADR-0014).
            Quaternion headYaw = Quaternion.Euler(0f, head.transform.eulerAngles.y, 0f);
            wristHeadLocal = Quaternion.Inverse(headYaw) * wrist.rotation;
            return true;
        }

        private static GhostPoseAsset Load(string fileName) =>
            AssetDatabase.LoadAssetAtPath<GhostPoseAsset>($"{PoseDir}/{fileName}.asset");

        private static GhostPoseAsset CreateAssetFile(string fileName)
        {
            if (!AssetDatabase.IsValidFolder(PoseDir))
            {
                AssetDatabase.CreateFolder("Assets/Jazztures/Input/Poses", "Ghost");
            }

            var asset = CreateInstance<GhostPoseAsset>();
            AssetDatabase.CreateAsset(asset, $"{PoseDir}/{fileName}.asset");
            return asset;
        }
    }
}
