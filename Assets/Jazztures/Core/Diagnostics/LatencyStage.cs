namespace Jazztures.Core.Diagnostics
{
    /// <summary>
    /// The segments of the gesture→sound path that <see cref="LatencyRecorder"/> tracks
    /// (CLAUDE.md §4.3). Report the percentiles in the thesis — latency and jitter are
    /// the standard technical benchmarks in this literature.
    /// </summary>
    public enum LatencyStage
    {
        /// <summary>Hand-tracking pose available → gesture confirmed (hold + frames).</summary>
        PoseToConfirm,

        /// <summary>Gesture confirmed → the resulting note event emitted by the domain.</summary>
        ConfirmToNoteEvent,

        /// <summary>Note event emitted → scheduled for playback on the DSP timeline.</summary>
        NoteEventToScheduled,

        /// <summary>Whole path: pose available → audible.</summary>
        EndToEnd,
    }
}
