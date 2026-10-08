using Jazztures.Config;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using UnityEngine;

namespace Jazztures.Audio
{
    /// <summary>
    /// The audible metronome (CLAUDE.md §3.6; ADR-0043). Pumps a <see cref="Metronome"/>
    /// ahead of the DSP clock and hands every click to a pooled <see cref="AudioSource"/>
    /// with <c>PlayScheduled</c> — so a click lands on its exact DSP sample, not on
    /// whichever frame happened to notice it was due.
    ///
    /// <para>
    /// Clicks bypass <see cref="INoteSink"/>: they are not part of the performance, so
    /// they never reach the OSC or telemetry fan-out. The click sounds are synthesised at
    /// start-up by <see cref="ClickWaveform"/> — no audio asset.
    /// </para>
    ///
    /// <para>
    /// All calls on the main thread. <see cref="Stop"/> also stops every pool source, which
    /// cancels clicks already scheduled ahead.
    /// </para>
    /// </summary>
    public sealed class MetronomeVoice : MonoBehaviour, IMetronomeOutput
    {
        [SerializeField] private MetronomeSettings _settings;

        [Tooltip("Round-robin AudioSources. A click is ~20 ms long, so 4 is ample; this only " +
                 "needs to exceed the clicks that can be in flight inside the look-ahead.")]
        [Range(4, 16)]
        [SerializeField] private int _poolSize = 4;

        private AudioSource[] _sources = System.Array.Empty<AudioSource>();
        private AudioClip _beatClip;
        private AudioClip _downbeatClip;
        private Metronome _metronome;
        private int _nextSource;
        private bool _warnedAboutLateClick;

        public double PreRollSeconds =>
            _settings != null ? _settings.PreRollSeconds : MetronomeSettings.DefaultPreRollSeconds;

        /// <summary>True while a metronome is being sounded.</summary>
        public bool IsPlaying => _metronome != null && _metronome.Running;

        private void Awake()
        {
            if (_settings == null)
            {
                Debug.LogError($"{nameof(MetronomeVoice)}: no {nameof(MetronomeSettings)} assigned — the metronome will be silent.", this);
                enabled = false;
                return;
            }

            int sampleRate = AudioSettings.outputSampleRate;
            _beatClip = CreateClip("MetronomeBeat", sampleRate, _settings.BeatClickHz);
            _downbeatClip = _settings.AccentDownbeat
                ? CreateClip("MetronomeDownbeat", sampleRate, _settings.DownbeatClickHz)
                : _beatClip;

            _sources = new AudioSource[_poolSize];
            for (int i = 0; i < _sources.Length; i++)
            {
                var go = new GameObject($"Click{i:00}");
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.bypassReverbZones = true;
                _sources[i] = source;
            }
        }

        public void Begin(Metronome metronome)
        {
            if (metronome == null)
            {
                throw new System.ArgumentNullException(nameof(metronome));
            }

            Stop();
            if (_sources.Length == 0)
            {
                return; // no settings — Awake already said so
            }

            _metronome = metronome;
            _warnedAboutLateClick = false;

            // One line per run: with the start DSP time, tempo, bar length, subdivision and
            // swing, every click time can be reconstructed (ADR-0043). Not per-click, so it
            // stays on plain Debug.Log rather than DiagLog.
            Debug.Log(
                $"[Metronome] grid start={metronome.StartDspTime:0.000000}s "
                + $"tempo={metronome.Tempo.BeatsPerMinute:0.##}bpm bar={metronome.BeatsPerBar} "
                + $"subdivision={metronome.Subdivision} swing={metronome.Swing.Value:0.###}", this);
        }

        public void Stop()
        {
            _metronome = null;

            for (int i = 0; i < _sources.Length; i++)
            {
                _sources[i].Stop(); // also cancels a click scheduled but not yet sounded
            }
        }

        private void Update()
        {
            Metronome metronome = _metronome;
            if (metronome == null || !metronome.Running)
            {
                return;
            }

            double now = AudioSettings.dspTime;
            double horizon = now + _settings.LookAheadSeconds;

            while (metronome.TryDequeueClick(horizon, out MetronomeClick click))
            {
                Schedule(click, now);
            }
        }

        private void Schedule(in MetronomeClick click, double now)
        {
            // A click already in the past (a frame hitch longer than the look-ahead) would
            // play immediately, i.e. late — an audible timing error. Drop it instead.
            if (click.DspTime < now)
            {
                if (!_warnedAboutLateClick)
                {
                    _warnedAboutLateClick = true;
                    Debug.LogWarning(
                        $"{nameof(MetronomeVoice)}: dropped a click {(now - click.DspTime) * 1000.0:0} ms late "
                        + "(frame hitch longer than the look-ahead). Further drops this run are not logged.", this);
                }

                return;
            }

            AudioSource source = _sources[_nextSource];
            _nextSource = (_nextSource + 1) % _sources.Length;

            bool accent = click.Kind == MetronomeClickKind.Downbeat;
            source.clip = accent ? _downbeatClip : _beatClip;
            source.volume = _settings.ClickVolume
                * (click.Kind == MetronomeClickKind.OffBeat ? _settings.OffBeatVolumeScale : 1f);
            source.PlayScheduled(click.DspTime);
        }

        private AudioClip CreateClip(string clipName, int sampleRate, float frequencyHz)
        {
            var samples = new float[ClickWaveform.SampleCountFor(sampleRate, _settings.ClickDurationSeconds)];
            ClickWaveform.Render(samples, sampleRate, frequencyHz);

            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDisable() => Stop();

        private void OnDestroy()
        {
            if (_beatClip != null)
            {
                Destroy(_beatClip);
            }

            if (_downbeatClip != null && _downbeatClip != _beatClip)
            {
                Destroy(_downbeatClip);
            }
        }
    }
}
