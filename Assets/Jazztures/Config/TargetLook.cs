namespace Jazztures.Config
{
    /// <summary>How the ten right-hand touch targets are drawn (ADR-0046). A look, not a behaviour: the trigger volume is the same either way.</summary>
    public enum TargetLook
    {
        /// <summary>
        /// A flat pad on a faint rail, drawn at the trigger volume's own radius. It says "push
        /// me" and is carried along the rail in front of a fingertip, so depth is visible.
        /// </summary>
        Paddle = 0,

        /// <summary>
        /// Clone the rig's <c>Target Prefab</c> (a scene object or asset) as-is. The prefab has
        /// no moving part and its size need not match the trigger volume.
        /// </summary>
        Prefab = 1,
    }
}
