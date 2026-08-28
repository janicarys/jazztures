using UnityEngine;
using UnityEditor;
using Oculus.Interaction.PoseDetection;
using Oculus.Interaction.Input;

/// <summary>
/// Editor utility to create and configure hand detection assets.
/// Run via: Tools > Hand Tracking > Setup Detectors
/// </summary>
public class HandDetectorSetup
{
    [MenuItem("Tools/Hand Tracking/Setup Left Hand Chord Recognizers")]
    public static void CreateLeftHandRecognizers()
    {
        string folder = "Assets/Resources/PoseDetection";
        System.IO.Directory.CreateDirectory(folder);

        // II Chord Recognizer - "Pointing Gun" (Index extended, thumb up, others curled)
        var iiRecognizer = CreateShapeRecognizer(folder, "LeftHand_II_Recognizer");

        // V Chord Recognizer - "Peace Sign" (Index + Middle extended, others curled)
        var vRecognizer = CreateShapeRecognizer(folder, "LeftHand_V_Recognizer");

        // I Chord Recognizer - "Open Palm" (All fingers extended)
        var iRecognizer = CreateShapeRecognizer(folder, "LeftHand_I_Recognizer");

        Debug.Log("Left hand chord recognizers created in " + folder);
        Debug.Log("Now add ShapeRecognizerActiveState components to LeftHand GameObject and assign these recognizers.");
    }

    [MenuItem("Tools/Hand Tracking/Setup Right Hand Tone Detector")]
    public static void CreateRightHandDetector()
    {
        Debug.Log("RightHandToneDetector uses a FingerFeatureStateProvider component (MonoBehaviour), not a ScriptableObject asset.");
        Debug.Log("Add FingerFeatureStateProvider component to RightHand GameObject and configure finger thresholds.");
    }

    [MenuItem("Tools/Hand Tracking/Setup All Detectors")]
    public static void SetupAllDetectors()
    {
        CreateLeftHandRecognizers();
        CreateRightHandDetector();
        Debug.Log("Setup complete. See SETUP_INSTRUCTIONS.md for next steps.");
    }

    private static ShapeRecognizer CreateShapeRecognizer(string folder, string name)
    {
        var recognizer = ScriptableObject.CreateInstance<ShapeRecognizer>();
        string path = $"{folder}/{name}.asset";
        AssetDatabase.CreateAsset(recognizer, path);
        AssetDatabase.SaveAssets();
        return recognizer;
    }
}