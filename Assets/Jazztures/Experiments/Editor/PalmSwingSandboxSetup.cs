using System.IO;
using Jazztures.App;
using Jazztures.Input;
using Jazztures.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jazztures.Experiments.Editor
{
    /// <summary>
    /// Builds the palm-swing sandbox (ADR-0078) without anyone wiring a dozen Inspector slots. It adds the palm-swing
    /// input, its editor simulation, the central-view visuals, the experimenter's panel and the drill runner to a scene
    /// that already has the performance harness, reading the hand, head and channels off what is there. Both right-hand
    /// inputs end up in the scene (the finger bend is added by <see cref="FingerBendSetup"/> if missing), switched by a
    /// <see cref="RightHandInputSwitch"/>. It also turns the lesson flow off, so the menu and tutorial do not compete
    /// for the right hand. Idempotent and undoable; the scene must then be saved.
    ///
    /// <para>
    /// "Create Sandbox Scene" first copies <c>Assets/main.unity</c> to <c>Assets/Jazztures/Scenes/Sandbox</c>, so the main scene is
    /// never touched. The copy is not added to the build settings.
    /// </para>
    /// </summary>
    public static class PalmSwingSandboxSetup
    {
        private const string MainScene = "Assets/main.unity";
        private const string SandboxDir = "Assets/Jazztures/Scenes/Sandbox";
        private const string SandboxScene = SandboxDir + "/PalmSwingSandbox.unity";
        private const string SettingsPath = "Assets/Jazztures/Config/PalmSwingSettings.asset";
        private const string ChildName = "Palm Swing Sandbox";

        [MenuItem("Jazztures/Experiments/Palm Swing/Create Sandbox Scene (ADR-0078)")]
        public static void CreateSandboxScene()
        {
            if (File.Exists(SandboxScene)
                && !EditorUtility.DisplayDialog(
                    "Palm swing sandbox", $"{SandboxScene} exists. Replace it with a fresh copy of main.unity?", "Replace", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Directory.CreateDirectory(SandboxDir);
            if (!AssetDatabase.CopyAsset(MainScene, SandboxScene))
            {
                Debug.LogError($"Palm swing setup: could not copy {MainScene} to {SandboxScene}.");
                return;
            }

            EditorSceneManager.OpenScene(SandboxScene);
            if (AddToOpenScene())
            {
                EditorSceneManager.SaveOpenScenes();
                Debug.Log($"Palm swing setup: {SandboxScene} is ready. Press Play (Editor simulation on by default). It is not in the build settings: add it to build for a headset.");
            }
        }

        [MenuItem("Jazztures/Experiments/Palm Swing/Add Sandbox To Open Scene")]
        public static void AddSandboxToOpenScene() => AddToOpenScene();

        private static bool AddToOpenScene()
        {
            var root = Object.FindFirstObjectByType<PerformanceCompositionRoot>(FindObjectsInactive.Include);
            if (root == null)
            {
                Debug.LogError("Palm swing setup: no PerformanceCompositionRoot in the open scene.");
                return false;
            }

            var rootSo = new SerializedObject(root);
            if (rootSo.FindProperty("_fingerBend").objectReferenceValue == null)
            {
                FingerBendSetup.UseFingerBend(); // creates the finger-bend input and copies its hand, config and channels from the touch targets
                rootSo.Update();
            }

            var fingerBend = rootSo.FindProperty("_fingerBend").objectReferenceValue as FingerBendBinder;
            if (fingerBend == null)
            {
                Debug.LogError("Palm swing setup: the scene has no finger-bend input to take the right hand from. Nothing was changed.");
                return false;
            }

            Object rightHand = new SerializedObject(fingerBend).FindProperty("_rightHand").objectReferenceValue;
            Object noteChannel = rootSo.FindProperty("_noteChannel").objectReferenceValue;
            Transform head = Camera.main != null ? Camera.main.transform : null;
            Object settings = AssetDatabase.LoadAssetAtPath<Object>(SettingsPath);
            if (settings == null || rightHand == null)
            {
                Debug.LogError($"Palm swing setup: needs {SettingsPath} and a right hand on the finger-bend input. Nothing was changed.");
                return false;
            }

            Undo.SetCurrentGroupName("Add palm swing sandbox");
            int group = Undo.GetCurrentGroup();

            Transform existing = root.transform.Find(ChildName);
            GameObject go;
            if (existing == null)
            {
                go = new GameObject(ChildName);
                Undo.RegisterCreatedObjectUndo(go, "Create palm swing sandbox");
                go.transform.SetParent(root.transform, worldPositionStays: false);
            }
            else
            {
                go = existing.gameObject;
                Undo.RecordObject(go, "Show palm swing sandbox");
                go.SetActive(true);
            }

            var meta = Ensure<MetaXRPalmSwingSampleSource>(go);
            var sim = Ensure<EditorSimulatedPalmInput>(go);
            var input = Ensure<PalmSwingInputSource>(go);
            var view = Ensure<PalmSwingView>(go);
            var hud = Ensure<SandboxHud>(go);
            var sandbox = Ensure<PalmSwingSandbox>(go);
            var inputSwitch = Ensure<RightHandInputSwitch>(go);

            Set(meta, "_rightHand", rightHand);
            Set(meta, "_head", head);
            Set(meta, "_settings", settings);
            Set(input, "_settings", settings);
            Set(input, "_sampleSource", meta);
            Set(input, "_editorSimulation", sim);
            Set(view, "_head", head);
            Set(hud, "_head", head);
            Set(sandbox, "_settings", settings);
            Set(sandbox, "_palmSwing", input);
            Set(sandbox, "_headsetSource", meta);
            Set(sandbox, "_inputSwitch", inputSwitch);
            Set(sandbox, "_view", view);
            Set(sandbox, "_hud", hud);
            Set(sandbox, "_noteChannel", noteChannel);
            Set(sandbox, "_head", head);
            Set(inputSwitch, "_fingerBend", fingerBend);
            Set(inputSwitch, "_palmSwing", input);

            var switchSo = new SerializedObject(inputSwitch);
            switchSo.FindProperty("_mode").enumValueIndex = 1; // RightHandInputMode.PalmSwing
            switchSo.ApplyModifiedProperties();

            Set(root, "_palmSwing", input);
            Set(root, "_palmSwingSandbox", sandbox);

            // The menu and tutorial read the same right hand (flicks, a fist to go home): leave the sandbox alone with it.
            var flow = Object.FindFirstObjectByType<LessonFlowController>(FindObjectsInactive.Include);
            if (flow != null)
            {
                Undo.RecordObject(flow, "Disable lesson flow for the sandbox");
                flow.enabled = false;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log("Palm swing setup: sandbox added under the performance harness; the lesson flow is disabled in this scene. Save the scene (Ctrl+S).");
            return true;
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
