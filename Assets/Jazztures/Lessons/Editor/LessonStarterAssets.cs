using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using UnityEditor;
using UnityEngine;

namespace Jazztures.Lessons.Editor
{
    /// <summary>
    /// Creates starter <see cref="LessonDefinition"/> assets for Session 1 (L1–L3, §3.9)
    /// so M5 can be exercised without hand-editing asset YAML. The content is minimal and
    /// meant to be refined in the inspector; the SMF importer (M6) will replace the
    /// hand-authored timelines.
    /// </summary>
    public static class LessonStarterAssets
    {
        private const string Folder = "Assets/Jazztures/Lessons";

        [MenuItem("Jazztures/Lessons/Create Session 1 Starter Lessons (L1-L3)")]
        public static void CreateSession1()
        {
            CreateL1();
            CreateL2();
            CreateL3();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateL1()
        {
            LessonDefinition lesson = NewAsset("Lesson_L1_Chords");
            var so = new SerializedObject(lesson);

            SetString(so, "_id", "L1");
            SetString(so, "_title", "ii-V-I chords");
            SetString(so, "_conceptExplanation",
                "Three hand shapes, one per chord. Open palm to the right prepares. " +
                "A fist is the tension. Open palm down is the release. Hold them in any order.");
            SetFloat(so, "_tempoBpm", 80f);
            SetEnum(so, "_hands", (int)ActiveHands.Left);

            SetModes(so, LearningMode.WatchAndListen, LearningMode.TryYourself);
            SetChords(so, (0f, ChordFunction.Two), (2f, ChordFunction.Five), (4f, ChordFunction.One));

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lesson);
        }

        private static void CreateL2()
        {
            LessonDefinition lesson = NewAsset("Lesson_L2_Timing");
            var so = new SerializedObject(lesson);

            SetString(so, "_id", "L2");
            SetString(so, "_title", "Timing");
            SetString(so, "_conceptExplanation",
                "Keep a steady pulse with the metronome. The harmony stays frozen so you " +
                "can put all your attention on the beat.");
            SetFloat(so, "_tempoBpm", 80f);
            SetEnum(so, "_hands", (int)ActiveHands.Left);
            SetBool(so, "_useMetronome", true);

            SetModes(so, LearningMode.WatchAndListen, LearningMode.TryYourself, LearningMode.TestYourself);
            SetChords(so, (0f, ChordFunction.Two), (4f, ChordFunction.Two));

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lesson);
        }

        private static void CreateL3()
        {
            LessonDefinition lesson = NewAsset("Lesson_L3_ChordTones");
            var so = new SerializedObject(lesson);

            SetString(so, "_id", "L3");
            SetString(so, "_title", "Chord tones");
            SetString(so, "_conceptExplanation",
                "The right-hand targets are the notes that belong to the current chord: " +
                "root, third, fifth, seventh, ninth. Reach for them over the changes.");
            SetFloat(so, "_tempoBpm", 80f);
            SetEnum(so, "_hands", (int)ActiveHands.Right);

            SetModes(so, LearningMode.WatchAndListen, LearningMode.TryYourself);
            SetChords(so, (0f, ChordFunction.Two), (4f, ChordFunction.Five), (8f, ChordFunction.One));
            SetNotes(so, (0f, 0), (1f, 1), (2f, 2), (4f, 0), (5f, 2), (8f, 0), (9f, 4));

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lesson);
        }

        private static LessonDefinition NewAsset(string name)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Jazztures", "Lessons");
            }

            string path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<LessonDefinition>(path);
            if (existing != null)
            {
                return existing;
            }

            var lesson = ScriptableObject.CreateInstance<LessonDefinition>();
            AssetDatabase.CreateAsset(lesson, path);
            return lesson;
        }

        private static void SetString(SerializedObject so, string field, string value) =>
            so.FindProperty(field).stringValue = value;

        private static void SetFloat(SerializedObject so, string field, float value) =>
            so.FindProperty(field).floatValue = value;

        private static void SetBool(SerializedObject so, string field, bool value) =>
            so.FindProperty(field).boolValue = value;

        private static void SetEnum(SerializedObject so, string field, int value) =>
            so.FindProperty(field).enumValueIndex = value;

        private static void SetModes(SerializedObject so, params LearningMode[] modes)
        {
            SerializedProperty list = so.FindProperty("_modes");
            list.ClearArray();
            for (int i = 0; i < modes.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).enumValueIndex = (int)modes[i];
            }
        }

        private static void SetChords(SerializedObject so, params (float beat, ChordFunction function)[] chords)
        {
            SerializedProperty list = so.FindProperty("_chords");
            list.ClearArray();
            for (int i = 0; i < chords.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("beat").floatValue = chords[i].beat;
                element.FindPropertyRelative("function").enumValueIndex = (int)chords[i].function;
            }
        }

        private static void SetNotes(SerializedObject so, params (float beat, int targetIndex)[] notes)
        {
            SerializedProperty list = so.FindProperty("_notes");
            list.ClearArray();
            for (int i = 0; i < notes.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("beat").floatValue = notes[i].beat;
                element.FindPropertyRelative("targetIndex").intValue = notes[i].targetIndex;
                element.FindPropertyRelative("velocity").intValue = 90;
            }
        }
    }
}
