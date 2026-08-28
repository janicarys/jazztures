# Hand Detector Setup Instructions

## Overview
This document explains how to set up the left hand chord recognizers (3 gestures) and right hand tone detectors (5 finger curls) for the jazz chord mapping system.

---

## Quick Setup (Automated)

1. In Unity Editor: **Tools > Hand Tracking > Setup All Detectors**
2. This creates all required assets in `Assets/Resources/PoseDetection/`
3. Assign the created assets to the detector components in your scene

---

## Manual Setup (If Automated Fails)

### Left Hand: 3 ShapeRecognizers (Gestures → Chords)

Create 3 ShapeRecognizer assets via **Create > Interaction SDK > Pose Detection > Shape Recognizer**

| Asset Name | Gesture | Chord | Key Features |
|------------|---------|-------|--------------|
| `LeftHand_II_Recognizer` | Pointing Gun | ii (Dm7) | Index straight, Thumb extended, Middle/Ring/Pinky curled |
| `LeftHand_V_Recognizer` | Peace Sign | V (G7) | Index + Middle straight, Ring/Pinky curled, Thumb tucked |
| `LeftHand_I_Recognizer` | Open Palm | I (Cmaj7) | All 5 fingers straight, palm facing forward |

**Configure each ShapeRecognizer:**
1. Add **TransformFeatureStateProvider** for palm pose (position + rotation)
2. Add **FingerFeatureStateProvider** for each finger's curl state
3. Set thresholds for "Active" state

### Right Hand: 5 FingerFeatureStateProviders (Finger Curls → Tones)

Create 5 FingerFeatureStateProvider assets via **Create > Interaction SDK > Pose Detection > Finger Feature State Provider**

| Asset Name | Finger | Tone | Maps To |
|------------|--------|------|---------|
| `RightHand_IndexCurl` | Index | 0 (Root) | D3/G3/C3 |
| `RightHand_MiddleCurl` | Middle | 1 (Third) | F3/B3/E3 |
| `RightHand_RingCurl` | Ring | 2 (Fifth) | A3/D4/G3 |
| `RightHand_PinkyCurl` | Pinky | 3 (Seventh) | C4/F4/B3 |
| `RightHand_ThumbCurl` | Thumb | 4 (Ninth) | E4/A4/D4 |

**Configure each detector:**
- Feature: `Curl` (0 = straight, 1 = fully curled)
- Active threshold: ~0.6 (adjust based on testing)
- Inactive threshold: ~0.4 (hysteresis)

---

## Scene Setup

### 1. Add Detector Components

**Left Hand:**
- Add `LeftHandChordDetector` to LeftHand GameObject (or parent)
- Assign the 3 ShapeRecognizers in Inspector

**Right Hand:**
- Add `RightHandToneDetector` to RightHand GameObject (or parent)
- Assign the 5 FingerFeatureStateProviders in Inspector

### 2. Add ChordMapper
- Add `ChordMapper` to a GameObject in scene (e.g., XR Origin)
- Auto-finds detectors and AudioEngine
- Or manually assign in Inspector

### 3. Verify Hand References
- LeftHandChordDetector: `_leftHand` should auto-find IHand on same/parent object
- RightHandToneDetector: `_rightHand` should auto-find IHand
- Or manually drag LeftHand/RightHand GameObjects to the fields

---

## Testing in Editor

1. **Window > XR > Simulation** → Enable "Simulate Hands"
2. Press Play
3. Use hand simulator to test gestures:
   - Left hand: Pointing Gun → ii, Peace Sign → V, Open Palm → I
   - Right hand: Curl fingers individually to trigger tones
4. Check Console for:
   - `ChordMapper: Chord changed to II/V/I`
   - `ChordMapper: Tone changed` messages

---

## Testing on Quest 2

1. Build and Run to Quest 2
2. Use Meta XR Simulator App for hand tracking
3. Test gestures in VR:
   - Left hand gestures change chord
   - Right hand finger curls play chord tones
4. Adjust thresholds in Inspector if detection is unreliable

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Gesture not detected | Check ShapeRecognizer State in Inspector (should show "Active") |
| Finger curl not detected | Verify FingerFeatureStateProvider State, adjust thresholds |
| Wrong chord/note | Check ChordType enum mapping in AudioEngine |
| No audio | Verify AudioEngine and SampleBank are working |
| Hand not tracked | Ensure IHand components on hand GameObjects |

---

## Threshold Tuning

**Left Hand (ShapeRecognizers):**
- Start with default feature thresholds
- Increase if false positives, decrease if missed detections

**Right Hand (FingerFeatureStateProviders):**
- `Activation Threshold`: 0.55-0.65 (when finger considered "curled")
- `Release Threshold`: 0.35-0.45 (when finger considered "straight")
- Use hysteresis to prevent flickering

---

## File Locations

- Scripts: `Assets/Scripts/Hands/`
- Assets: `Assets/Resources/PoseDetection/`
- Setup Menu: `Tools > Hand Tracking > Setup All Detectors`

---

## Next Steps After Setup

1. Calibrate thresholds for your hands
2. Test chord transitions (II → V → I)
3. Test polyphonic right hand (multiple fingers curled)
4. Replace procedural samples with real piano samples
5. Add visual feedback (hand pose indicators)