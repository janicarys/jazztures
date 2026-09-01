using Jazztures.Audio;
using UnityEditor;
using UnityEngine;

namespace Jazztures.App.Editor
{
    /// <summary>
    /// Drops a ready-wired debug harness into the open scene so M2 can be exercised
    /// without hand-editing scene YAML: a <see cref="SamplerNoteSink"/> +
    /// <see cref="PerformanceCompositionRoot"/> on one GameObject, with the
    /// <see cref="PianoSampleBank"/> assigned if one exists.
    /// </summary>
    public static class PerformanceHarnessMenu
    {
        private const string BankAssetPath = "Assets/Jazztures/Audio/PianoSampleBank.asset";

        [MenuItem("Jazztures/Debug/Create Performance Harness")]
        public static void Create()
        {
            var go = new GameObject("Jazztures Performance Harness");
            var sampler = go.AddComponent<SamplerNoteSink>();
            var root = go.AddComponent<PerformanceCompositionRoot>();

            var bank = AssetDatabase.LoadAssetAtPath<PianoSampleBank>(BankAssetPath);
            if (bank != null)
            {
                var so = new SerializedObject(sampler);
                so.FindProperty("_bank").objectReferenceValue = bank;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning(
                    "No PianoSampleBank found. Run Jazztures > Audio > Rebuild Piano Sample Bank, " +
                    "then assign it on the harness.");
            }

            var rootSo = new SerializedObject(root);
            rootSo.FindProperty("_sampler").objectReferenceValue = sampler;
            rootSo.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(go, "Create Jazztures Performance Harness");
            Selection.activeObject = go;
        }
    }
}
