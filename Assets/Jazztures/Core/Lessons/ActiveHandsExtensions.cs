namespace Jazztures.Core.Lessons
{
    public static class ActiveHandsExtensions
    {
        /// <summary>
        /// True for a right-hand-only lesson: the learner has no left hand to select the chords with, so the
        /// system has to (<see cref="ScriptedHarmony"/>, ADR-0043 decision 3) or the melody has no chord to follow.
        /// The one place the rule is written down, for the policy that decides what plays
        /// (<see cref="ModePolicy.For(LearningMode, ActiveHands)"/>) and the runner that selects.
        /// </summary>
        public static bool SystemSelectsChords(this ActiveHands hands) =>
            (hands & ActiveHands.Left) == 0 && (hands & ActiveHands.Right) != 0;
    }
}
