using System.Text;
using Jazztures.Config;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Melody;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Events;
using Jazztures.Input;
using Jazztures.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Jazztures.App
{
    /// <summary>
    /// The palm-swing sandbox (ADR-0078): a place to compare right-hand inputs with a drill. The system loops
    /// ii–V–I (one chord a bar), clicks a metronome with a count-in, and asks for a short pattern of up and down
    /// steps on the beat — UU, DD, UD or DU, in straight quarters or swung eighths. Every crossing (or, for the finger
    /// bends, every press) is scored against the nearest beat and written to a CSV.
    ///
    /// <para>
    /// Timing is on the audio clock throughout: the metronome, the chords and the targets are all anchored to
    /// one DSP time, and the phrase reset for each task lands halfway between its last beat and the next. The learner's
    /// left hand is not used — a script selects the chords (the way a right-hand lesson does) and the harmony engine
    /// plays them. Debug keys (a connected keyboard, or the editor): <b>1-4</b> drill · <b>B</b> rhythm · <b>T</b> tempo ·
    /// <b>G</b> height zones · <b>M</b> input · <b>Enter</b> start/stop · <b>R</b> re-centre.
    /// </para>
    /// </summary>
    public sealed class PalmSwingSandbox : MonoBehaviour
    {
        private enum RunState
        {
            Idle,
            Running,
            Done,
        }

        [SerializeField] private PalmSwingSettings _settings;
        [SerializeField] private PalmSwingInputSource _palmSwing;
        [Tooltip("The headset hand reader, whose lane frame places the view on a device. Optional.")]
        [SerializeField] private MetaXRPalmSwingSampleSource _headsetSource;
        [SerializeField] private RightHandInputSwitch _inputSwitch;
        [SerializeField] private PalmSwingView _view;
        [SerializeField] private SandboxHud _hud;
        [Tooltip("Sees every note the domain emits; used to score the finger-bend input.")]
        [SerializeField] private NoteTriggeredChannel _noteChannel;
        [SerializeField] private Transform _head;

        [Tooltip("Tasks per run. A task is four targets: one bar of quarters, or two beats of swung eighths.")]
        [Min(1)] [SerializeField] private int _tasks = 8;
        [Min(1)] [SerializeField] private int _countInBeats = 4;
        [Tooltip("Start a run by itself the first time the hand's gate opens (for a headset with no keyboard).")]
        [SerializeField] private bool _autoStartOnGate;
        [Tooltip("Start the next run a few seconds after one ends.")]
        [SerializeField] private bool _autoRepeat;
        [SerializeField] private float _autoRepeatDelaySeconds = 3f;
        [Tooltip("A closed gate shorter than this is not logged as a tracking loss.")]
        [SerializeField] private float _minTrackingLossSeconds = 0.2f;

        private static readonly int[] TempoChoices = { 60, 70, 80 };
        private static readonly ChordFunction[] Bars =
        {
            ChordFunction.Two, ChordFunction.Five, ChordFunction.One, ChordFunction.One,
        };

        private readonly PalmSwingCsvLogger _csv = new PalmSwingCsvLogger();
        private readonly StringBuilder _text = new StringBuilder(512);

        private IMusicalClock _clock;
        private ModeGatedNoteSink _gate;
        private ScriptedHarmony _scripted;
        private IMetronomeOutput _metronomeOutput;

        private SwingDrill[] _drills;
        private int _drillIndex;
        private int _tempoIndex;
        private DrillRhythm _rhythm = DrillRhythm.StraightQuarters;

        private RunState _state = RunState.Idle;
        private DrillSchedule _schedule;
        private DrillMatcher _matcher;
        private Metronome _metronome;
        private TimelinePlayback _playback;
        private bool _metronomeSounding;
        private double _firstTargetTime;
        private double _secondsPerBeat;
        private int _nextResetTarget;
        private long _nextBar;
        private double _endedAt;
        private bool _lossActive;
        private double _lossStart;
        private PhraseResetReason _pendingReset;
        private int _lastNoteZone = -1;
        private int _previousFingerMidi = -1;
        private PalmSwingPipeline _hookedPipeline;
        private float _nextHudTime;

        private SwingDrill Drill => _drills[_drillIndex];

        private bool UsingPalmSwing => _inputSwitch == null || _inputSwitch.Mode == RightHandInputMode.PalmSwing;

        /// <summary>Wire the domain up. Call once, from the composition root's <c>Awake</c>, after the palm-swing input is bound.</summary>
        public void Bind(IMusicalClock clock, ModeGatedNoteSink gate, ScriptedHarmony scripted, IMetronomeOutput metronomeOutput)
        {
            _clock = clock;
            _gate = gate;
            _scripted = scripted;
            _metronomeOutput = metronomeOutput;
            _drills = SwingDrill.All;

            if (_palmSwing != null)
            {
                _palmSwing.NotePlayed += OnPalmNote;
                _palmSwing.PipelineRebuilt += HookPipeline;
                if (_palmSwing.Pipeline != null)
                {
                    HookPipeline(_palmSwing.Pipeline);
                }
            }
        }

        private void OnEnable() => _noteChannel?.Register(OnNoteEvent);

        private void OnDisable()
        {
            _noteChannel?.Unregister(OnNoteEvent);
            StopRun(finish: false);
        }

        private void OnDestroy() => _csv.Dispose();

        private void HookPipeline(PalmSwingPipeline pipeline)
        {
            if (_hookedPipeline != null)
            {
                _hookedPipeline.PhraseReset -= OnPhraseReset;
            }

            _hookedPipeline = pipeline;
            pipeline.PhraseReset += OnPhraseReset;
            if (_view != null)
            {
                _view.Bind(pipeline);
            }
        }

        private void OnPhraseReset(PhraseResetReason reason, Jazztures.Core.Music.Pitch pitch) => _pendingReset = reason;

        private void OnPalmNote(PalmSwingNote note)
        {
            _view?.OnNote(note);
            if (_state != RunState.Running || _matcher == null || !UsingPalmSwing)
            {
                return;
            }

            _lastNoteZone = note.Zone;
            _matcher.OnCrossing(note.Time, note.SampledDirection, note.Swing, note.Pitch.Midi);
        }

        private void OnNoteEvent(NoteEvent note)
        {
            if (_state != RunState.Running || _matcher == null || UsingPalmSwing)
            {
                return;
            }

            if (note.Kind != NoteEventKind.On || note.Channel != MidiChannel.Melody || note.Source != Handedness.Right)
            {
                return;
            }

            // The finger-bend input has no direction of its own: score each press by how its pitch moved.
            StepDirection direction = StepDirections.FromPitchMovement(_previousFingerMidi, note.Pitch.Midi);
            _previousFingerMidi = note.Pitch.Midi;
            _lastNoteZone = -1;
            _matcher.OnCrossing(note.DspTime, direction, SwingDirection.LeftToRight, note.Pitch.Midi);
        }

        // ---- running a drill ----

        public void StartRun()
        {
            if (_clock == null || _scripted == null)
            {
                return;
            }

            StopRun(finish: false);

            var tempo = Tempo.Bpm(TempoChoices[_tempoIndex]);
            SwingRatio swing = SwingRatio.Default;
            _secondsPerBeat = tempo.SecondsPerBeat;

            double preRoll = _metronomeOutput != null ? System.Math.Max(0.1, _metronomeOutput.PreRollSeconds) : 0.1;
            double clickStart = _clock.Now + preRoll;
            _firstTargetTime = clickStart + _countInBeats * _secondsPerBeat;

            _schedule = new DrillSchedule(tempo, _rhythm, swing, Drill, _firstTargetTime, _tasks);
            float window = _settings != null ? _settings.MatchWindowSeconds : (float)DrillMatcher.DefaultWindowSeconds;
            _matcher = new DrillMatcher(_schedule, window);
            _matcher.Scored += OnScored;

            _metronome = new Metronome(tempo, Metronome.DefaultBeatsPerBar);
            _metronome.Start(clickStart);
            if (_metronomeOutput != null)
            {
                _metronomeOutput.Begin(_metronome);
                _metronomeSounding = true;
            }

            LessonTimeline harmony = BuildHarmony(tempo, swing);
            _gate.SetMode(LearningMode.ComposeOnTheFly);
            _gate.SetGestureCorrect(true);
            _scripted.Start(harmony, _firstTargetTime);
            _playback = new TimelinePlayback(harmony, _clock, _gate, includeMelody: false);
            _playback.Start(_firstTargetTime);

            _nextResetTarget = 0;
            _nextBar = 0;
            _previousFingerMidi = -1;
            _pendingReset = PhraseResetReason.None;
            _lossActive = !(_palmSwing != null && _palmSwing.GateOpen);
            _lossStart = _clock.Now;

            _csv.Open($"{Drill.Name}_{TempoChoices[_tempoIndex]}_{(_rhythm == DrillRhythm.StraightQuarters ? "straight" : "swung")}_{(UsingPalmSwing ? "palm" : "bend")}");
            _state = RunState.Running;
        }

        public void StopRun(bool finish)
        {
            if (_state == RunState.Running)
            {
                if (_lossActive && UsingPalmSwing)
                {
                    EndLoss(_clock.Now);
                }

                if (finish)
                {
                    _matcher.Finish();
                }
            }

            if (_metronomeSounding)
            {
                _metronomeOutput.Stop();
                _metronomeSounding = false;
            }

            _metronome?.Stop();
            _playback?.Stop();
            _scripted?.Stop();
            _csv.Close();
            if (_matcher != null)
            {
                _matcher.Scored -= OnScored;
            }

            _state = finish ? RunState.Done : RunState.Idle;
            if (finish && _clock != null)
            {
                _endedAt = _clock.Now;
            }
        }

        private LessonTimeline BuildHarmony(Tempo tempo, SwingRatio swing)
        {
            double totalBeats = (_schedule.EndTime - _firstTargetTime) / _secondsPerBeat + 2.0;
            int bars = Mathf.CeilToInt((float)(totalBeats / Metronome.DefaultBeatsPerBar)) + 1;
            LessonTimelineBuilder builder = new LessonTimelineBuilder()
                .WithTempo(tempo)
                .WithSwing(swing)
                .WithBeatsPerBar(Metronome.DefaultBeatsPerBar)
                .WithMinimumDuration(bars * Metronome.DefaultBeatsPerBar);
            for (int bar = 0; bar < bars; bar++)
            {
                builder.Chord(bar * Metronome.DefaultBeatsPerBar, Bars[bar % Bars.Length]);
            }

            return builder.Build();
        }

        private void OnScored(DrillEvent e)
        {
            int zone = e.PitchMidi >= 0 ? _lastNoteZone : -1;
            _csv.Write(DrillCsv.Row(_schedule, e, Drill.StartMidi, _pendingReset, zone));
            _pendingReset = PhraseResetReason.None;
        }

        private void EndLoss(double now)
        {
            double duration = now - _lossStart;
            _lossActive = false;
            if (duration >= _minTrackingLossSeconds)
            {
                _matcher.OnTrackingLost(_lossStart, duration);
            }
        }

        // ---- per frame ----

        private void Update()
        {
            if (_clock == null)
            {
                return;
            }

            if (_state == RunState.Idle && _autoStartOnGate && _palmSwing != null && _palmSwing.GateOpen)
            {
                StartRun();
            }

            HandleKeys();

            if (_state == RunState.Running)
            {
                TickRun(_clock.Now);
            }
            else if (_state == RunState.Done && _autoRepeat && _clock.Now - _endedAt >= _autoRepeatDelaySeconds)
            {
                StartRun();
            }

            if (Time.unscaledTime >= _nextHudTime)
            {
                _nextHudTime = Time.unscaledTime + 0.2f;
                RefreshHud();
            }
        }

        private void TickRun(double now)
        {
            _playback?.Tick();
            _scripted?.Tick();

            bool palm = UsingPalmSwing && _palmSwing != null && _palmSwing.Pipeline != null;

            // Each task restarts from the drill's start pitch, halfway between its last beat and its first.
            while (_nextResetTarget < _schedule.Count && now >= ResetTime(_nextResetTarget))
            {
                if (palm)
                {
                    _palmSwing.Pipeline.BeginTask(PhraseStart.FromMidi(Drill.StartMidi));
                }

                _nextResetTarget += _schedule.NotesPerTask;
            }

            // Bar lines come from the audio clock, for the OnBar policy.
            double barLength = _secondsPerBeat * Metronome.DefaultBeatsPerBar;
            while (now >= _firstTargetTime + _nextBar * barLength)
            {
                if (palm)
                {
                    _palmSwing.Pipeline.NotifyBarLine();
                }

                _nextBar++;
            }

            if (palm)
            {
                bool gateOpen = _palmSwing.GateOpen;
                if (!gateOpen && !_lossActive)
                {
                    _lossActive = true;
                    _lossStart = now;
                }
                else if (gateOpen && _lossActive)
                {
                    EndLoss(now);
                }
            }

            _matcher.Advance(now);

            if (now > _schedule.EndTime + 0.6)
            {
                StopRun(finish: true);
            }
        }

        private double ResetTime(int targetIndex)
        {
            double first = _schedule[targetIndex].Time;
            if (targetIndex == 0)
            {
                return first - _secondsPerBeat * 0.5;
            }

            return first - (first - _schedule[targetIndex - 1].Time) * 0.5;
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.digit1Key.wasPressedThisFrame) { SelectDrill(0); }
            if (keyboard.digit2Key.wasPressedThisFrame) { SelectDrill(1); }
            if (keyboard.digit3Key.wasPressedThisFrame) { SelectDrill(2); }
            if (keyboard.digit4Key.wasPressedThisFrame) { SelectDrill(3); }

            if (keyboard.bKey.wasPressedThisFrame && _state != RunState.Running)
            {
                _rhythm = _rhythm == DrillRhythm.StraightQuarters ? DrillRhythm.SwungEighths : DrillRhythm.StraightQuarters;
            }

            if (keyboard.tKey.wasPressedThisFrame && _state != RunState.Running)
            {
                _tempoIndex = (_tempoIndex + 1) % TempoChoices.Length;
            }

            if (keyboard.gKey.wasPressedThisFrame && _state != RunState.Running && _palmSwing != null)
            {
                _palmSwing.SetRegisterSource(
                    _palmSwing.RegisterSource == RegisterSource.None ? RegisterSource.HandHeightZones : RegisterSource.None);
            }

            if (keyboard.mKey.wasPressedThisFrame && _state != RunState.Running && _inputSwitch != null)
            {
                _inputSwitch.Mode = UsingPalmSwing ? RightHandInputMode.FingerBend : RightHandInputMode.PalmSwing;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                _palmSwing?.Recenter();
            }

            if (keyboard.enterKey.wasPressedThisFrame)
            {
                if (_state == RunState.Running)
                {
                    StopRun(finish: false);
                }
                else
                {
                    StartRun();
                }
            }
        }

        private void SelectDrill(int index)
        {
            if (_state != RunState.Running && index < _drills.Length)
            {
                _drillIndex = index;
            }
        }

        // ---- display ----

        private void LateUpdate()
        {
            if (_view == null)
            {
                return;
            }

            if (_headsetSource != null && _headsetSource.LaneSet && (_palmSwing == null || !_palmSwing.UsesSimulation))
            {
                _view.SetLane(_headsetSource.LaneOrigin, _headsetSource.LaneRight);
            }
            else if (_head != null && _settings != null)
            {
                // No headset lane (the editor simulation, or the hand not seen yet): stand the line in the right lane.
                Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
                if (forward.sqrMagnitude > 1e-4f)
                {
                    forward.Normalize();
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    Vector3 origin = _head.position + forward * _settings.LaneDistanceMetres
                        + right * _settings.LaneOffsetRightMetres + Vector3.down * 0.3f;
                    _view.SetLane(origin, right);
                }
            }
        }

        private void RefreshHud()
        {
            if (_hud == null)
            {
                return;
            }

            _text.Clear();
            _text.Append("PALM SWING SANDBOX\n");
            _text.Append("Input: ").Append(UsingPalmSwing ? "palm swing" : "finger bend");
            _text.Append("   Height zones: ")
                .Append(_palmSwing != null && _palmSwing.RegisterSource == RegisterSource.HandHeightZones ? "on" : "off");
            _text.Append('\n');
            _text.Append("Drill ").Append(Drill.Name).Append(" · ").Append(TempoChoices[_tempoIndex]).Append(" BPM · ")
                .Append(_rhythm == DrillRhythm.StraightQuarters ? "straight quarters" : "swung eighths").Append('\n');

            _text.Append("Next bar: ");
            for (int i = 0; i < DrillSchedule.DefaultNotesPerTask; i++)
            {
                _text.Append(Drill.DirectionAt(i) == StepDirection.Up ? "UP" : Drill.DirectionAt(i) == StepDirection.Down ? "DOWN" : "SAME");
                _text.Append(' ');
            }

            _text.Append('\n');
            _text.Append(StateLine()).Append('\n');

            if (_matcher != null)
            {
                DrillSummary s = _matcher.Summary;
                _text.Append("Hits ").Append(s.Hits).Append("  Wrong ").Append(s.WrongDirections)
                    .Append("  Miss ").Append(s.Misses).Append("  Extra ").Append(s.Extras)
                    .Append("  |offset| ").Append(Mathf.RoundToInt((float)(s.MeanAbsoluteOffsetSeconds * 1000.0))).Append(" ms\n");
            }

            if (_state != RunState.Running)
            {
                _text.Append("1-4 drill · B rhythm · T tempo · G zones · M input · Enter start");
            }

            _hud.SetText(_text.ToString());
        }

        private string StateLine()
        {
            switch (_state)
            {
                case RunState.Running:
                {
                    double untilFirst = _firstTargetTime - _clock.Now;
                    if (untilFirst > 0.0)
                    {
                        return "Count-in " + Mathf.CeilToInt((float)(untilFirst / _secondsPerBeat));
                    }

                    return "Go";
                }

                case RunState.Done:
                    return "Done" + (_csv.Path != null ? " — saved " + System.IO.Path.GetFileName(_csv.Path) : string.Empty);
                default:
                    return _palmSwing != null && !_palmSwing.GateOpen
                        ? "Open palm, facing left, to begin"
                        : "Ready";
            }
        }
    }
}
