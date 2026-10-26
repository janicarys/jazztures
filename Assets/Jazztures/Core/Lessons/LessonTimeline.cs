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
            TimelineMarker[] markers,
            OnsetPattern scoredPulse,
            double minimumDurationBeats,
            TurnWindow turnWindow)
        {
            Tempo = tempo;
            Swing = swing;
            BeatsPerBar = beatsPerBar;
            _chords = chords;
            _notes = notes;
            _markers = markers;
            ScoredPulse = scoredPulse;
            Turns = turnWindow;

            double last = Math.Max(0.0, minimumDurationBeats);
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

        /// <summary>
        /// Position of the last event, in beats from the origin — or the minimum duration the
        /// builder was given, if that is longer (a drill's chord has to ring to the end of
        /// the run, not just to its last event).
        /// </summary>
        public double DurationBeats { get; }

        /// <summary>
        /// Onsets the learner is expected to play that are not melody notes — e.g. L2's steady
        /// pulse on the left hand, which has chords but no notes. Spans beat 0 up to
        /// <see cref="DurationBeats"/>. <see cref="OnsetPattern.None"/> for an ordinary lesson.
        /// </summary>
        public OnsetPattern ScoredPulse { get; }

        /// <summary>
        /// Whose turn each stretch of the lesson is (ADR-0077). <see cref="TurnWindow.None"/> for every lesson
        /// that is the system's from start to end; a call-and-response lesson hands part of every round to the learner.
        /// </summary>
        public TurnWindow Turns { get; }

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

        /// <summary>
        /// The beats on which the ghost's left hand should strike the pad (ADR-0052), swing applied,
        /// ascending. If the lesson scores a pulse (L2), those beats are the strikes — the dip
        /// <i>is</i> the rhythm being asked for. Otherwise each chord is struck
        /// <paramref name="afterPoseBeats"/> after its pose appears, which is time for the ghost to
        /// form the shape first.
        /// </summary>
        public double[] StrikeBeats(double afterPoseBeats) => StrikeBeats(afterPoseBeats, useScoredPulse: true);

        /// <summary>
        /// As <see cref="StrikeBeats(double)"/>, but <paramref name="useScoredPulse"/> false always strikes the
        /// chords, ignoring any scored pulse. Watch and Listen needs that: the system sounds the chords, not
        /// the pulse, and the ghost's hand has to land exactly when a chord does (ADR-0052).
        /// </summary>
        public double[] StrikeBeats(double afterPoseBeats, bool useScoredPulse)
        {
            double[] beats;
            if (useScoredPulse && !ScoredPulse.IsNone)
            {
                beats = ScoredPulse.BeatsBetween(0.0, DurationBeats, BeatsPerBar);
            }
            else
            {
                beats = new double[_chords.Length];
                for (int i = 0; i < _chords.Length; i++)
                {
                    beats[i] = _chords[i].Beat.Position + afterPoseBeats;
                }
            }

            for (int i = 0; i < beats.Length; i++)
            {
                beats[i] = SwingQuantizer.Swing(beats[i], Swing);
            }

            return beats;
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
        /// The expected onsets in seconds from the grid origin, with swing applied — ready
        /// to hand to <see cref="Evaluation.OnsetScorer.Evaluate"/>: the melody notes plus any
        /// <see cref="ScoredPulse"/> beats, in time order.
        /// </summary>
        public double[] ExpectedOnsetSeconds()
        {
            double[] pulse = ScoredPulse.IsNone
                ? Array.Empty<double>()
                : ScoredPulse.BeatsBetween(0.0, DurationBeats, BeatsPerBar);

            var seconds = new double[_notes.Length + pulse.Length];
            for (int i = 0; i < _notes.Length; i++)
            {
                seconds[i] = SwingQuantizer.SwingToSeconds(_notes[i].Beat.Position, Swing, Tempo);
            }

            for (int i = 0; i < pulse.Length; i++)
            {
                seconds[_notes.Length + i] = SwingQuantizer.SwingToSeconds(pulse[i], Swing, Tempo);
            }

            if (pulse.Length > 0)
            {
                Array.Sort(seconds);
            }

            return seconds;
        }
    }
}
