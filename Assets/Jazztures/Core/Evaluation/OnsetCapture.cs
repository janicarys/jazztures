using System;
using System.Collections.Generic;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Evaluation
{
    /// <summary>
    /// Collects the <b>learner's</b> onsets from the note stream, for scoring against
    /// <see cref="OnsetScorer"/> (CLAUDE.md §3.7; ADR-0043). It is an <see cref="INoteSink"/>,
    /// so it can sit on any fan-out that sees every note.
    ///
    /// <para>
    /// Three filters decide what counts as the learner playing:
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Kind</b> — note-ons only.
    ///   </item>
    ///   <item>
    ///     <b>Hand and channel</b> — the scored hand's own channel and source: the left hand's
    ///     chord strikes are <see cref="MidiChannel.Harmony"/>, the right hand's notes are
    ///     <see cref="MidiChannel.Melody"/>. System playback is on
    ///     <see cref="MidiChannel.Accompaniment"/> and is <b>never</b> captured, so a lesson can
    ///     play chords under the learner without those chords counting as the learner's playing.
    ///   </item>
    ///   <item>
    ///     <b>One strike is one onset</b> — a chord strike emits one note-on per voicing pitch,
    ///     all at the same instant (<c>HarmonyEngine.Strike</c>). Left-hand note-ons within
    ///     <see cref="CoincidenceSeconds"/> of the previous one count once. Right-hand notes
    ///     are each their own onset: two targets entered together really are two notes, and
    ///     scoring shows them as an extra.
    ///   </item>
    /// </list>
    ///
    /// <para>
    /// Times are stored relative to the origin given to <see cref="Begin"/>, so they line up
    /// with expected onsets measured from the same shared start. Allocation-free after
    /// warm-up apart from the list growing.
    /// </para>
    /// </summary>
    public sealed class OnsetCapture : INoteSink
    {
        /// <summary>
        /// Left-hand note-ons closer together than this are one strike's voicing. `[TUNABLE]`
        /// — a strike stamps every pitch with one clock reading, so this is only a guard
        /// against float noise; real strikes are ≥ 120 ms apart (§3.4).
        /// </summary>
        public const double DefaultCoincidenceSeconds = 0.005;

        private readonly List<double> _onsets = new List<double>(64);
        private readonly Handedness _hand;
        private readonly int _channel;
        private readonly double _coincidenceSeconds;

        private bool _active;
        private double _origin;
        private bool _hasLast;
        private double _lastOnsetAbsolute;

        public OnsetCapture(Handedness hand, double coincidenceSeconds = DefaultCoincidenceSeconds)
        {
            if (double.IsNaN(coincidenceSeconds) || double.IsInfinity(coincidenceSeconds) || coincidenceSeconds < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(coincidenceSeconds), coincidenceSeconds, "Must be finite and not negative.");
            }

            _hand = hand;
            _channel = hand == Handedness.Left ? MidiChannel.Harmony : MidiChannel.Melody;
            _coincidenceSeconds = coincidenceSeconds;
        }

        /// <summary>The hand whose playing is being captured.</summary>
        public Handedness Hand => _hand;

        /// <summary>True between <see cref="Begin"/> and <see cref="Stop"/>.</summary>
        public bool IsCapturing => _active;

        /// <summary>The captured onsets in seconds from the origin, in arrival order.</summary>
        public IReadOnlyList<double> OnsetsSeconds => _onsets;

        public int Count => _onsets.Count;

        /// <summary>
        /// Clear everything and start capturing, measuring from <paramref name="originDspTime"/>
        /// (the shared start of the phrase or drill). An onset before the origin is kept, as a
        /// negative time — a note played before the pulse started is the learner's too.
        /// </summary>
        public void Begin(double originDspTime)
        {
            if (double.IsNaN(originDspTime) || double.IsInfinity(originDspTime))
            {
                throw new ArgumentOutOfRangeException(nameof(originDspTime), originDspTime, "Must be finite.");
            }

            _onsets.Clear();
            _origin = originDspTime;
            _hasLast = false;
            _active = true;
        }

        /// <summary>Stop capturing. The onsets collected so far stay readable.</summary>
        public void Stop() => _active = false;

        /// <summary>Offer one note event. Returns true if it was recorded as a learner onset.</summary>
        public bool Observe(in NoteEvent note)
        {
            if (!_active
                || note.Kind != NoteEventKind.On
                || note.Source != _hand
                || note.Channel != _channel)
            {
                return false;
            }

            if (_channel == MidiChannel.Harmony
                && _hasLast
                && Math.Abs(note.DspTime - _lastOnsetAbsolute) <= _coincidenceSeconds)
            {
                return false; // another pitch of the same strike
            }

            _lastOnsetAbsolute = note.DspTime;
            _hasLast = true;
            _onsets.Add(note.DspTime - _origin);
            return true;
        }

        public void Send(in NoteEvent note) => Observe(note);
    }
}
