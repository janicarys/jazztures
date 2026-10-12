using System;
using System.Globalization;
using System.IO;
using System.Text;
using Jazztures.Core.Drills;
using Jazztures.Core.Evaluation;
using Jazztures.Core.Lessons;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Events;
using UnityEngine;

namespace Jazztures.Lessons
{
    /// <summary>
    /// Runs one <see cref="TimingDrillDefinition"/> (ADR-0043): the metronome, the scripted
    /// harmony and the learner's note capture, all anchored to <b>one shared start time</b>
    /// a pre-roll after "now" so the first click can be scheduled ahead. When the run ends it
    /// scores the capture with <see cref="OnsetScorer"/> (unchanged), writes a CSV and raises
    /// <see cref="Finished"/> — the feedback arrives after the run, never mid-phrase (§3.7).
    ///
    /// <para>
    /// The gesture pipeline keeps running and everything the learner plays still reaches the
    /// unconditional sink (telemetry, OSC) as in any mode; the drill only <i>listens</i> to the
    /// note stream. Like <see cref="LessonRunner"/> it is wired by the composition root
    /// (<see cref="Bind"/>) and driven by the flow controller; it never touches domain state
    /// itself (§2.3) — the scripted chord goes through <see cref="ScriptedHarmony"/>.
    /// </para>
    /// </summary>
    public sealed class TimingDrillRunner : MonoBehaviour
    {
        [Tooltip("Learner note-ons are read from here to build the scored capture. Wire the same " +
                 "channel the composition root and LessonRunner use.")]
        [SerializeField] private NoteTriggeredChannel _noteChannel;

        [Tooltip("An anonymous code written into every CSV. Never a name. Confirm informal " +
                 "testers are covered by the ethics approval before collecting (ADR-0043).")]
        [SerializeField] private string _testerCode = "T00";

        [Tooltip("Seconds after the last scored beat before the run ends, so a late final note " +
                 "still counts.")]
        [Min(0f)]
        [SerializeField] private float _tailSeconds = 1.0f;

        [Tooltip("Write a CSV per scored run under Application.persistentDataPath/timing/ " +
                 "(pull with adb).")]
        [SerializeField] private bool _writeCsv = true;

        private IMusicalClock _clock;
        private ModeGatedNoteSink _gate;
        private ScriptedHarmony _scripted;
        private IMetronomeOutput _metronomeOutput;

        private TimingDrill _drill;
        private DrillRunInfo _info;
        private Metronome _metronome;
        private TimelinePlayback _playback;
        private OnsetCapture _capture;
        private double _t0;
        private bool _running;
        private bool _scriptStarted;
        private bool _metronomeStarted;
        private bool _noteChannelRegistered;

        /// <summary>True from <see cref="Run"/> until the drill ends or is aborted.</summary>
        public bool IsRunning => _running;

        /// <summary>The CSV from the most recent scored run, or null.</summary>
        public string LastCsvPath { get; private set; }

        /// <summary>Raised once when a run reaches its end (not when it is aborted).</summary>
        public event Action<DrillOutcome> Finished;

        /// <summary>Wire the domain in. Call once from the composition root's <c>Awake</c>.</summary>
        public void Bind(
            IMusicalClock clock,
            ModeGatedNoteSink gate,
            ScriptedHarmony scriptedHarmony,
            IMetronomeOutput metronomeOutput)
        {
            _clock = clock;
            _gate = gate;
            _scripted = scriptedHarmony;
            _metronomeOutput = metronomeOutput;
        }

        /// <summary>
        /// Start <paramref name="definition"/>. <paramref name="mechanic"/> and
        /// <paramref name="harmonyCommit"/> are recorded in the CSV header so a result can be
        /// traced to the interaction that produced it. Returns false (after logging why) if the
        /// preset does not make a valid drill or the runner was never bound.
        /// </summary>
        public bool Run(TimingDrillDefinition definition, string mechanic, string harmonyCommit)
        {
            if (definition == null)
            {
                Debug.LogError($"{nameof(TimingDrillRunner)}: {nameof(Run)}(null).", this);
                return false;
            }

            if (_clock == null || _gate == null)
            {
                Debug.LogError($"{nameof(TimingDrillRunner)}: Bind() must run before Run().", this);
                return false;
            }

            TimingDrill drill;
            try
            {
                drill = definition.BuildDrill();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogError($"{nameof(TimingDrillRunner)}: '{definition.name}' is not a valid drill — {e.Message}", definition);
                return false;
            }

            Abort();
            RegisterNoteChannel();

            _drill = drill;
            _info = new DrillRunInfo(
                mechanic, harmonyCommit, BuildId(), _testerCode, DateTime.UtcNow.ToString("o"));

            // The learner is always audible in a drill; the mode only matters for the gate.
            _gate.SetMode(LearningMode.ComposeOnTheFly);
            _gate.SetGestureCorrect(true);

            // One start time for the click, the chords and the capture. Anchoring them at
            // "now" would make the first click late (it can only be scheduled ahead).
            double preRoll = _metronomeOutput != null ? _metronomeOutput.PreRollSeconds : 0.0;
            _t0 = _clock.Now + preRoll;

            _capture = new OnsetCapture(_drill.ScoredHand);
            _capture.Begin(_t0);

            _metronome = _drill.CreateMetronome();
            _metronome.Start(_t0);
            if (_drill.MetronomeAudible && _metronomeOutput != null)
            {
                _metronomeOutput.Begin(_metronome);
                _metronomeStarted = true;
            }

            LessonTimeline harmony = _drill.BuildHarmonyTimeline();
            if (harmony != null)
            {
                if (_scripted != null)
                {
                    _scripted.Start(harmony, _t0);
                    _scriptStarted = true;
                }

                if (_drill.HarmonyAudible)
                {
                    _playback = new TimelinePlayback(harmony, _clock, _gate, includeMelody: false);
                    _playback.Start(_t0);
                }
            }

            _running = true;
            return true;
        }

        /// <summary>Stop a run in progress without scoring it. Does nothing if none is running.</summary>
        public void Abort()
        {
            if (!_running)
            {
                return;
            }

            Teardown();
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            _playback?.Tick();
            if (_scriptStarted)
            {
                _scripted.Tick();
            }

            if (_clock.Now >= _t0 + _drill.TotalSeconds + _tailSeconds)
            {
                Finish();
            }
        }

        private void Finish()
        {
            TimingDrill drill = _drill;
            OnsetCapture capture = _capture;
            DrillRunInfo info = _info;
            Teardown();

            DrillReport report = null;
            string csvPath = null;
            string summary;

            if (drill.ScoringEnabled)
            {
                report = DrillReport.Build(drill, capture.OnsetsSeconds, OnsetWindows.Default);
                summary = report.SummaryLine;
                if (_writeCsv)
                {
                    csvPath = TryWriteCsv(report, info);
                }
            }
            else
            {
                summary = $"{drill.Name}: done";
            }

            LastCsvPath = csvPath;
            Finished?.Invoke(new DrillOutcome(drill, report, summary, csvPath));
        }

        /// <summary>Stop everything this run started, and only that.</summary>
        private void Teardown()
        {
            _running = false;

            _metronome?.Stop();
            if (_metronomeStarted)
            {
                _metronomeOutput.Stop();
                _metronomeStarted = false;
            }

            _playback?.Stop();
            _playback = null;

            if (_scriptStarted)
            {
                _scripted.Stop();
                _scriptStarted = false;
            }

            _capture?.Stop();
        }

        private string TryWriteCsv(DrillReport report, DrillRunInfo info)
        {
            // A failed write must never reach the learner (§4.4): log it and carry on.
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "timing");
                Directory.CreateDirectory(directory);

                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string fileName = $"{stamp}_{Sanitise(report.Drill.Name)}_{Sanitise(_testerCode)}.csv";
                string path = Path.Combine(directory, fileName);

                using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
                {
                    report.WriteCsv(writer, info);
                }

                Debug.Log($"[TimingDrill] wrote {path}");
                return path;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"{nameof(TimingDrillRunner)}: could not write the CSV — {e.Message}", this);
                return null;
            }
        }

        private void OnNoteTriggered(NoteEvent note)
        {
            if (_running && _capture != null)
            {
                _capture.Observe(note);
            }
        }

        private void RegisterNoteChannel()
        {
            if (_noteChannel != null && !_noteChannelRegistered)
            {
                _noteChannel.Register(OnNoteTriggered);
                _noteChannelRegistered = true;
            }
        }

        private void OnDestroy()
        {
            if (_noteChannel != null && _noteChannelRegistered)
            {
                _noteChannel.Unregister(OnNoteTriggered);
                _noteChannelRegistered = false;
            }
        }

        private static string BuildId()
        {
            string guid = Application.buildGUID;
            string shortGuid = string.IsNullOrEmpty(guid) ? "editor" : guid.Substring(0, Math.Min(8, guid.Length));
            return $"{Application.version}+{shortGuid}";
        }

        /// <summary>A file-name-safe version of free text.</summary>
        private static string Sanitise(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "x";
            }

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '-');
            }

            string result = sb.ToString().Trim('-');
            if (result.Length > 40)
            {
                result = result.Substring(0, 40);
            }

            return result.Length == 0 ? "x" : result;
        }
    }
}
