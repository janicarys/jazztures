using Jazztures.Config;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Events;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.HandGrab.Visuals;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The ghost-hand demonstration renderer (ADR-0012) — the M6 replacement for the text
    /// prompt. A pure subscriber to <see cref="GhostFrameChannel"/> (§2.3): it never
    /// raises anything, it just shows what the lesson is asking for.
    ///
    /// <list type="bullet">
    ///   <item><b>Left hand</b> — a translucent articulated mesh (<see cref="HandPuppet"/>)
    ///   superimposed on the learner's own wrist, morphing between the ii / V / I target
    ///   poses. The fingers show the delta between where the hand is and where it should be.</item>
    ///   <item><b>Left-hand strike</b> (ADR-0052) — between chords the ghost lifts off the learner's
    ///   wrist, dips onto the strike pad and flashes it, then returns. A strike is a movement, not a
    ///   pose, so it is described by <see cref="GhostFrame.StrikeProgress"/> rather than recorded.</item>
    ///   <item><b>Chord name</b> (ADR-0058) — the name of the chord the ghost is posing floats above it, as the
    ///   learner's own left hand's does (<see cref="ChordNameLabel"/>), and lights up as the ghost's strike lands.</item>
    ///   <item><b>Right hand</b> — no ghost fingertip. The demonstrated melody stop lights
    ///   (<see cref="TouchTarget.SetDemonstrated"/>); the learner chooses the reach.</item>
    /// </list>
    ///
    /// <para>
    /// Visible only in ghost-hand modes — the runner sends <see cref="GhostFrame.Hidden"/>
    /// in Test Yourself / Compose (<c>ModePolicy.GhostHandsVisible</c>).
    /// </para>
    /// </summary>
    public sealed class GhostHandView : MonoBehaviour
    {
        [Header("Channel")]
        [SerializeField] private GhostFrameChannel _ghostChannel;

        [Header("Left-hand ghost")]
        [Tooltip("HandPuppet on the ghost mesh — sibling of OVRLeftHandVisual under HandVisualsLeft.")]
        [SerializeField] private HandPuppet _puppet;

        [Tooltip("The left Interaction SDK Hand — the ghost sits at the learner's own wrist (ADR-0012).")]
        [SerializeField] private MonoBehaviour _leftHand;

        [Tooltip("The centre-eye / head transform. The demonstrated wrist orientation is " +
                 "relative to head yaw (§3.4), so ii vs I reads correctly however the learner turns.")]
        [SerializeField] private Transform _head;

        [Tooltip("Beats to morph between poses. ADR-0012: the ghost conveys the movement, so ease, never snap.")]
        [Min(0.1f)] [SerializeField] private float _morphBeats = 1.5f;

        [Header("Target poses (authored via Jazztures ▸ Ghost Pose Recorder)")]
        [SerializeField] private GhostPoseAsset _ii;
        [SerializeField] private GhostPoseAsset _v;
        [SerializeField] private GhostPoseAsset _i;

        [Header("Left-hand strike (ADR-0052)")]
        [Tooltip("The strike pad. The ghost hand dips onto its top face and flashes it on contact - the same " +
                 "flash a real hit gives. Optional: without it the ghost dips straight down instead.")]
        [SerializeField] private ChordStrikeTarget _strikePad;

        [Tooltip("StrikePad: the ghost hovers above the strike pad, ready to strike, wherever the learner's " +
                 "hand is - so a strike is a straight drop, and the ghost also shows where to hold the hand. " +
                 "Wrist: the ghost is superimposed on the learner's own wrist (ADR-0012) and a strike runs " +
                 "from there to the pad, at an angle unless the hand is right above it. Falls back to Wrist " +
                 "when no pad is assigned.")]
        [SerializeField] private GhostAnchor _anchor = GhostAnchor.StrikePad;

        [Tooltip("StrikePad only: how high above the pad's top face the ghost's wrist hovers while ready, " +
                 "metres. The strike drops from here, so this is also the length of the strike.")]
        [Min(0f)] [SerializeField] private float _stationHeightMetres = 0.18f;

        [Tooltip("StrikePad only: shifts the hovering ghost and its contact point together, metres, in the " +
                 "head's heading frame (x right, y up, z forward). The pad's centre is where the wrist goes; " +
                 "pull the hand back (negative z) if its fingers overshoot the pad.")]
        [SerializeField] private Vector3 _stationOffsetMetres = Vector3.zero;

        [Tooltip("How far above the pad's top face the ghost's wrist stops at contact, metres. A hand's wrist " +
                 "sits a few centimetres above its lowest edge (the pinky side, the knuckles, the palm), " +
                 "so this makes that edge meet the pad.")]
        [Min(0f)] [SerializeField] private float _contactHeightMetres = 0.05f;

        [Tooltip("How far down the ghost dips when there is no pad assigned, metres.")]
        [Min(0f)] [SerializeField] private float _fallbackDipMetres = 0.12f;

        [Header("Chord name label (ADR-0058)")]
        [Tooltip("Label size and colours come from here - the same settings as the learner's own chord name. " +
                 "Left empty, the built-in defaults apply. Jazztures ▸ Labels ▸ Set Up Ghost Hand Labels fills it in.")]
        [SerializeField] private MelodyConfig _config;

        [Header("Right-hand demo lights")]
        [Tooltip("Optional. The touch-target rig whose stops light while the demo plays a note.")]
        [SerializeField] private TouchTargetRig _rig;

        private IHand _hand;
        private Renderer[] _ghostRenderers;
        private HandPose _previous;
        private HandPose _target;
        private HandPose _scratch;
        private Quaternion _previousWrist = Quaternion.identity;
        private Quaternion _targetWrist = Quaternion.identity;
        private bool _haveTargetWrist;
        private ChordFunction? _lastPose;
        private bool _seededPose;
        private double _morphStartBeat;
        private float _morphT;
        private int _litIndex = -1;
        private bool _ghostVisible;
        private float _strikeProgress;
        private int _strikesSeen;

        // How the chord name is drawn, not how anything behaves - cosmetic, so not [TUNABLE]. As ChordNameLabel.
        private const float NameHoverAboveHandMetres = 0.11f;
        private const float NameSizeVersusLetter = 1.25f;

        private GhostLabel _chordName;
        private Transform _nameAnchor;
        private ChordFunction? _namedFor;
        private bool _hasName;

        private void Awake()
        {
            _hand = _leftHand as IHand;
            _previous = new HandPose(Handedness.Left);
            _target = new HandPose(Handedness.Left);
            _scratch = new HandPose(Handedness.Left);

            if (_puppet != null)
            {
                _ghostRenderers = _puppet.GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            if (_puppet == null)
            {
                Debug.LogError($"{nameof(GhostHandView)}: assign {nameof(_puppet)} (the ghost HandPuppet).", this);
                enabled = false;
                return;
            }

            if (_hand == null)
            {
                Debug.LogWarning(
                    $"{nameof(GhostHandView)}: no left {nameof(IHand)} — the ghost will morph in place " +
                    "but won't ride the learner's wrist (ADR-0012 superimposition).", this);
            }

            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform; // the recorder uses Camera.main too — keep them the same
            }

            if (_head == null)
            {
                Debug.LogWarning(
                    $"{nameof(GhostHandView)}: no head transform and no Camera.main — the demonstrated " +
                    "wrist orientation can't be shown, so ii and I will look identical (ADR-0014).", this);
            }

            _chordName = new GhostLabel(transform, "Ghost Chord Name", _config, NameSizeVersusLetter);
            _nameAnchor = FindJoint(HandJointId.HandMiddle1); // the middle knuckle: the middle of the hand, as the real label uses
            if (_nameAnchor == null)
            {
                _nameAnchor = _puppet.transform;
            }

            SetGhostVisible(false);
        }

        private Transform FindJoint(HandJointId id)
        {
            foreach (HandJointMap map in _puppet.JointMaps)
            {
                if (map.id == id)
                {
                    return map.transform;
                }
            }

            return null;
        }

        private void OnEnable() => _ghostChannel?.Register(OnGhost);

        private void OnDisable()
        {
            _ghostChannel?.Unregister(OnGhost);
            ClearDemoLight();
        }

        private void OnGhost(GhostFrame frame)
        {
            ShowFrame(frame);
            UpdateChordName(frame); // after the pose: the name hangs off the hand where it now is
        }

        /// <summary>
        /// Name the chord the ghost is posing, over its hand. Shown while the ghost is and it holds a pose; the
        /// glow is lit by <see cref="FollowStrike"/> as the ghost's strike lands.
        /// </summary>
        private void UpdateChordName(GhostFrame frame)
        {
            if (!_hasName || frame.DemonstratedPose != _namedFor)
            {
                _namedFor = frame.DemonstratedPose;
                _hasName = true;
                _chordName.Text = _namedFor is { } function ? Progression.ChordFor(function).ToString() : string.Empty;
            }

            Vector3 viewer = _head != null ? _head.position : Vector3.zero;
            _chordName.Place(
                frame.Visible,
                _nameAnchor.position + Vector3.up * NameHoverAboveHandMetres,
                viewer,
                Time.deltaTime);
        }

        private void ShowFrame(GhostFrame frame)
        {
            SetGhostVisible(frame.Visible);

            if (!frame.Visible)
            {
                ClearDemoLight();
                _chordName.Reset();
                _strikeProgress = 0f;
                _strikesSeen = 0;
                return;
            }

            LightTarget(frame.LitTargetIndex);
            FollowStrike(frame);

            GhostPoseAsset wantedAsset = AssetFor(frame.DemonstratedPose);
            HandPose wanted = wantedAsset != null ? wantedAsset.Pose : null;
            if (wanted == null)
            {
                // null = "relaxed hand" (GhostFrame doc). No relaxed pose authored yet —
                // hold the last shape rather than snapping to identity.
                // TODO(OPEN): author a relaxed ghost pose.
                ApplyScratch();
                return;
            }

            if (!_seededPose)
            {
                _previous.CopyFrom(wanted);
                _target.CopyFrom(wanted);
                _scratch.CopyFrom(wanted);
                _previousWrist = _targetWrist = WristFor(wantedAsset);
                _haveTargetWrist = wantedAsset.HasWrist;
                _lastPose = frame.DemonstratedPose;
                _morphStartBeat = frame.PoseChangedAtBeat;
                _seededPose = true;
            }
            else if (frame.DemonstratedPose != _lastPose)
            {
                _previous.CopyFrom(_scratch);        // morph on from where we visually are
                _target.CopyFrom(wanted);
                _previousWrist = _haveTargetWrist
                    ? Quaternion.SlerpUnclamped(_previousWrist, _targetWrist, _morphT)
                    : _targetWrist;
                _targetWrist = WristFor(wantedAsset);
                _haveTargetWrist = wantedAsset.HasWrist;
                _lastPose = frame.DemonstratedPose;
                _morphStartBeat = frame.PoseChangedAtBeat;
            }

            _morphT = _morphBeats > 0f
                ? Mathf.Clamp01((float)((frame.Beat - _morphStartBeat) / _morphBeats))
                : 1f;
            HandPose.Lerp(_previous, _target, _morphT, ref _scratch);
            ApplyScratch();
        }

        /// <summary>
        /// Remember how far the ghost is through a strike, and flash the pad each time one lands. The
        /// frame carries a running count rather than a one-frame flag, so a skipped frame cannot lose a hit.
        /// </summary>
        private void FollowStrike(GhostFrame frame)
        {
            _strikeProgress = (float)frame.StrikeProgress;

            if (frame.StrikeCount > _strikesSeen)
            {
                if (_strikePad != null)
                {
                    _strikePad.Flash();
                }

                _chordName.Flash(); // the chord's name lights as the ghost's hand lands: its chord sounds
            }

            _strikesSeen = frame.StrikeCount; // also resyncs downward when a new phrase restarts the count
        }

        private void ApplyScratch()
        {
            if (_anchor == GhostAnchor.StrikePad && _strikePad != null)
            {
                PlaceAtStation();
            }
            else if (_hand != null && _hand.GetRootPose(out Pose wrist))
            {
                // Position rides the learner's wrist (superimposition); orientation is the
                // demonstrated one, re-expressed in world via current head yaw (§3.4). That
                // difference is what shows ii vs I — same finger shape, wrist turned.
                Quaternion rotation = wrist.rotation;
                if (_haveTargetWrist && _head != null)
                {
                    rotation = YawOnly(_head) * Quaternion.SlerpUnclamped(_previousWrist, _targetWrist, _morphT);
                }

                // The strike: lift off the learner's wrist toward the pad and back. At rest
                // (progress 0) the ghost is exactly where it always was - on the wrist.
                Vector3 position = wrist.position;
                if (_strikeProgress > 0f)
                {
                    Vector3 contact = _strikePad != null
                        ? _strikePad.TopCenterWorld + Vector3.up * _contactHeightMetres
                        : wrist.position + Vector3.down * _fallbackDipMetres;
                    position = Vector3.Lerp(wrist.position, contact, _strikeProgress);
                }

                _puppet.SetRootPose(new Pose(position, rotation));
            }

            _puppet.SetJointRotations(_scratch.JointRotations);
        }

        /// <summary>
        /// ADR-0052: hold the ghost above the pad, in the demonstrated orientation, and drop it straight
        /// onto the pad by <see cref="_strikeProgress"/>. Independent of the learner's hand, so it neither
        /// jumps nor vanishes when that hand is lost, and it follows the pad as the pad follows the body.
        /// </summary>
        private void PlaceAtStation()
        {
            Quaternion yaw = _head != null ? YawOnly(_head) : Quaternion.identity;
            Quaternion rotation = _haveTargetWrist
                ? yaw * Quaternion.SlerpUnclamped(_previousWrist, _targetWrist, _morphT)
                : yaw;

            Vector3 top = _strikePad.TopCenterWorld + yaw * _stationOffsetMetres;
            Vector3 ready = top + Vector3.up * _stationHeightMetres;
            Vector3 contact = top + Vector3.up * _contactHeightMetres;

            _puppet.SetRootPose(new Pose(Vector3.Lerp(ready, contact, _strikeProgress), rotation));
        }

        private GhostPoseAsset AssetFor(ChordFunction? function) => function switch
        {
            ChordFunction.Two => _ii,
            ChordFunction.Five => _v,
            ChordFunction.One => _i,
            _ => null,
        };

        private static Quaternion WristFor(GhostPoseAsset asset) =>
            asset != null && asset.HasWrist ? asset.WristRotationHeadLocal : Quaternion.identity;

        /// <summary>
        /// The head's horizontal facing as a rotation about world up — pitch and roll
        /// dropped. From the flattened forward vector, not <c>eulerAngles.y</c> (which is
        /// ambiguous once the head tilts). Recorder and renderer must agree on this so the
        /// captured wrist orientation round-trips (§3.4).
        /// </summary>
        public static Quaternion YawOnly(Transform head)
        {
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = head.up; // looking straight up/down — fall back to the up vector's heading
                forward.y = 0f;
                if (forward.sqrMagnitude < 1e-6f)
                {
                    return Quaternion.identity;
                }
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void LightTarget(int index)
        {
            if (_rig == null || index == _litIndex)
            {
                return;
            }

            var targets = _rig.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].SetDemonstrated(targets[i].Index == index);
            }

            _litIndex = index;
        }

        private void ClearDemoLight() => LightTarget(-1);

        private void SetGhostVisible(bool visible)
        {
            if (_ghostVisible == visible || _ghostRenderers == null)
            {
                return;
            }

            _ghostVisible = visible;
            foreach (Renderer r in _ghostRenderers)
            {
                if (r != null)
                {
                    r.enabled = visible;
                }
            }
        }
    }
}
