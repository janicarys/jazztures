namespace Jazztures.Core.Sampling
{
    /// <summary>
    /// A recorded dynamic layer of a sampled instrument. The Salamander Grand set ships
    /// two (<c>vL</c>, <c>vH</c>); this enum stays small on purpose and can grow if a
    /// richer set is adopted.
    /// </summary>
    public enum VelocityLayer
    {
        /// <summary>Quiet layer — Salamander <c>vL</c>.</summary>
        Soft,

        /// <summary>Loud layer — Salamander <c>vH</c>.</summary>
        Hard,
    }
}
