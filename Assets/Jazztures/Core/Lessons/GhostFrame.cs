using Jazztures.Core.Harmony;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// What the ghost-hand demonstration should show at the current instant (ADR-0012).
    /// <see cref="LessonRunner"/> publishes one of these per frame while a ghost-hand mode
    /// is active; the renderer is a pure subscriber and owns all mesh / translucency /
    /// tint. Immutable value type (ADR-0007).
    ///
    /// <para>
    /// The right hand shows <b>no ghost fingertip</b> (ADR-0012) — only
    /// <see cref="LitTargetIndex"/> tells the view which target sphere to light.
    /// </para>
    ///
    /// <para>
    /// The left hand also strikes the pad to sound the chord (ADR-0025, ADR-0052):
    /// <see cref="StrikeProgress"/> says how far the ghost hand is toward the pad, and
    /// <see cref="StrikeCount"/> how many strikes have landed so far this phrase.
    /// </para>
    ///
    /// <para>
    /// With the finger-bend right hand (ADR-0050) the ghost right hand presses a finger per note
    /// (ADR-0055): <see cref="PressFinger"/> is the finger that is moving and
    /// <see cref="PressProgress"/> how far down it is.
    /// </para>
    /// </summary>
    public readonly struct GhostFrame
    {
        /// <summary>A frame with the ghost hands hidden.</summary>
        public static GhostFrame Hidden => default;

        public GhostFrame(
            double beat,
            ChordFunction? demonstratedPose,
            double poseChangedAtBeat,
            int litTargetIndex,
            double strikeProgress = 0.0,
            int strikeCount = 0,
            int pressFinger = GhostPressSchedule.NoFinger,
            double pressProgress = 0.0,
            ChordFunction? melodyPose = null,
            int nextFinger = GhostPressSchedule.NoFinger,
            ChordFunction? nextMelodyPose = null,
            GhostPressSchedule? pressSchedule = null,
            double melodyBeat = 0.0)
        {
            Beat = beat;
            DemonstratedPose = demonstratedPose;
            PoseChangedAtBeat = poseChangedAtBeat;
            LitTargetIndex = litTargetIndex;
            StrikeProgress = strikeProgress < 0.0 ? 0.0 : strikeProgress > 1.0 ? 1.0 : strikeProgress;
            StrikeCount = strikeCount < 0 ? 0 : strikeCount;
            PressFinger = pressFinger < 0 ? GhostPressSchedule.NoFinger : pressFinger;
            PressProgress = pressFinger < 0 ? 0.0 : pressProgress < 0.0 ? 0.0 : pressProgress > 1.0 ? 1.0 : pressProgress;
            MelodyPose = melodyPose;
            NextFinger = nextFinger < 0 ? GhostPressSchedule.NoFinger : nextFinger;
            NextMelodyPose = nextFinger < 0 ? null : nextMelodyPose;
            PressSchedule = pressSchedule;
            MelodyBeat = melodyBeat;
            Visible = true;
        }

        /// <summary>True when the ghost hands should be drawn at all (from the mode policy).</summary>
        public bool Visible { get; }

        /// <summary>Current phrase position, in beats.</summary>
        public double Beat { get; }

        /// <summary>The left-hand pose the ghost is holding, or null for a relaxed hand.</summary>
        public ChordFunction? DemonstratedPose { get; }

        /// <summary>The beat <see cref="DemonstratedPose"/> began — lets the view time the morph.</summary>
        public double PoseChangedAtBeat { get; }

        /// <summary>Right-hand target sphere to light right now, or -1 for none.</summary>
        public int LitTargetIndex { get; }

        /// <summary>
        /// How far the ghost's left hand is toward the pad, 0 (at rest on the learner's wrist) to
        /// 1 (touching the pad). Always 0 when the lesson shows no strike.
        /// </summary>
        public double StrikeProgress { get; }

        /// <summary>
        /// How many ghost strikes have landed so far this phrase. The view flashes the pad
        /// whenever this goes up, so a skipped frame cannot lose a hit.
        /// </summary>
        public int StrikeCount { get; }

        /// <summary>
        /// The ghost right hand's finger that is pressing a key right now, 0 (thumb) .. 4 (pinky), or
        /// <see cref="GhostPressSchedule.NoFinger"/> when none is moving.
        /// </summary>
        public int PressFinger { get; }

        /// <summary>
        /// How far down <see cref="PressFinger"/> is, 0 (up) to 1 (the key is down - the note's onset).
        /// Always 0 when <see cref="PressFinger"/> is none.
        /// </summary>
        public double PressProgress { get; }

        /// <summary>
        /// The chord function the demonstrated <b>notes</b> belong to right now, or null before the first chord
        /// (ADR-0058) - what the labels on the ghost right hand's fingers name. It is
        /// <see cref="DemonstratedPose"/> except where the system's playback trails the ghost (Watch and Listen on
        /// a lesson that strikes chords): the ghost has moved on to the next chord's pose while a note that
        /// belongs to the last chord is still to be shown, and the letters must follow the note.
        /// </summary>
        public ChordFunction? MelodyPose { get; }

        /// <summary>
        /// The finger the ghost will press next, 0 (thumb) .. 4 (pinky), or <see cref="GhostPressSchedule.NoFinger"/>
        /// when no press is close enough to announce - the learner's look-ahead: the finger itself only starts to
        /// move a fraction of a beat before its note. While that press is under way this is still its finger.
        /// </summary>
        public int NextFinger { get; }

        /// <summary>
        /// The chord function the <see cref="NextFinger"/>'s note belongs to - what its letter should name. It
        /// differs from <see cref="MelodyPose"/> in the beat before a chord change, when the next note is already
        /// the new chord's but the current notes are the old one's. Null when there is no next finger.
        /// </summary>
        public ChordFunction? NextMelodyPose { get; }

        /// <summary>
        /// Every finger press of the lesson, in time order (ADR-0060): what the note lanes draw coming in. Null when
        /// the frame carries none. Immutable, so sharing the reference costs the frame nothing.
        /// </summary>
        public GhostPressSchedule? PressSchedule { get; }

        /// <summary>
        /// The beat the lesson's notes are shown on - <see cref="Beat"/> less however far the system's playback
        /// trails the ghost - and so the beat to measure <see cref="PressSchedule"/>'s onsets against.
        /// </summary>
        public double MelodyBeat { get; }

        public override string ToString() => Visible
            ? $"ghost @ beat {Beat:0.##}: pose {DemonstratedPose?.ToString() ?? "-"}, lit {LitTargetIndex}, strike {StrikeProgress:0.##} ({StrikeCount}), press {(PressFinger < 0 ? "-" : PressFinger.ToString())} {PressProgress:0.##}, next {(NextFinger < 0 ? "-" : NextFinger.ToString())}"
            : "ghost hidden";
    }
}
