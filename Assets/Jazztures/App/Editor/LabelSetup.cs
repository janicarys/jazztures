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

        /// <summary>
        /// Gives both ghost hands the learner's own label look (ADR-0058): copies the label settings
        /// (<c>MelodyConfig</c>) the finger letters and the chord name already use into the left ghost
        /// (<see cref="GhostHandView"/>, the chord name) and the right ghost (<see cref="RightGhostHandView"/>, the
        /// note letters). The labels work without it, in the built-in look; this makes them match. Idempotent and
        /// undoable; it changes the scene, which must then be saved.
        /// </summary>
        [MenuItem("Jazztures/Labels/Set Up Ghost Hand Labels")]
        public static void ApplyGhostLabels()
        {
            Object config = FindLabelConfig();
            if (config == null)
            {
                Debug.LogError(
                    "Label setup: could not find the label settings (no finger letters, chord name or touch-target binder with a " +
                    "config assigned). Nothing was changed.");
                return;
            }

            var left = Object.FindFirstObjectByType<GhostHandView>(FindObjectsInactive.Include);
            var right = Object.FindFirstObjectByType<RightGhostHandView>(FindObjectsInactive.Include);
            if (left == null && right == null)
            {
                Debug.LogError(
                    "Label setup: no ghost hand in the open scene. Run Jazztures ▸ Menu ▸ Set Up Gesture Menu, and Jazztures ▸ " +
                    "Experiments ▸ Use Finger-Bend Input, first. Nothing was changed.");
                return;
            }

            Undo.SetCurrentGroupName("Set up ghost hand labels");
            int group = Undo.GetCurrentGroup();

            int wired = 0;
            if (left != null)
            {
                Undo.RecordObject(left, "Ghost chord name look");
                SetReference(new SerializedObject(left), "_config", config);
                wired++;
            }

            if (right != null)
            {
                Undo.RecordObject(right, "Ghost finger letters look");
                SetReference(new SerializedObject(right), "_config", config);
                wired++;
            }

            Undo.CollapseUndoOperations(group);
            var scene = (left != null ? left.gameObject : right.gameObject).scene;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(
                $"Label setup: {wired} ghost hand(s) now use the learner's label look" +
                (left == null ? " (no left ghost in the scene)" : string.Empty) +
                (right == null ? " (no right ghost in the scene - run Use Finger-Bend Input)" : string.Empty) +
                ". Save the scene (Ctrl+S) to keep it.");
        }

        /// <summary>The label settings the scene's own labels already use.</summary>
        private static Object FindLabelConfig()
        {
            var letters = Object.FindFirstObjectByType<FingerNoteLabels>(FindObjectsInactive.Include);
            if (letters != null)
            {
                Object config = new SerializedObject(letters).FindProperty("_config").objectReferenceValue;
                if (config != null)
                {
                    return config;
                }
            }

            var name = Object.FindFirstObjectByType<ChordNameLabel>(FindObjectsInactive.Include);
            if (name != null)
            {
                Object config = new SerializedObject(name).FindProperty("_config").objectReferenceValue;
                if (config != null)
                {
                    return config;
                }
            }

            var touch = Object.FindFirstObjectByType<TouchTargetBinder>(FindObjectsInactive.Include);
            return touch != null ? new SerializedObject(touch).FindProperty("_config").objectReferenceValue : null;
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
