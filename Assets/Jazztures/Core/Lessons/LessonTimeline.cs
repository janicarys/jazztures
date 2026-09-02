using System;
using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// The baked musical content of a lesson phrase (ADR-0011): a beat grid, the left-hand
    /// chord-function changes, the right-hand melody targets, and named markers. Produced
    /// at edit time by the SMF importer and consumed at runtime by the ghost hands and
    /// the onset scorer. Never parses notation — this is the already-baked form.
    ///
    /// <para>Immutable. Build one with <see cref="LessonTimelineBuilder"/>.</para>
    /// </summary>
    public sealed class LessonTimeline
    {
        private readonly TimelineChord[] _chords;
        private readonly TimelineNote[] _notes;
        private readonly TimelineMarker[] _markers;

        internal LessonTimeline(
            Tempo tempo,
            SwingRatio swing,
            int beatsPerBar,
            TimelineChord[] chords,
            TimelineNote[] notes,
            TimelineMarker[] markers)
        {
            Tempo = tempo;
            Swing = swing;
            BeatsPerBar = beatsPerBar;
            _chords = chords;
            _notes = notes;
            _markers = markers;

            double last = 0.0;
            for (int i = 0; i < notes.Length; i++)
            {
                last = Math.Max(last, notes[i].Beat.Position);
            }

            for (int i = 0; i < chords.Length; i++)
            {
                last = Math.Max(last, chords[i].Beat.Position);
            }

            DurationBeats = last;
        }

        public Tempo Tempo { get; }

        public SwingRatio Swing { get; }

        public int BeatsPerBar { get; }

        /// <summary>Position of the last event, in beats from the origin.</summary>
        public double DurationBeats { get; }

        public IReadOnlyList<TimelineChord> Chords => _chords;

        public IReadOnlyList<TimelineNote> Notes => _notes;

        public IReadOnlyList<TimelineMarker> Markers => _markers;

        /// <summary>
        /// The chord function in effect at <paramref name="beat"/> — the last chord change
        /// at or before it — or null if the phrase has not reached its first chord yet.
        /// </summary>
        public ChordFunction? ChordFunctionAt(double beat)
        {
            ChordFunction? active = null;
            for (int i = 0; i < _chords.Length; i++)
            {
                if (_chords[i].Beat.Position <= beat)
                {
                    active = _chords[i].Function;
                }
                else
                {
                    break;
                }
            }

            return active;
        }

        /// <summary>The beat a marker sits on, or null if no marker has that name.</summary>
        public double? MarkerBeat(string name)
        {
            for (int i = 0; i < _markers.Length; i++)
            {
                if (_markers[i].Name == name)
                {
                    return _markers[i].Beat.Position;
                }
            }

            return null;
        }

        /// <summary>
        /// The melody onsets in seconds from the grid origin, with swing applied — ready
        /// to hand to <see cref="Evaluation.OnsetScorer.Evaluate"/> as the expected onsets.
        /// </summary>
        public double[] ExpectedOnsetSeconds()
        {
            var seconds = new double[_notes.Length];
            for (int i = 0; i < _notes.Length; i++)
            {
                seconds[i] = SwingQuantizer.SwingToSeconds(_notes[i].Beat.Position, Swing, Tempo);
            }

            return seconds;
        }
    }
}
