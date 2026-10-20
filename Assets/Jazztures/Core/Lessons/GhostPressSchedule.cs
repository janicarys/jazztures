using System;
using System.Collections.Generic;
using Jazztures.Core.Harmony;
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
        private readonly double[] _onsets;          // every press's swung onset, ascending
        private readonly int[] _fingers;            // the finger that presses at _onsets[i]
        private readonly ChordFunction?[] _chords;  // the chord _onsets[i]'s note belongs to
        private readonly double _endBeat;

        private GhostPressSchedule(
            GhostStrikeSchedule[] byFinger, double[] onsets, int[] fingers, ChordFunction?[] chords)
        {
            _byFinger = byFinger;
            _onsets = onsets;
            _fingers = fingers;
            _chords = chords;
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

            int count = timeline.Notes.Count;
            var allOnsets = new double[count];
            var allFingers = new int[count];
            var allChords = new ChordFunction?[count];
            for (int i = 0; i < count; i++)
            {
                TimelineNote note = timeline.Notes[i];
                int finger = FingerFor(note.TargetIndex);
                double onset = SwingQuantizer.Swing(note.Beat.Position, timeline.Swing);
                onsets[finger].Add(onset);
                allOnsets[i] = onset;
                allFingers[i] = finger;
                allChords[i] = timeline.ChordFunctionAt(onset);
            }

            // Time order, whatever order the notes were authored in: sort the onsets and carry the index.
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            Array.Sort((double[])allOnsets.Clone(), order);

            var sortedOnsets = new double[count];
            var sortedFingers = new int[count];
            var sortedChords = new ChordFunction?[count];
            for (int i = 0; i < count; i++)
            {
                sortedOnsets[i] = allOnsets[order[i]];
                sortedFingers[i] = allFingers[order[i]];
                sortedChords[i] = allChords[order[i]];
            }

            var byFinger = new GhostStrikeSchedule[onsets.Length];
            for (int f = 0; f < byFinger.Length; f++)
            {
                byFinger[f] = new GhostStrikeSchedule(onsets[f], approachBeats, returnBeats);
            }

            return new GhostPressSchedule(byFinger, sortedOnsets, sortedFingers, sortedChords);
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

        /// <summary>How many presses there are - one per melody note. Zero for a lesson with no melody.</summary>
        public int Count => _onsets.Length;

        /// <summary>The (swung) beat press <paramref name="index"/> lands, in time order.</summary>
        public double OnsetAt(int index) => _onsets[index];

        /// <summary>The finger (0 thumb .. 4 pinky) that makes press <paramref name="index"/>.</summary>
        public int FingerAt(int index) => _fingers[index];

        /// <summary>
        /// The chord press <paramref name="index"/>'s note belongs to - what its letter names - or null before
        /// the lesson's first chord.
        /// </summary>
        public ChordFunction? ChordAt(int index) => _chords[index];

        /// <summary>
        /// The index of the first press that lands at or after <paramref name="beat"/>, or <see cref="Count"/>
        /// when none does. With <see cref="OnsetAt"/> this walks the presses in a window of beats without
        /// allocating - the note lanes (ADR-0060) draw every note from a little ago to a few beats ahead.
        /// </summary>
        public int FirstAtOrAfter(double beat)
        {
            int i = 0;
            while (i < _onsets.Length && _onsets[i] < beat)
            {
                i++;
            }

            return i;
        }

        /// <summary>
        /// The press that is coming up next: the earliest one whose onset is still ahead of
        /// <paramref name="beat"/> (a press whose finger is already down has landed and is not "next") and
        /// no more than <paramref name="lookAheadBeats"/> away. Lets the view mark the finger the learner
        /// should play before it starts to move: the finger itself only begins its approach a fraction of a
        /// beat ahead, which is too late to read and react to. Only the nearest press is reported, however
        /// many fall inside the window. False (and <see cref="NoFinger"/>) when none does.
        /// </summary>
        public bool TryGetNext(double beat, double lookAheadBeats, out int finger, out double onsetBeat)
        {
            if (lookAheadBeats > 0.0)
            {
                for (int i = 0; i < _onsets.Length; i++)
                {
                    double onset = _onsets[i];
                    if (onset <= beat)
                    {
                        continue; // landed already
                    }

                    if (onset - beat <= lookAheadBeats)
                    {
                        finger = _fingers[i];
                        onsetBeat = onset;
                        return true;
                    }

                    break; // sorted: nothing nearer follows
                }
            }

            finger = NoFinger;
            onsetBeat = 0.0;
            return false;
        }

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
