using Jazztures.App;
using Jazztures.Input;
using Jazztures.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jazztures.Experiments.Editor
{
    /// <summary>
    /// Switches the open scene's right-hand melody input between the touch targets and the
    /// finger-bend exploration (ADR-0050, branch <c>explore/finger-bend-keys</c>), so nobody has to
    /// wire eight Inspector slots by hand. Idempotent and undoable (Ctrl+Z); it changes the scene,
    /// which must then be saved.
    ///
    /// <para>
    /// "Use finger-bend" adds a <c>Finger Bend Input</c> child under the performance harness (the
    /// binder plus the fingertip letters), points the harness and the lesson flow at it, and
    /// disables the touch-target binder. "Use touch targets" puts it all back. Everything is read
    /// from what the touch-target binder already references (hand, config, channels, head), so the
    /// two inputs are always driven by the same objects.
    /// </para>
    /// </summary>
    public static class FingerBendSetup
    {
        private const string ChildName = "Finger Bend Input";

        [MenuItem("Jazztures/Experiments/Use Finger-Bend Input (ADR-0050)")]
        public static void UseFingerBend() => Switch(fingerBend: true);

        [MenuItem("Jazztures/Experiments/Use Touch Targets Again")]
        public static void UseTouchTargets() => Switch(fingerBend: false);

        private static void Switch(bool fingerBend)
        {
            var root = Object.FindFirstObjectByType<PerformanceCompositionRoot>();
            if (root == null)
            {
                Debug.LogError("Finger-bend setup: no PerformanceCompositionRoot in the open scene — open main.unity first.");
                return;
            }

            var rootSo = new SerializedObject(root);
            var touch = rootSo.FindProperty("_touchTargets").objectReferenceValue as TouchTargetBinder;
            if (touch == null)
            {
                Debug.LogError("Finger-bend setup: the performance harness has no Touch Targets binder assigned, so there is nothing to copy the hand, config and channels from. Nothing was changed.");
                return;
            }

            var touchSo = new SerializedObject(touch);
            Object rightHand = touchSo.FindProperty("_rightHand").objectReferenceValue;
            Object config = touchSo.FindProperty("_config").objectReferenceValue;
            Object chordChanged = touchSo.FindProperty("_chordChanged").objectReferenceValue;
            var rig = touchSo.FindProperty("_rig").objectReferenceValue as TouchTargetRig;
            Transform head = rig != null
                ? new SerializedObject(rig).FindProperty("_head").objectReferenceValue as Transform
                : null;
            Object noteChannel = rootSo.FindProperty("_noteChannel").objectReferenceValue;
            var flow = Object.FindFirstObjectByType<LessonFlowController>();

            Undo.SetCurrentGroupName(fingerBend ? "Use finger-bend input" : "Use touch targets");
            int group = Undo.GetCurrentGroup();

            if (fingerBend)
            {
                if (rightHand == null)
                {
                    Debug.LogError("Finger-bend setup: the touch-target binder has no right hand assigned. Nothing was changed.");
                    return;
                }

                FingerBendBinder binder = EnsureFingerBendInput(
                    root, rightHand, config, chordChanged, noteChannel, head, rig);

                SetReference(rootSo, "_fingerBend", binder);
                if (flow != null)
                {
                    SetReference(new SerializedObject(flow), "_melodyInput", binder);
                }
                else
                {
                    Debug.LogWarning("Finger-bend setup: no LessonFlowController in the scene — the harness will use the finger bends directly.");
                }

                Undo.RecordObject(touch, "Disable touch targets");
                touch.enabled = false;
            }
            else
            {
                Transform child = root.transform.Find(ChildName);
                if (child != null)
                {
                    Undo.RecordObject(child.gameObject, "Hide finger-bend input");
                    child.gameObject.SetActive(false);
                }

                SetReference(rootSo, "_fingerBend", null);
                if (flow != null)
                {
                    SetReference(new SerializedObject(flow), "_melodyInput", touch);
                }

                Undo.RecordObject(touch, "Enable touch targets");
                touch.enabled = true;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log(fingerBend
                ? "Finger-bend setup: the right hand now plays by bending fingers (thumb = root … pinky = 9th). Save the scene (Ctrl+S) to keep it; Jazztures ▸ Experiments ▸ Use Touch Targets Again undoes it."
                : "Finger-bend setup: the right hand is back on the touch targets. Save the scene (Ctrl+S) to keep it.");
        }

        private static FingerBendBinder EnsureFingerBendInput(
            PerformanceCompositionRoot root, Object rightHand, Object config, Object chordChanged,
            Object noteChannel, Transform head, TouchTargetRig rig)
        {
            Transform child = root.transform.Find(ChildName);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(ChildName);
                Undo.RegisterCreatedObjectUndo(go, "Create finger-bend input");
                go.transform.SetParent(root.transform, worldPositionStays: false);
            }
            else
            {
                go = child.gameObject;
                Undo.RecordObject(go, "Show finger-bend input");
                go.SetActive(true);
            }

            FingerBendBinder binder = go.GetComponent<FingerBendBinder>();
            if (binder == null)
            {
                binder = Undo.AddComponent<FingerBendBinder>(go);
            }

            var binderSo = new SerializedObject(binder);
            SetReference(binderSo, "_rightHand", rightHand);
            SetReference(binderSo, "_config", config);

            FingerNoteLabels labels = go.GetComponent<FingerNoteLabels>();
            if (labels == null)
            {
                labels = Undo.AddComponent<FingerNoteLabels>(go);
            }

            var labelsSo = new SerializedObject(labels);
            SetReference(labelsSo, "_rightHand", rightHand);
            SetReference(labelsSo, "_config", config);
            SetReference(labelsSo, "_chordChanged", chordChanged);
            SetReference(labelsSo, "_noteTriggered", noteChannel);
            SetReference(labelsSo, "_head", head);
            SetReference(labelsSo, "_follow", binder);
            SetReference(labelsSo, "_hideTouchTargets", rig);

            return binder;
        }

        private static void SetReference(SerializedObject owner, string property, Object value)
        {
            SerializedProperty p = owner.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"Finger-bend setup: {owner.targetObject.GetType().Name} has no field '{property}'.");
                return;
            }

            p.objectReferenceValue = value;
            owner.ApplyModifiedProperties();
        }
    }
}
