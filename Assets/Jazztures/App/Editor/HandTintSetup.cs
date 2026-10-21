using System.Collections.Generic;
using Jazztures.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jazztures.App.Editor
{
    /// <summary>
    /// Wires how the learner's own hands are drawn into the open scene, so nobody has to hand-edit scene YAML:
    /// <list type="number">
    ///   <item>adds <see cref="HandUseTint"/> (ADR-0064, ADR-0065, ADR-0070) to the <see cref="PerformanceCompositionRoot"/>'s
    ///   object (which is what says which hands a lesson uses);</item>
    ///   <item>points it at the <b>property-block editors of the Interaction SDK's hand visuals</b>
    ///   (<c>OVRHandVisualLeft</c> / <c>OVRHandVisualRight</c>, inside the <c>OVRHands</c> prefab) - the hands the learner
    ///   actually sees. They are found by object and type name, so this assembly needs no reference to the Meta SDK;</item>
    ///   <item>points it at the <see cref="TensionColorDriver"/>, whose colour the left hand wears while it holds a pose;</item>
    ///   <item>gives the <see cref="NoteLanes"/> the same <c>TensionPalette</c> the driver uses, so the coming letters wear
    ///   the colour of their chord.</item>
    /// </list>
    /// Idempotent and undoable (Ctrl+Z). It changes the scene, which must then be saved.
    /// </summary>
    public static class HandTintSetup
    {
        [MenuItem("Jazztures/Hands/Set Up Hand Dimming + Chord Colours (ADR-0064, ADR-0065, ADR-0070)")]
        public static void Apply()
        {
            var root = Object.FindFirstObjectByType<PerformanceCompositionRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                Debug.LogError("Hand tint setup: no PerformanceCompositionRoot in the open scene - open main.unity first.");
                return;
            }

            FindBlocks(out Component[] left, out Component[] right, out List<string> seen);
            if (left.Length == 0 && right.Length == 0)
            {
                Debug.LogError(
                    "Hand tint setup: found no MaterialPropertyBlockEditor under a hand visual (an object with 'HandVisual' and " +
                    "'Left' or 'Right' in its name, inside the Interaction SDK's OVRHands prefab) in the open scene. Is the scene " +
                    "open? Property-block editors found anywhere in it, by their parent chain: " +
                    (seen.Count == 0 ? "none." : string.Join("  |  ", seen)));
                return;
            }

            int changes = 0;

            var tint = root.GetComponent<HandUseTint>();
            if (tint == null)
            {
                tint = Undo.AddComponent<HandUseTint>(root.gameObject);
                changes++;
            }

            changes += SetReferences(tint, "_leftHandBlocks", left);
            changes += SetReferences(tint, "_rightHandBlocks", right);
            changes += SetReference(tint, "_source", root);

            if (left.Length == 0 || right.Length == 0)
            {
                Debug.LogWarning(
                    $"Hand tint setup: found no editors for the {(left.Length == 0 ? "left" : "right")} hand - only the other will be " +
                    "dimmed until you assign them on the Hand Use Tint component.");
            }

            // ADR-0065: the left hand wears the driver's colour, and the coming letters wear the driver's palette.
            var driver = Object.FindFirstObjectByType<TensionColorDriver>(FindObjectsInactive.Include);
            if (driver == null)
            {
                Debug.LogWarning(
                    "Hand tint setup: no TensionColorDriver in the scene - the left hand will not take the chord's colour, " +
                    "and the note lanes will not know the palette.");
            }
            else
            {
                changes += SetReference(tint, "_tension", driver);

                Object palette = new SerializedObject(driver).FindProperty("_palette").objectReferenceValue;
                var lanes = Object.FindFirstObjectByType<NoteLanes>(FindObjectsInactive.Include);
                if (palette == null)
                {
                    Debug.LogWarning("Hand tint setup: the TensionColorDriver has no palette assigned - the letters stay one colour.");
                }
                else if (lanes == null)
                {
                    Debug.Log("Hand tint setup: no Note Lanes in the scene - nothing to give the palette to (the left hand is still set up).");
                }
                else
                {
                    changes += SetReference(lanes, "_palette", palette);
                }
            }

            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                Debug.Log(
                    $"Hand tint setup: {changes} change(s) made ({left.Length} left and {right.Length} right property-block editor(s) found). " +
                    "Save the scene (Ctrl+S) to keep them; Ctrl+Z undoes.");
            }
            else
            {
                Debug.Log("Hand tint setup: already set up - nothing to change.");
            }
        }

        /// <summary>
        /// Every <c>MaterialPropertyBlockEditor</c> in the scene that sits under a hand visual, inactive ones included - the
        /// visual ships one for the OVR mesh path and one for the OpenXR path, and either may be the one in use - split by side.
        /// "A hand visual" is the nearest ancestor whose name contains "HandVisual", and the side is "Left" or "Right" in that
        /// name. Not an exact name: inside <c>OVRHands</c> the prefabs are renamed (<c>OVRLeftHandVisual</c>, not the prefab's
        /// own <c>OVRHandVisualLeft</c>), and a search for the prefab's name found nothing. The ghost hands are not matched
        /// (their names have no "HandVisual").
        /// </summary>
        private static void FindBlocks(out Component[] left, out Component[] right, out List<string> seen)
        {
            var leftBlocks = new List<Component>();
            var rightBlocks = new List<Component>();
            seen = new List<string>();

            foreach (Component component in Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (component == null || component.GetType().Name != "MaterialPropertyBlockEditor")
                {
                    continue;
                }

                string visual = null;
                var chain = new List<string>();
                for (Transform t = component.transform; t != null; t = t.parent)
                {
                    chain.Add(t.name);
                    if (visual == null && t.name.IndexOf("HandVisual", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        visual = t.name;
                    }
                }

                if (seen.Count < 8)
                {
                    seen.Add(string.Join("<", chain.GetRange(0, System.Math.Min(chain.Count, 4))));
                }

                if (visual == null)
                {
                    continue;
                }

                if (visual.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    leftBlocks.Add(component);
                }
                else if (visual.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    rightBlocks.Add(component);
                }
            }

            left = leftBlocks.ToArray();
            right = rightBlocks.ToArray();
        }

        /// <summary>
        /// Set an array-of-references field to <paramref name="values"/> if it differs; returns 1 when it changed. Never
        /// clears one: finding nothing is not a reason to undo what somebody assigned by hand.
        /// </summary>
        private static int SetReferences(Object target, string field, Component[] values)
        {
            if (values.Length == 0)
            {
                return 0;
            }

            var so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(field);
            if (array == null || !array.isArray)
            {
                Debug.LogError($"Hand tint setup: {target.GetType().Name} has no serialized array '{field}'.");
                return 0;
            }

            bool same = array.arraySize == values.Length;
            for (int i = 0; same && i < values.Length; i++)
            {
                same = array.GetArrayElementAtIndex(i).objectReferenceValue == values[i];
            }

            if (same)
            {
                return 0;
            }

            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedProperties();
            return 1;
        }

        /// <summary>
        /// Set an object-reference field if it differs; returns 1 when it changed. Never clears one: finding nothing is
        /// not a reason to undo a reference somebody assigned by hand.
        /// </summary>
        private static int SetReference(Object target, string field, Object value)
        {
            if (value == null)
            {
                return 0;
            }

            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"Hand tint setup: {target.GetType().Name} has no serialized field '{field}'.");
                return 0;
            }

            if (property.objectReferenceValue == value)
            {
                return 0;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            return 1;
        }
    }
}
