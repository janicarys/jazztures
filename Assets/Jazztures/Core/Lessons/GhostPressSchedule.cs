using System;
using System.Collections.Generic;
using Jazztures.Core.Music;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Which of the ghost right hand's fingers is pressing, and how far down it is (ADR-0055). The right
    /// hand plays one finger per chord tone (ADR-0050), so a melody note is a finger press: this turns the
    /// lesson's notes into a 0..1 "how far is the finger down" for the finger that is moving, which the view
    /// maps to a bend. Finger <c>f</c> (0 = thumb .. 4 = pinky) plays slot <c>f</c> of an octave —
    /// <see cref="ChordToneSet.DegreesPerOctave"/> of them — so a note's finger is its target index modulo that.
    ///
    /// <para>
    /// The shape is <see cref="GhostStrikeSchedule"/>'s, one schedule per finger: the finger accelerates down
    /// and is fully down exactly on the note's (swung) onset, then lifts on a smooth ease. The fingertip meets
    /// the key as the note sounds, not a moment after. Where two fingers' presses overlap the deeper one is
    /// reported; the view is responsible for letting the other one up gently.
    /// </para>
    ///
    /// <para>Immutable. Allocation-free after construction.</para>
    /// </summary>
    public sealed class GhostPressSchedule
    {
        /// <summary>The "no finger" answer of <see cref="ProgressAt"/>.</summary>
        public const int NoFinger = -1;

        private readonly GhostStrikeSchedule[] _byFinger;
        private readonly double _endBeat;

        private GhostPressSchedule(GhostStrikeSchedule[] byFinger)
        {
            _byFinger = byFinger;
            foreach (GhostStrikeSchedule schedule in byFinger)
            {
                if (schedule.EndBeat > _endBeat)
                {
                    _endBeat = schedule.EndBeat;
                }
            }
        }

        /// <summary>No presses - the ghost's fingers stay where they are.</summary>
        public static GhostPressSchedule None { get; } =
            FromTimeline(new LessonTimelineBuilder().Build(), 0.25, 0.35);

        /// <summary>
        /// One press per melody note in <paramref name="timeline"/>, landing on its swung onset. The
        /// finger takes <paramref name="approachBeats"/> to go down and <paramref name="returnBeats"/> to
        /// come back up.
        /// </summary>
        public static GhostPressSchedule FromTimeline(LessonTimeline timeline, double approachBeats, double returnBeats)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            var onsets = new List<double>[ChordToneSet.DegreesPerOctave];
            for (int f = 0; f < onsets.Length; f++)
            {
                onsets[f] = new List<double>();
            }

            for (int i = 0; i < timeline.Notes.Count; i++)
            {
                TimelineNote note = timeline.Notes[i];
                onsets[FingerFor(note.TargetIndex)].Add(SwingQuantizer.Swing(note.Beat.Position, timeline.Swing));
            }

            var byFinger = new GhostStrikeSchedule[onsets.Length];
            for (int f = 0; f < byFinger.Length; f++)
            {
                byFinger[f] = new GhostStrikeSchedule(onsets[f], approachBeats, returnBeats);
            }

            return new GhostPressSchedule(byFinger);
        }

        /// <summary>
        /// The finger that plays <paramref name="targetIndex"/>: 0 thumb .. 4 pinky. The upper octave
        /// (targets 5..9) shares the lower octave's fingers - the same scale degree.
        /// TODO(OPEN): ADR-0050 gives the finger input one octave, so a note in the upper octave is shown
        /// on the finger that plays that degree an octave lower; the learner cannot reach the pitch itself.
        /// </summary>
        public static int FingerFor(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= ChordToneSet.TargetCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetIndex), targetIndex, $"Must be in 0..{ChordToneSet.TargetCount - 1}.");
            }

            return targetIndex % ChordToneSet.DegreesPerOctave;
        }

        /// <summary>
        /// The beat by which the last press has finished lifting - the earliest a phrase can end without
        /// cutting one short. 0 when there are none.
        /// </summary>
        public double EndBeat => _endBeat;

        /// <summary>
        /// How far down the most-pressed finger is at <paramref name="beat"/> (0 .. 1, 1 on the note's
        /// onset), and which finger that is; <paramref name="finger"/> is <see cref="NoFinger"/> and the
        /// result 0 when nothing is moving.
        /// </summary>
        public double ProgressAt(double beat, out int finger)
        {
            double deepest = 0.0;
            finger = NoFinger;

            for (int f = 0; f < _byFinger.Length; f++)
            {
                double progress = _byFinger[f].ProgressAt(beat);
                if (progress > deepest)
                {
                    deepest = progress;
                    finger = f;
                }
            }

            return deepest;
        }
    }
}
