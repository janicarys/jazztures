using UnityEngine;

public class SamplePlayer
{
    private AudioSource _audioSource;
    private float _sampleRate;
    private bool _isPlaying;
    private float _gain;
    private ADSREnvelope _envelope;

    public SamplePlayer(float sampleRate = 44100f)
    {
        _sampleRate = sampleRate;
        _envelope = new ADSREnvelope(sampleRate);
        _envelope.SetTimes(0.005f, 0.1f, 0.7f, 0.3f);
    }

    public void Initialize(AudioSource audioSource)
    {
        _audioSource = audioSource;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 1f;
    }

    public void Play(AudioClip clip, float velocity = 1f, float pitchSemitones = 0f)
    {
        if (clip == null || _audioSource == null) return;

        _gain = Mathf.Clamp01(velocity);
        _audioSource.clip = clip;
        _audioSource.pitch = Mathf.Pow(2f, pitchSemitones / 12f);
        _audioSource.Play();
        
        _envelope.NoteOn(_gain);
        _isPlaying = true;
    }

    public void Stop()
    {
        if (_audioSource != null && _isPlaying)
        {
            _envelope.NoteOff();
            _audioSource.Stop();
            _isPlaying = false;
        }
    }

    public void Release()
    {
        if (_isPlaying)
        {
            _envelope.NoteOff();
        }
    }

    public float GetNextSample()
    {
        if (!_isPlaying) return 0f;

        float envelopeLevel = _envelope.GetNextSample();
        
        if (!_envelope.IsActive && _envelope.CurrentState == ADSREnvelope.State.Idle)
        {
            _isPlaying = false;
            return 0f;
        }

        return envelopeLevel * _gain;
    }

    public bool IsPlaying => _isPlaying || _envelope.IsActive;

    public void SetADSR(float attack, float decay, float sustain, float release)
    {
        _envelope.SetTimes(attack, decay, sustain, release);
    }

    public void SetSampleRate(float sampleRate)
    {
        _sampleRate = sampleRate;
        _envelope = new ADSREnvelope(sampleRate);
    }
}