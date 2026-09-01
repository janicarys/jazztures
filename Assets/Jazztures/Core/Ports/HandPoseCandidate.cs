namespace Jazztures.Core.Ports
{
    /// <summary>
    /// What the left-hand pose recognisers currently match, for one frame. The Meta XR
    /// Interaction SDK (<c>ShapeRecognizer</c> + <c>TransformRecognizer</c> +
    /// <c>ActiveStateGroup</c>) produces this per frame; the temporal decision — hold
    /// time, confirming frames, debounce, tracking-loss policy — is
    /// <see cref="Jazztures.Core.Gesture.GestureInterpreter"/>'s job.
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
        /// never guess (§3.4).
        /// </summary>
        Ambiguous = 4,
    }
}
