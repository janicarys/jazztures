namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// The five learning modes (CLAUDE.md §3.8) — four from ImproVisAR plus Jazztures'
    /// Gesture Learning. A mode gates the <b>note sink</b>, never the gesture recogniser:
    /// recognition always runs and every attempt is always logged to telemetry, even
    /// when it is not sounded. "Silent-but-logged" is a first-class state.
    /// </summary>
    public enum LearningMode
    {
        /// <summary>
        /// Jazztures addition. No musical target — pose fluency only. Ghost hands shown,
        /// the learner's input is audible. Precedes Watch-and-Listen so a movement
        /// lexicon is built before musical demand is added (compensates for no haptics).
        /// </summary>
        GestureLearning,

        /// <summary>Full demonstration. The system plays, ghost hands show it, the learner's input is logged but not sounded.</summary>
        WatchAndListen,

        /// <summary>Ghost hands show the target; the learner's audio is the reward for matching the gesture.</summary>
        TryYourself,

        /// <summary>No ghost hands. The learner plays; correctness feedback is deferred to the end of the attempt (§3.7).</summary>
        TestYourself,

        /// <summary>Free improvisation over a backing track. No ghost hands.</summary>
        ComposeOnTheFly,
    }
}
