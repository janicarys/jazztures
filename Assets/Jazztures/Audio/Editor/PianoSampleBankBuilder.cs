using System.Collections.Generic;
using Jazztures.Core.Music;
using Jazztures.Core.Sampling;
using UnityEditor;
using UnityEngine;

namespace Jazztures.Audio.Editor
{
    /// <summary>
    /// Editor tool that (re)builds the <see cref="PianoSampleBank"/> asset from the WAVs
    /// in <c>Assets/Jazztures/Audio/piano/</c>. Run it after adding or changing samples.
    /// Clips are sorted low→high so the bank inspector reads sensibly.
    /// </summary>
    public static class PianoSampleBankBuilder
    {
        private const string PianoFolder = "Assets/Jazztures/Audio/piano";
        private const string BankAssetPath = "Assets/Jazztures/Audio/PianoSampleBank.asset";

        [MenuItem("Jazztures/Audio/Rebuild Piano Sample Bank")]
        public static void Rebuild()
        {
            var recognised = new List<(AudioClip clip, int midi)>();
            var skipped = new List<string>();

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { PianoFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    continue;
                }

                if (SampleFileName.TryParse(clip.name, out Pitch root, out VelocityLayer _))
                {
                    recognised.Add((clip, root.Midi));
                }
                else
                {
                    skipped.Add(clip.name);
                }
            }

            if (recognised.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Rebuild Piano Sample Bank",
                    $"No Salamander-style samples found in {PianoFolder}.",
                    "OK");
                return;
            }

            recognised.Sort((a, b) => a.midi.CompareTo(b.midi));
            var clips = new AudioClip[recognised.Count];
            for (int i = 0; i < recognised.Count; i++)
            {
                clips[i] = recognised[i].clip;
            }

            PianoSampleBank bank = AssetDatabase.LoadAssetAtPath<PianoSampleBank>(BankAssetPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<PianoSampleBank>();
                AssetDatabase.CreateAsset(bank, BankAssetPath);
            }

            bank.SetClips(clips);
            AssetDatabase.SaveAssets();
            Selection.activeObject = bank;

            string message = $"Piano sample bank: {clips.Length} clips.";
            if (skipped.Count > 0)
            {
                message += $"\nSkipped {skipped.Count} unrecognised file(s): {string.Join(", ", skipped)}";
            }

            Debug.Log(message, bank);
        }
    }
}
