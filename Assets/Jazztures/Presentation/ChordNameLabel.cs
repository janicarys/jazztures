using Jazztures.Config;
using Jazztures.Core.Harmony;
using Jazztures.Core.Ports;
using Jazztures.Events;
using Oculus.Interaction.Input;
using UnityEngine;
using CoreHandedness = Jazztures.Core.Ports.Handedness;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The left hand's counterpart to the right hand's finger letters (<see cref="FingerNoteLabels"/>):
    /// the name of the chord that hand is holding — "Dm7", "G7", "Cmaj7" — floating just above the
    /// hand. It appears when the pose is confirmed and goes when the hand lets go, and it
    /// <b>lights up</b> when the chord actually sounds (the strike, ADR-0025), in the same look as the
    /// finger letters (<see cref="LabelGlow"/>). Selecting a chord does not sound it (§3.2), so the
    /// name rests while a pose is merely held and lights on the strike — the learner sees the two
    /// events apart, just as they hear them apart.
    ///
    /// <para>
    /// It lights for every chord note on the left-hand harmony stream — the learner's own strike
    /// (<see cref="MidiChannel.Harmony"/>) and the system's demonstration
    /// (<see cref="MidiChannel.Accompaniment"/>) — and, like the finger letters, for silent but
    /// logged notes too, since it reads the unconditional note stream (§3.8).
    /// </para>
    ///
    /// <para>
    /// Presentation only (§2.3): it reads the chord and note channels and the hand's joint pose and
    /// writes to nothing in the domain. When the hand is not tracked it is hidden — the chord itself
    /// sustains (§3.5); only the label, which has nowhere to hang, goes. The name is chord notation,
    /// like the finger letters' note names (ADR-0044) — no theory words, no function names (§1.5).
    /// </para>
    ///
    /// <para>
    /// It is hidden, too, in a lesson that does not use the left hand (<see cref="IHandsInUse"/>, ADR-0063): the
    /// system selects that lesson's chords, so the hand has no part in them and carries nothing.
    /// </para>
    /// </summary>
    public sealed class ChordNameLabel : MonoBehaviour
    {
        // How the label is drawn, not how anything behaves — cosmetic, so not [TUNABLE].
        private const float HoverAbovePalmMetres = 0.11f;
        private const float SizeVersusLetter = 1.25f; // a name of up to five characters, against a single letter

        [Tooltip("The left Interaction SDK Hand component (the same one the hand-pose source reads).")]
        [SerializeField] private MonoBehaviour _leftHand;

        [Tooltip("Size and colours come from here (the same Label settings as the finger letters).")]
        [SerializeField] private MelodyConfig _config;

        [SerializeField] private ChordChangedChannel _chordChanged;

        [Tooltip("Lights the name when a chord sounds.")]
        [SerializeField] private NoteTriggeredChannel _noteTriggered;

        [Tooltip("The centre-eye / head transform — the name turns to face it.")]
        [SerializeField] private Transform _head;

        [Tooltip("Optional. Show the name only while this component is enabled — assign the " +
                 "PerformanceCompositionRoot so it hides on the menu screens, as the finger letters do. If it " +
                 "also says which hands the running lesson uses (IHandsInUse, as the root does), the name is " +
                 "left off a lesson that does not use the left hand (ADR-0063).")]
        [SerializeField] private Behaviour _follow;

        private TextMesh _label;
        private IHand _hand;
        private IHandsInUse _handsInUse;
        private LabelGlow _glow;
        private float _flash;
        private int _litFrame = -1;

        private void Awake()
        {
            _hand = _leftHand as IHand;
            if (_hand == null)
            {
                Debug.LogError($"{nameof(ChordNameLabel)}: assign the left {nameof(IHand)}.", this);
                enabled = false;
                return;
            }

            _handsInUse = _follow as IHandsInUse;

            _glow = new LabelGlow(_config);
            float size = (_config != null ? _config.LabelCharacterSize : 0.005f) * SizeVersusLetter;

            var go = new GameObject("Chord Name");
            go.transform.SetParent(transform, worldPositionStays: false);

            _label = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.font = font;
            _label.fontSize = 90;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.characterSize = size;
            _label.color = _glow.Resting;
            _label.text = string.Empty;

            if (font != null && go.TryGetComponent(out MeshRenderer labelRenderer))
            {
                labelRenderer.sharedMaterial = font.material;
                labelRenderer.sortingOrder = 20;
            }

            go.SetActive(false);
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
            _label.text = change.CurrentChord is { } chord ? chord.ToString() : string.Empty;

            // A new name starts at rest and lights when it is struck - so the last chord's glow must not
            // carry over onto it. Unless the system sounded the new chord in this very frame (a demonstration
            // changes and plays together, and the two events can arrive in either order).
            if (Time.frameCount != _litFrame)
            {
                _flash = 0f;
            }
        }

        private void OnNote(NoteEvent note)
        {
            // The left hand's chord notes only. The melody is the right hand's and is the finger
            // letters' to light; chord voicings (Harmony: the learner's strike, Accompaniment: the
            // system's demonstration) are this label's.
            if (note.Kind == NoteEventKind.On
                && note.Source == CoreHandedness.Left
                && (note.Channel == MidiChannel.Harmony || note.Channel == MidiChannel.Accompaniment))
            {
                _flash = 1f;
                _litFrame = Time.frameCount;
            }
        }

        private void LateUpdate()
        {
            _flash = _glow.Decay(_flash, Time.deltaTime); // counts down out of sight too

            // The name is for a hand the lesson uses: a right-hand lesson selects its chords itself (ADR-0063), and
            // a name floating over a left hand that does nothing would only say it is meant to be doing something.
            bool shown = _label.text.Length > 0
                         && _hand.IsTrackedDataValid
                         && (_follow == null || _follow.isActiveAndEnabled)
                         && (_handsInUse == null || _handsInUse.IsInUse(CoreHandedness.Left));

            // The palm if the rig provides it, else the middle knuckle: the middle of the hand either way.
            Pose pose = default;
            bool visible = shown
                           && (_hand.GetJointPose(HandJointId.HandPalm, out pose)
                               || _hand.GetJointPose(HandJointId.HandMiddle1, out pose));

            if (_label.gameObject.activeSelf != visible)
            {
                _label.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            // Straight up from the hand, whichever way it is turned: the pose is open palm to the right,
            // fist, or open palm down, and the name has to be readable above all three.
            Vector3 position = pose.position + Vector3.up * HoverAbovePalmMetres;
            _label.transform.position = position;

            Vector3 viewer = _head != null ? _head.position : Vector3.zero;
            Vector3 away = position - viewer;
            if (away.sqrMagnitude > 1e-6f)
            {
                _label.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }

            _glow.Apply(_label, _flash);
        }
    }
}
