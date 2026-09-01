namespace Jazztures.Core.Ports
{
    /// <summary>
    /// Channel numbers that keep harmony and melody separable everywhere downstream — in
    /// the sampler mix, the OSC/DAW capture and the telemetry log. The cognitive split
    /// across hands (CLAUDE.md §1.3) is preserved as a channel split.
    /// </summary>
    public static class MidiChannel
    {
        /// <summary>Left hand — the chord voicing.</summary>
        public const int Harmony = 0;

        /// <summary>Right hand — the melody line.</summary>
        public const int Melody = 1;

        /// <summary>System-played demonstration audio (Watch-and-Listen, backing).</summary>
        public const int Accompaniment = 2;
    }
}
