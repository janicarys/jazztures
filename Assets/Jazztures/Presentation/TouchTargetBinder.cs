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
    /// change ("re-pitched, never re-arranged", §3.1), lighting them briefly. Each frame
    /// it reads the right index fingertip and, when it enters a target's sphere, calls
    /// <see cref="MelodyEngine.TriggerTarget"/> with the fingertip speed — the engine owns
    /// the entry-velocity gate, the retrigger cooldown and the speed→velocity curve.
    ///
    /// <para>Direction rule (§2.3): this only reads the channel; it never raises it.</para>
    /// </summary>
    public sealed class TouchTargetBinder : MonoBehaviour
    {
        [SerializeField] private TouchTargetRig _rig;

        [SerializeField] private ChordChangedChannel _chordChanged;

        [Tooltip("The right Interaction SDK Hand component.")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Tooltip("How long targets stay lit after a chord change, seconds.")]
        [Min(0f)] [SerializeField] private float _highlightSeconds = 0.6f;

        private readonly bool[] _wasInside = new bool[ChordToneSet.TargetCount];
        private MelodyEngine _melody;
        private IHand _hand;
        private Vector3 _previousTip;
        private bool _hasPreviousTip;
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
            // Runs in LateUpdate so the rig has (usually) re-anchored the targets first.
            // If this component's LateUpdate happens to run before the rig's, the test is
            // against last frame's target positions — a sub-frame lag that only matters
            // mid-turn, when the grid is easing. Set a script execution order if it shows.
            if (!TryReadFingertip(out Vector3 tip))
            {
                _hasPreviousTip = false;
                return;
            }

            float dt = Time.deltaTime;
            float speed = _hasPreviousTip && dt > 0f
                ? Vector3.Distance(tip, _previousTip) / dt
                : 0f;
            _previousTip = tip;
            _hasPreviousTip = true;

            var targets = _rig.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                TouchTarget target = targets[i];
                bool inside = target.IsSounding && target.Contains(tip);

                if (inside && !_wasInside[target.Index])
                {
                    if (_melody != null && _melody.TriggerTarget(target.Index, speed))
                    {
                        target.Strike();
                    }
                }

                _wasInside[target.Index] = inside;
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

        private bool TryReadFingertip(out Vector3 tip)
        {
            tip = default;
            if (_hand == null || !_hand.IsTrackedDataValid)
            {
                return false;
            }

            if (!_hand.GetJointPose(HandJointId.HandIndexTip, out Pose pose))
            {
                return false;
            }

            tip = pose.position;
            return true;
        }
    }
}
