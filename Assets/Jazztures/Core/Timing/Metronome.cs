using System;

namespace Jazztures.Core.Timing
{
    /// <summary>
    /// A look-ahead beat scheduler (CLAUDE.md §3.6). It owns no audio and reads no frame
    /// clock — it just answers "which clicks fall before this DSP time, and when exactly?"
    /// so the Unity adapter can schedule them ahead with <c>PlayScheduled</c>.
    ///
    /// <para>
    /// The quarter-note pulse is <b>not</b> swung — swing is a melodic phrasing concern
    /// (§3.6), the pulse the learner tracks stays even. An optional
    /// <see cref="MetronomeSubdivision"/> adds off-beat clicks between the beats; only
    /// those move under swing, at <see cref="SwingQuantizer.Swing"/>(beat + 0.5), so the
    /// quarter grid is identical with or without them (ADR-0043).
    /// </para>
    ///
    /// <para>
    /// Allocation-free: <see cref="TryDequeueClick"/> advances an internal cursor and
    /// returns one click at a time, so the caller pumps it in a <c>while</c> loop.
    /// </para>
    /// </summary>
    public sealed class Metronome
    {
        public const int DefaultBeatsPerBar = 4;

        private readonly Tempo _tempo;
        private readonly int _beatsPerBar;
        private readonly MetronomeSubdivision _subdivision;
        private readonly SwingRatio _swing;
        private readonly int _clicksPerBeat;
        private readonly double _offBeatPosition; // fraction of a beat after the beat itself

        private double _startDspTime;
        private long _nextClickIndex; // counts clicks, so with a subdivision it runs two per beat
        private bool _running;

        /// <param name="swing">
        /// Used only for <see cref="MetronomeSubdivision.SwungEighths"/>; defaults to
        /// <see cref="SwingRatio.Default"/> there. Ignored otherwise.
        /// </param>
        public Metronome(
            Tempo tempo,
            int beatsPerBar = DefaultBeatsPerBar,
            MetronomeSubdivision subdivision = MetronomeSubdivision.None,
            SwingRatio? swing = null)
        {
            if (beatsPerBar <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beatsPerBar), beatsPerBar, "Must be positive.");
            }

            if (!Enum.IsDefined(typeof(MetronomeSubdivision), subdivision))
            {
                throw new ArgumentOutOfRangeException(nameof(subdivision), subdivision, "Unknown subdivision.");
            }

            _tempo = tempo;
            _beatsPerBar = beatsPerBar;
            _subdivision = subdivision;
            _swing = subdivision == MetronomeSubdivision.SwungEighths
                ? swing ?? SwingRatio.Default
                : SwingRatio.Straight;
            _clicksPerBeat = subdivision == MetronomeSubdivision.None ? 1 : 2;
            _offBeatPosition = SwingQuantizer.Swing(0.5, _swing);
        }

        public Tempo Tempo => _tempo;

        public int BeatsPerBar => _beatsPerBar;

        public MetronomeSubdivision Subdivision => _subdivision;

        /// <summary>
        /// The swing the off-beat click is placed with: the configured ratio for
        /// <see cref="MetronomeSubdivision.SwungEighths"/>, otherwise <see cref="SwingRatio.Straight"/>.
        /// </summary>
        public SwingRatio Swing => _swing;

        public bool Running => _running;

        /// <summary>DSP time of beat 0, as passed to the last <see cref="Start"/>.</summary>
        public double StartDspTime => _startDspTime;

        /// <summary>The whole-beat index the next <see cref="TryDequeueClick"/> will consider.</summary>
        public long NextBeatIndex => _nextClickIndex / _clicksPerBeat;

        /// <summary>
        /// Begin (or restart) the pulse with beat 0 landing at <paramref name="atDspTime"/>.
        /// </summary>
        public void Start(double atDspTime)
        {
            if (double.IsNaN(atDspTime) || double.IsInfinity(atDspTime))
            {
                throw new ArgumentOutOfRangeException(nameof(atDspTime), atDspTime, "Must be finite.");
            }

            _startDspTime = atDspTime;
            _nextClickIndex = 0;
            _running = true;
        }

        /// <summary>Stop the pulse. Pending un-dequeued clicks are discarded.</summary>
        public void Stop() => _running = false;

        /// <summary>Absolute DSP time of a given beat index, whether or not it has been dequeued.</summary>
        public double DspTimeOf(long beatIndex) =>
            _startDspTime + _tempo.BeatsToSeconds(beatIndex);

        /// <summary>
        /// Absolute DSP time of a position on the beat timeline (e.g. 3.66 for a swung "and"
        /// of beat 3), whether or not its click has been dequeued.
        /// </summary>
        public double DspTimeOfPosition(double beatPosition) =>
            _startDspTime + _tempo.BeatsToSeconds(beatPosition);

        /// <summary>
        /// If the next un-emitted click sounds at or before <paramref name="horizonDspTime"/>,
        /// return it and advance. Call in a loop each frame:
        /// <c>while (m.TryDequeueClick(now + lookAhead, out var c)) audio.PlayScheduled(c.DspTime);</c>
        /// </summary>
        public bool TryDequeueClick(double horizonDspTime, out MetronomeClick click)
        {
            if (_running)
            {
                long beatIndex = _nextClickIndex / _clicksPerBeat;
                bool isOffBeat = _clicksPerBeat == 2 && (_nextClickIndex & 1) == 1;
                double position = isOffBeat ? beatIndex + _offBeatPosition : beatIndex;
                double due = DspTimeOfPosition(position);

                if (due <= horizonDspTime)
                {
                    click = new MetronomeClick(beatIndex, position, due, _beatsPerBar, isOffBeat);
                    _nextClickIndex++;
                    return true;
                }
            }

            click = default;
            return false;
        }
    }
}
