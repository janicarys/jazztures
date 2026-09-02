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

            return new LessonTimeline(_tempo, _swing, _beatsPerBar, chords, notes, markers);
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
