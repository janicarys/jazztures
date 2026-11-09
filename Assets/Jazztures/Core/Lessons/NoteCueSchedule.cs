#nullable enable
using System;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// When each of a timeline's notes is actually played (swing applied) and at which fixed pitch, in time order: what the
    /// swing-slice view needs to draw a circle that reaches the trigger line on its note's beat (ADR-0081). Chord-tone notes
    /// (no fixed pitch) carry <see cref="TimelineNote.NoPitch"/> and are not drawn. Immutable; built once per phase.
    /// </summary>
    public sealed class NoteCueSchedule
    {
        public static readonly NoteCueSchedule None = new NoteCueSchedule(Array.Empty<double>(), Array.Empty<int>());

        private readonly double[] _onsets;
        private readonly int[] _pitches;

        private NoteCueSchedule(double[] onsets, int[] pitches)
        {
            _onsets = onsets;
            _pitches = pitches;
        }

        public int Count => _onsets.Length;

        /// <summary>The beat note <paramref name="index"/> sounds on, swing included.</summary>
        public double OnsetBeat(int index) => _onsets[index];

        /// <summary>The note's MIDI pitch, or <see cref="TimelineNote.NoPitch"/>.</summary>
        public int MidiPitch(int index) => _pitches[index];

        public static NoteCueSchedule FromTimeline(LessonTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            int count = timeline.Notes.Count;
            var onsets = new double[count];
            var pitches = new int[count];
            for (int i = 0; i < count; i++)
            {
                onsets[i] = timeline.Notes[i].PlayedBeat(timeline.Swing);
                pitches[i] = timeline.Notes[i].MidiPitch;
            }

            Array.Sort(onsets, pitches); // time order, whatever order they were authored in
            return new NoteCueSchedule(onsets, pitches);
        }

        /// <summary>The first note at or after <paramref name="beat"/>, or <see cref="Count"/> if none.</summary>
        public int FirstAtOrAfter(double beat)
        {
            int lo = 0;
            int hi = _onsets.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (_onsets[mid] < beat)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo;
        }
    }
}
