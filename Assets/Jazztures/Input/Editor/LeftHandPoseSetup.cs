using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.PoseDetection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jazztures.Input.Editor
{
    /// <summary>
    /// Applies ADR-0047's two changes to the left-hand pose recognisers in the open scene, so
    /// nobody has to hand-edit scene YAML or click through four nested Inspectors:
    /// <list type="number">
    ///   <item>
    ///     <b>ii becomes "palm sideways"</b> rather than "thumb points up". The old rule
    ///     (<c>WristUp</c>, ADR-0026) accepted only fingers-forward / thumb-up; a palm facing the
    ///     user's right is just as naturally held fingers-up / thumb-forward. The SDK has no
    ///     "palm faces right" feature (ADR-0014), but a sideways palm is exactly the hand that is
    ///     none of palm-down, palm-up, palm-toward-face and palm-away-from-face — four
    ///     <c>IsNot</c> conditions, all of which must hold.
    ///   </item>
    ///   <item>
    ///     <b>ii and I measure "up" against gravity, not the headset</b> (<c>UpVectorType</c>
    ///     Head → Tracking). A learner looking down at the strike plate pitches the headset by
    ///     ~40°, which tilted the "up" the thumb and palm were being compared to.
    ///   </item>
    /// </list>
    /// Idempotent and undoable (Ctrl+Z). It changes the scene, which must then be saved.
    /// </summary>
    public static class LeftHandPoseSetup
    {
        // TransformFeature / FeatureStateActiveMode values (Meta XR Interaction SDK 205.0.0).
        private const int FeaturePalmDown = (int)TransformFeature.PalmDown;
        private const int FeaturePalmUp = (int)TransformFeature.PalmUp;
        private const int FeaturePalmTowardsFace = (int)TransformFeature.PalmTowardsFace;
        private const int FeaturePalmAwayFromFace = (int)TransformFeature.PalmAwayFromFace;
        private const int ModeIsNot = (int)FeatureStateActiveMode.IsNot;
        private const int UpVectorTracking = (int)UpVectorType.Tracking;

        [MenuItem("Jazztures/Gestures/Apply Sideways-Palm ii + Gravity-Up (ADR-0047)")]
        public static void Apply()
        {
            var source = Object.FindFirstObjectByType<MetaXRHandPoseSource>();
            if (source == null)
            {
                Debug.LogError("Left-hand pose setup: no MetaXRHandPoseSource in the open scene — open main.unity first.");
                return;
            }

            var sourceSo = new SerializedObject(source);
            TransformRecognizerActiveState ii = FindTransformRecogniser(sourceSo.FindProperty("_iiPoseState"));
            TransformRecognizerActiveState i = FindTransformRecogniser(sourceSo.FindProperty("_iPoseState"));
            if (ii == null || i == null)
            {
                Debug.LogError(
                    "Left-hand pose setup: could not find the TransformRecognizerActiveState behind the ii and/or I " +
                    "pose. Expected each to be an ActiveStateGroup containing one — nothing was changed.");
                return;
            }

            int changes = 0;
            changes += SetSidewaysPalm(ii);
            changes += SetGravityUp(ii);
            changes += SetGravityUp(i);

            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(source.gameObject.scene);
                Debug.Log($"Left-hand pose setup: {changes} setting(s) changed. Save the scene (Ctrl+S) to keep them; Ctrl+Z undoes.");
            }
            else
            {
                Debug.Log("Left-hand pose setup: already applied — nothing to change.");
            }
        }

        /// <summary>ii = open palm AND palm is none of down / up / toward face / away from face.</summary>
        private static int SetSidewaysPalm(TransformRecognizerActiveState recogniser)
        {
            var so = new SerializedObject(recogniser);
            SerializedProperty values = so.FindProperty("_transformFeatureConfigs._values");

            Object thresholds = recogniser.TransformConfig.FeatureThresholds;
            var wanted = new List<(int feature, string state)>
            {
                (FeaturePalmDown, FirstStateOf(thresholds, FeaturePalmDown)),
                (FeaturePalmUp, FirstStateOf(thresholds, FeaturePalmUp)),
                (FeaturePalmTowardsFace, FirstStateOf(thresholds, FeaturePalmTowardsFace)),
                (FeaturePalmAwayFromFace, FirstStateOf(thresholds, FeaturePalmAwayFromFace)),
            };

            if (wanted.Exists(w => w.state == null))
            {
                Debug.LogError("Left-hand pose setup: the thresholds asset has no first-state id for one of the palm features — ii was not changed.");
                return 0;
            }

            if (AlreadyMatches(values, wanted))
            {
                return 0;
            }

            values.ClearArray();
            for (int n = 0; n < wanted.Count; n++)
            {
                values.InsertArrayElementAtIndex(n);
                SerializedProperty element = values.GetArrayElementAtIndex(n);
                element.FindPropertyRelative("_mode").enumValueIndex = ModeIsNot;
                element.FindPropertyRelative("_feature").enumValueIndex = wanted[n].feature;
                element.FindPropertyRelative("_state").stringValue = wanted[n].state;
            }

            so.ApplyModifiedProperties();
            return 1;
        }

        private static int SetGravityUp(TransformRecognizerActiveState recogniser)
        {
            var so = new SerializedObject(recogniser);
            SerializedProperty up = so.FindProperty("_transformConfig.UpVectorType");
            if (up == null || up.enumValueIndex == UpVectorTracking)
            {
                return 0;
            }

            up.enumValueIndex = UpVectorTracking;
            so.ApplyModifiedProperties();
            return 1;
        }

        private static bool AlreadyMatches(SerializedProperty values, List<(int feature, string state)> wanted)
        {
            if (values.arraySize != wanted.Count)
            {
                return false;
            }

            for (int n = 0; n < wanted.Count; n++)
            {
                SerializedProperty element = values.GetArrayElementAtIndex(n);
                if (element.FindPropertyRelative("_mode").enumValueIndex != ModeIsNot
                    || element.FindPropertyRelative("_feature").enumValueIndex != wanted[n].feature
                    || element.FindPropertyRelative("_state").stringValue != wanted[n].state)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The state id the thresholds asset gives a feature when its angle is small (the "first"
        /// state) — read from the asset, not assumed, so it follows whatever the project has.
        /// </summary>
        private static string FirstStateOf(Object thresholdsAsset, int feature)
        {
            if (thresholdsAsset == null)
            {
                return null;
            }

            var so = new SerializedObject(thresholdsAsset);
            SerializedProperty features = so.FindProperty("_featureThresholds");
            for (int n = 0; features != null && n < features.arraySize; n++)
            {
                SerializedProperty entry = features.GetArrayElementAtIndex(n);
                if (entry.FindPropertyRelative("_feature").enumValueIndex != feature)
                {
                    continue;
                }

                SerializedProperty thresholds = entry.FindPropertyRelative("_thresholds");
                if (thresholds != null && thresholds.arraySize > 0)
                {
                    return thresholds.GetArrayElementAtIndex(0).FindPropertyRelative("_firstState").stringValue;
                }
            }

            return null;
        }

        /// <summary>
        /// The <see cref="TransformRecognizerActiveState"/> behind a pose reference: the reference
        /// itself, or the one inside an <see cref="ActiveStateGroup"/> (how ii and I are authored).
        /// </summary>
        private static TransformRecognizerActiveState FindTransformRecogniser(SerializedProperty reference)
        {
            Object target = reference?.objectReferenceValue;
            if (target is TransformRecognizerActiveState direct)
            {
                return direct;
            }

            if (target is ActiveStateGroup group)
            {
                SerializedProperty states = new SerializedObject(group).FindProperty("_activeStates");
                for (int n = 0; states != null && n < states.arraySize; n++)
                {
                    if (states.GetArrayElementAtIndex(n).objectReferenceValue is TransformRecognizerActiveState found)
                    {
                        return found;
                    }
                }
            }

            return null;
        }
    }
}
