using UnityEngine;

public enum ChordType { II, V, I }

public class AudioEngine : MonoBehaviour
{
    [Header("Chord MIDI Notes (C Major Jazz Voicings)")]
    [SerializeField] private int[] _iiMidiNotes = { 50, 53, 57, 60, 64 };   // D3, F3, A3, C4, E4
    [SerializeField] private int[] _vMidiNotes = { 55, 59, 62, 65, 69 };     // G3, B3, D4, F4, A4
    [SerializeField] private int[] _iMidiNotes = { 48, 52, 55, 59, 62 };     // C3, E3, G3, B3, D4

    [Header("ADSR Settings")]
    [SerializeField] private float _attackMs = 5f;
    [SerializeField] private float _decayMs = 150f;
    [SerializeField] private float _sustainLevel = 0.7f;
    [SerializeField] private float _releaseMs = 400f;

    [Header("Output")]
    [Range(0f, 1f)] [SerializeField] private float _masterGain = 0.4f;

    private ToneVoice[] _voices = new ToneVoice[5];
    private ChordType _currentChord = ChordType.II;
    private bool[] _activeTones = new bool[5];
    private float _sampleRate;
    private AudioSource[] _voiceAudioSources = new AudioSource[5];

    public ChordType CurrentChord => _currentChord;
    public bool[] ActiveTones => _activeTones;
    public float MasterGain => _masterGain;

    private void Awake()
    {
        var sampleBank = SampleBank.Instance;
        var config = AudioSettings.GetConfiguration();
        _sampleRate = config.sampleRate > 0 ? config.sampleRate : 44100f;
        InitializeVoices();
        Debug.Log($"AudioEngine.Awake: SampleRate={_sampleRate}, SpeakerMode={config.speakerMode}");
    }

    private void Start()
    {
        // Voices initialized in Awake()
    }

    private void InitializeVoices()
    {
        for (int i = 0; i < 5; i++)
        {
            _voiceAudioSources[i] = CreateVoiceAudioSource(i);
            _voices[i] = new ToneVoice(_voiceAudioSources[i], _sampleRate);
            _voices[i].SetADSR(
                _attackMs / 1000f,
                _decayMs / 1000f,
                _sustainLevel,
                _releaseMs / 1000f
            );
        }
        SetChord(ChordType.II);
    }

    private AudioSource CreateVoiceAudioSource(int index)
    {
        var audioGO = new GameObject($"Voice_{index}");
        audioGO.transform.SetParent(transform);
        audioGO.transform.localPosition = Vector3.zero;
        var source = audioGO.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = _masterGain;
        return source;
    }

    public void SetChord(ChordType chordType)
    {
        if (_currentChord == chordType) return;
        
        _currentChord = chordType;
        int[] midiNotes = chordType switch
        {
            ChordType.II => _iiMidiNotes,
            ChordType.V => _vMidiNotes,
            ChordType.I => _iMidiNotes,
            _ => _iiMidiNotes
        };

        for (int i = 0; i < 5; i++)
        {
            _voices[i].SetMidiNote(midiNotes[i]);
        }
    }

    public void NoteOn(int toneIndex, float velocity = 1f)
    {
        if (toneIndex < 0 || toneIndex >= 5) return;
        
        _activeTones[toneIndex] = true;
        _voices[toneIndex].NoteOn(_voices[toneIndex].MidiNote, velocity);
    }

    public void NoteOff(int toneIndex)
    {
        if (toneIndex < 0 || toneIndex >= 5) return;
        
        _activeTones[toneIndex] = false;
        _voices[toneIndex].NoteOff();
    }

    public void SetActiveTones(bool[] tones, float velocity = 1f)
    {
        for (int i = 0; i < 5; i++)
        {
            if (tones[i] && !_activeTones[i])
                NoteOn(i, velocity);
            else if (!tones[i] && _activeTones[i])
                NoteOff(i);
        }
    }

    public void AllNotesOff()
    {
        for (int i = 0; i < 5; i++)
        {
            _activeTones[i] = false;
            _voices[i].NoteOff();
        }
    }

    public void SetMasterGain(float gain)
    {
        _masterGain = Mathf.Clamp01(gain);
        for (int i = 0; i < 5; i++)
        {
            if (_voiceAudioSources[i] != null)
                _voiceAudioSources[i].volume = _masterGain;
        }
    }

    public void SetADSR(float attackMs, float decayMs, float sustain, float releaseMs)
    {
        _attackMs = attackMs;
        _decayMs = decayMs;
        _sustainLevel = sustain;
        _releaseMs = releaseMs;

        float attack = attackMs / 1000f;
        float decay = decayMs / 1000f;
        float release = releaseMs / 1000f;

        for (int i = 0; i < 5; i++)
        {
            _voices[i].SetADSR(attack, decay, sustain, release);
        }
    }

    private void OnValidate()
    {
        if (_voices != null)
        {
            for (int i = 0; i < 5; i++)
            {
                if (_voices[i] != null)
                {
                    _voices[i].SetADSR(
                        _attackMs / 1000f,
                        _decayMs / 1000f,
                        _sustainLevel,
                        _releaseMs / 1000f
                    );
                }
            }
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < 5; i++)
        {
            if (_voiceAudioSources[i] != null)
                Destroy(_voiceAudioSources[i].gameObject);
        }
    }
}