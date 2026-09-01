using Jazztures.Core.Ports;
using UnityEngine;

namespace Jazztures.Audio
{
    /// <summary>
    /// The production <see cref="IMusicalClock"/>: the DSP timeline
    /// (<see cref="AudioSettings.dspTime"/>). This is the clock everything rhythmic must
    /// read — it advances with the audio hardware, not with rendered frames
    /// (CLAUDE.md §2.4, §3.6).
    /// </summary>
    public sealed class DspMusicalClock : IMusicalClock
    {
        public double Now => AudioSettings.dspTime;
    }
}
