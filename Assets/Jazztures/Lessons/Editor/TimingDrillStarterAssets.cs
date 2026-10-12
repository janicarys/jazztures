using System.Collections.Generic;
using Jazztures.Core.Drills;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using UnityEditor;
using UnityEngine;

namespace Jazztures.Lessons.Editor
{
    /// <summary>
    /// Creates the starter <see cref="TimingDrillDefinition"/> presets for the right-hand
    /// baseline (ADR-0043 step 5) and adds them to the <see cref="LessonCatalog"/>, so the
    /// "Timing test" menu has something in it without hand-editing asset YAML. Safe to run
    /// again: existing presets are reused (and reset to these values), and a preset already in
    /// the catalog is not added twice.
    ///
    /// <para>
    /// The presets are the arc-era subset of ADR-0043's bake-off: right hand alone over an
    /// audible I chord at 60 and 80 BPM, one onset per beat or per eighth, plus a left-hand
    /// comping drill and a metronome-only run. The pitch-specific drills (alternating rungs,
    /// a repeated shape) wait for the ladder. Names are short because a menu row is ~15
    /// characters wide in the headset.
    /// </para>
    /// </summary>
    public static class TimingDrillStarterAssets
    {
        private const string Folder = "Assets/Jazztures/Lessons/TimingDrills";

        [MenuItem("Jazztures/Timing/Create Starter Drills (arc baseline)")]
        public static void Create()
        {
            var made = new List<TimingDrillDefinition>
            {
                RightHand("Drill_Beats_60", "Beats 60", 60f, OnsetPatternSpec.PatternKind.EveryBeat),
                RightHand("Drill_Beats_80", "Beats 80", 80f, OnsetPatternSpec.PatternKind.EveryBeat),
                RightHand("Drill_Eighths_60", "Eighths 60", 60f, OnsetPatternSpec.PatternKind.EveryEighth),
                RightHand("Drill_Eighths_80", "Eighths 80", 80f, OnsetPatternSpec.PatternKind.EveryEighth),
                LeftComping(),
                MetronomeOnly(),
            };

            int added = AddToCatalog(made);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Timing drills: {made.Count} presets in {Folder}, {added} newly added to the catalog.");
        }

        /// <summary>Right hand alone over an audible I chord, 1-bar count-in, 4 bars (ADR-0043 bake-off).</summary>
        private static TimingDrillDefinition RightHand(
            string assetName, string displayName, float bpm, OnsetPatternSpec.PatternKind pattern)
        {
            TimingDrillDefinition drill = NewAsset(assetName);
            var so = new SerializedObject(drill);

            Common(so, displayName, bpm, bars: 4, countIn: 1);
            SetBool(so, "_scoringEnabled", true);
            SetEnum(so, "_scoredHand", (int)Handedness.Right);
            SetEnum(so, "_activeHands", (int)ActiveHands.Right);
            SetEnum(so, "_pattern._kind", (int)pattern);
            SetEnum(so, "_harmony", (int)DrillHarmonyKind.Fixed);
            SetEnum(so, "_fixedChord", (int)ChordFunction.One);
            SetBool(so, "_harmonyAudible", true);

            Finish(so, drill);
            return drill;
        }

        /// <summary>The left hand strikes a chord on beats 1 and 3; the learner plays the harmony, so none is scripted.</summary>
        private static TimingDrillDefinition LeftComping()
        {
            TimingDrillDefinition drill = NewAsset("Drill_Comping_80");
            var so = new SerializedObject(drill);

            Common(so, "Comp 1+3 80", 80f, bars: 4, countIn: 1);
            SetBool(so, "_scoringEnabled", true);
            SetEnum(so, "_scoredHand", (int)Handedness.Left);
            SetEnum(so, "_activeHands", (int)ActiveHands.Left);
            SetEnum(so, "_pattern._kind", (int)OnsetPatternSpec.PatternKind.PositionsPerBar);
            SetPositions(so, 0f, 2f);
            SetEnum(so, "_harmony", (int)DrillHarmonyKind.None);

            Finish(so, drill);
            return drill;
        }

        /// <summary>The click alone, both hands live — informal free play against a pulse. Nothing is scored or written.</summary>
        private static TimingDrillDefinition MetronomeOnly()
        {
            TimingDrillDefinition drill = NewAsset("Drill_ClickOnly_80");
            var so = new SerializedObject(drill);

            Common(so, "Click only 80", 80f, bars: 8, countIn: 0);
            SetBool(so, "_scoringEnabled", false);
            SetEnum(so, "_scoredHand", (int)Handedness.Right);
            SetEnum(so, "_activeHands", (int)ActiveHands.Both);
            SetEnum(so, "_pattern._kind", (int)OnsetPatternSpec.PatternKind.None);
            SetEnum(so, "_harmony", (int)DrillHarmonyKind.None);

            Finish(so, drill);
            return drill;
        }

        private static void Common(SerializedObject so, string displayName, float bpm, int bars, int countIn)
        {
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_tempoBpm").floatValue = bpm;
            SetInt(so, "_beatsPerBar", Metronome.DefaultBeatsPerBar);
            SetEnum(so, "_subdivision", (int)MetronomeSubdivision.None);
            so.FindProperty("_swingRatio").floatValue = 0.5f;
            SetInt(so, "_countInBars", countIn);
            SetInt(so, "_bars", bars);
            SetBool(so, "_metronomeAudible", true);
        }

        private static void Finish(SerializedObject so, TimingDrillDefinition drill)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(drill);
        }

        private static TimingDrillDefinition NewAsset(string assetName)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Jazztures/Lessons", "TimingDrills");
            }

            string path = $"{Folder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TimingDrillDefinition>(path);
            if (existing != null)
            {
                return existing;
            }

            var drill = ScriptableObject.CreateInstance<TimingDrillDefinition>();
            AssetDatabase.CreateAsset(drill, path);
            return drill;
        }

        private static int AddToCatalog(List<TimingDrillDefinition> drills)
        {
            string[] guids = AssetDatabase.FindAssets("t:LessonCatalog");
            if (guids.Length == 0)
            {
                Debug.LogWarning("Timing drills: no LessonCatalog asset found — add the presets to its Timing Drills list by hand.");
                return 0;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<LessonCatalog>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var so = new SerializedObject(catalog);
            SerializedProperty list = so.FindProperty("_timingDrills");

            int added = 0;
            foreach (TimingDrillDefinition drill in drills)
            {
                if (Contains(list, drill))
                {
                    continue;
                }

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = drill;
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return added;
        }

        private static bool Contains(SerializedProperty list, Object target)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == target)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetInt(SerializedObject so, string path, int value) =>
            so.FindProperty(path).intValue = value;

        private static void SetBool(SerializedObject so, string path, bool value) =>
            so.FindProperty(path).boolValue = value;

        // Every enum set here is declared with sequential values from 0, so its index is its value
        // (the same assumption LessonStarterAssets makes for ActiveHands).
        private static void SetEnum(SerializedObject so, string path, int value) =>
            so.FindProperty(path).enumValueIndex = value;

        private static void SetPositions(SerializedObject so, params float[] positions)
        {
            SerializedProperty list = so.FindProperty("_pattern._positionsInBar");
            list.ClearArray();
            for (int i = 0; i < positions.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).floatValue = positions[i];
            }
        }
    }
}
