using UnityEngine;

public class Oscillator
{
    public enum Waveform { Sine, Saw, Square, Triangle }

    private float _phase;
    private float _frequency;
    private float _sampleRate;
    private Waveform _waveform;
    private float _sawMix;

    public Oscillator(float sampleRate = 48000f)
    {
        _sampleRate = sampleRate;
        _waveform = Waveform.Sine;
        _sawMix = 0.1f;
    }

    public void SetFrequency(float frequency)
    {
        _frequency = Mathf.Max(20f, Mathf.Min(_sampleRate * 0.49f, frequency));
    }

    public void SetWaveform(Waveform waveform, float sawMix = 0.1f)
    {
        _waveform = waveform;
        _sawMix = Mathf.Clamp01(sawMix);
    }

    public void ResetPhase()
    {
        _phase = 0f;
    }

    public float GetNextSample()
    {
        float phaseIncrement = _frequency / _sampleRate;
        _phase += phaseIncrement;
        if (_phase >= 1f) _phase -= 1f;

        float sine = Mathf.Sin(_phase * Mathf.PI * 2f);
        float saw = 2f * (_phase - 0.5f);
        float square = _phase < 0.5f ? 1f : -1f;
        float triangle = 2f * Mathf.Abs(2f * _phase - 1f) - 1f;

        return _waveform switch
        {
            Waveform.Sine => sine,
            Waveform.Saw => saw,
            Waveform.Square => square,
            Waveform.Triangle => triangle,
            _ => Mathf.Lerp(sine, saw, _sawMix)
        };
    }

    public float GetNextSampleStereo(out float right)
    {
        float sample = GetNextSample();
        right = sample;
        return sample;
    }
}