using UnityEngine;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using System.Collections.Generic;

public class RightHandToneDetector : MonoBehaviour
{
    [Header("Finger Feature State Provider (Right Hand)")]
    [SerializeField] private FingerFeatureStateProvider _fingerStateProvider;

    [Header("Hand Reference")]
    [SerializeField] private IHand _rightHand;

    [Header("Curl Thresholds")]
    [Range(0f, 1f)] [SerializeField] private float _activationThreshold = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float _releaseThreshold = 0.4f;

    public bool[] ActiveTones { get; private set; } = new bool[5];
    public float[] CurlValues { get; private set; } = new float[5];
    public bool IsTracking => _rightHand != null && _rightHand.IsConnected;

    private bool[] _prevActiveTones = new bool[5];

    private void Awake()
    {
        if (_rightHand == null)
        {
            _rightHand = GetComponentInParent<IHand>();
        }
    }

    private void Start()
    {
        ValidateDetectors();
    }

    private void ValidateDetectors()
    {
        if (_fingerStateProvider == null) Debug.LogError("RightHandToneDetector: FingerFeatureStateProvider not assigned!");
        if (_rightHand == null) Debug.LogError("RightHandToneDetector: No IHand reference found!");
    }

    private void Update()
    {
        if (!IsTracking) return;

        UpdateCurlValues();
        UpdateActiveTones();
        CheckForChanges();
    }

    private void UpdateCurlValues()
    {
        // FingerFeature.Curl = curl feature
        CurlValues[0] = GetCurlValue(HandFinger.Index);   // Root (tone 0)
        CurlValues[1] = GetCurlValue(HandFinger.Middle);  // Third (tone 1)
        CurlValues[2] = GetCurlValue(HandFinger.Ring);    // Fifth (tone 2)
        CurlValues[3] = GetCurlValue(HandFinger.Pinky);   // Seventh (tone 3)
        CurlValues[4] = GetCurlValue(HandFinger.Thumb);   // Ninth (tone 4)
    }

    private float GetCurlValue(HandFinger finger)
    {
        if (_fingerStateProvider == null) return 0f;
        
        // Get the curl feature value (0 = straight, 1 = fully curled)
        float? value = _fingerStateProvider.GetFeatureValue(finger, FingerFeature.Curl);
        return value ?? 0f;
    }

    private void UpdateActiveTones()
    {
        for (int i = 0; i < 5; i++)
        {
            float curl = CurlValues[i];
            bool wasActive = ActiveTones[i];

            if (!wasActive && curl >= _activationThreshold)
            {
                ActiveTones[i] = true;
            }
            else if (wasActive && curl <= _releaseThreshold)
            {
                ActiveTones[i] = false;
            }
        }
    }

    private void CheckForChanges()
    {
        for (int i = 0; i < 5; i++)
        {
            if (ActiveTones[i] != _prevActiveTones[i])
            {
                OnToneChanged?.Invoke(i, ActiveTones[i], CurlValues[i]);
                _prevActiveTones[i] = ActiveTones[i];
            }
        }
    }

    public event System.Action<int, bool, float> OnToneChanged; // toneIndex, isActive, curlValue

    public void SetHand(IHand hand)
    {
        _rightHand = hand;
    }

    public void SetFingerProvider(FingerFeatureStateProvider provider)
    {
        _fingerStateProvider = provider;
    }

    public int GetActiveToneCount()
    {
        int count = 0;
        for (int i = 0; i < 5; i++)
        {
            if (ActiveTones[i]) count++;
        }
        return count;
    }

    public bool[] GetActiveTonesCopy()
    {
        bool[] copy = new bool[5];
        System.Array.Copy(ActiveTones, copy, 5);
        return copy;
    }
}