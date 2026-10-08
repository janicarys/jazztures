namespace Jazztures.Core.Timing
{
    /// <summary>
    /// One scheduled metronome tick. The Unity adapter turns this into a
    /// <c>PlayScheduled</c> call on the DSP timeline (CLAUDE.md §3.6) — never a
    /// frame-triggered play. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct MetronomeClick
    {
        public MetronomeClick(
            long beatIndex, double beatPosition, double dspTime, int beatsPerBar, bool isOffBeat)
        {
            BeatIndex = beatIndex;
            BeatPosition = beatPosition;
            DspTime = dspTime;
            BeatInBar = beatsPerBar > 0
                ? (int)(((beatIndex % beatsPerBar) + beatsPerBar) % beatsPerBar)
                : 0;
            Kind = isOffBeat
                ? MetronomeClickKind.OffBeat
                : BeatInBar == 0 ? MetronomeClickKind.Downbeat : MetronomeClickKind.Beat;
        }

        /// <summary>
        /// Whole-beat index from the metronome's start, 0-based. An off-beat click carries
        /// the index of the beat it follows.
        /// </summary>
        public long BeatIndex { get; }

        /// <summary>
        /// Where the click sits on the beat timeline, in beats from the start — the performed
        /// position, so a swung off-beat of beat 3 at ratio 0.66 is 3.66.
        /// </summary>
        public double BeatPosition { get; }

        /// <summary>Absolute DSP time this click should sound.</summary>
        public double DspTime { get; }

        /// <summary>Position within the bar, 0-based (0 is the downbeat).</summary>
        public int BeatInBar { get; }

        public MetronomeClickKind Kind { get; }

        public bool IsDownbeat => Kind == MetronomeClickKind.Downbeat;

        public bool IsOffBeat => Kind == MetronomeClickKind.OffBeat;

        public override string ToString() =>
            $"click beat {BeatPosition:0.###} ({Kind}) @ {DspTime:0.###}";
    }
}
