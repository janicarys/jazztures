using Jazztures.Core.Diagnostics;
using Jazztures.Core.Ports;
using Jazztures.Core.Sampling;
using UnityEngine;

namespace Jazztures.Audio
{
    /// <summary>
    /// The on-device audio sink (CLAUDE.md §2.2, §4.2). Consumes <see cref="NoteEvent"/>s
    /// and plays piano samples through a pooled set of <see cref="AudioSource"/> voices.
    /// No MIDI anywhere on this path — that is the OSC bridge's job.
    ///
    /// <para>
    /// Struck model: a note-on plays a full piano sample (which decays on its own); the
    /// paired note-off just applies a short fade so the tail does not click. A stuck note
    /// is impossible — see <c>MelodyEngine</c> / §3.5.
    /// </para>
    ///
    /// <para>All calls must be on the main thread (Unity audio API). Send never throws.</para>
    /// </summary>
    public sealed class SamplerNoteSink : MonoBehaviour, INoteSink
    {
        [SerializeField] private PianoSampleBank _bank;

        [Tooltip("Polyphony. CLAUDE.md §4.2 wants at least 32.")]
        [Range(8, 64)]
        [SerializeField] private int _voiceCount = 32;

        [Range(0f, 1f)]
        [SerializeField] private float _masterGain = 0.5f;

        [Tooltip("Fade applied on note-off so the sample tail does not click. [TUNABLE]")]
        [Range(0.01f, 0.5f)]
        [SerializeField] private float _releaseSeconds = 0.08f;

        private AudioSource[] _sources = System.Array.Empty<AudioSource>();
        private Voice[] _voices = System.Array.Empty<Voice>();
        private bool _ready;
        private LatencyRecorder _latency;

        /// <summary>Optional: record the note-event → scheduled latency (§4.3).</summary>
        public void SetLatencyRecorder(LatencyRecorder recorder) => _latency = recorder;

        private void Awake()
        {
            _sources = new AudioSource[_voiceCount];
            _voices = new Voice[_voiceCount];

            for (int i = 0; i < _voiceCount; i++)
            {
                var go = new GameObject($"Voice{i:00}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.bypassReverbZones = true;
                _sources[i] = src;
            }

            _ready = _bank != null;
            if (!_ready)
            {
                Debug.LogError($"{nameof(SamplerNoteSink)}: no PianoSampleBank assigned.", this);
            }
        }

        public void Send(in NoteEvent note)
        {
            if (!_ready)
            {
                return;
            }

            if (note.Kind == NoteEventKind.On)
            {
                StartNote(note);
            }
            else
            {
                ReleaseNote(note);
            }
        }

        private void StartNote(in NoteEvent note)
        {
            SampleSelection selection = _bank.Library.Resolve(note.Pitch, note.Velocity);
            var clips = _bank.Clips;
            if (selection.ClipIndex < 0 || selection.ClipIndex >= clips.Count)
            {
                return;
            }

            AudioClip clip = clips[selection.ClipIndex];
            if (clip == null)
            {
                return;
            }

            int slot = AcquireVoice();
            AudioSource src = _sources[slot];
            float gain = _masterGain * Mathf.Lerp(0.35f, 1f, note.Velocity / 127f);

            src.clip = clip;
            src.pitch = (float)selection.PlaybackRate;
            src.volume = gain;

            double now = AudioSettings.dspTime;
            double when = System.Math.Max(note.DspTime, now);
            src.PlayScheduled(when);

            _latency?.Record(LatencyStage.NoteEventToScheduled, (now - note.DspTime) * 1000.0);

            _voices[slot] = new Voice
            {
                Active = true,
                Midi = note.Pitch.Midi,
                Channel = note.Channel,
                StartDsp = when,
                BaseGain = gain,
            };
        }

        private void ReleaseNote(in NoteEvent note)
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                ref Voice v = ref _voices[i];
                if (v.Active && !v.Releasing && v.Midi == note.Pitch.Midi && v.Channel == note.Channel)
                {
                    v.Releasing = true;
                    v.ReleaseStartDsp = AudioSettings.dspTime;
                    return;
                }
            }
        }

        private int AcquireVoice()
        {
            int oldest = 0;
            for (int i = 0; i < _voices.Length; i++)
            {
                if (!_voices[i].Active)
                {
                    return i;
                }

                if (_voices[i].StartDsp < _voices[oldest].StartDsp)
                {
                    oldest = i;
                }
            }

            _sources[oldest].Stop();
            return oldest;
        }

        private void Update()
        {
            if (!_ready)
            {
                return;
            }

            double now = AudioSettings.dspTime;
            for (int i = 0; i < _voices.Length; i++)
            {
                ref Voice v = ref _voices[i];
                if (!v.Active)
                {
                    continue;
                }

                if (v.Releasing)
                {
                    float t = (float)((now - v.ReleaseStartDsp) / _releaseSeconds);
                    if (t >= 1f)
                    {
                        _sources[i].Stop();
                        v = default;
                    }
                    else
                    {
                        _sources[i].volume = v.BaseGain * (1f - t);
                    }
                }
                else if (now > v.StartDsp && !_sources[i].isPlaying)
                {
                    // The sample decayed to its end on its own.
                    v = default;
                }
            }
        }

        private struct Voice
        {
            public bool Active;
            public bool Releasing;
            public int Midi;
            public int Channel;
            public double StartDsp;
            public double ReleaseStartDsp;
            public float BaseGain;
        }
    }
}
