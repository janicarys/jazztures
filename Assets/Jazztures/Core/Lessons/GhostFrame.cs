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
            int strikeCount = 0)
        {
            Beat = beat;
            DemonstratedPose = demonstratedPose;
            PoseChangedAtBeat = poseChangedAtBeat;
            LitTargetIndex = litTargetIndex;
            StrikeProgress = strikeProgress < 0.0 ? 0.0 : strikeProgress > 1.0 ? 1.0 : strikeProgress;
            StrikeCount = strikeCount < 0 ? 0 : strikeCount;
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

        public override string ToString() => Visible
            ? $"ghost @ beat {Beat:0.##}: pose {DemonstratedPose?.ToString() ?? "-"}, lit {LitTargetIndex}, strike {StrikeProgress:0.##} ({StrikeCount})"
            : "ghost hidden";
    }
}
