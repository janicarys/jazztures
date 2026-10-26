using System;
using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Music;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Assembles a <see cref="LessonTimeline"/>. Both the edit-time SMF importer and the
    /// edit-mode tests build timelines through this, so the invariants (sorted events,
    /// valid target indices, a chord active before the first note) live in one place.
    /// </summary>
    public sealed class LessonTimelineBuilder
    {
        private readonly List<TimelineChord> _chords = new List<TimelineChord>();
        private readonly List<TimelineNote> _notes = new List<TimelineNote>();
        private readonly List<TimelineMarker> _markers = new List<TimelineMarker>();

        private Tempo _tempo = Tempo.Default;
        private SwingRatio _swing = SwingRatio.Straight;
        private int _beatsPerBar = Metronome.DefaultBeatsPerBar;
        private OnsetPattern _scoredPulse = OnsetPattern.None;
        private double _minimumDurationBeats;
        private TurnWindow _turns = TurnWindow.None;

        public LessonTimelineBuilder WithTempo(Tempo tempo)
        {
            _tempo = tempo;
            return this;
        }

        public LessonTimelineBuilder WithSwing(SwingRatio swing)
        {
            _swing = swing;
            return this;
        }

        public LessonTimelineBuilder WithBeatsPerBar(int beatsPerBar)
        {
            if (beatsPerBar <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beatsPerBar), beatsPerBar, "Must be positive.");
            }

            _beatsPerBar = beatsPerBar;
            return this;
        }

        /// <summary>
        /// Score the learner against <paramref name="pulse"/> as well as the melody notes —
        /// for a lesson that has expected onsets but no notes to carry them (L2).
        /// </summary>
        public LessonTimelineBuilder WithScoredPulse(OnsetPattern pulse)
        {
            _scoredPulse = pulse;
            return this;
        }

        /// <summary>
        /// Make the timeline at least this long even if its last event comes earlier — so a
        /// chord with no later change rings to the end of a drill instead of dying after the
        /// default tail.
        /// </summary>
        public LessonTimelineBuilder WithMinimumDuration(double beats)
        {
            if (double.IsNaN(beats) || double.IsInfinity(beats) || beats < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(beats), beats, "Must be finite and not negative.");
            }

            _minimumDurationBeats = beats;
            return this;
        }

        /// <summary>
        /// Split every round into the system's turn and the learner's (ADR-0077). Chords and notes authored in the
        /// learner's part are never played by the system.
        /// </summary>
        public LessonTimelineBuilder WithTurns(TurnWindow turns)
        {
            _turns = turns;
            return this;
        }

        public LessonTimelineBuilder Chord(double beat, ChordFunction function)
        {
            _chords.Add(new TimelineChord(new Beat(beat), function));
            return this;
        }

        public LessonTimelineBuilder Note(double beat, int targetIndex, byte velocity = 90)
        {
            if (targetIndex < 0 || targetIndex >= ChordToneSet.TargetCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetIndex), targetIndex,
                    $"Must be in 0..{ChordToneSet.TargetCount - 1}.");
            }

            _notes.Add(new TimelineNote(new Beat(beat), targetIndex, velocity));
            return this;
        }

        public LessonTimelineBuilder Marker(double beat, string name)
        {
            _markers.Add(new TimelineMarker(new Beat(beat), name));
            return this;
        }

        public LessonTimeline Build()
        {
            TimelineChord[] chords = _chords.ToArray();
            TimelineNote[] notes = _notes.ToArray();
            TimelineMarker[] markers = _markers.ToArray();

            Array.Sort(chords, (a, b) => a.Beat.CompareTo(b.Beat));
            Array.Sort(notes, (a, b) => a.Beat.CompareTo(b.Beat));
            Array.Sort(markers, (a, b) => a.Beat.CompareTo(b.Beat));

            RequireNoDuplicateChordBeats(chords);
            RequireDistinctMarkerNames(markers);
            RequireAChordBeforeTheFirstNote(chords, notes);

            return new LessonTimeline(
                _tempo, _swing, _beatsPerBar, chords, notes, markers, _scoredPulse, _minimumDurationBeats, _turns);
        }

        private static void RequireNoDuplicateChordBeats(TimelineChord[] chords)
        {
            for (int i = 1; i < chords.Length; i++)
            {
                if (chords[i].Beat == chords[i - 1].Beat)
                {
                    throw new InvalidOperationException(
                        $"Two chord changes on {chords[i].Beat} — a beat holds one function.");
                }
            }
        }

        private static void RequireDistinctMarkerNames(TimelineMarker[] markers)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (TimelineMarker marker in markers)
            {
                if (!seen.Add(marker.Name))
                {
                    throw new InvalidOperationException($"Duplicate marker name \"{marker.Name}\".");
                }
            }
        }

        private static void RequireAChordBeforeTheFirstNote(TimelineChord[] chords, TimelineNote[] notes)
        {
            if (notes.Length == 0)
            {
                return;
            }

            if (chords.Length == 0 || chords[0].Beat > notes[0].Beat)
            {
                throw new InvalidOperationException(
                    "The first melody note has no chord active — a target index is meaningless without one.");
            }
        }
    }
}
