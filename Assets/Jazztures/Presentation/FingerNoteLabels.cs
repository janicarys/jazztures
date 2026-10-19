using Jazztures.Config;
using Jazztures.Core.Harmony;
using Jazztures.Core.Music;
using Jazztures.Core.Ports;
using Jazztures.Events;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// EXPLORATION (ADR-0050, branch <c>explore/finger-bend-keys</c>): with finger bends as the
    /// right-hand input there are no targets to carry the note letters, so each fingertip carries
    /// its own — a small letter floating just above the finger saying which note that finger plays
    /// <i>now</i>. It re-pitches on every chord change, exactly as the touch targets' labels do
    /// (ADR-0044), and lights up when its note sounds, the same way the left hand's chord name does
    /// (<see cref="LabelGlow"/>). Letters only, no octave numbers (§1.5).
    ///
    /// <para>
    /// Presentation only (§2.3): it reads the chord and note channels and the hand's joint poses and
    /// writes to nothing in the domain. The input component is deliberately not referenced; a
    /// <c>Follow</c> slot lets the lesson flow's on/off switch for the melody input hide the letters
    /// too.
    /// </para>
    /// </summary>
    public sealed class FingerNoteLabels : MonoBehaviour
    {
        // How the labels are drawn, not how anything behaves — cosmetic, so not [TUNABLE].
        private const float HoverAboveTipMetres = 0.035f;

        [Tooltip("The right Interaction SDK Hand component.")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Tooltip("Label size and colour come from here (the same settings as the touch targets' letters).")]
        [SerializeField] private MelodyConfig _config;

        [SerializeField] private ChordChangedChannel _chordChanged;

        [Tooltip("Flashes a finger's letter when its note sounds.")]
        [SerializeField] private NoteTriggeredChannel _noteTriggered;

        [Tooltip("The centre-eye / head transform — the letters turn to face it.")]
        [SerializeField] private Transform _head;

        [Tooltip("Optional. Show the letters only while this component is enabled — assign the " +
                 "finger-bend input so the lesson flow's switch for the melody input hides them " +
                 "on the menu screen.")]
        [SerializeField] private Behaviour _follow;

        [Tooltip("Optional. The touch-target rig to hide while this input is in use — its ten pads " +
                 "are not played in this mode and would only mislead.")]
        [SerializeField] private TouchTargetRig _hideTouchTargets;

        // Thumb, index, middle, ring, pinky — slot f is the f-th chord tone (root, 3rd, 5th, 7th, 9th).
        private static readonly HandJointId[] Tips =
        {
            HandJointId.HandThumbTip, HandJointId.HandIndexTip, HandJointId.HandMiddleTip,
            HandJointId.HandRingTip, HandJointId.HandPinkyTip,
        };

        private readonly TextMesh[] _labels = new TextMesh[Tips.Length];
        private readonly int[] _midi = new int[Tips.Length];
        private readonly float[] _flash = new float[Tips.Length];
        private IHand _hand;
        private LabelGlow _glow;

        private void Awake()
        {
            _hand = _rightHand as IHand;
            if (_hand == null)
            {
                Debug.LogError($"{nameof(FingerNoteLabels)}: assign the right {nameof(IHand)}.", this);
                enabled = false;
                return;
            }

            float size = _config != null ? _config.LabelCharacterSize : 0.005f;
            _glow = new LabelGlow(_config);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int f = 0; f < _labels.Length; f++)
            {
                var go = new GameObject($"Finger Label {f}");
                go.transform.SetParent(transform, worldPositionStays: false);

                TextMesh label = go.AddComponent<TextMesh>();
                label.font = font;
                label.fontSize = 90;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.characterSize = size;
                label.color = _glow.Resting;
                label.text = string.Empty;

                if (font != null && go.TryGetComponent(out MeshRenderer labelRenderer))
                {
                    labelRenderer.sharedMaterial = font.material;
                    labelRenderer.sortingOrder = 20;
                }

                _labels[f] = label;
                _midi[f] = -1;
            }
        }

        private void Start()
        {
            // In Start, not Awake/OnEnable: the rig spawns its pads in its own Awake.
            if (_hideTouchTargets != null)
            {
                _hideTouchTargets.SetTargetsVisible(false);
            }
        }

        private void OnEnable()
        {
            if (_chordChanged != null)
            {
                _chordChanged.Register(OnChordChanged);
            }

            if (_noteTriggered != null)
            {
                _noteTriggered.Register(OnNote);
            }
        }

        private void OnDisable()
        {
            if (_chordChanged != null)
            {
                _chordChanged.Unregister(OnChordChanged);
            }

            if (_noteTriggered != null)
            {
                _noteTriggered.Unregister(OnNote);
            }
        }

        private void OnChordChanged(ChordChange change)
        {
            if (change.CurrentChord is { } chord)
            {
                ChordToneSet set = ChordToneSet.For(chord);
                for (int f = 0; f < _labels.Length; f++)
                {
                    ChordTarget tone = set[f]; // slot f of the lower octave
                    _midi[f] = tone.Pitch.Midi;
                    _labels[f].text = PitchNames.Of(tone.Pitch.Class);
                }
            }
            else
            {
                for (int f = 0; f < _labels.Length; f++)
                {
                    _midi[f] = -1;
                    _labels[f].text = string.Empty;
                }
            }
        }

        private void OnNote(NoteEvent note)
        {
            // The learner's own notes (Melody) and the system's demonstration (Accompaniment) both
            // flash the finger that plays that note: in Watch and Listen this is how the learner
            // sees which finger to bend, standing in for the target spheres the ghost hand lights
            // in touch mode. The chord voicings are far below the five melody pitches, so only
            // melody notes ever match.
            if (note.Kind != NoteEventKind.On
                || (note.Channel != MidiChannel.Melody && note.Channel != MidiChannel.Accompaniment))
            {
                return;
            }

            for (int f = 0; f < _midi.Length; f++)
            {
                if (_midi[f] == note.Pitch.Midi)
                {
                    _flash[f] = 1f;
                }
            }
        }

        private void LateUpdate()
        {
            bool shown = _hand != null && _hand.IsTrackedDataValid && (_follow == null || _follow.isActiveAndEnabled);
            Vector3 viewer = _head != null ? _head.position : Vector3.zero;

            for (int f = 0; f < _labels.Length; f++)
            {
                TextMesh label = _labels[f];
                _flash[f] = _glow.Decay(_flash[f], Time.deltaTime); // counts down out of sight too

                Pose pose = default;
                bool visible = shown && _hand.GetJointPose(Tips[f], out pose);
                if (label.gameObject.activeSelf != visible)
                {
                    label.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                Vector3 position = pose.position + Vector3.up * HoverAboveTipMetres;
                label.transform.position = position;

                Vector3 away = position - viewer;
                if (away.sqrMagnitude > 1e-6f)
                {
                    label.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
                }

                _glow.Apply(label, _flash[f]);
            }
        }
    }
}
