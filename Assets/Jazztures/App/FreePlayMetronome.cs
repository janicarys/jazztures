using Jazztures.Audio;
using Jazztures.Config;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using Jazztures.Input;
using UnityEngine;

namespace Jazztures.App
{
    /// <summary>
    /// The metronome in free play (ADR-0043): a pulse with no lesson behind it.
    /// <see cref="LessonFlowController"/> calls <see cref="Begin"/> on entering free play and
    /// <see cref="End"/> on leaving it; lessons drive <c>MetronomeVoice</c> themselves.
    ///
    /// <para>
    /// Starts from <see cref="MetronomeSettings"/> (running only if
    /// <see cref="MetronomeSettings.ClickInFreePlay"/>). The tempo is changed, in the Editor /
    /// over Link, by desk keys (<see cref="KeyboardMetronomeInput"/>) via <see cref="StepTempo"/>.
    /// The gesture menu has no control for it (ADR-0051 — its tempo card sets a <i>lesson's</i>
    /// tempo). The tempo is kept across entering and leaving free play. Every change restarts the
    /// pulse from a fresh pre-roll so the grid stays exact.
    /// </para>
    ///
    /// <para>
    /// Stepping the tempo while free play is not running plays a short preview of
    /// <see cref="MetronomeSettings.TempoPreviewBars"/> bars, so the change can be heard
    /// without entering free play.
    /// </para>
    /// </summary>
    public sealed class FreePlayMetronome : MonoBehaviour
    {
        [SerializeField] private MetronomeSettings _settings;
        [SerializeField] private MetronomeVoice _voice;

        [Tooltip("Editor / Link only: M on-off, - / = tempo, B subdivision.")]
        [SerializeField] private bool _deskKeys = true;

        private readonly IMusicalClock _clock = new DspMusicalClock();
        private readonly KeyboardMetronomeInput _keys = new KeyboardMetronomeInput();

        private bool _initialised;
        private bool _active;       // free play is the current screen
        private bool _running;      // the click is switched on
        private double _bpm;
        private MetronomeSubdivision _subdivision;

        private bool _previewing;
        private double _previewEndDsp;

        /// <summary>True once both the settings asset and the voice are assigned.</summary>
        public bool IsConfigured => _settings != null && _voice != null;

        /// <summary>The free-play tempo in BPM (the settings' tempo until it is stepped).</summary>
        public double TempoBpm
        {
            get
            {
                EnsureInitialised();
                return _bpm;
            }
        }

        /// <summary>Whether <see cref="StepTempo"/> in <paramref name="direction"/> (−1 / +1) would change anything.</summary>
        public bool CanStepTempo(int direction)
        {
            if (!IsConfigured)
            {
                return false;
            }

            EnsureInitialised();
            double max = _settings.AllowUnvalidatedTempo ? TempoRange.AbsoluteMaxBpm : TempoRange.ValidatedMaxBpm;
            return direction < 0 ? _bpm > TempoRange.MinBpm : _bpm < max;
        }

        /// <summary>
        /// Change the tempo by <paramref name="steps"/> × <see cref="MetronomeSettings.TempoStepBpm"/>,
        /// clamped to <see cref="TempoRange"/>, and return the new tempo. While free play runs the
        /// pulse restarts at the new tempo; otherwise a short preview plays.
        /// </summary>
        public double StepTempo(int steps)
        {
            if (!IsConfigured)
            {
                return 0.0;
            }

            EnsureInitialised();
            _bpm = TempoRange.Clamp(_bpm + steps * _settings.TempoStepBpm, _settings.AllowUnvalidatedTempo);

            if (_active)
            {
                Restart();
            }
            else
            {
                StartPreview();
            }

            return _bpm;
        }

        /// <summary>Free play was entered: start clicking if the settings say so.</summary>
        public void Begin()
        {
            if (!IsConfigured)
            {
                Debug.LogWarning(
                    $"{nameof(FreePlayMetronome)}: assign {nameof(MetronomeSettings)} and {nameof(MetronomeVoice)} — free play will have no click.", this);
                return;
            }

            EnsureInitialised();
            _active = true;
            _running = _settings.ClickInFreePlay;
            Restart();
        }

        /// <summary>Free play was left (or a lesson is starting): stop clicking, including any menu preview.</summary>
        public void End()
        {
            _active = false;
            _previewing = false;
            if (_voice != null)
            {
                _voice.Stop();
            }
        }

        private void Awake() => EnsureInitialised();

        private void EnsureInitialised()
        {
            if (_initialised || _settings == null)
            {
                return;
            }

            _bpm = _settings.Tempo.BeatsPerMinute;
            _subdivision = _settings.Subdivision;
            _initialised = true;
        }

        private void Update()
        {
            if (_previewing && _clock.Now >= _previewEndDsp)
            {
                _previewing = false;
                _voice.Stop();
            }

            if (!_active || !_deskKeys)
            {
                return;
            }

            if (!_keys.Poll(out bool toggle, out int tempoSteps, out bool cycleSubdivision))
            {
                return;
            }

            if (toggle)
            {
                _running = !_running;
            }

            if (tempoSteps != 0)
            {
                _bpm = TempoRange.Clamp(_bpm + tempoSteps * _settings.TempoStepBpm, _settings.AllowUnvalidatedTempo);
            }

            if (cycleSubdivision)
            {
                _subdivision = _subdivision switch
                {
                    MetronomeSubdivision.None => MetronomeSubdivision.Eighths,
                    MetronomeSubdivision.Eighths => MetronomeSubdivision.SwungEighths,
                    _ => MetronomeSubdivision.None,
                };
            }

            Restart();
        }

        private void Restart()
        {
            _previewing = false;
            _voice.Stop();
            if (!_running)
            {
                return;
            }

            StartPulse();
        }

        /// <summary>A fixed-length click at the current tempo, for hearing a change made from the menu.</summary>
        private void StartPreview()
        {
            _voice.Stop();
            Metronome metronome = StartPulse();

            // End half a beat before the first click past the last bar, so the next downbeat
            // is never scheduled.
            double beats = _settings.TempoPreviewBars * metronome.BeatsPerBar - 0.5;
            _previewEndDsp = metronome.StartDspTime + metronome.Tempo.BeatsToSeconds(beats);
            _previewing = true;
        }

        private Metronome StartPulse()
        {
            var metronome = new Metronome(
                Tempo.Bpm(_bpm), _settings.BeatsPerBar, _subdivision, _settings.Swing);
            metronome.Start(_clock.Now + _voice.PreRollSeconds);
            _voice.Begin(metronome);
            return metronome;
        }

        private void OnDisable() => End();
    }
}
