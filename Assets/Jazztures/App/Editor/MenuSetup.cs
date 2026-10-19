using Jazztures.Config;
using Jazztures.Input;
using Jazztures.Lessons;
using Jazztures.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jazztures.App.Editor
{
    /// <summary>
    /// Wires the gesture menu (ADR-0051) into the open scene, so nobody has to hand-edit scene
    /// YAML or click through nested Inspectors:
    /// <list type="number">
    ///   <item>creates <c>Config/MenuConfig.asset</c> if it does not exist;</item>
    ///   <item>adds a <see cref="MenuView"/> and a <see cref="MenuInputDriver"/> +
    ///   <see cref="MetaXRMenuHandSource"/>, pointing at the same right hand and head the rest of
    ///   the scene already uses;</item>
    ///   <item>assigns them, the config and the evaluation channel to the
    ///   <see cref="LessonFlowController"/>;</item>
    ///   <item>removes the <c>LessonSelector</c> and <c>ExitButton</c> objects the menu replaces —
    ///   their scripts are gone, so they show as "Missing (Mono Script)";</item>
    ///   <item>gives the ghost hand the strike pad it dips onto (ADR-0052);</item>
    ///   <item>sets up the tutorial (ADR-0053): the script asset, its line of text, the sky dome, and a
    ///   ghost <i>right</i> hand made from the SDK's own <c>OpenXRGhost-HandRight</c> prefab - wired to
    ///   the existing <i>ii</i> and <i>V</i> ghost poses, so nothing needs recording.</item>
    /// </list>
    /// Idempotent and undoable (Ctrl+Z). It changes the scene, which must then be saved.
    /// </summary>
    public static class MenuSetup
    {
        private const string ConfigPath = "Assets/Jazztures/Config/MenuConfig.asset";

        // The translucent plate material the old selector used, if it still exists in the project.
        private const string OldPlateMaterialGuid = "f6236b26e7de43fe9e8fd9a3245dbb2a";

        private const string TutorialPath = "Assets/Jazztures/Lessons/Tutorial.asset";
        private const string GhostPoseDir = "Assets/Jazztures/Input/Poses/Ghost";
        private const string GhostMaterialPath = "Assets/Jazztures/Presentation/Materials/GhostHand.mat";

        [MenuItem("Jazztures/Menu/Set Up Gesture Menu + Tutorial (ADR-0051, ADR-0053)")]
        public static void Apply()
        {
            var flow = Object.FindFirstObjectByType<LessonFlowController>(FindObjectsInactive.Include);
            if (flow == null)
            {
                Debug.LogError("Menu setup: no LessonFlowController in the open scene — open main.unity first.");
                return;
            }

            var poseSource = Object.FindFirstObjectByType<MetaXRHandPoseSource>(FindObjectsInactive.Include);
            Object rightHand = poseSource != null
                ? new SerializedObject(poseSource).FindProperty("_rightHand").objectReferenceValue
                : null;
            if (rightHand == null)
            {
                Debug.LogWarning(
                    "Menu setup: could not find the right hand (no MetaXRHandPoseSource with a right hand assigned). " +
                    "The menu will work from the keyboard only until you assign 'Right Hand' on the Menu Input object.");
            }

            Transform head = FindHead();
            MenuConfig config = LoadOrCreateConfig();
            int changes = 0;

            changes += RemoveReplacedObjects();

            MenuView view = Object.FindFirstObjectByType<MenuView>(FindObjectsInactive.Include);
            if (view == null)
            {
                var go = new GameObject("Menu View");
                Undo.RegisterCreatedObjectUndo(go, "Create Menu View");
                view = go.AddComponent<MenuView>();
                changes++;
            }

            changes += SetReference(view, "_head", head);
            Material plate = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(OldPlateMaterialGuid));
            if (plate != null && new SerializedObject(view).FindProperty("_plateMaterial").objectReferenceValue == null)
            {
                changes += SetReference(view, "_plateMaterial", plate);
            }

            var driver = Object.FindFirstObjectByType<MenuInputDriver>(FindObjectsInactive.Include);
            if (driver == null)
            {
                var go = new GameObject("Menu Input");
                Undo.RegisterCreatedObjectUndo(go, "Create Menu Input");
                driver = go.AddComponent<MenuInputDriver>();
                changes++;
            }

            var source = driver.GetComponent<MetaXRMenuHandSource>();
            if (source == null)
            {
                source = Undo.AddComponent<MetaXRMenuHandSource>(driver.gameObject);
                changes++;
            }

            changes += SetReference(source, "_rightHand", rightHand);
            changes += SetReference(source, "_head", head);
            changes += SetReference(driver, "_handSource", source);

            changes += SetReference(flow, "_menuView", view);
            changes += SetReference(flow, "_menuInput", driver);
            changes += SetReference(flow, "_menuConfig", config);

            var runner = Object.FindFirstObjectByType<LessonRunner>(FindObjectsInactive.Include);
            if (runner != null)
            {
                var runnerSo = new SerializedObject(runner);
                changes += SetReference(flow, "_evaluationChannel", runnerSo.FindProperty("_evaluationChannel").objectReferenceValue);
                changes += SetReference(flow, "_cueChannel", runnerSo.FindProperty("_cueChannel").objectReferenceValue);
            }

            // ADR-0052: the ghost hand dips onto the same pad the learner strikes.
            var ghost = Object.FindFirstObjectByType<GhostHandView>(FindObjectsInactive.Include);
            var pad = Object.FindFirstObjectByType<ChordStrikeTarget>(FindObjectsInactive.Include);
            if (ghost != null && pad != null)
            {
                changes += SetReference(ghost, "_strikePad", pad);
            }
            else
            {
                Debug.LogWarning(
                    "Menu setup: could not give the ghost hand its strike pad (" +
                    (ghost == null ? "no GhostHandView" : "no ChordStrikeTarget") +
                    " in the scene) - it will dip straight down instead of onto the pad.");
            }

            changes += SetUpTutorial(flow, source, poseSource, head);

            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
                Debug.Log($"Menu setup: {changes} change(s) made. Save the scene (Ctrl+S) to keep them; Ctrl+Z undoes.");
            }
            else
            {
                Debug.Log("Menu setup: already set up — nothing to change.");
            }
        }

        // --- tutorial (ADR-0053) ----------------------------------------------------------------

        private static int SetUpTutorial(
            LessonFlowController flow, MetaXRMenuHandSource menuSource, MetaXRHandPoseSource poseSource, Transform head)
        {
            int changes = 0;

            // The left hand, so the tutorial can tell whether both hands are in view.
            if (poseSource != null)
            {
                Object leftHand = new SerializedObject(poseSource).FindProperty("_leftHand").objectReferenceValue;
                changes += SetReference(menuSource, "_leftHand", leftHand);
            }

            // The script.
            var definition = AssetDatabase.LoadAssetAtPath<TutorialDefinition>(TutorialPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<TutorialDefinition>();
                AssetDatabase.CreateAsset(definition, TutorialPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Menu setup: created {TutorialPath} (the shipped tutorial script).");
                changes++;
            }

            // Its line of text.
            var view = Object.FindFirstObjectByType<TutorialView>(FindObjectsInactive.Include);
            if (view == null)
            {
                var go = new GameObject("Tutorial View");
                Undo.RegisterCreatedObjectUndo(go, "Create Tutorial View");
                view = go.AddComponent<TutorialView>();
                changes++;
            }

            changes += SetReference(view, "_head", head);

            // The sky.
            var sky = Object.FindFirstObjectByType<SkyDome>(FindObjectsInactive.Include);
            if (sky == null)
            {
                var go = new GameObject("Sky Dome");
                Undo.RegisterCreatedObjectUndo(go, "Create Sky Dome");
                sky = go.AddComponent<SkyDome>();
                changes++;
            }

            changes += SetReference(sky, "_head", head);
            changes += FillCameras(sky, head);

            // The ghost right hand.
            changes += SetUpTutorialGhost(flow, head, out TutorialGhostView ghost);

            changes += SetReference(flow, "_tutorial", definition);
            changes += SetReference(flow, "_tutorialView", view);
            changes += SetReference(flow, "_tutorialGhost", ghost);
            changes += SetReference(flow, "_sky", sky);
            return changes;
        }

        /// <summary>The cameras that draw the scene: every camera under the rig that holds the head, else the main camera.</summary>
        private static int FillCameras(SkyDome sky, Transform head)
        {
            var so = new SerializedObject(sky);
            SerializedProperty cameras = so.FindProperty("_cameras");
            if (cameras.arraySize > 0)
            {
                return 0; // someone already chose
            }

            Camera[] found = head != null
                ? head.root.GetComponentsInChildren<Camera>(true)
                : (Camera.main != null ? new[] { Camera.main } : new Camera[0]);
            if (found.Length == 0)
            {
                return 0;
            }

            cameras.arraySize = found.Length;
            for (int i = 0; i < found.Length; i++)
            {
                cameras.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
            }

            so.ApplyModifiedProperties();
            return 1;
        }

        private static int SetUpTutorialGhost(LessonFlowController flow, Transform head, out TutorialGhostView ghost)
        {
            int changes = 0;
            ghost = Object.FindFirstObjectByType<TutorialGhostView>(FindObjectsInactive.Include);

            if (ghost == null)
            {
                var root = new GameObject("Tutorial Ghost");
                Undo.RegisterCreatedObjectUndo(root, "Create Tutorial Ghost");
                ghost = root.AddComponent<TutorialGhostView>();
                changes++;
            }

            // The SDK's own right ghost hand, parented under the view. It is the sibling of the left one the
            // left-hand lessons already use.
            Component puppet = FindPuppet(ghost.gameObject);
            if (puppet == null)
            {
                GameObject prefab = FindSdkGhostPrefab();
                if (prefab == null)
                {
                    Debug.LogWarning(
                        "Menu setup: could not find the SDK prefab OpenXRGhost-HandRight (Meta XR Interaction SDK). " +
                        "The tutorial will run without a ghost hand until you add a right ghost hand and assign its HandPuppet.");
                }
                else
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ghost.transform);
                    Undo.RegisterCreatedObjectUndo(instance, "Create Tutorial Ghost Hand");
                    instance.name = "Right Ghost Hand";

                    var material = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
                    if (material != null)
                    {
                        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                        {
                            renderer.sharedMaterial = material; // the same ghost look as the left hand
                        }
                    }

                    puppet = FindPuppet(instance);
                    changes++;
                }
            }

            changes += SetReference(ghost, "_puppet", puppet);
            changes += SetReference(ghost, "_head", head);
            changes += SetReference(ghost, "_openPose", AssetDatabase.LoadAssetAtPath<GhostPoseAsset>($"{GhostPoseDir}/Ii.asset"));
            changes += SetReference(ghost, "_fistPose", AssetDatabase.LoadAssetAtPath<GhostPoseAsset>($"{GhostPoseDir}/V.asset"));
            return changes;
        }

        /// <summary>The <c>HandPuppet</c> component under <paramref name="root"/>, found by name so this assembly needs no SDK reference.</summary>
        private static Component FindPuppet(GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == "HandPuppet")
                {
                    return component;
                }
            }

            return null;
        }

        private static GameObject FindSdkGhostPrefab()
        {
            foreach (string guid in AssetDatabase.FindAssets("OpenXRGhost-HandRight t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Packages/") && path.EndsWith("OpenXRGhost-HandRight.prefab"))
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            return null;
        }

        /// <summary>The same head the HUD is placed against, else the main camera.</summary>
        private static Transform FindHead()
        {
            var hud = Object.FindFirstObjectByType<LessonHud>(FindObjectsInactive.Include);
            if (hud != null)
            {
                var head = new SerializedObject(hud).FindProperty("_head").objectReferenceValue as Transform;
                if (head != null)
                {
                    return head;
                }
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        private static MenuConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MenuConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<MenuConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Menu setup: created {ConfigPath}.");
            return config;
        }

        /// <summary>
        /// Delete the <c>LessonSelector</c> and <c>ExitButton</c> objects: ones with those names
        /// whose only components are a Transform and scripts that no longer exist. Anything else
        /// by that name is left alone.
        /// </summary>
        private static int RemoveReplacedObjects()
        {
            int removed = 0;
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in transforms)
            {
                GameObject go = t.gameObject;
                if (go == null || (go.name != "LessonSelector" && go.name != "ExitButton"))
                {
                    continue;
                }

                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                int components = go.GetComponents<Component>().Length; // includes the missing ones, as null
                if (missing > 0 && components == missing + 1)
                {
                    Undo.DestroyObjectImmediate(go);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Debug.Log($"Menu setup: removed {removed} replaced object(s) (LessonSelector / ExitButton).");
            }

            return removed;
        }

        /// <summary>Set an object-reference field if it differs; returns 1 when it changed.</summary>
        private static int SetReference(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"Menu setup: {target.GetType().Name} has no serialized field '{field}'.");
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
