using System;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The target beats of one palm-swing drill run (ADR-0078), computed from the tempo, rhythm and drill so
    /// the audio clock — not a frame counter — says when each falls. A task is <see cref="NotesPerTask"/> targets:
    /// one bar of quarters, or two beats of swung eighths; each task restarts the cursor from the drill's start pitch.
    /// </summary>
    public sealed class DrillSchedule
    {
        public const int DefaultNotesPerTask = 4;

        private readonly Tempo _tempo;
        private readonly double _firstBeatTime;
        private readonly double _offBeatPosition;

        public DrillSchedule(
            Tempo tempo, DrillRhythm rhythm, SwingRatio swing, SwingDrill drill, double firstBeatTime,
            int tasks, int notesPerTask = DefaultNotesPerTask)
        {
            if (tasks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tasks), tasks, "Must be >= 1.");
            }

            if (notesPerTask < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(notesPerTask), notesPerTask, "Must be >= 1.");
            }

            _tempo = tempo;
            Rhythm = rhythm;
            Drill = drill ?? throw new ArgumentNullException(nameof(drill));
            _firstBeatTime = firstBeatTime;
            NotesPerTask = notesPerTask;
            Count = tasks * notesPerTask;
            _offBeatPosition = SwingQuantizer.Swing(0.5, swing);
        }

        public DrillRhythm Rhythm { get; }

        public SwingDrill Drill { get; }

        public int NotesPerTask { get; }

        public int Count { get; }

        public double BeatsPerMinute => _tempo.BeatsPerMinute;

        public DrillTarget this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index, null);
                }

                double beat = Rhythm == DrillRhythm.StraightQuarters
                    ? index
                    : index / 2 + (index % 2 == 0 ? 0.0 : _offBeatPosition);
                return new DrillTarget(
                    index,
                    _firstBeatTime + _tempo.BeatsToSeconds(beat),
                    Drill.DirectionAt(index),
                    index / NotesPerTask,
                    index % NotesPerTask == 0);
            }
        }

        /// <summary>The musical-clock time the last target falls.</summary>
        public double EndTime => this[Count - 1].Time;
    }
}
