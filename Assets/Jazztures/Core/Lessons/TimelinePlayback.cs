using System;
using Jazztures.Core.Harmony;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Plays a <see cref="LessonTimeline"/> as the system demonstration (§3.8 —
    /// Watch and Listen, and the demo half of any ghost-hand mode). It schedules the
    /// left-hand voicings and the right-hand melody as <see cref="NoteEvent"/>s on the
    /// <see cref="MidiChannel.Accompaniment"/> channel, so a <c>ModeGatedNoteSink</c>
    /// always sounds them regardless of the learner-audio gate.
    ///
    /// <para>
    /// Pure and headless. The schedule is built once at construction; <see cref="Tick"/>
    /// is allocation-free. Melody onsets carry the timeline's swing; chords do not (§3.6).
    /// </para>
    /// </summary>
    public sealed class TimelinePlayback
    {
        /// <summary>Sounding length of a demonstrated melody note. `[TUNABLE]`.</summary>
        public const double MelodyNoteSeconds = 0.4;

        /// <summary>How long the final chord rings past the last event. `[TUNABLE]`.</summary>
        public const double ChordTailSeconds = 0.6;

        private readonly IMusicalClock _clock;
        private readonly INoteSink _sink;
        private readonly ScheduledEvent[] _events;
        private readonly System.Collections.Generic.List<NoteEvent> _sounding =
            new System.Collections.Generic.List<NoteEvent>();

        private double _startDsp;
        private int _cursor;
        private bool _playing;

        public TimelinePlayback(LessonTimeline timeline, IMusicalClock clock, INoteSink sink, byte chordVelocity = 80)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _events = BuildSchedule(timeline, chordVelocity);
            TotalSeconds = _events.Length > 0 ? _events[_events.Length - 1].Offset : 0.0;
        }

        /// <summary>Raised once when the last scheduled event has been emitted by <see cref="Tick"/>.</summary>
        public event Action? Ended;

        public bool IsPlaying => _playing;

        public bool HasEnded { get; private set; }

        /// <summary>Offset of the last event from the start, in seconds.</summary>
        public double TotalSeconds { get; }

        /// <summary>Seconds since <see cref="Start"/>, or 0 when not playing.</summary>
        public double PositionSeconds => _playing ? _clock.Now - _startDsp : 0.0;

        /// <summary>Begin (or restart) playback, anchored at the current clock time.</summary>
        public void Start()
        {
            _startDsp = _clock.Now;
            _cursor = 0;
            _sounding.Clear();
            _playing = _events.Length > 0;
            HasEnded = _events.Length == 0;
        }

        /// <summary>
        /// Stop early. Every note the schedule had turned on but not yet off is released
        /// immediately, so nothing is left ringing.
        /// </summary>
        public void Stop()
        {
            if (!_playing)
            {
                return;
            }

            double now = _clock.Now;
            for (int i = 0; i < _sounding.Count; i++)
            {
                NoteEvent on = _sounding[i];
                _sink.Send(NoteEvent.Off(on.Pitch, now, on.Channel, on.Source));
            }

            _sounding.Clear();
            _playing = false;
            HasEnded = true;
        }

        /// <summary>Emit every event whose time has arrived. Call once per frame.</summary>
        public void Tick()
        {
            if (!_playing)
            {
                return;
            }

            double now = _clock.Now;
            while (_cursor < _events.Length && _startDsp + _events[_cursor].Offset <= now)
            {
                ScheduledEvent scheduled = _events[_cursor];
                double dspTime = _startDsp + scheduled.Offset;
                NoteEvent emitted = Retime(scheduled.Event, dspTime);
                _sink.Send(emitted);
                TrackSounding(emitted);
                _cursor++;
            }

            if (_cursor >= _events.Length)
            {
                _playing = false;
                HasEnded = true;
                Ended?.Invoke();
            }
        }

        private void TrackSounding(in NoteEvent e)
        {
            if (e.Kind == NoteEventKind.On)
            {
                _sounding.Add(e);
                return;
            }

            for (int i = 0; i < _sounding.Count; i++)
            {
                if (_sounding[i].Pitch == e.Pitch && _sounding[i].Channel == e.Channel)
                {
                    _sounding.RemoveAt(i);
                    return;
                }
            }
        }

        private static NoteEvent Retime(in NoteEvent e, double dspTime) =>
            e.Kind == NoteEventKind.On
                ? NoteEvent.On(e.Pitch, e.Velocity, dspTime, e.Channel, e.Source)
                : NoteEvent.Off(e.Pitch, dspTime, e.Channel, e.Source);

        private static ScheduledEvent[] BuildSchedule(LessonTimeline timeline, byte chordVelocity)
        {
            var events = new System.Collections.Generic.List<ScheduledEvent>();
            Tempo tempo = timeline.Tempo;

            // Left hand — close voicings, no swing, held until the next chord (last one + tail).
            for (int i = 0; i < timeline.Chords.Count; i++)
            {
                TimelineChord chord = timeline.Chords[i];
                double onOffset = tempo.BeatsToSeconds(chord.Beat.Position);
                double offOffset = i + 1 < timeline.Chords.Count
                    ? tempo.BeatsToSeconds(timeline.Chords[i + 1].Beat.Position)
                    : tempo.BeatsToSeconds(timeline.DurationBeats) + ChordTailSeconds;

                ChordVoicing voicing = Voicing.Close(Progression.ChordFor(chord.Function));
                foreach (Pitch pitch in voicing)
                {
                    events.Add(new ScheduledEvent(onOffset,
                        NoteEvent.On(pitch, chordVelocity, 0.0, MidiChannel.Accompaniment, Handedness.Left)));
                    events.Add(new ScheduledEvent(offOffset,
                        NoteEvent.Off(pitch, 0.0, MidiChannel.Accompaniment, Handedness.Left)));
                }
            }

            // Right hand — melody targets resolved against the active chord, swing applied.
            for (int i = 0; i < timeline.Notes.Count; i++)
            {
                TimelineNote note = timeline.Notes[i];
                ChordFunction? function = timeline.ChordFunctionAt(note.Beat.Position);
                if (!function.HasValue)
                {
                    continue; // builder forbids this, but never dereference a null chord
                }

                Pitch pitch = ChordToneSet.For(Progression.ChordFor(function.Value))[note.TargetIndex].Pitch;
                double onOffset = SwingQuantizer.SwingToSeconds(note.Beat.Position, timeline.Swing, tempo);

                events.Add(new ScheduledEvent(onOffset,
                    NoteEvent.On(pitch, note.Velocity, 0.0, MidiChannel.Accompaniment, Handedness.Right)));
                events.Add(new ScheduledEvent(onOffset + MelodyNoteSeconds,
                    NoteEvent.Off(pitch, 0.0, MidiChannel.Accompaniment, Handedness.Right)));
            }

            // Stable order: by time, and a note-off before a note-on at the same instant.
            events.Sort((a, b) =>
            {
                int byTime = a.Offset.CompareTo(b.Offset);
                if (byTime != 0)
                {
                    return byTime;
                }

                int aRank = a.Event.Kind == NoteEventKind.Off ? 0 : 1;
                int bRank = b.Event.Kind == NoteEventKind.Off ? 0 : 1;
                return aRank.CompareTo(bRank);
            });

            return events.ToArray();
        }

        private readonly struct ScheduledEvent
        {
            public ScheduledEvent(double offset, NoteEvent @event)
            {
                Offset = offset;
                Event = @event;
            }

            public double Offset { get; }

            public NoteEvent Event { get; }
        }
    }
}
