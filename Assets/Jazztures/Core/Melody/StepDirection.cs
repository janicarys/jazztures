namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Which way the next melody note steps (palm-swing input, ADR-0078). The value is the
    /// step in pitch-set positions, so <c>(int)direction</c> is the index delta.
    /// </summary>
    public enum StepDirection
    {
        Down = -1,
        Same = 0,
        Up = 1,
    }
}
