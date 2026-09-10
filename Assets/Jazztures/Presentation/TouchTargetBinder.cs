using Jazztures.Config;
using Jazztures.Core.Harmony;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using Jazztures.Events;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// Connects the touch-target rig to the domain (CLAUDE.md §3.3, §3.10). Listens on
    /// <see cref="ChordChangedChannel"/> and re-pitches the ten targets in place on every
    /// change ("re-pitched, never re-arranged", §3.1), lighting them briefly. Each frame it
    /// reads every tracked right-hand fingertip and, when one enters a target's volume,
    /// calls <see cref="MelodyEngine.TriggerTarget"/> with the fingertip's peak recent
    /// speed — the engine owns the entry gate, the retrigger cooldown and the
    /// speed→velocity curve. Targets a fingertip is merely *near* glow, so the learner can
    /// learn the depth (ADR-0018).
    ///
    /// <para>Direction rule (§2.3): this only reads the channel; it never raises it.</para>
    /// </summary>
    public sealed class TouchTargetBinder : MonoBehaviour
    {
        [SerializeField] private TouchTargetRig _rig;

        [SerializeField] private ChordChangedChannel _chordChanged;

        [Tooltip("Tuning for the hover volume and the speed window (§3.3, ADR-0018).")]
        [SerializeField] private MelodyConfig _config;

        [Tooltip("The right Interaction SDK Hand component.")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Tooltip("Right-hand fingertips that can strike a target. Index + middle only " +
                 "(ADR-0019): spare fingers clip neighbouring targets when reaching for " +
                 "one. Drop to index alone if the middle finger still causes strays.")]
        [SerializeField] private HandJointId[] _fingerTips =
        {
            HandJointId.HandIndexTip,
            HandJointId.HandMiddleTip,
        };

        [Tooltip("How long targets stay lit after a chord change, seconds.")]
        [Min(0f)] [SerializeField] private float _highlightSeconds = 0.6f;

        [Header("Pinch spike — temporary, ADR-0022 de-risk (see the plan)")]
        [Tooltip("Trial the pinch mechanic instead of volume entry: the nearest stop to the " +
                 "thumb/index midpoint is 'armed' (glows); a pinch sounds it; a held pinch " +
                 "swept across the arc plays a glissando. On-device evaluation only.")]
        [SerializeField] private bool _pinchSpike;

        [Tooltip("Pinch spike: max distance from the thumb/index midpoint to a stop for it to arm, metres.")]
        [Min(0.02f)] [SerializeField] private float _pinchSelectRadiusMetres = 0.18f;

        private const float DefaultHoverScale = 1.6f;
        private const int DefaultSpeedSampleFrames = 3;

        private MelodyEngine _melody;
        private IHand _hand;

        private bool[,] _wasInside;          // [finger, target] — entry-edge state
        private Vector3[] _previousTips;
        private bool[] _hasPreviousTip;
        private float[,] _speedSamples;      // [finger, frame] — rolling window
        private int _speedCursor;
        private bool[] _nearThisFrame;       // [target] — any finger hovering, this frame
        private bool[] _insideThisFrame;     // [target] — any finger inside, this frame
        private float[] _depthThisFrame;     // [target] — local-Z of a finger inside, for the depth disc

        private float _hoverScale = DefaultHoverScale;
        private int _speedSampleFrames = DefaultSpeedSampleFrames;
        private float _highlightUntil;

        // Pinch spike state (temporary).
        private bool _wasPinching;
        private int _armedIndex = -1;
        private Vector3 _prevSelectPoint;
        private bool _hasPrevSelectPoint;
        private readonly float[] _pinchSpeed = new float[3];
        private int _pinchSpeedCursor;
        private int _spikeNoteCount;
        private int _spikeLowConfFrames;

        /// <summary>Wire the domain up. Call once, from the composition root's <c>Awake</c>.</summary>
        public void Bind(MelodyEngine melody) => _melody = melody;

        private void Awake()
        {
            if (_rig == null || _chordChanged == null)
            {
                Debug.LogError($"{nameof(TouchTargetBinder)}: assign {nameof(_rig)} and {nameof(_chordChanged)}.", this);
                enabled = false;
                return;
            }

            if (_fingerTips == null || _fingerTips.Length == 0)
            {
                _fingerTips = new[] { HandJointId.HandIndexTip };
            }

            ValidateFingerTips();

            if (_config != null)
            {
                _hoverScale = Mathf.Max(1f, _config.HoverScale);
                _speedSampleFrames = Mathf.Max(1, _config.SpeedSampleFrames);
            }

            int fingers = _fingerTips.Length;
            _wasInside = new bool[fingers, ChordToneSet.TargetCount];
            _previousTips = new Vector3[fingers];
            _hasPreviousTip = new bool[fingers];
            _speedSamples = new float[fingers, _speedSampleFrames];
            _nearThisFrame = new bool[ChordToneSet.TargetCount];
            _insideThisFrame = new bool[ChordToneSet.TargetCount];
            _depthThisFrame = new float[ChordToneSet.TargetCount];

            _hand = _rightHand as IHand;
            if (_hand == null)
            {
                Debug.LogError(
                    $"{nameof(TouchTargetBinder)}: '{(_rightHand == null ? "<none>" : _rightHand.GetType().Name)}' " +
                    $"is not an {nameof(IHand)}.", this);
            }
        }

        private void OnEnable()
        {
            if (_chordChanged != null)
            {
                _chordChanged.Register(OnChordChanged);
            }

            // The melody instrument is not part of the selector menu — the lesson flow
            // disables this binder there, which also hides the ten markers.
            _rig?.SetTargetsVisible(true);
        }

        private void OnDisable()
        {
            if (_chordChanged != null)
            {
                _chordChanged.Unregister(OnChordChanged);
            }

            _rig?.SetTargetsVisible(false);

            if (_pinchSpike && _spikeNoteCount > 0)
            {
                Debug.Log(
                    $"[PinchSpike] session end: {_spikeNoteCount} notes, {_spikeLowConfFrames} low-confidence frames.");
            }
        }

        private void Update()
        {
            if (_highlightUntil > 0f && Time.time >= _highlightUntil)
            {
                _highlightUntil = 0f;
                SetHighlight(false);
            }
        }

        private void LateUpdate()
        {
            if (_pinchSpike)
            {
                PinchSpikeLateUpdate();
                return;
            }

            // LateUpdate, and TouchTargetRig runs at DefaultExecutionOrder(-10), so the rig
            // has re-anchored the targets before this hit test reads their transforms.
            if (_hand == null || !_hand.IsTrackedDataValid)
            {
                return;
            }

            var targets = _rig.Targets;
            float dt = Time.deltaTime;

            for (int t = 0; t < _nearThisFrame.Length; t++)
            {
                _nearThisFrame[t] = false;
                _insideThisFrame[t] = false;
            }

            _speedCursor = (_speedCursor + 1) % _speedSampleFrames;

            for (int f = 0; f < _fingerTips.Length; f++)
            {
                if (!_hand.GetJointPose(_fingerTips[f], out Pose pose))
                {
                    _hasPreviousTip[f] = false;
                    _speedSamples[f, _speedCursor] = 0f;
                    continue;
                }

                Vector3 tip = pose.position;
                float instant = _hasPreviousTip[f] && dt > 0f
                    ? Vector3.Distance(tip, _previousTips[f]) / dt
                    : 0f;
                _previousTips[f] = tip;
                _hasPreviousTip[f] = true;
                _speedSamples[f, _speedCursor] = instant;

                float peak = 0f;
                for (int s = 0; s < _speedSampleFrames; s++)
                {
                    if (_speedSamples[f, s] > peak)
                    {
                        peak = _speedSamples[f, s];
                    }
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    TouchTarget target = targets[i];
                    if (!target.IsSounding)
                    {
                        _wasInside[f, target.Index] = false;
                        continue;
                    }

                    // One inverse-transform per (finger, target); containment, hover and
                    // the depth readout all test the same local point.
                    Vector3 local = target.ToLocal(tip);
                    bool inside = target.ContainsLocal(local);

                    if (inside && !_wasInside[f, target.Index])
                    {
                        if (_melody != null && _melody.TriggerTarget(target.Index, peak))
                        {
                            target.Strike();
                        }
                    }

                    _wasInside[f, target.Index] = inside;

                    if (inside)
                    {
                        _insideThisFrame[target.Index] = true;
                        _depthThisFrame[target.Index] = local.z;
                    }
                    else if (target.ContainsLocal(local, _hoverScale))
                    {
                        _nearThisFrame[target.Index] = true;
                    }
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                TouchTarget target = targets[i];
                target.SetHovered(_nearThisFrame[target.Index]);
                target.SetFingerDepth(_insideThisFrame[target.Index], _depthThisFrame[target.Index]);
            }
        }

        private void OnChordChanged(ChordChange change)
        {
            var targets = _rig.Targets;

            if (change.CurrentChord is { } chord)
            {
                // The melody engine already recomputed this from the same event — it
                // subscribes first (PerformanceCompositionRoot) — so the pitches shown are
                // exactly the pitches TriggerTarget will sound. Build directly only when
                // there is no engine bound (isolated tests).
                ChordToneSet set = _melody?.ActiveChordToneSet ?? ChordToneSet.For(chord);
                for (int i = 0; i < targets.Count; i++)
                {
                    TouchTarget target = targets[i];
                    target.RePitch(set[target.Index]);
                    target.SetHighlighted(true);
                }

                _highlightUntil = Time.time + _highlightSeconds;
            }
            else
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    targets[i].Clear();
                    targets[i].SetHovered(false);
                }

                _highlightUntil = 0f;
            }
        }

        private void SetHighlight(bool on)
        {
            var targets = _rig.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].SetHighlighted(on);
            }
        }

        // ---- Pinch spike (temporary; ADR-0022 de-risk) -------------------------------
        // The nearest sounding stop to the thumb/index midpoint is "armed" (reuses the
        // hover glow). A pinch rising edge sounds it; while the pinch is held, sliding to a
        // new nearest stop re-fires (glissando). The engine's 80 ms per-target cooldown
        // debounces the slide. Low finger-tracking confidence suppresses all fires (§3.5
        // analog). Movement without a pinch never sounds a note.
        private void PinchSpikeLateUpdate()
        {
            if (_hand == null || !_hand.IsTrackedDataValid)
            {
                return;
            }

            if (!_hand.GetJointPose(HandJointId.HandThumbTip, out Pose thumb) ||
                !_hand.GetJointPose(HandJointId.HandIndexTip, out Pose index))
            {
                return;
            }

            Vector3 point = (thumb.position + index.position) * 0.5f;

            float dt = Time.deltaTime;
            float instant = _hasPrevSelectPoint && dt > 0f
                ? Vector3.Distance(point, _prevSelectPoint) / dt
                : 0f;
            _prevSelectPoint = point;
            _hasPrevSelectPoint = true;
            _pinchSpeed[_pinchSpeedCursor] = instant;
            _pinchSpeedCursor = (_pinchSpeedCursor + 1) % _pinchSpeed.Length;
            float peakSpeed = 0f;
            for (int i = 0; i < _pinchSpeed.Length; i++)
            {
                if (_pinchSpeed[i] > peakSpeed)
                {
                    peakSpeed = _pinchSpeed[i];
                }
            }

            var targets = _rig.Targets;
            int nearest = -1;
            float nearestSqr = _pinchSelectRadiusMetres * _pinchSelectRadiusMetres;
            for (int i = 0; i < targets.Count; i++)
            {
                if (!targets[i].IsSounding)
                {
                    continue;
                }

                float sqr = (targets[i].transform.position - point).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = i;
                }
            }

            if (nearest != _armedIndex)
            {
                if (_armedIndex >= 0 && _armedIndex < targets.Count)
                {
                    targets[_armedIndex].SetArmed(false);
                }

                if (nearest >= 0)
                {
                    targets[nearest].SetArmed(true);
                }
            }

            bool confident =
                _hand.GetFingerIsHighConfidence(HandFinger.Index) &&
                _hand.GetFingerIsHighConfidence(HandFinger.Thumb);
            if (!confident)
            {
                _spikeLowConfFrames++;
            }

            bool pinching = confident && _hand.GetIndexFingerIsPinching();
            bool justPinched = pinching && !_wasPinching;
            bool slidWhilePinched = pinching && _wasPinching && nearest >= 0 && nearest != _armedIndex;

            if (((justPinched && nearest >= 0) || slidWhilePinched)
                && _melody != null && _melody.TriggerTarget(nearest, peakSpeed))
            {
                targets[nearest].Strike();
                _spikeNoteCount++;
                Debug.Log(
                    $"[PinchSpike] stop {nearest} · {targets[nearest].Degree}" +
                    $"{(targets[nearest].OctaveOffset == 0 ? string.Empty : "+8")} · midi {targets[nearest].Pitch}" +
                    $" · {peakSpeed:0.00} m/s · {(slidWhilePinched ? "gliss" : "strike")}");
            }

            _armedIndex = nearest;
            _wasPinching = pinching;
        }

        private void OnValidate() => ValidateFingerTips();

        private void ValidateFingerTips()
        {
            if (_fingerTips == null)
            {
                return;
            }

            for (int i = 0; i < _fingerTips.Length; i++)
            {
                if (!IsTipJoint(_fingerTips[i]))
                {
                    Debug.LogError(
                        $"{nameof(TouchTargetBinder)}: _fingerTips[{i}] is {_fingerTips[i]}, not a fingertip " +
                        $"joint ({HandJointId.HandThumbTip}..{HandJointId.HandPinkyTip}). A note would fire " +
                        $"from a knuckle or the palm, behind where the learner is aiming — set it to " +
                        $"{HandJointId.HandIndexTip} or {HandJointId.HandMiddleTip} (ADR-0019).", this);
                }
            }
        }

        // The five bone tips are contiguous at the end of the enum from HandThumbTip
        // (== HandMaxSkinnable); anything below is a knuckle or a metacarpal.
        private static bool IsTipJoint(HandJointId joint) =>
            joint >= HandJointId.HandThumbTip && joint <= HandJointId.HandPinkyTip;
    }
}
