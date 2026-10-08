using Jazztures.Core.Timing;

namespace Jazztures.Core.Ports
{
    /// <summary>
    /// Where a <see cref="Metronome"/>'s clicks become sound (CLAUDE.md §2.2; ADR-0043).
    /// <c>MetronomeVoice</c> implements it on the DSP timeline. Clicks bypass
    /// <see cref="INoteSink"/> — they are not part of the performance, so they never reach
    /// the OSC or telemetry fan-out.
    /// </summary>
    public interface IMetronomeOutput
    {
        /// <summary>
        /// How far ahead of "now" a pulse must be started so its first click can still be
        /// scheduled rather than played late. A caller anchoring a phrase to the pulse starts
        /// the metronome (and the phrase) at <c>now + PreRollSeconds</c>.
        /// </summary>
        double PreRollSeconds { get; }

        /// <summary>
        /// Start sounding <paramref name="metronome"/>, which the caller has already
        /// <see cref="Metronome.Start"/>ed. Replaces any metronome already playing.
        /// </summary>
        void Begin(Metronome metronome);

        /// <summary>Stop sounding, including clicks already scheduled ahead. Idempotent.</summary>
        void Stop();
    }
}
