namespace Jazztures.Lessons
{
    /// <summary>Which regions a lesson gives the swing-slice right hand (ADR-0081). Serialised by value: do not reorder.</summary>
    public enum LessonPitchSetKind
    {
        /// <summary>The swing-slices default: the active chord's tones, retuned on every chord change.</summary>
        ChordTonesDefault = 0,

        /// <summary>The C major scale ascending from a lowest note, a fixed number of regions; never retuned.</summary>
        CMajorScale = 1,

        /// <summary>An authored list of pitches, one per region; never retuned.</summary>
        FixedList = 2,
    }
}
