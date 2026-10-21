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

        public ScriptedHarmony(IMusicalClock clock, HarmonyEngine harmony)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _harmony = harmony ?? throw new ArgumentNullException(nameof(harmony));
        }

        /// <summary>True from a start call until <see cref="Stop"/>.</summary>
        public bool IsRunning => _running;

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

            int count = timeline.Chords.Count;
            _functions = new ChordFunction[count];
            _dueDspTimes = new double[count];
            for (int i = 0; i < count; i++)
            {
                TimelineChord chord = timeline.Chords[i];
                _functions[i] = chord.Function;
                _dueDspTimes[i] = startDspTime + timeline.Tempo.BeatsToSeconds(chord.Beat.Position);
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

        private void Reset()
        {
            _running = false;
            _cursor = 0;
            _functions = Array.Empty<ChordFunction>();
            _dueDspTimes = Array.Empty<double>();
        }
    }
}
