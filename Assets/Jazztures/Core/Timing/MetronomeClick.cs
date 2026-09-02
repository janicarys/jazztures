namespace Jazztures.Core.Timing
{
    /// <summary>
    /// One scheduled metronome tick. The Unity adapter turns this into a
    /// <c>PlayScheduled</c> call on the DSP timeline (CLAUDE.md §3.6) — never a
    /// frame-triggered play. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct MetronomeClick
    {
        public MetronomeClick(long beatIndex, double dspTime, int beatsPerBar)
        {
            BeatIndex = beatIndex;
            DspTime = dspTime;
            BeatInBar = beatsPerBar > 0
                ? (int)(((beatIndex % beatsPerBar) + beatsPerBar) % beatsPerBar)
                : 0;
        }

        /// <summary>Whole-beat index from the metronome's start, 0-based.</summary>
        public long BeatIndex { get; }

        /// <summary>Absolute DSP time this click should sound.</summary>
        public double DspTime { get; }

        /// <summary>Position within the bar, 0-based (0 is the downbeat).</summary>
        public int BeatInBar { get; }

        public bool IsDownbeat => BeatInBar == 0;

        public override string ToString() =>
            $"click beat {BeatIndex} ({(IsDownbeat ? "downbeat" : "beat " + BeatInBar)}) @ {DspTime:0.###}";
    }
}
