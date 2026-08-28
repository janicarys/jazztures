using UnityEngine;

public class ToneVoice
{
    private SamplePlayer _samplePlayer;
    private AudioSource _audioSource;
    private int _midiNote;
    private float _velocity;
    private bool _isPlaying;

    public ToneVoice(AudioSource audioSource, float sampleRate = 44100f)
    {
        _audioSource = audioSource;
        _samplePlayer = new SamplePlayer(sampleRate);
        _samplePlayer.Initialize(_audioSource);
    }

    public void SetMidiNote(int midiNote)
    {
        _midiNote = midiNote;
    }

    public void SetSampleRate(float sampleRate)
    {
        _samplePlayer.SetSampleRate(sampleRate);
    }

    public void SetADSR(float attack, float decay, float sustain, float release)
    {
        _samplePlayer.SetADSR(attack, decay, sustain, release);
    }

    public void NoteOn(int midiNote, float velocity = 1f)
    {
        _midiNote = midiNote;
        _velocity = Mathf.Clamp01(velocity);
        _isPlaying = true;
        
        var clip = SampleBank.Instance.GetClip(midiNote, _velocity);
        _samplePlayer.Play(clip, _velocity);
    }

    public void NoteOff()
    {
        _samplePlayer.Release();
    }

    public float GetNextSample()
    {
        return _samplePlayer.GetNextSample();
    }

    public void Reset()
    {
        _samplePlayer.Stop();
        _isPlaying = false;
    }

    public bool IsActive => _samplePlayer.IsPlaying;
    public int MidiNote => _midiNote;
}