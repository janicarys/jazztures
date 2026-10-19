using Jazztures.Input;
using Jazztures.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jazztures.App.Editor
{
    /// <summary>
    /// Adds the left hand's chord-name label (<see cref="ChordNameLabel"/>) to the open scene, so nobody
    /// has to wire six Inspector slots by hand. It copies everything it can from what the scene already
    /// has — the left hand from the hand-pose source, the chord and note channels and the label look
    /// from the finger letters (or, failing those, the touch-target binder), the head from the same —
    /// so the label is driven by the very objects the right hand's letters are.
    ///
    /// <para>
    /// Idempotent and undoable (Ctrl+Z). It changes the scene, which must then be saved.
    /// </para>
    /// </summary>
    public static class LabelSetup
    {
        private const string ChildName = "Chord Name Label";

        [MenuItem("Jazztures/Labels/Set Up Left-Hand Chord Name")]
        public static void ApplyChordName()
        {
            var root = Object.FindFirstObjectByType<PerformanceCompositionRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                Debug.LogError("Label setup: no PerformanceCompositionRoot in the open scene — open main.unity first.");
                return;
            }

            var poseSource = Object.FindFirstObjectByType<MetaXRHandPoseSource>(FindObjectsInactive.Include);
            Object leftHand = poseSource != null
                ? new SerializedObject(poseSource).FindProperty("_leftHand").objectReferenceValue
                : null;
            if (leftHand == null)
            {
                Debug.LogError(
                    "Label setup: could not find the left hand (no MetaXRHandPoseSource with a left hand assigned). Nothing was changed.");
                return;
            }

            // Everything else is borrowed from the right hand's letters (or the touch targets), which the
            // scene already has wired to the same channels and config.
            Object chordChanged = null;
            Object config = null;
            Object head = null;

            var letters = Object.FindFirstObjectByType<FingerNoteLabels>(FindObjectsInactive.Include);
            if (letters != null)
            {
                var so = new SerializedObject(letters);
                chordChanged = so.FindProperty("_chordChanged").objectReferenceValue;
                config = so.FindProperty("_config").objectReferenceValue;
                head = so.FindProperty("_head").objectReferenceValue;
            }

            var touch = Object.FindFirstObjectByType<TouchTargetBinder>(FindObjectsInactive.Include);
            if (touch != null)
            {
                var so = new SerializedObject(touch);
                chordChanged = chordChanged != null ? chordChanged : so.FindProperty("_chordChanged").objectReferenceValue;
                config = config != null ? config : so.FindProperty("_config").objectReferenceValue;

                var rig = so.FindProperty("_rig").objectReferenceValue;
                if (head == null && rig != null)
                {
                    head = new SerializedObject(rig).FindProperty("_head").objectReferenceValue;
                }
            }

            if (head == null && Camera.main != null)
            {
                head = Camera.main.transform;
            }

            Object noteChannel = new SerializedObject(root).FindProperty("_noteChannel").objectReferenceValue;
            if (chordChanged == null || noteChannel == null)
            {
                Debug.LogError(
                    "Label setup: could not find the chord-changed channel (from the finger letters or the touch-target " +
                    "binder) or the note channel (from the performance harness). Nothing was changed.");
                return;
            }

            Undo.SetCurrentGroupName("Set up chord name label");
            int group = Undo.GetCurrentGroup();

            Transform child = root.transform.Find(ChildName);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(ChildName);
                Undo.RegisterCreatedObjectUndo(go, "Create chord name label");
                go.transform.SetParent(root.transform, worldPositionStays: false);
            }
            else
            {
                go = child.gameObject;
                Undo.RecordObject(go, "Show chord name label");
                go.SetActive(true);
            }

            var label = go.GetComponent<ChordNameLabel>();
            if (label == null)
            {
                label = Undo.AddComponent<ChordNameLabel>(go);
            }

            var labelSo = new SerializedObject(label);
            SetReference(labelSo, "_leftHand", leftHand);
            SetReference(labelSo, "_config", config);
            SetReference(labelSo, "_chordChanged", chordChanged);
            SetReference(labelSo, "_noteTriggered", noteChannel);
            SetReference(labelSo, "_head", head);
            SetReference(labelSo, "_follow", root); // hidden on the menu screens, like the finger letters

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log(
                "Label setup: the left hand now carries its chord's name, lighting up when the chord sounds. " +
                "Save the scene (Ctrl+S) to keep it.");
        }

        private static void SetReference(SerializedObject owner, string property, Object value)
        {
            SerializedProperty p = owner.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"Label setup: {owner.targetObject.GetType().Name} has no field '{property}'.");
                return;
            }

            p.objectReferenceValue = value;
            owner.ApplyModifiedProperties();
        }
    }
}
