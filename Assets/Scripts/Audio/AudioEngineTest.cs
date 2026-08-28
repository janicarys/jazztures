using UnityEngine;
using System.Collections;

public class AudioEngineTest : MonoBehaviour
{
    [SerializeField] private AudioEngine _audioEngine;
    [SerializeField] private bool _testOnStart = true;
    [SerializeField] private float _noteDuration = 0.5f;
    [SerializeField] private float _gapDuration = 0.1f;

    private float _timer;
    private int _testStep = 0;
    private bool _testing;

    private void Awake()
    {
        if (_audioEngine == null)
            _audioEngine = GetComponent<AudioEngine>();
    }

    private void Start()
    {
        if (_testOnStart && _audioEngine != null)
        {
            StartCoroutine(DelayedTest());
        }
    }

    private IEnumerator DelayedTest()
    {
        yield return null; // Wait one frame for AudioEngine.Start()
        StartTest();
    }

    private void Update()
    {
        if (!_testing) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            NextTestStep();
        }
    }

    public void StartTest()
    {
        _testing = true;
        _testStep = 0;
        NextTestStep();
    }

    private void NextTestStep()
    {
        switch (_testStep)
        {
            case 0:
                Debug.Log("Testing II chord (Dm7) - Root");
                _audioEngine.SetChord(ChordType.II);
                _audioEngine.NoteOn(0, 0.8f);
                _timer = _noteDuration;
                break;
            case 1:
                _audioEngine.NoteOff(0);
                _timer = _gapDuration;
                break;
            case 2:
                Debug.Log("Testing II chord - Third");
                _audioEngine.NoteOn(1, 0.8f);
                _timer = _noteDuration;
                break;
            case 3:
                _audioEngine.NoteOff(1);
                _timer = _gapDuration;
                break;
            case 4:
                Debug.Log("Testing II chord - Fifth");
                _audioEngine.NoteOn(2, 0.8f);
                _timer = _noteDuration;
                break;
            case 5:
                _audioEngine.NoteOff(2);
                _timer = _gapDuration;
                break;
            case 6:
                Debug.Log("Testing II chord - Seventh");
                _audioEngine.NoteOn(3, 0.8f);
                _timer = _noteDuration;
                break;
            case 7:
                _audioEngine.NoteOff(3);
                _timer = _gapDuration;
                break;
            case 8:
                Debug.Log("Testing II chord - Ninth");
                _audioEngine.NoteOn(4, 0.8f);
                _timer = _noteDuration;
                break;
            case 9:
                _audioEngine.NoteOff(4);
                _timer = _gapDuration;
                break;
            case 10:
                Debug.Log("Testing V chord (G7) - all tones");
                _audioEngine.SetChord(ChordType.V);
                _audioEngine.SetActiveTones(new bool[] { true, true, true, true, true }, 0.6f);
                _timer = 1f;
                break;
            case 11:
                _audioEngine.AllNotesOff();
                _timer = _gapDuration;
                break;
            case 12:
                Debug.Log("Testing I chord (Cmaj7) - all tones");
                _audioEngine.SetChord(ChordType.I);
                _audioEngine.SetActiveTones(new bool[] { true, true, true, true, true }, 0.6f);
                _timer = 1f;
                break;
            case 13:
                _audioEngine.AllNotesOff();
                _testing = false;
                Debug.Log("Audio engine test complete!");
                break;
        }
        _testStep++;
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Run Audio Test", GUILayout.Height(40)))
        {
            StartTest();
        }
        
        if (_audioEngine != null)
        {
            GUILayout.Label($"Current Chord: {_audioEngine.CurrentChord}");
            GUILayout.Label($"Active Tones: {string.Join(", ", _audioEngine.ActiveTones)}");
        }
    }
}