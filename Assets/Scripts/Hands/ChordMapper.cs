using UnityEngine;
using Oculus.Interaction.Input;

public class ChordMapper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LeftHandChordDetector _leftHandDetector;
    [SerializeField] private RightHandToneDetector _rightHandDetector;
    [SerializeField] private AudioEngine _audioEngine;

    [Header("Settings")]
    [SerializeField] private float _defaultVelocity = 0.7f;
    [SerializeField] private bool _useCurlForVelocity = true;

    private bool _leftHandValid;
    private bool _rightHandValid;

    private void Awake()
    {
        AutoFindReferences();
        SubscribeToEvents();
    }

    private void AutoFindReferences()
    {
        if (_leftHandDetector == null)
            _leftHandDetector = FindObjectOfType<LeftHandChordDetector>();

        if (_rightHandDetector == null)
            _rightHandDetector = FindObjectOfType<RightHandToneDetector>();

        if (_audioEngine == null)
            _audioEngine = FindObjectOfType<AudioEngine>();

        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (_leftHandDetector == null)
            Debug.LogError("ChordMapper: LeftHandChordDetector not found!");
        if (_rightHandDetector == null)
            Debug.LogError("ChordMapper: RightHandToneDetector not found!");
        if (_audioEngine == null)
            Debug.LogError("ChordMapper: AudioEngine not found!");
    }

    private void SubscribeToEvents()
    {
        if (_leftHandDetector != null)
        {
            _leftHandDetector.OnChordChanged += OnChordChanged;
            // Initialize with current chord
            OnChordChanged(_leftHandDetector.CurrentChord);
        }

        if (_rightHandDetector != null)
        {
            _rightHandDetector.OnToneChanged += OnToneChanged;
        }
    }

    private void OnDestroy()
    {
        if (_leftHandDetector != null)
            _leftHandDetector.OnChordChanged -= OnChordChanged;

        if (_rightHandDetector != null)
            _rightHandDetector.OnToneChanged -= OnToneChanged;
    }

    private void Update()
    {
        _leftHandValid = _leftHandDetector != null && _leftHandDetector.IsTracking;
        _rightHandValid = _rightHandDetector != null && _rightHandDetector.IsTracking;

        if (!_leftHandValid)
        {
            _audioEngine.AllNotesOff();
        }
    }

    private void OnChordChanged(ChordType chordType)
    {
        if (!_leftHandValid) return;

        _audioEngine.SetChord(chordType);
        Debug.Log($"ChordMapper: Chord changed to {chordType}");
    }

    private void OnToneChanged(int toneIndex, bool isActive, float curlValue)
    {
        if (!_rightHandValid) return;

        float velocity = _useCurlForVelocity 
            ? Mathf.Lerp(0.3f, 1f, curlValue) 
            : _defaultVelocity;

        if (isActive)
        {
            _audioEngine.NoteOn(toneIndex, velocity);
        }
        else
        {
            _audioEngine.NoteOff(toneIndex);
        }
    }

    private void OnValidate()
    {
        AutoFindReferences();
    }
}