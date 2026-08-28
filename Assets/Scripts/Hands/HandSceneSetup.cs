using UnityEngine;
using UnityEditor;
using Oculus.Interaction.PoseDetection;
using Oculus.Interaction.Input;

/// <summary>
/// Editor utility to auto-configure hand tracking components on scene GameObjects.
/// Run via: Tools > Hand Tracking > Auto-Configure Scene
/// </summary>
public class HandSceneSetup
{
    [MenuItem("Tools/Hand Tracking/Auto-Configure Scene")]
    public static void AutoConfigureScene()
    {
        // Find hand GameObjects
        var leftHand = FindHandGameObject("Left");
        var rightHand = FindHandGameObject("Right");

        if (leftHand == null || rightHand == null)
        {
            Debug.LogError("Could not find LeftHand/RightHand GameObjects. Make sure Building Blocks hands are in scene.");
            return;
        }

        // Configure Left Hand
        ConfigureLeftHand(leftHand);

        // Configure Right Hand
        ConfigureRightHand(rightHand);

        // Add ChordMapper to XR Origin or scene root
        ConfigureChordMapper();

        // Ensure SampleBank exists
        EnsureSampleBank();

        Debug.Log("Scene auto-configuration complete! Check SETUP_INSTRUCTIONS.md for recognizer configuration.");
    }

    private static GameObject FindHandGameObject(string handName)
    {
        // Try common naming patterns
        var patterns = new[]
        {
            handName + "Hand",
            handName + "HandAnchor",
            "Hand" + handName,
            "HandAnchor" + handName,
        };

        foreach (var pattern in patterns)
        {
            var go = GameObject.Find(pattern);
            if (go != null) return go;
        }

        // Try finding by IHand component
        var hands = Object.FindObjectsOfType<Oculus.Interaction.Input.Hand>();
        foreach (var hand in hands)
        {
            if (hand.Handedness.ToString().Contains(handName))
                return hand.gameObject;
        }

        return null;
    }

    private static void ConfigureLeftHand(GameObject leftHand)
    {
        // Add LeftHandChordDetector
        var leftDetector = leftHand.GetComponent<LeftHandChordDetector>();
        if (leftDetector == null)
            leftDetector = leftHand.AddComponent<LeftHandChordDetector>();

        // Add ShapeRecognizerActiveState components (3 - one per chord)
        var iiState = leftHand.AddComponent<ShapeRecognizerActiveState>();
        iiState.name = "II_RecognizerState";

        var vState = leftHand.AddComponent<ShapeRecognizerActiveState>();
        vState.name = "V_RecognizerState";

        var iState = leftHand.AddComponent<ShapeRecognizerActiveState>();
        iState.name = "I_RecognizerState";

        // Auto-assign if recognizers exist in Resources
        var recognizers = Resources.LoadAll<ShapeRecognizer>("PoseDetection");
        foreach (var rec in recognizers)
        {
            if (rec.name.Contains("II")) iiState.InjectShapes(new[] { rec });
            else if (rec.name.Contains("V")) vState.InjectShapes(new[] { rec });
            else if (rec.name.Contains("I")) iState.InjectShapes(new[] { rec });
        }

        // Assign finger state provider (need one on left hand too for shape recognition)
        var fingerProvider = leftHand.GetComponent<FingerFeatureStateProvider>();
        if (fingerProvider == null)
            fingerProvider = leftHand.AddComponent<FingerFeatureStateProvider>();

        // Inject finger provider into all shape states
        iiState.InjectFingerFeatureStateProvider(fingerProvider);
        vState.InjectFingerFeatureStateProvider(fingerProvider);
        iState.InjectFingerFeatureStateProvider(fingerProvider);

        // Assign to detector via public method
        leftDetector.SetRecognizers(
            leftHand.GetComponents<ShapeRecognizerActiveState>()[0],
            leftHand.GetComponents<ShapeRecognizerActiveState>()[1],
            leftHand.GetComponents<ShapeRecognizerActiveState>()[2]
        );

        Debug.Log($"Configured LeftHand: {leftHand.name}");
    }

    private static void ConfigureRightHand(GameObject rightHand)
    {
        // Add RightHandToneDetector
        var rightDetector = rightHand.GetComponent<RightHandToneDetector>();
        if (rightDetector == null)
            rightDetector = rightHand.AddComponent<RightHandToneDetector>();

        // Add FingerFeatureStateProvider
        var fingerProvider = rightHand.GetComponent<FingerFeatureStateProvider>();
        if (fingerProvider == null)
            fingerProvider = rightHand.AddComponent<FingerFeatureStateProvider>();

        // Configure finger state thresholds for curl detection
        ConfigureFingerThresholds(fingerProvider);

        // Assign to detector via public method
        rightDetector.SetFingerProvider(fingerProvider);

        Debug.Log($"Configured RightHand: {rightHand.name}");
    }

    private static void ConfigureFingerThresholds(FingerFeatureStateProvider provider)
    {
        // This requires serialized data setup - user needs to configure in Inspector
        // The provider needs FingerFeatureStateThresholds for each finger
        Debug.Log("Configure finger curl thresholds in FingerFeatureStateProvider Inspector:");
        Debug.Log("- Add 5 entries to Finger State Thresholds (one per finger)");
        Debug.Log("- Set Feature to 'Curl' for each");
        Debug.Log("- Configure thresholds: Low=0.3, High=0.6 (adjust as needed)");
    }

    private static void ConfigureChordMapper()
    {
        var xrOrigin = GameObject.Find("XR Origin");
        if (xrOrigin == null) xrOrigin = GameObject.Find("[BuildingBlock] Camera Rig");
        if (xrOrigin == null) xrOrigin = GameObject.Find("UnityXRComprehensiveInteractionRig");
        if (xrOrigin == null) xrOrigin = GameObject.Find("Camera Rig");

        if (xrOrigin == null)
        {
            Debug.LogWarning("Could not find XR Origin. Add ChordMapper manually to scene root.");
            return;
        }

        var mapper = xrOrigin.GetComponent<ChordMapper>();
        if (mapper == null)
            mapper = xrOrigin.AddComponent<ChordMapper>();

        Debug.Log($"Added ChordMapper to: {xrOrigin.name}");
    }

    private static void EnsureSampleBank()
    {
        var sampleBank = Object.FindObjectOfType<SampleBank>();
        if (sampleBank == null)
        {
            var go = new GameObject("SampleBank");
            go.AddComponent<SampleBank>();
            Debug.Log("Created SampleBank GameObject");
        }
    }
}