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

        private float _hoverScale = DefaultHoverScale;
        private int _speedSampleFrames = DefaultSpeedSampleFrames;
        private float _highlightUntil;

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
        }

        private void OnDisable()
        {
            if (_chordChanged != null)
            {
                _chordChanged.Unregister(OnChordChanged);
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
            // LateUpdate so the rig has re-anchored the targets first. If this runs before
            // the rig's LateUpdate the test is one frame stale — only visible mid-turn,
            // while the grid eases. Set a script execution order if it shows.
            if (_hand == null || !_hand.IsTrackedDataValid)
            {
                return;
            }

            var targets = _rig.Targets;
            float dt = Time.deltaTime;

            for (int t = 0; t < _nearThisFrame.Length; t++)
            {
                _nearThisFrame[t] = false;
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

                    bool inside = target.Contains(tip);
                    if (inside && !_wasInside[f, target.Index])
                    {
                        if (_melody != null && _melody.TriggerTarget(target.Index, peak))
                        {
                            target.Strike();
                        }
                    }

                    _wasInside[f, target.Index] = inside;

                    if (!inside && target.Contains(tip, _hoverScale))
                    {
                        _nearThisFrame[target.Index] = true;
                    }
                }
            }

            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].SetHovered(_nearThisFrame[targets[i].Index]);
            }
        }

        private void OnChordChanged(ChordChange change)
        {
            var targets = _rig.Targets;

            if (change.CurrentChord is { } chord)
            {
                ChordToneSet set = ChordToneSet.For(chord);
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
    }
}
