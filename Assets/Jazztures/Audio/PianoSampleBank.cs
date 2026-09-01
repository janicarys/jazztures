using System.Collections.Generic;
using Jazztures.Core.Music;
using Jazztures.Core.Sampling;
using UnityEngine;

namespace Jazztures.Audio
{
    /// <summary>
    /// Serialised list of piano <see cref="AudioClip"/>s plus the <see cref="SampleLibrary"/>
    /// built from their names. Populate it with <b>Jazztures &gt; Audio &gt; Rebuild Piano
    /// Sample Bank</b>, which scans <c>Assets/Jazztures/Audio/piano/</c> and assigns every
    /// clip whose name is a Salamander sample name (<c>&lt;pitch&gt;v&lt;L|H&gt;</c>).
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Piano Sample Bank", fileName = "PianoSampleBank")]
    public sealed class PianoSampleBank : ScriptableObject
    {
        [Tooltip("Piano samples. Names must be Salamander-style, e.g. A4vH, D#3vL.")]
        [SerializeField] private AudioClip[] _clips = new AudioClip[0];

        [Tooltip("Velocities at or above this use the Hard layer, below it the Soft layer.")]
        [Range(1, 126)]
        [SerializeField] private int _layerSplitVelocity = SampleLibrary.DefaultLayerSplitVelocity;

        private SampleLibrary _library;

        public IReadOnlyList<AudioClip> Clips => _clips;

        /// <summary>The sample-selection library. Built once, lazily, from the clip names.</summary>
        public SampleLibrary Library => _library ??= BuildLibrary();

#if UNITY_EDITOR
        /// <summary>Editor-only: replace the clip list (used by the rebuild menu item).</summary>
        public void SetClips(AudioClip[] clips)
        {
            _clips = clips;
            _library = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private SampleLibrary BuildLibrary()
        {
            var entries = new List<SampleEntry>();
            for (int i = 0; i < _clips.Length; i++)
            {
                AudioClip clip = _clips[i];
                if (clip == null)
                {
                    continue;
                }

                if (SampleFileName.TryParse(clip.name, out Pitch root, out VelocityLayer layer))
                {
                    entries.Add(new SampleEntry(root, layer, i));
                }
                else
                {
                    Debug.LogWarning(
                        $"{name}: clip '{clip.name}' is not a recognised sample name; skipped.", this);
                }
            }

            if (entries.Count == 0)
            {
                Debug.LogError($"{name}: no usable piano samples. Run Jazztures > Audio > Rebuild Piano Sample Bank.", this);
                // A one-entry stand-in keeps the sink from throwing; it will just sound wrong.
                entries.Add(new SampleEntry(Pitch.MiddleC, VelocityLayer.Soft, 0));
            }

            return new SampleLibrary(entries, (byte)_layerSplitVelocity);
        }
    }
}
