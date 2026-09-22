namespace Jazztures.Core.Ports
{
    /// <summary>
    /// What the left hand's selection mechanism currently reads, for one frame. Two
    /// producers: the discrete Meta XR Interaction SDK recognisers (<c>ShapeRecognizer</c>
    /// + <c>TransformRecognizer</c> + <c>ActiveStateGroup</c>), or, under Design A
    /// (ADR-0038), <see cref="Jazztures.Core.Gesture.HarmonicField"/>'s continuous
    /// nearest-landmark classification. Either way the temporal decision — hold time,
    /// confirming frames, debounce, tracking-loss policy — is
    /// <see cref="Jazztures.Core.Gesture.GestureInterpreter"/>'s job, unchanged by which
    /// producer is upstream of it.
    /// </summary>
    public enum HandPoseCandidate
    {
        /// <summary>No pose matches — a legal state meaning "release" once confirmed (§3.2).</summary>
        None = 0,

        /// <summary>ii — open palm facing the user's right.</summary>
        Ii = 1,

        /// <summary>V — fist.</summary>
        V = 2,

        /// <summary>I — open palm facing down.</summary>
        I = 3,

        /// <summary>
        /// ii and I both match — they share a hand shape and differ only in palm
        /// orientation. The interpreter must hold the previous state and emit nothing;
        /// never guess (§3.4). Never produced by <see cref="Jazztures.Core.Gesture.HarmonicField"/>
        /// — a continuous field has no ambiguity state to hold; this is one of Design A's
        /// stated wins (ADR-0038).
        /// </summary>
        Ambiguous = 4,
    }
}
