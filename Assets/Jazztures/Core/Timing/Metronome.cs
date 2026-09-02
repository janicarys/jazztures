using System;

namespace Jazztures.Core.Timing
{
    /// <summary>
    /// A look-ahead beat scheduler (CLAUDE.md §3.6). It owns no audio and reads no frame
    /// clock — it just answers "which clicks fall before this DSP time, and when exactly?"
    /// so the Unity adapter can schedule them ahead with <c>PlayScheduled</c>.
    ///
    /// <para>
    /// Clicks are quarter-note pulses and are <b>not</b> swung — swing is a melodic
    /// phrasing concern (§3.6), the pulse the learner tracks stays even.
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

        private double _startDspTime;
        private long _nextBeatIndex;
        private bool _running;

        public Metronome(Tempo tempo, int beatsPerBar = DefaultBeatsPerBar)
        {
            if (beatsPerBar <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(beatsPerBar), beatsPerBar, "Must be positive.");
            }

            _tempo = tempo;
            _beatsPerBar = beatsPerBar;
        }

        public Tempo Tempo => _tempo;

        public int BeatsPerBar => _beatsPerBar;

        public bool Running => _running;

        /// <summary>The beat index the next <see cref="TryDequeueClick"/> will consider.</summary>
        public long NextBeatIndex => _nextBeatIndex;

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
            _nextBeatIndex = 0;
            _running = true;
        }

        /// <summary>Stop the pulse. Pending un-dequeued clicks are discarded.</summary>
        public void Stop() => _running = false;

        /// <summary>Absolute DSP time of a given beat index, whether or not it has been dequeued.</summary>
        public double DspTimeOf(long beatIndex) =>
            _startDspTime + _tempo.BeatsToSeconds(beatIndex);

        /// <summary>
        /// If the next un-emitted click sounds at or before <paramref name="horizonDspTime"/>,
        /// return it and advance. Call in a loop each frame:
        /// <c>while (m.TryDequeueClick(now + lookAhead, out var c)) audio.PlayScheduled(c.DspTime);</c>
        /// </summary>
        public bool TryDequeueClick(double horizonDspTime, out MetronomeClick click)
        {
            if (_running)
            {
                double due = DspTimeOf(_nextBeatIndex);
                if (due <= horizonDspTime)
                {
                    click = new MetronomeClick(_nextBeatIndex, due, _beatsPerBar);
                    _nextBeatIndex++;
                    return true;
                }
            }

            click = default;
            return false;
        }
    }
}
