using System;
using Jazztures.Core.Harmony;
using Jazztures.Core.Ports;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// Selects the harmony from a script instead of the left hand (ADR-0043): either one
    /// fixed <see cref="ChordFunction"/> or the chord changes of a <see cref="LessonTimeline"/>.
    /// It exists so a right-hand-only lesson or drill has a chord — the melody engine
    /// recomputes its chord-tone set from <see cref="HarmonyEngine.ChordChanged"/>, and without
    /// a selected chord there is nothing to play.
    ///
    /// <para>
    /// <b>Silent by construction.</b> It only ever calls
    /// <see cref="HarmonyEngine.SetHeldFunction"/>, which selects without sounding
    /// (ADR-0025). The chords the learner <i>hears</i> are a separate thing —
    /// <see cref="TimelinePlayback"/> on <see cref="MidiChannel.Accompaniment"/> — driven by the
    /// same timeline and start time, so the chord heard is the chord the melody follows.
    /// </para>
    ///
    /// <para>
    /// Wired by the composition root and started by whoever runs the exercise, so the
    /// direction rule holds (§2.3): the runner is input, this is domain, presentation only
    /// listens. Call <see cref="Tick"/> once per frame while running.
    /// </para>
    /// </summary>
    public sealed class ScriptedHarmony
    {
        private readonly IMusicalClock _clock;
        private readonly HarmonyEngine _harmony;

        private ChordFunction[] _functions = Array.Empty<ChordFunction>();
        private double[] _dueDspTimes = Array.Empty<double>();
        private int _cursor;
        private bool _running;

        // ADR-0077: a lesson that hands part of every round to the learner. Without turns the script selects from
        // its start until Stop (every earlier use); with them it selects only in the system's turn.
        private TurnWindow _turns;
        private Jazztures.Core.Timing.Tempo _tempo;
        private double _startDsp;

        public ScriptedHarmony(IMusicalClock clock, HarmonyEngine harmony)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _harmony = harmony ?? throw new ArgumentNullException(nameof(harmony));
        }

        /// <summary>True from a start call until <see cref="Stop"/>.</summary>
        public bool IsRunning => _running;

        /// <summary>
        /// True while the script, not the left hand, is choosing the chord: from a start call to <see cref="Stop"/>,
        /// except in the learner's turn of a lesson with turns (ADR-0077), when the left hand chooses again.
        /// The thing the left hand's selection should be ignored on - <see cref="IsRunning"/> only says a script exists.
        /// </summary>
        public bool IsSelecting => _running && (!_turns.IsEnabled || _turns.IsSystemTurn(BeatNow()));

        /// <summary>
        /// True in the system's turn of a lesson with turns: the left hand is ignored altogether, so a strike
        /// of its own must not sound the chord the script has selected (ADR-0077). False when there are no turns -
        /// a right-hand lesson or a drill never relied on this, and still does not.
        /// </summary>
        public bool BlocksLeftHandStrikes => _running && _turns.IsEnabled && _turns.IsSystemTurn(BeatNow());

        /// <summary>Select one function now and hold it until <see cref="Stop"/>.</summary>
        public void StartFixed(ChordFunction function)
        {
            Reset();
            _running = true;
            _harmony.SetHeldFunction(function);
        }

        /// <summary>
        /// Follow <paramref name="timeline"/>'s chord changes, with beat 0 landing at
        /// <paramref name="startDspTime"/> (which may be in the future — nothing is selected
        /// until then).
        /// </summary>
        public void Start(LessonTimeline timeline, double startDspTime)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            if (double.IsNaN(startDspTime) || double.IsInfinity(startDspTime))
            {
                throw new ArgumentOutOfRangeException(nameof(startDspTime), startDspTime, "Must be finite.");
            }

            Reset();

            // From here the script is the one selecting. Drop whatever was left selected before (a pose the
            // learner's left hand held on the way in, or the last lesson's last chord), so the lead-in before the
            // first chord is bare and the first chord is the lesson's - not a leftover with the wrong tones under it.
            _harmony.SetHeldFunction(null);

            _turns = timeline.Turns;
            _tempo = timeline.Tempo;
            _startDsp = startDspTime;

            // A chord authored in the learner's turn is the learner's to choose (ADR-0077), not scripted.
            int count = 0;
            for (int i = 0; i < timeline.Chords.Count; i++)
            {
                if (_turns.IsSystemTurn(timeline.Chords[i].Beat.Position))
                {
                    count++;
                }
            }

            _functions = new ChordFunction[count];
            _dueDspTimes = new double[count];
            int slot = 0;
            for (int i = 0; i < timeline.Chords.Count; i++)
            {
                TimelineChord chord = timeline.Chords[i];
                if (!_turns.IsSystemTurn(chord.Beat.Position))
                {
                    continue;
                }

                _functions[slot] = chord.Function;
                _dueDspTimes[slot] = startDspTime + timeline.Tempo.BeatsToSeconds(chord.Beat.Position);
                slot++;
            }

            _running = true;
            Tick(); // a chord already due (a start at "now") is selected without waiting a frame
        }

        /// <summary>Select every scripted chord whose time has arrived.</summary>
        public void Tick()
        {
            if (!_running)
            {
                return;
            }

            double now = _clock.Now;
            while (_cursor < _dueDspTimes.Length && _dueDspTimes[_cursor] <= now)
            {
                _harmony.SetHeldFunction(_functions[_cursor]);
                _cursor++;
            }
        }

        /// <summary>
        /// Stop scripting and release the selection. A no-op if not running, so a lesson
        /// without scripted harmony never clears the learner's own selection.
        /// </summary>
        public void Stop()
        {
            if (!_running)
            {
                return;
            }

            Reset();
            _harmony.SetHeldFunction(null);
        }

        // The lesson beat now, from the start time. Negative in the lead-in (which counts as the system's turn).
        private double BeatNow() => _tempo.SecondsToBeats(_clock.Now - _startDsp);

        private void Reset()
        {
            _turns = TurnWindow.None;
            _running = false;
            _cursor = 0;
            _functions = Array.Empty<ChordFunction>();
            _dueDspTimes = Array.Empty<double>();
        }
    }
}
