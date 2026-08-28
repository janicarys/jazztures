using UnityEngine;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;

public class LeftHandChordDetector : MonoBehaviour
{
    [Header("Shape Recognizer Active States (Left Hand Gestures)")]
    [SerializeField] private ShapeRecognizerActiveState _iiRecognizer;
    [SerializeField] private ShapeRecognizerActiveState _vRecognizer;
    [SerializeField] private ShapeRecognizerActiveState _iRecognizer;

    [Header("Hand Reference")]
    [SerializeField] private IHand _leftHand;

    public ChordType CurrentChord { get; private set; } = ChordType.II;
    public bool IsTracking => _leftHand != null && _leftHand.IsConnected;

    private void Awake()
    {
        if (_leftHand == null)
        {
            _leftHand = GetComponentInParent<IHand>();
        }
    }

    private void Start()
    {
        ValidateRecognizers();
    }

    private void ValidateRecognizers()
    {
        if (_iiRecognizer == null) Debug.LogError("LeftHandChordDetector: II recognizer not assigned!");
        if (_vRecognizer == null) Debug.LogError("LeftHandChordDetector: V recognizer not assigned!");
        if (_iRecognizer == null) Debug.LogError("LeftHandChordDetector: I recognizer not assigned!");
        if (_leftHand == null) Debug.LogError("LeftHandChordDetector: No IHand reference found!");
    }

    private void Update()
    {
        if (!IsTracking) return;

        ChordType detectedChord = DetectChord();
        if (detectedChord != CurrentChord)
        {
            CurrentChord = detectedChord;
            OnChordChanged?.Invoke(CurrentChord);
        }
    }

    private ChordType DetectChord()
    {
        if (_iiRecognizer != null && _iiRecognizer.Active)
            return ChordType.II;

        if (_vRecognizer != null && _vRecognizer.Active)
            return ChordType.V;

        if (_iRecognizer != null && _iRecognizer.Active)
            return ChordType.I;

        return ChordType.II; // Default
    }

    public event System.Action<ChordType> OnChordChanged;

    public void SetHand(IHand hand)
    {
        _leftHand = hand;
    }

    public void SetRecognizers(ShapeRecognizerActiveState ii, ShapeRecognizerActiveState v, ShapeRecognizerActiveState i)
    {
        _iiRecognizer = ii;
        _vRecognizer = v;
        _iRecognizer = i;
    }
}