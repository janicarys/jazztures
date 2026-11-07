namespace Jazztures.Core.Melody
{
    /// <summary>Why the cursor was reset. Logged as <c>reset_event</c> (ADR-0078).</summary>
    public enum PhraseResetReason
    {
        None = 0,
        Task = 1,
        Bar = 2,
        Rest = 3,
    }
}
