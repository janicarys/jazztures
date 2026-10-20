using Jazztures.Core.Melody;
using UnityEngine;

namespace Jazztures.Config
{
    /// <summary>
    /// The single tuning asset for the right-hand melody (CLAUDE.md §3.3, §3.1) and the
    /// body-anchored touch-target rig (ADR-0015). `[TUNABLE]`, pilot-calibrated at M8;
    /// mirror any change in <c>Docs/CALIBRATION.md</c>.
    ///
    /// <para>
    /// The top group (geometry + anchor) is read live by the touch-target rig in
    /// <c>Presentation</c>. The bottom group mirrors the `[TUNABLE]` constants that still
    /// live in <c>Jazztures.Core.Melody</c> (<c>MelodyEngine</c>, <c>VelocityCurve</c>) —
    /// recorded here for the thesis and for M8 calibration; Core consumes its own
    /// constants until Phase 8 wires this asset to the parameterised overloads
    /// (see <c>Docs/CALIBRATION.md</c> §3.1). Keep the two in sync by hand.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Config/Melody", fileName = "MelodyConfig")]
    public sealed class MelodyConfig : ScriptableObject
    {
        [Header("Target geometry — consumed by the touch-target rig (§3.3)")]
        [Tooltip("Radius of each target's circular face, metres — selects scale degree + octave.")]
        [Min(0.001f)] [SerializeField] private float _targetRadiusMetres = 0.04f;

        [Tooltip("Total depth of each target along the approach axis, metres. Generous on " +
                 "purpose: mid-air VR gives no depth cue and haptics are ruled out, so Z is " +
                 "the axis nobody can aim (ADR-0018). ±half this from the face.")]
        [Min(0.001f)] [SerializeField] private float _targetDepthMetres = 0.15f;

        [Tooltip("Angle between adjacent scale-degree columns, measured at the shoulder " +
                 "pivot (ADR-0019). Chord spacing = 2·reach·sin(angle/2); it must clear " +
                 "2×radius plus tracking jitter with room to spare, or a finger reaching " +
                 "for one target clips its neighbours.")]
        [Range(4f, 40f)] [SerializeField] private float _columnAngleDegrees = 12f;

        [Tooltip("Spacing between the two octave rows, metres, centre to centre. The targets lie " +
                 "face up and are struck from above, so the rows are NEAR and FAR rather than " +
                 "stacked (ADR-0049): the near row (lower octave) sits half this closer than the " +
                 "arc radius, the far row (higher octave) half this farther. Costs reach — keep " +
                 "the far row (reach + half this) comfortably inside an arm's length.")]
        [Min(0.001f)] [SerializeField] private float _rowSpacingMetres = 0.11f;

        [Tooltip("Volume multiplier for the 'hovering' glow — a target lights this much " +
                 "bigger than its trigger volume as a fingertip nears, so aim is learnable.")]
        [Min(1f)] [SerializeField] private float _hoverScale = 1.6f;

        [Tooltip("Frames the touch-target binder keeps fingertip speed over — it fires on " +
                 "the peak, so a hand decelerating into a target still reports its approach.")]
        [Min(1)] [SerializeField] private int _speedSampleFrames = 3;

        [Header("Shoulder pivot — consumed by the touch-target rig (ADR-0015/0019)")]
        [Tooltip("Arc radius: the reach from the shoulder pivot to the middle of the two rows, " +
                 "metres. Within a row every column sits at the same distance, so the outer " +
                 "degrees are no further away than the centre and a glissando is one shoulder " +
                 "sweep. The targets are all at the pivot's height — raise or lower them with " +
                 "Shoulder Height Offset below.")]
        [Min(0.05f)] [SerializeField] private float _reachDistanceMetres = 0.45f;

        [Tooltip("Vertical offset of the shoulder pivot from head height, metres. Negative " +
                 "= below the head, toward the real shoulder.")]
        [SerializeField] private float _shoulderHeightOffsetMetres = -0.25f;

        [Tooltip("Lateral offset of the shoulder pivot from the body centreline, metres. " +
                 "Positive = right. Small on purpose (ADR-0020): on a Quest 2 the hand-" +
                 "tracking cone is narrower than §1.4's ~140°, so a big rightward offset " +
                 "makes the learner turn their head to see the melody arc and drop the " +
                 "left hand out of frame. Keep the arc mostly in front.")]
        [SerializeField] private float _shoulderLateralOffsetMetres = 0.08f;

        [Tooltip("Head-yaw divergence from the rig's facing before a recenter begins, degrees.")]
        [Range(0f, 90f)] [SerializeField] private float _recenterAngleDegrees = 35f;

        [Tooltip("How long the divergence must be sustained before the recenter starts, seconds.")]
        [Min(0f)] [SerializeField] private float _recenterDwellSeconds = 0.6f;

        [Tooltip("Time for the rig to ease to the new facing once a recenter starts, seconds.")]
        [Min(0.01f)] [SerializeField] private float _recenterEaseSeconds = 0.5f;

        [Header("Target look (ADR-0046)")]
        [Tooltip("Paddle: a flat pad on a faint rail, pushed along the approach axis and drawn at " +
                 "the trigger volume's own radius. Prefab: clone the rig's Target Prefab as-is " +
                 "(the rig's Target Prefab slot is ignored while this is Paddle).")]
        [SerializeField] private TargetLook _targetLook = TargetLook.Paddle;

        [Header("Touch — a tap inside the volume plays (ADR-0045)")]
        [Tooltip("Fingertip speed that counts as a TAP while the fingertip is already inside a " +
                 "target's volume, m/s. Much higher than the entry gate because there is no entry " +
                 "edge to show intent — a resting hand's tracking jitter must stay under it. " +
                 "Starts from the left-hand strike's tuned 0.25. Too high: a gentle tap does " +
                 "nothing. Too low: a resting hand fires notes. [TUNABLE]")]
        [Min(0f)] [SerializeField] private float _tapSpeedMetresPerSecond = 0.25f;

        [Tooltip("After a note, the fingertip must slow to this before another tap can play, m/s " +
                 "(hysteresis: must be below the tap speed). [TUNABLE]")]
        [Min(0f)] [SerializeField] private float _tapSettleSpeedMetresPerSecond = 0.10f;

        [Tooltip("Consecutive frames at or below the settle speed that re-arm a tap. [TUNABLE]")]
        [Min(1)] [SerializeField] private int _tapSettleFrames = 3;

        [Header("Finger bend — EXPLORATION, branch explore/finger-bend-keys only (ADR-0050)")]
        [Tooltip("How far a free finger must rise above its lowest recent point to count as a key " +
                 "press (0 flat .. 1 fully bent). A modest, key-sized bend — not a fist. Too high: " +
                 "presses are missed. Too low: ordinary movement sounds notes. [TUNABLE]")]
        [Range(0.05f, 1f)] [SerializeField] private float _bendPressDelta = FingerBendThresholds.DefaultPressDelta;

        [Tooltip("How far a pressed finger must fall back from its PEAK before it can press again " +
                 "(not from a resting level, so a hand that settles back a little more bent than it " +
                 "began still re-arms). Must be below the press level (hysteresis). Too high: a quick " +
                 "repeat on the same finger is missed. Too low: wobble at the peak can re-fire. [TUNABLE]")]
        [Range(0f, 0.9f)] [SerializeField] private float _bendReleaseDelta = FingerBendThresholds.DefaultReleaseDelta;

        [Tooltip("Seconds over which a finger's 'lowest recent point' forgets upward. A bend slower than " +
                 "about (press level ÷ this) per second counts as drift and never presses, so a SHORT " +
                 "value misses moderately slow presses (the first version, at 0.4 s, did) and a long " +
                 "one lets slower drifts through. [TUNABLE]")]
        [Min(0.05f)] [SerializeField] private float _bendBaselineSeconds = FingerBendThresholds.DefaultBaselineSeconds;

        [Tooltip("A finger presses only if it rose this much further than every other free finger. " +
                 "Stops a fist, or a neighbour dragged along by tendon coupling, from sounding " +
                 "notes. Raise it if neighbouring fingers fire; lower it if presses are ignored. [TUNABLE]")]
        [Min(0f)] [SerializeField] private float _bendIsolationMargin = FingerBendThresholds.DefaultIsolationMargin;

        [Tooltip("THUMB ONLY. Degrees of thumb curl that count as 1.0 of bend. The SDK's own thumb range " +
                 "is 30 degrees, because its grab test needs the thumb to move little — which makes the thumb " +
                 "about twice as sensitive as the other fingers (whose range is about 70 degrees but, blended " +
                 "with knuckle flexion, behaves like roughly 50-70). Larger = the thumb needs more curl to " +
                 "press. Too easy to trigger: raise this and Thumb Press Span. [TUNABLE]")]
        [Range(10f, 120f)] [SerializeField] private float _bendThumbCurlSpanDegrees = 60f;

        [Tooltip("THUMB ONLY. The thumb is pressed DOWN as often as it is curled in, and the SDK's " +
                 "curl feature only sees it curl in. So the thumb's bend also counts how far its tip has " +
                 "moved toward the palm side of the hand, in this many metres per unit of bend " +
                 "(smaller = more sensitive). 0 turns it off (curl only). NEGATIVE flips the direction — use " +
                 "it if the thumb sounds when you lift it instead of when you press it. Measured in the hand's own " +
                 "frame, so it works however the hand is turned. With JAZZTURES_DIAG the thumb's press " +
                 "log shows how many millimetres it moved. [TUNABLE]")]
        [Range(-0.2f, 0.2f)] [SerializeField] private float _bendThumbPressSpanMetres = 0.08f;

        [Header("Note labels — written on each target (ADR-0044)")]
        [Tooltip("Write the note each target currently plays (C, E, G, B, D …) on the target. " +
                 "Replaces colour-coding the chord: the targets stay one colour. Letters only — " +
                 "no octave numbers, which would be unexplained notation for a novice (§1.5).")]
        [SerializeField] private bool _showNoteLabels = true;

        [Tooltip("Size of the label letters (TextMesh character size); the height scales " +
                 "linearly with it, in world metres — independent of the target's own scale. " +
                 "0.005 is roughly a 3 cm capital by estimate, which fits the 6 cm pad. The " +
                 "first labels looked tiny because they inherited a 0.05-scaled sphere's scale " +
                 "(fixed in ADR-0046), not because this number was too small. Columns are " +
                 "~8 cm apart at the near row, so much above ~0.010 starts to crowd neighbours. [TUNABLE]")]
        [Min(0.001f)] [SerializeField] private float _labelCharacterSize = 0.005f;

        [Tooltip("Label colour at rest. The touch targets' letters keep it for good; the finger letters and " +
                 "the chord name dim it a little (Resting Brightness) so that lighting up stands out.")]
        [SerializeField] private Color _labelColor = new Color(0.96f, 0.96f, 0.98f, 1f);

        [Header("Label glow — a finger's letter or the chord name lights when it sounds")]
        [Tooltip("The colour a label lights to when its note or chord sounds. Warm, to match the strike " +
                 "flash. The same for both hands. Cosmetic - a look, not a behaviour.")]
        [SerializeField] private Color _labelLitColor = new Color(1f, 0.82f, 0.30f, 1f);

        [Tooltip("How bright a label rests, 1 = the full Label Colour. Below 1 so that lighting up is a " +
                 "visible change rather than white to slightly-less-white. Cosmetic.")]
        [Range(0.2f, 1f)] [SerializeField] private float _labelRestingBrightness = 0.72f;

        [Tooltip("Seconds a label takes to fade from lit back to resting. It holds bright for the first " +
                 "part, then eases off. Cosmetic.")]
        [Min(0.05f)] [SerializeField] private float _labelLitSeconds = 0.45f;

        [Tooltip("How much bigger a label swells at the peak of lighting up (0.35 = 35%). The swell is a " +
                 "second channel beside the colour, so the glow still reads if the hues are hard to tell " +
                 "apart (§3.10). 0 = colour only. Cosmetic.")]
        [Range(0f, 1f)] [SerializeField] private float _labelLitPop = 0.35f;

        [Header("Ghost letters — only the finger to play now or next stands out (ADR-0059)")]
        [Tooltip("How opaque the ghost right hand's letters are for the fingers that are neither pressing nor " +
                 "next, 1 = not faded. The finger to play stays at full strength, so which one it is reads " +
                 "from the fade as well as from the finger moving. Cosmetic.")]
        [Range(0.05f, 1f)] [SerializeField] private float _ghostQuietAlpha = 0.3f;

        [Tooltip("How big those faded letters are against the one to play, 1 = the same size. Size is a second " +
                 "channel beside the fade, so the cue still reads if the fade is hard to see against a bright " +
                 "background (§3.10). Cosmetic.")]
        [Range(0.3f, 1f)] [SerializeField] private float _ghostQuietScale = 0.8f;

        [Tooltip("Seconds a ghost letter takes to move between faded and full strength, so the cue slides from " +
                 "one finger to the next rather than flickering. Cosmetic.")]
        [Min(0.01f)] [SerializeField] private float _ghostCueSeconds = 0.15f;

        [Header("Note lanes — notes travel in along lanes out of the fingertips (ADR-0060, exploration)")]
        [Tooltip("Beats ahead the lanes show a note: a lane is this many beats long. [TUNABLE] - unmeasured.")]
        [Range(1f, 8f)] [SerializeField] private float _laneLookAheadBeats = 3f;

        [Tooltip("Metres a note travels per beat, so the spacing of the letters on a lane is the spacing of the " +
                 "notes in the score whatever the tempo. A lane is Look Ahead Beats x this long. [TUNABLE] - unmeasured.")]
        [Range(0.05f, 0.6f)] [SerializeField] private float _laneMetresPerBeat = 0.2f;

        [Tooltip("Beats a letter stays at the fingertip after it arrives, glowing, before it fades. Cosmetic.")]
        [Range(0f, 1.5f)] [SerializeField] private float _laneLingerBeats = 0.4f;

        [Tooltip("Width of a lane, metres. Cosmetic.")]
        [Range(0.001f, 0.02f)] [SerializeField] private float _laneWidthMetres = 0.004f;

        [Tooltip("Opacity of a lane with no note coming. Not zero, so the five lanes read as the five fingers " +
                 "even between notes. Cosmetic.")]
        [Range(0f, 1f)] [SerializeField] private float _laneIdleAlpha = 0.10f;

        [Tooltip("Opacity of a lane with a note on its way. Cosmetic.")]
        [Range(0f, 1f)] [SerializeField] private float _laneActiveAlpha = 0.45f;

        [Tooltip("How far above its lane a letter floats, metres, so the line does not strike through it. Cosmetic.")]
        [Range(0f, 0.08f)] [SerializeField] private float _laneLetterHoverMetres = 0.02f;

        [Header("Melody engine — mirrors Jazztures.Core.Melody consts, kept in sync by hand")]
        [Tooltip("Minimum fingertip speed to fire a note, m/s — 'was this a deliberate " +
                 "strike?'. Mirrors MelodyEngine.EntryVelocityGateMetresPerSecond. Distinct " +
                 "from the loudness curve's minimum speed (ADR-0018).")]
        [Min(0f)] [SerializeField] private float _entryVelocityGateMetresPerSecond = 0.08f;

        [Tooltip("Fingertip speed mapped to the maximum MIDI velocity, m/s. Mirrors VelocityCurve.MaxSpeed.")]
        [Min(0f)] [SerializeField] private float _velocityCurveMaxSpeedMetresPerSecond = 1.5f;

        [Tooltip("Per-target minimum interval between triggers, seconds. Mirrors MelodyEngine.RetriggerCooldownSeconds.")]
        [Min(0f)] [SerializeField] private float _retriggerCooldownSeconds = 0.080f;

        [Tooltip("MIDI velocity range from fingertip speed, then clamped. Mirrors VelocityCurve.MinVelocity/MaxVelocity.")]
        [Range(1, 127)] [SerializeField] private int _midiVelocityMin = 40;

        [Range(1, 127)] [SerializeField] private int _midiVelocityMax = 110;

        [Tooltip("Hard floor — no note-on is ever quieter than this. Mirrors VelocityCurve.AbsoluteFloor (§3.3).")]
        [Range(1, 127)] [SerializeField] private int _midiVelocityFloor = 30;

        [Tooltip("Fixed note length, seconds. [OPEN] — mirrors MelodyEngine.DefaultSustainSeconds.")]
        [Min(0.01f)] [SerializeField] private float _noteSustainSeconds = 0.5f;

        /// <summary>The recenter parameters for the touch-target rig (ADR-0015).</summary>
        public LazyRecenterSettings ToRecenterSettings() => LazyRecenterSettings.FromDegrees(
            _recenterAngleDegrees, _recenterDwellSeconds, _recenterEaseSeconds);

        public float TargetRadiusMetres => _targetRadiusMetres;

        public float TargetDepthMetres => _targetDepthMetres;

        public float ColumnAngleDegrees => _columnAngleDegrees;

        public float RowSpacingMetres => _rowSpacingMetres;

        public float HoverScale => _hoverScale;

        public int SpeedSampleFrames => _speedSampleFrames;

        public float ReachDistanceMetres => _reachDistanceMetres;

        public float ShoulderHeightOffsetMetres => _shoulderHeightOffsetMetres;

        public float ShoulderLateralOffsetMetres => _shoulderLateralOffsetMetres;

        public float RecenterAngleDegrees => _recenterAngleDegrees;

        public float RecenterDwellSeconds => _recenterDwellSeconds;

        public float RecenterEaseSeconds => _recenterEaseSeconds;

        /// <summary>
        /// The touch thresholds for <see cref="TouchContact"/>, kept consistent whatever is typed
        /// in the Inspector: the tap speed is never below the entry speed and the settle speed is
        /// always below the tap speed (hysteresis), so a bad value cannot throw at startup.
        /// </summary>
        public TouchContactThresholds ToTouchContactThresholds()
        {
            float entry = Mathf.Max(0f, _entryVelocityGateMetresPerSecond);
            float tap = Mathf.Max(entry, _tapSpeedMetresPerSecond);
            float settle = Mathf.Clamp(_tapSettleSpeedMetresPerSecond, 0f, Mathf.Max(0f, tap - 0.001f));
            return new TouchContactThresholds(entry, tap, settle, Mathf.Max(1, _tapSettleFrames));
        }

        /// <summary>
        /// The finger-bend levels (ADR-0050), kept consistent whatever is typed in the Inspector:
        /// the release level is always below the press level, so a bad value cannot throw at startup.
        /// </summary>
        public FingerBendThresholds ToFingerBendThresholds()
        {
            float press = Mathf.Clamp(_bendPressDelta, 0.05f, 1f);
            float release = Mathf.Clamp(_bendReleaseDelta, 0f, press - 0.01f);
            return new FingerBendThresholds(press, release, Mathf.Max(0.05f, _bendBaselineSeconds), Mathf.Max(0f, _bendIsolationMargin));
        }

        /// <summary>Degrees of thumb curl that count as 1.0 of thumb bend (ADR-0050).</summary>
        public float BendThumbCurlSpanDegrees => _bendThumbCurlSpanDegrees;

        /// <summary>How far the thumb tip travels toward the palm side per unit of thumb bend, metres; 0 = off (ADR-0050).</summary>
        public float BendThumbPressSpanMetres => _bendThumbPressSpanMetres;

        public TargetLook TargetLook => _targetLook;

        public bool ShowNoteLabels => _showNoteLabels;

        public float LabelCharacterSize => _labelCharacterSize;

        public Color LabelColor => _labelColor;

        public Color LabelLitColor => _labelLitColor;

        public float LabelRestingBrightness => _labelRestingBrightness;

        public float LabelLitSeconds => _labelLitSeconds;

        public float LabelLitPop => _labelLitPop;

        public float GhostQuietAlpha => _ghostQuietAlpha;

        public float GhostQuietScale => _ghostQuietScale;

        public float GhostCueSeconds => _ghostCueSeconds;

        public float LaneLookAheadBeats => _laneLookAheadBeats;

        public float LaneMetresPerBeat => _laneMetresPerBeat;

        public float LaneLingerBeats => _laneLingerBeats;

        public float LaneWidthMetres => _laneWidthMetres;

        public float LaneIdleAlpha => _laneIdleAlpha;

        public float LaneActiveAlpha => _laneActiveAlpha;

        public float LaneLetterHoverMetres => _laneLetterHoverMetres;

        public float EntryVelocityGateMetresPerSecond => _entryVelocityGateMetresPerSecond;

        public float VelocityCurveMaxSpeedMetresPerSecond => _velocityCurveMaxSpeedMetresPerSecond;

        public float RetriggerCooldownSeconds => _retriggerCooldownSeconds;

        public int MidiVelocityMin => _midiVelocityMin;

        public int MidiVelocityMax => _midiVelocityMax;

        public int MidiVelocityFloor => _midiVelocityFloor;

        public float NoteSustainSeconds => _noteSustainSeconds;
    }
}
