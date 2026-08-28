using UnityEngine;

public class ADSREnvelope
{
    public enum State { Idle, Attack, Decay, Sustain, Release }

    public State CurrentState { get; private set; } = State.Idle;

    private float _attackTime;
    private float _decayTime;
    private float _sustainLevel;
    private float _releaseTime;
    private float _sampleRate;

    private float _currentLevel;
    private float _targetLevel;
    private float _rate;
    private float _elapsed;

    public ADSREnvelope(float sampleRate = 48000f)
    {
        _sampleRate = sampleRate;
        SetTimes(0.005f, 0.15f, 0.6f, 0.3f);
    }

    public void SetTimes(float attack, float decay, float sustain, float release)
    {
        _attackTime = Mathf.Max(0.001f, attack);
        _decayTime = Mathf.Max(0.001f, decay);
        _sustainLevel = Mathf.Clamp01(sustain);
        _releaseTime = Mathf.Max(0.001f, release);
    }

    public void NoteOn(float velocity = 1f)
    {
        _currentLevel = 0f;
        _targetLevel = Mathf.Clamp01(velocity);
        _rate = 1f / (_attackTime * _sampleRate);
        _elapsed = 0f;
        CurrentState = State.Attack;
    }

    public void NoteOff()
    {
        if (CurrentState == State.Idle) return;
        _targetLevel = 0f;
        _rate = -1f / (_releaseTime * _sampleRate);
        CurrentState = State.Release;
    }

    public float GetNextSample()
    {
        switch (CurrentState)
        {
            case State.Attack:
                _currentLevel += _rate;
                if (_currentLevel >= _targetLevel)
                {
                    _currentLevel = _targetLevel;
                    _targetLevel = _sustainLevel;
                    _rate = (_targetLevel - _currentLevel) / (_decayTime * _sampleRate);
                    CurrentState = State.Decay;
                }
                break;

            case State.Decay:
                _currentLevel += _rate;
                if ((_rate > 0 && _currentLevel >= _targetLevel) || (_rate < 0 && _currentLevel <= _targetLevel))
                {
                    _currentLevel = _targetLevel;
                    CurrentState = State.Sustain;
                }
                break;

            case State.Sustain:
                _currentLevel = _targetLevel;
                break;

            case State.Release:
                _currentLevel += _rate;
                if (_currentLevel <= 0f)
                {
                    _currentLevel = 0f;
                    CurrentState = State.Idle;
                }
                break;

            case State.Idle:
                _currentLevel = 0f;
                break;
        }

        return _currentLevel;
    }

    public bool IsActive => CurrentState != State.Idle;
    public float CurrentLevel => _currentLevel;

    public void Reset()
    {
        _currentLevel = 0f;
        CurrentState = State.Idle;
    }
}
