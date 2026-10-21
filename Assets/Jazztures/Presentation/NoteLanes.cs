using Jazztures.Config;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Music;
using Jazztures.Events;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// EXPLORATION (ADR-0060, branch <c>explore/finger-bend-keys</c>): the lesson's right-hand cue as lanes. Five
    /// faint lanes run out in front of the learner's own fingertips, and each note of the phrase is a letter that
    /// travels in along its finger's lane and reaches the fingertip exactly on the beat - the cue to bend that
    /// finger now. It replaces the ghost right hand's finger presses and letters (ADR-0055, ADR-0058, ADR-0059):
    /// the learner's eyes stay on their own hand, and the notes to come are in view, several beats ahead, instead
    /// of announced one at a time.
    ///
    /// <para>
    /// <b>This contradicts a thesis note, as an experiment.</b> CLAUDE.md §1.3 records the three-lane design (three
    /// keys) as cut because it "made the system feel like a rhythm game"; a lane of arriving notes is the same
    /// idiom. It is built on this branch as a trial, CLAUDE.md is deliberately not edited, and it must not be merged
    /// as a thesis decision until the student has decided (ADR-0060).
    /// </para>
    ///
    /// <para>
    /// A lane's length is a number of <i>beats</i>, not seconds, laid out at a fixed distance per beat, so the
    /// spacing of the letters is the spacing of the notes in the score whatever the tempo. A letter's distance from
    /// the fingertip is the time until its note. The next note is at full strength, the ones behind it quiet
    /// (ADR-0059's fade); a letter that arrives glows gold at the fingertip, lingers briefly, and goes.
    /// </para>
    ///
    /// <para>
    /// Presentation only (§2.3): it reads the ghost channel (which carries the lesson's press schedule and the beat
    /// to measure it on) and the hand's joint poses, and writes to nothing in the domain. Frame-rate work only
    /// moves a fixed pool of lines and labels - nothing is allocated per frame. When the hand is not tracked the
    /// lanes are simply not drawn (§3.5).
    /// </para>
    /// </summary>
    public sealed class NoteLanes : MonoBehaviour
    {
        // How the lanes are drawn, not how anything behaves - cosmetic, so not [TUNABLE].
        private const int FingerCount = 5;
        private const int LetterPool = 12;             // simultaneous letters on all the lanes
        private const float StaleFrameSeconds = 0.25f; // no ghost frame for this long means nothing is running
        private const float LaneEaseSeconds = 0.2f;    // a lane brightens or dims over this long
        private const float FadeInFromFraction = 0.7f; // letters fade in over the last 30% of the lane

        [Tooltip("The right Interaction SDK Hand component - the lanes start at its fingertips.")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Tooltip("The lesson's ghost frame channel - carries the notes and the beat they are measured on.")]
        [SerializeField] private GhostFrameChannel _ghostChannel;

        [Tooltip("Lane and letter look and layout come from here (Note lanes, Ghost letters, Label glow).")]
        [SerializeField] private MelodyConfig _config;

        [Tooltip("Optional. The chord colours (ADR-0065): each coming letter takes the colour of the chord it belongs to, " +
                 "the same the left hand takes while it holds that pose. Empty leaves every letter the usual colour.")]
        [SerializeField] private TensionPalette _palette;

        [Tooltip("The centre-eye / head transform. The lanes run along the learner's heading (§3.4) and the " +
                 "letters turn to face it.")]
        [SerializeField] private Transform _head;

        [Tooltip("Optional. Show the lanes only while this component is enabled - assign the finger-bend input, " +
                 "which the lesson flow switches on for a lesson that uses the right hand and off on the menu.")]
        [SerializeField] private Behaviour _follow;

        [Tooltip("Optional. The letters over the learner's own fingertips: hidden while the lanes are up, because " +
                 "the arriving letter already names the note at that fingertip.")]
        [SerializeField] private FingerNoteLabels _ownLetters;

        // Thumb, index, middle, ring, pinky - lane f is the f-th chord tone (root, 3rd, 5th, 7th, 9th).
        private static readonly HandJointId[] Tips =
        {
            HandJointId.HandThumbTip, HandJointId.HandIndexTip, HandJointId.HandMiddleTip,
            HandJointId.HandRingTip, HandJointId.HandPinkyTip,
        };

        private readonly string[,] _names = new string[3, FingerCount]; // [chord function, finger] -> note letter
        private readonly LineRenderer[] _lanes = new LineRenderer[FingerCount];
        private readonly float[] _laneAlpha = new float[FingerCount];
        private readonly bool[] _laneHasNote = new bool[FingerCount];
        private readonly Vector3[] _tips = new Vector3[FingerCount];
        private readonly bool[] _tipOk = new bool[FingerCount];
        private readonly GhostLabel[] _letters = new GhostLabel[LetterPool];
        private readonly int[] _slotPress = new int[LetterPool];       // which press each letter is showing
        private readonly bool[] _slotArrived = new bool[LetterPool];   // whether it has been lit on arrival yet
        private IHand _hand;
        private Material _material;
        private Color _laneColor = new Color(0.96f, 0.96f, 0.98f, 1f);
        private GhostFrame _frame;
        private float _frameTime = float.NegativeInfinity;

        private void Awake()
        {
            _hand = _rightHand as IHand;
            if (_hand == null)
            {
                Debug.LogError($"{nameof(NoteLanes)}: assign the right {nameof(IHand)}.", this);
                enabled = false;
                return;
            }

            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform;
            }

            if (_config != null)
            {
                _laneColor = _config.LabelColor;
            }

            // Every letter a lane can show, named once: a chord's tone set is an allocation, and the letters
            // change on the beat.
            for (int fn = 0; fn < _names.GetLength(0); fn++)
            {
                ChordToneSet set = ChordToneSet.For(Progression.ChordFor((ChordFunction)fn));
                for (int f = 0; f < FingerCount; f++)
                {
                    _names[fn, f] = PitchNames.Of(set[f].Pitch.Class);
                }
            }

            _material = RuntimeMaterial.Unlit("Note Lane");
            float width = _config != null ? _config.LaneWidthMetres : 0.004f;
            for (int f = 0; f < FingerCount; f++)
            {
                var go = new GameObject($"Note Lane {f}");
                go.transform.SetParent(transform, worldPositionStays: false);

                LineRenderer line = go.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.sharedMaterial = _material;
                line.widthMultiplier = width;
                line.numCapVertices = 2;
                line.alignment = LineAlignment.View;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.enabled = false;
                _lanes[f] = line;
            }

            for (int s = 0; s < LetterPool; s++)
            {
                _letters[s] = new GhostLabel(transform, $"Lane Letter {s}", _config);
                _slotPress[s] = -1;
            }
        }

        private void OnEnable() => _ghostChannel?.Register(OnGhost);

        private void OnDisable()
        {
            _ghostChannel?.Unregister(OnGhost);
            _frameTime = float.NegativeInfinity;
            if (_ownLetters != null)
            {
                _ownLetters.Suppressed = false;
            }

            HideAll();
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private void OnGhost(GhostFrame frame)
        {
            _frame = frame;
            _frameTime = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;

            // Wanted: the lesson is running, shows its ghosts, has a right-hand melody, and the right-hand input
            // is on. Up: wanted and the hand is tracked, so there are fingertips to run the lanes from.
            GhostPressSchedule schedule = Time.unscaledTime - _frameTime <= StaleFrameSeconds && _frame.Visible
                ? _frame.PressSchedule
                : null;
            bool wanted = schedule != null && schedule.Count > 0 && (_follow == null || _follow.isActiveAndEnabled);

            if (_ownLetters != null)
            {
                _ownLetters.Suppressed = wanted;
            }

            if (!wanted || _hand == null || !_hand.IsTrackedDataValid)
            {
                HideAll();
                return;
            }

            for (int f = 0; f < FingerCount; f++)
            {
                _tipOk[f] = _hand.GetJointPose(Tips[f], out Pose pose);
                _tips[f] = pose.position;
                _laneHasNote[f] = false;
            }

            Vector3 forward = _head != null ? GhostHandView.YawOnly(_head) * Vector3.forward : Vector3.forward;
            Vector3 viewer = _head != null ? _head.position : Vector3.zero;

            float lookAhead = _config != null ? _config.LaneLookAheadBeats : 3f;
            float metresPerBeat = _config != null ? _config.LaneMetresPerBeat : 0.2f;
            float lingerBeats = _config != null ? _config.LaneLingerBeats : 0.4f;
            float hover = _config != null ? _config.LaneLetterHoverMetres : 0.02f;
            float chordTint = _config != null ? _config.LaneLetterChordTint : 1f;
            float laneLength = lookAhead * metresPerBeat;

            double beat = _frame.MelodyBeat;
            int usedSlots = 0;
            bool nextFound = false;
            int shown = 0;

            for (int i = schedule.FirstAtOrAfter(beat - lingerBeats); i < schedule.Count && shown < LetterPool; i++)
            {
                double beatsAway = schedule.OnsetAt(i) - beat; // negative once the note has arrived
                if (beatsAway > lookAhead)
                {
                    break; // time order: nothing nearer follows
                }

                int finger = schedule.FingerAt(i);
                int slot = i % LetterPool;
                GhostLabel letter = _letters[slot];
                shown++;
                usedSlots |= 1 << slot;

                if (_slotPress[slot] != i)
                {
                    _slotPress[slot] = i;
                    _slotArrived[slot] = false;
                    letter.Reset();
                    ChordFunction? chord = schedule.ChordAt(i);
                    letter.Text = chord is { } fn && (int)fn < _names.GetLength(0) ? _names[(int)fn, finger] : string.Empty;

                    // The letter is in the colour of ITS chord, not the current one: the lanes show several beats
                    // ahead, so the next chord's notes can be on their way while this chord's are still arriving,
                    // and each wears the colour the left hand takes when it holds that pose (ADR-0065).
                    letter.SetHue(
                        _palette != null && chord.HasValue ? ChordHue.Of(_palette.For(chord)) : (Color?)null,
                        chordTint);
                }

                // The letter reaches the fingertip on the beat: light it as it does.
                if (beatsAway <= 0.0 && !_slotArrived[slot])
                {
                    _slotArrived[slot] = true;
                    letter.Flash();
                }

                bool arrived = beatsAway <= 0.0;
                float distance = arrived ? 0f : (float)beatsAway * metresPerBeat;

                // Only the note due next (and one that has just arrived) is at full strength.
                bool isNext = !arrived && !nextFound;
                nextFound |= !arrived;
                letter.Cue(arrived || isNext);

                // In from the far end of the lane, out as it lingers at the fingertip.
                float fadeIn = 1f - Smooth(distance / Mathf.Max(laneLength, 1e-4f), FadeInFromFraction, 1f);
                float fadeOut = arrived
                    ? 1f - Smooth((float)(-beatsAway / Mathf.Max(lingerBeats, 1e-4f)), 0.5f, 1f)
                    : 1f;
                letter.Fade = fadeIn * fadeOut;

                letter.Place(
                    _tipOk[finger],
                    _tips[finger] + forward * distance + Vector3.up * hover,
                    viewer,
                    dt);

                if (!arrived)
                {
                    _laneHasNote[finger] = true;
                }
            }

            for (int s = 0; s < LetterPool; s++)
            {
                if ((usedSlots & (1 << s)) == 0)
                {
                    _slotPress[s] = -1;
                    _letters[s].Hide();
                }
            }

            float idle = _config != null ? _config.LaneIdleAlpha : 0.10f;
            float active = _config != null ? _config.LaneActiveAlpha : 0.45f;
            for (int f = 0; f < FingerCount; f++)
            {
                _laneAlpha[f] = Mathf.MoveTowards(_laneAlpha[f], _laneHasNote[f] ? active : idle, dt / LaneEaseSeconds);

                LineRenderer line = _lanes[f];
                if (line.enabled != _tipOk[f])
                {
                    line.enabled = _tipOk[f];
                }

                if (!_tipOk[f])
                {
                    continue;
                }

                Color near = _laneColor;
                near.a = _laneAlpha[f];
                Color far = _laneColor;
                far.a = 0f;
                line.startColor = near;
                line.endColor = far;
                line.SetPosition(0, _tips[f]);
                line.SetPosition(1, _tips[f] + forward * laneLength);
            }
        }

        /// <summary>Everything out of sight, and ready for the next phrase: no letter remembers its press.</summary>
        private void HideAll()
        {
            for (int f = 0; f < FingerCount; f++)
            {
                if (_lanes[f] != null && _lanes[f].enabled)
                {
                    _lanes[f].enabled = false;
                }

                _laneAlpha[f] = 0f;
            }

            for (int s = 0; s < LetterPool; s++)
            {
                _slotPress[s] = -1;
                _letters[s]?.Hide();
            }
        }

        /// <summary>0 below <paramref name="from"/>, 1 above <paramref name="to"/>, a smooth ramp between.</summary>
        private static float Smooth(float value, float from, float to)
        {
            float t = Mathf.Clamp01((value - from) / Mathf.Max(to - from, 1e-4f));
            return t * t * (3f - 2f * t);
        }
    }
}
