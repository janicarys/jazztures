using System;
using System.Collections;
using System.Collections.Generic;

namespace Jazztures.Core.Music
{
    /// <summary>
    /// The ten right-hand touch targets for one chord (CLAUDE.md §3.1): five chord tones
    /// (root, 3rd, 5th, 7th, 9th) across two octaves.
    ///
    /// <para>
    /// Slot layout is fixed and degree-major: slots 0..4 are the lower octave
    /// (Root, Third, Fifth, Seventh, Ninth), slots 5..9 the upper octave in the same
    /// order. A given slot always carries the same <see cref="ScaleDegree"/> — so slot 2
    /// ("the fifth", target #3 counting from one) never moves. On a chord change the
    /// melody engine builds a fresh <see cref="ChordToneSet"/>; nothing is cached across
    /// changes (§3.3). Spatial placement of the slots is the presentation layer's job;
    /// the only contract here is that <see cref="ChordTarget.Index"/> is a stable
    /// identity, never sorted by pitch.
    /// </para>
    ///
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct ChordToneSet : IReadOnlyList<ChordTarget>, IEquatable<ChordToneSet>
    {
        /// <summary>Chord tones exposed to the right hand, in slot order within an octave.</summary>
        public static readonly IReadOnlyList<ScaleDegree> Degrees = new[]
        {
            ScaleDegree.Root,
            ScaleDegree.Third,
            ScaleDegree.Fifth,
            ScaleDegree.Seventh,
            ScaleDegree.Ninth,
        };

        public const int DegreesPerOctave = 5;

        public const int OctaveCount = 2;

        public const int TargetCount = DegreesPerOctave * OctaveCount;

        /// <summary>
        /// Lowest MIDI note any target may take. `[TUNABLE]` (CLAUDE.md §3.1: "lowest
        /// target ≥ MIDI 72") — mirror changes in <c>Docs/CALIBRATION.md</c> and drive
        /// from Config at M8 via <see cref="For(Chord,int)"/>.
        /// </summary>
        public const int DefaultLowestTargetFloorMidi = 72;

        private readonly ChordTarget[] _targets;

        private ChordToneSet(Chord chord, ChordTarget[] targets)
        {
            Chord = chord;
            _targets = targets;
        }

        public Chord Chord { get; }

        /// <summary>Build the target set for <paramref name="chord"/> using the §3.1 default floor.</summary>
        public static ChordToneSet For(Chord chord) =>
            For(chord, DefaultLowestTargetFloorMidi);

        /// <summary>
        /// Build the target set for <paramref name="chord"/> with the lower octave's root
        /// at the lowest pitch of the root class at or above
        /// <paramref name="lowestTargetFloorMidi"/>. The root has the smallest interval
        /// from itself of any tone, so anchoring it at the floor keeps every target at
        /// or above the floor.
        /// </summary>
        public static ChordToneSet For(Chord chord, int lowestTargetFloorMidi)
        {
            Pitch lowerRoot = Pitch.LowestAtOrAbove(chord.Root, lowestTargetFloorMidi);

            var targets = new ChordTarget[TargetCount];
            for (int octave = 0; octave < OctaveCount; octave++)
            {
                for (int d = 0; d < DegreesPerOctave; d++)
                {
                    ScaleDegree degree = Degrees[d];
                    int index = octave * DegreesPerOctave + d;
                    int semitones = chord.SemitoneAbove(degree) + octave * 12;
                    targets[index] = new ChordTarget(
                        index, degree, octave, lowerRoot.Transpose(semitones));
                }
            }

            return new ChordToneSet(chord, targets);
        }

        public int Count => TargetCount;

        public ChordTarget this[int index] => Targets[index];

        /// <summary>The target for a given degree and octave (0 = lower, 1 = upper).</summary>
        public ChordTarget ForDegree(ScaleDegree degree, int octaveOffset)
        {
            if (octaveOffset is < 0 or >= OctaveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(octaveOffset), octaveOffset, null);
            }

            for (int d = 0; d < DegreesPerOctave; d++)
            {
                if (Degrees[d] == degree)
                {
                    return Targets[octaveOffset * DegreesPerOctave + d];
                }
            }

            throw new ArgumentOutOfRangeException(
                nameof(degree), degree, "Not a right-hand chord tone.");
        }

        /// <summary>The scale degree carried by slot <paramref name="index"/>, independent of chord.</summary>
        public static ScaleDegree DegreeAt(int index)
        {
            if (index is < 0 or >= TargetCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, null);
            }

            return Degrees[index % DegreesPerOctave];
        }

        public IEnumerator<ChordTarget> GetEnumerator()
        {
            foreach (ChordTarget target in Targets)
            {
                yield return target;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(ChordToneSet other)
        {
            if (Chord != other.Chord)
            {
                return false;
            }

            for (int i = 0; i < TargetCount; i++)
            {
                if (Targets[i] != other.Targets[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is ChordToneSet other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Chord.GetHashCode();
                foreach (ChordTarget target in Targets)
                {
                    hash = (hash * 397) ^ target.GetHashCode();
                }

                return hash;
            }
        }

        public override string ToString() => $"{Chord} targets [{string.Join(", ", Targets)}]";

        /// <summary>Guards against a <c>default(ChordToneSet)</c> with a null backing array.</summary>
        private ChordTarget[] Targets => _targets
            ?? throw new InvalidOperationException(
                "Uninitialised ChordToneSet — use ChordToneSet.For(chord).");
    }
}
