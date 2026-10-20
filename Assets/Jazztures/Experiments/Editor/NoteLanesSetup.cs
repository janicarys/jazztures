using Jazztures.Input;
using Jazztures.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jazztures.Experiments.Editor
{
    /// <summary>
    /// Switches the open scene's right-hand lesson cue between the ghost right hand (ADR-0055, ADR-0058, ADR-0059)
    /// and the note lanes (ADR-0060, branch <c>explore/finger-bend-keys</c>), so nobody wires six Inspector slots by
    /// hand. Idempotent and undoable (Ctrl+Z); it changes the scene, which must then be saved.
    ///
    /// <para>
    /// "Use note lanes" adds a <c>Note Lanes</c> child under the finger-bend input and switches the
    /// <c>Right Ghost Hand View</c> child off; "Use ghost right hand" puts it back. It needs the finger-bend input
    /// to exist already (<see cref="FingerBendSetup"/>): the lanes run out of the same hand and are shown by the same
    /// switch. Everything is read from objects the scene already has, so the lanes are driven by the same hand,
    /// config, head and ghost channel as the cue they replace. Re-running Use Finger-Bend Input switches the ghost
    /// right hand back on; run this again after it.
    /// </para>
    /// </summary>
    public static class NoteLanesSetup
    {
        private const string LanesChildName = "Note Lanes";
        private const string GhostChildName = "Right Ghost Hand View";

        [MenuItem("Jazztures/Experiments/Use Note Lanes Instead of the Ghost Right Hand (ADR-0060)")]
        public static void UseNoteLanes() => Switch(lanes: true);

        [MenuItem("Jazztures/Experiments/Use the Ghost Right Hand Again")]
        public static void UseGhostRightHand() => Switch(lanes: false);

        private static void Switch(bool lanes)
        {
            var binder = Object.FindFirstObjectByType<FingerBendBinder>(FindObjectsInactive.Include);
            if (binder == null)
            {
                Debug.LogError("Note lanes setup: no finger-bend input in the open scene - run Jazztures ▸ Experiments ▸ Use Finger-Bend Input first. Nothing was changed.");
                return;
            }

            Undo.SetCurrentGroupName(lanes ? "Use note lanes" : "Use ghost right hand");
            int group = Undo.GetCurrentGroup();

            Transform ghost = binder.transform.Find(GhostChildName);
            Transform laneChild = binder.transform.Find(LanesChildName);

            if (lanes)
            {
                if (!EnsureLanes(binder, laneChild))
                {
                    Undo.CollapseUndoOperations(group);
                    return;
                }

                SetActive(ghost, false, "Hide the ghost right hand");
            }
            else
            {
                SetActive(laneChild, false, "Hide the note lanes");
                SetActive(ghost, true, "Show the ghost right hand");
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(binder.gameObject.scene);
            Debug.Log(lanes
                ? "Note lanes setup: the right-hand lesson cue is now five lanes out of the fingertips with the notes arriving along them; the ghost right hand is off. Save the scene (Ctrl+S) to keep it; Jazztures ▸ Experiments ▸ Use the Ghost Right Hand Again undoes it."
                : "Note lanes setup: the lesson cue is the ghost right hand again; the lanes are off. Save the scene (Ctrl+S) to keep it.");
        }

        private static bool EnsureLanes(FingerBendBinder binder, Transform existing)
        {
            var binderSo = new SerializedObject(binder);
            Object rightHand = binderSo.FindProperty("_rightHand").objectReferenceValue;
            Object config = binderSo.FindProperty("_config").objectReferenceValue;

            // The ghost channel is the one the left ghost listens to; the head is the one the fingertip letters use.
            var leftGhost = Object.FindFirstObjectByType<GhostHandView>(FindObjectsInactive.Include);
            Object channel = leftGhost != null
                ? new SerializedObject(leftGhost).FindProperty("_ghostChannel").objectReferenceValue
                : null;
            if (channel == null)
            {
                Debug.LogError("Note lanes setup: no GhostHandView with a ghost channel in the scene, so there is no channel to read the lesson's notes from. Run Jazztures ▸ Menu ▸ Set Up Gesture Menu first. Nothing was changed.");
                return false;
            }

            if (rightHand == null)
            {
                Debug.LogError("Note lanes setup: the finger-bend input has no right hand assigned. Nothing was changed.");
                return false;
            }

            var ownLetters = binder.GetComponent<FingerNoteLabels>();
            Object head = ownLetters != null
                ? new SerializedObject(ownLetters).FindProperty("_head").objectReferenceValue
                : null;

            GameObject go;
            if (existing == null)
            {
                go = new GameObject(LanesChildName);
                Undo.RegisterCreatedObjectUndo(go, "Create note lanes");
                go.transform.SetParent(binder.transform, worldPositionStays: false);
            }
            else
            {
                go = existing.gameObject;
                Undo.RecordObject(go, "Show note lanes");
                go.SetActive(true);
            }

            NoteLanes view = go.GetComponent<NoteLanes>();
            if (view == null)
            {
                view = Undo.AddComponent<NoteLanes>(go);
            }

            var so = new SerializedObject(view);
            SetReference(so, "_rightHand", rightHand);
            SetReference(so, "_ghostChannel", channel);
            SetReference(so, "_config", config);
            SetReference(so, "_head", head);
            SetReference(so, "_follow", binder);
            SetReference(so, "_ownLetters", ownLetters);
            return true;
        }

        private static void SetActive(Transform child, bool active, string undoName)
        {
            if (child == null)
            {
                return;
            }

            Undo.RecordObject(child.gameObject, undoName);
            child.gameObject.SetActive(active);
        }

        private static void SetReference(SerializedObject owner, string property, Object value)
        {
            SerializedProperty p = owner.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"Note lanes setup: {owner.targetObject.GetType().Name} has no field '{property}'.");
                return;
            }

            p.objectReferenceValue = value;
            owner.ApplyModifiedProperties();
        }
    }
}
