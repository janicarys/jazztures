using System;
using System.Collections.Generic;
using Jazztures.Core.Music;

namespace Jazztures.Core.Sampling
{
    /// <summary>
    /// A pitched sample set. Given a target pitch and a MIDI velocity, picks the recorded
    /// clip whose root is nearest in semitones (ties go to the lower root) and the
    /// playback rate to shift it there. Layer choice follows from velocity but is
    /// subordinate to pitch accuracy — a closer root in the "wrong" layer beats a
    /// further root in the right one.
    /// </summary>
    /// <remarks>
    /// Pure and headless-tested: getting nearest-neighbour selection and the
    /// equal-temperament ratio right matters, and neither needs Unity. The Unity audio
    /// layer owns the actual clips and indexes them with <see cref="SampleSelection.ClipIndex"/>.
    /// </remarks>
    public sealed class SampleLibrary
    {
        /// <summary>
        /// Velocities at or above this use <see cref="VelocityLayer.Hard"/>, below it
        /// <see cref="VelocityLayer.Soft"/>. `[TUNABLE]` — see <c>Docs/CALIBRATION.md</c>.
        /// </summary>
        public const byte DefaultLayerSplitVelocity = 64;

        private readonly SampleEntry[] _entries;
        private readonly byte _layerSplitVelocity;

        public SampleLibrary(
            IEnumerable<SampleEntry> entries,
            byte layerSplitVelocity = DefaultLayerSplitVelocity)
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            var list = new List<SampleEntry>();
            foreach (SampleEntry entry in entries)
            {
                list.Add(entry);
            }

            if (list.Count == 0)
            {
                throw new ArgumentException("A sample library needs at least one entry.", nameof(entries));
            }

            list.Sort((a, b) => a.Root.Midi.CompareTo(b.Root.Midi));
            _entries = list.ToArray();
            _layerSplitVelocity = layerSplitVelocity;
        }

        public int Count => _entries.Length;

        public IReadOnlyList<SampleEntry> Entries => _entries;

        /// <summary>Lowest recorded root in the set.</summary>
        public Pitch LowestRoot => _entries[0].Root;

        /// <summary>Highest recorded root in the set.</summary>
        public Pitch HighestRoot => _entries[_entries.Length - 1].Root;

        public SampleSelection Resolve(Pitch target, byte velocity)
        {
            VelocityLayer preferred =
                velocity >= _layerSplitVelocity ? VelocityLayer.Hard : VelocityLayer.Soft;

            int nearestRootMidi = _entries[0].Root.Midi;
            int nearestDistance = Math.Abs(target.Midi - nearestRootMidi);
            for (int i = 1; i < _entries.Length; i++)
            {
                int rootMidi = _entries[i].Root.Midi;
                int distance = Math.Abs(target.Midi - rootMidi);
                if (distance < nearestDistance
                    || (distance == nearestDistance && rootMidi < nearestRootMidi))
                {
                    nearestDistance = distance;
                    nearestRootMidi = rootMidi;
                }
            }

            int preferredClip = -1;
            int otherClip = -1;
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Root.Midi != nearestRootMidi)
                {
                    continue;
                }

                if (_entries[i].Layer == preferred)
                {
                    preferredClip = _entries[i].ClipIndex;
                }
                else
                {
                    otherClip = _entries[i].ClipIndex;
                }
            }

            int clip = preferredClip >= 0 ? preferredClip : otherClip;
            double rate = Math.Pow(2.0, (target.Midi - nearestRootMidi) / 12.0);
            return new SampleSelection(clip, rate);
        }

        /// <summary>
        /// Build a library by parsing Salamander-style file names. Names that do not
        /// parse are skipped; <paramref name="clipIndexOf"/> gives the clip index for a
        /// name that does.
        /// </summary>
        public static SampleLibrary FromFileNames(
            IEnumerable<string> fileNames,
            Func<string, int> clipIndexOf,
            byte layerSplitVelocity = DefaultLayerSplitVelocity)
        {
            if (fileNames == null)
            {
                throw new ArgumentNullException(nameof(fileNames));
            }

            if (clipIndexOf == null)
            {
                throw new ArgumentNullException(nameof(clipIndexOf));
            }

            var entries = new List<SampleEntry>();
            foreach (string fileName in fileNames)
            {
                if (SampleFileName.TryParse(fileName, out Pitch root, out VelocityLayer layer))
                {
                    entries.Add(new SampleEntry(root, layer, clipIndexOf(fileName)));
                }
            }

            return new SampleLibrary(entries, layerSplitVelocity);
        }
    }
}
