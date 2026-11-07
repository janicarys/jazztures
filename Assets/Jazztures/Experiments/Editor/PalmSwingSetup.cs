using Jazztures.App;
using Jazztures.Input;
using Jazztures.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jazztures.Experiments.Editor
{
    /// <summary>
    /// Adds the palm-swing right-hand input (ADR-0078, branch <c>explore/melodic-movement-mapping</c>) to the open
    /// scene beside the finger-bend input, so it can be tried in free play and the lessons without wiring Inspector
    /// slots by hand. A <see cref="RightHandInputSwitch"/> becomes the lesson flow's melody input and turns the chosen
    /// input on and off with it. Idempotent and undoable (Ctrl+Z); the scene must then be saved. The finger-bend input
    /// is added first (<see cref="FingerBendSetup"/>) if the scene lacks it, because the hand and channels are read off it.
    /// </summary>
    public static class PalmSwingSetup
    {
        private const string SettingsPath = "Assets/Jazztures/Config/PalmSwingSettings.asset";
        private const string ChildName = "Palm Swing Input";

        [MenuItem("Jazztures/Experiments/Use Palm-Swing Input (ADR-0078)")]
        public static void UsePalmSwing() => Apply(palmSwing: true);

        [MenuItem("Jazztures/Experiments/Use Finger Bend Again (palm swing stays in the scene)")]
        public static void UseFingerBendAgain() => Apply(palmSwing: false);

        private static void Apply(bool palmSwing)
        {
            var root = Object.FindFirstObjectByType<PerformanceCompositionRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                Debug.LogError("Palm swing setup: no PerformanceCompositionRoot in the open scene — open main.unity first.");
                return;
            }

            var rootSo = new SerializedObject(root);
            if (rootSo.FindProperty("_fingerBend").objectReferenceValue == null)
            {
                FingerBendSetup.UseFingerBend(); // creates the finger-bend input, copying the hand, config and channels from the touch targets
                rootSo.Update();
            }

            var fingerBend = rootSo.FindProperty("_fingerBend").objectReferenceValue as FingerBendBinder;
            Object settings = AssetDatabase.LoadAssetAtPath<Object>(SettingsPath);
            if (fingerBend == null || settings == null)
            {
                Debug.LogError($"Palm swing setup: needs a finger-bend input in the scene and {SettingsPath}. Nothing was changed.");
                return;
            }

            Object rightHand = new SerializedObject(fingerBend).FindProperty("_rightHand").objectReferenceValue;
            Transform head = Camera.main != null ? Camera.main.transform : null;

            Undo.SetCurrentGroupName(palmSwing ? "Use palm-swing input" : "Use finger bend again");
            int group = Undo.GetCurrentGroup();

            Transform existing = root.transform.Find(ChildName);
            GameObject go;
            if (existing == null)
            {
                go = new GameObject(ChildName);
                Undo.RegisterCreatedObjectUndo(go, "Create palm-swing input");
                go.transform.SetParent(root.transform, worldPositionStays: false);
            }
            else
            {
                go = existing.gameObject;
                Undo.RecordObject(go, "Show palm-swing input");
                go.SetActive(true);
            }

            var meta = Ensure<MetaXRPalmSwingSampleSource>(go);
            var input = Ensure<PalmSwingInputSource>(go);
            var view = Ensure<PalmSwingView>(go);
            var binder = Ensure<PalmSwingViewBinder>(go);
            var inputSwitch = Ensure<RightHandInputSwitch>(go);

            Set(meta, "_rightHand", rightHand);
            Set(meta, "_head", head);
            Set(meta, "_settings", settings);
            Set(input, "_settings", settings);
            Set(input, "_sampleSource", meta);
            Set(view, "_head", head);
            Set(binder, "_palmSwing", input);
            Set(binder, "_headsetSource", meta);
            Set(binder, "_view", view);
            Set(binder, "_settings", settings);
            Set(binder, "_head", head);
            Set(inputSwitch, "_fingerBend", fingerBend);
            Set(inputSwitch, "_palmSwing", input);
            Set(root, "_palmSwing", input);

            var switchSo = new SerializedObject(inputSwitch);
            switchSo.FindProperty("_mode").enumValueIndex = palmSwing ? 1 : 0; // RightHandInputMode.PalmSwing / FingerBend
            switchSo.ApplyModifiedProperties();

            // The lesson flow enables and disables its melody input as lessons start and stop: that is now the switch.
            var flow = Object.FindFirstObjectByType<LessonFlowController>(FindObjectsInactive.Include);
            if (flow != null)
            {
                Set(flow, "_melodyInput", inputSwitch);
            }
            else
            {
                Debug.LogWarning("Palm swing setup: no LessonFlowController in the scene; the switch will turn the chosen input on by itself.");
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log(palmSwing
                ? "Palm swing setup: the right hand now plays the palm swing (it needs the headset or Link: there is no keyboard stand-in). Switch back with Jazztures ▸ Experiments ▸ Use Finger Bend Again, or the Mode field on 'Palm Swing Input'. Save the scene (Ctrl+S)."
                : "Palm swing setup: the right hand is back on the finger bends; the palm-swing input stays in the scene. Save the scene (Ctrl+S).");
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        private static void Set(Object owner, string property, Object value)
        {
            var so = new SerializedObject(owner);
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"Palm swing setup: {owner.GetType().Name} has no field '{property}'.");
                return;
            }

            p.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
