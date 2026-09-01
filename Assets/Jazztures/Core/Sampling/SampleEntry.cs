using System;
using Jazztures.Core.Music;

namespace Jazztures.Core.Sampling
{
    /// <summary>
    /// One recorded clip in a <see cref="SampleLibrary"/>: the pitch it was recorded at,
    /// its dynamic layer, and its index in the clip list. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct SampleEntry : IEquatable<SampleEntry>
    {
        public SampleEntry(Pitch root, VelocityLayer layer, int clipIndex)
        {
            if (clipIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(clipIndex), clipIndex, null);
            }

            Root = root;
            Layer = layer;
            ClipIndex = clipIndex;
        }

        public Pitch Root { get; }

        public VelocityLayer Layer { get; }

        public int ClipIndex { get; }

        public bool Equals(SampleEntry other) =>
            Root == other.Root && Layer == other.Layer && ClipIndex == other.ClipIndex;

        public override bool Equals(object? obj) => obj is SampleEntry other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Root.GetHashCode();
                hash = (hash * 397) ^ (int)Layer;
                hash = (hash * 397) ^ ClipIndex;
                return hash;
            }
        }

        public override string ToString() => $"{Root} {Layer} (clip {ClipIndex})";

        public static bool operator ==(SampleEntry left, SampleEntry right) => left.Equals(right);

        public static bool operator !=(SampleEntry left, SampleEntry right) => !left.Equals(right);
    }
}
