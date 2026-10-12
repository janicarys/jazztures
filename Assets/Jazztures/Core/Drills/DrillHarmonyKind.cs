namespace Jazztures.Core.Drills
{
    /// <summary>Where a <see cref="TimingDrill"/>'s harmony comes from (ADR-0043).</summary>
    public enum DrillHarmonyKind
    {
        /// <summary>None scripted — the learner's own left hand supplies it (or the drill has none).</summary>
        None = 0,

        /// <summary>One fixed chord function, struck on every bar line.</summary>
        Fixed = 1,

        /// <summary>An authored chord timeline.</summary>
        Timeline = 2,
    }
}
