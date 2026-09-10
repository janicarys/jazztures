using Jazztures.Core.Evaluation;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Events;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The follow-along instruction surface for a running lesson (CLAUDE.md §3.9, §3.10).
    /// Reads four channels and shows, in plain language, what the learner should be doing
    /// right now:
    ///
    /// <list type="bullet">
    ///   <item><see cref="LessonPhaseChannel"/> → which mode we are in ("Watch and listen" /
    ///   "Your turn");</item>
    ///   <item><see cref="GhostFrameChannel"/> → the pose the lesson is asking for, named
    ///   the way the learner was taught it — never "dominant seventh" (§1.5);</item>
    ///   <item><see cref="ChordChangedChannel"/> → the learner's own confirmed pose, so the
    ///   panel can say whether it matches;</item>
    ///   <item><see cref="CueActionChannel"/> → authored captions from the lesson script;</item>
    ///   <item><see cref="EvaluationResultChannel"/> → the end-of-attempt summary (§3.7,
    ///   deferred — never mid-phrase).</item>
    /// </list>
    ///
    /// <para>
    /// M5 placeholder, deliberately: ADR-0012 puts the real articulated ghost hand in M6.
    /// Text is built at runtime from Unity's builtin font, so there is no TextMeshPro
    /// essentials import and no extra assembly reference to get wrong before a device test.
    /// </para>
    ///
    /// <para>Direction rule (§2.3): this only reads channels; it never raises one.</para>
    /// </summary>
    public sealed class LessonHud : MonoBehaviour
    {
        [Header("Channels")]
        [SerializeField] private LessonPhaseChannel _phaseChannel;
        [SerializeField] private GhostFrameChannel _ghostChannel;
        [SerializeField] private CueActionChannel _cueChannel;
        [SerializeField] private ChordChangedChannel _chordChanged;
        [SerializeField] private EvaluationResultChannel _evaluationChannel;

        [Header("Placement")]
        [Tooltip("The centre-eye / head transform. Leave empty to stay where it is placed.")]
        [SerializeField] private Transform _head;

        [Tooltip("Distance in front of the learner, metres. Beyond the touch targets so it " +
                 "never sits between the hand and the arc.")]
        [Min(0.2f)] [SerializeField] private float _distanceMetres = 1.1f;

        [Tooltip("Height relative to the head, metres. Slightly below eye line so reading it " +
                 "does not pull the head up and the hands out of the tracking cone (ADR-0020).")]
        [SerializeField] private float _heightOffsetMetres = -0.15f;

        [Tooltip("Seconds for the panel to ease to a new position. Soft-follow, not " +
                 "head-locked — rigid geometry at reading distance is a comfort problem.")]
        [Min(0f)] [SerializeField] private float _followEaseSeconds = 0.5f;

        [Header("Type")]
        [Min(0.001f)] [SerializeField] private float _characterSize = 0.006f;
        [Min(8)] [SerializeField] private int _fontSize = 90;
        [SerializeField] private Color _bannerColor = new Color(0.62f, 0.66f, 0.72f, 1f);
        [SerializeField] private Color _promptColor = new Color(0.96f, 0.95f, 0.90f, 1f);
        [SerializeField] private Color _matchedColor = new Color(0.55f, 0.82f, 0.55f, 1f);
        [SerializeField] private Color _captionColor = new Color(0.75f, 0.75f, 0.78f, 1f);

        private TextMesh _banner;   // the mode
        private TextMesh _prompt;   // the pose to make now
        private TextMesh _caption;  // authored lesson text

        private bool _lessonActive;
        private ChordFunction? _asked;   // what the lesson wants
        private ChordFunction? _held;    // what the learner is actually holding
        private Vector3 _targetPosition;
        private bool _hasTargetPosition;

        private void Awake()
        {
            _banner = CreateLine("Banner", new Vector3(0f, 0.085f, 0f), 0.8f, _bannerColor);
            _prompt = CreateLine("Prompt", Vector3.zero, 1.35f, _promptColor);
            _caption = CreateLine("Caption", new Vector3(0f, -0.10f, 0f), 0.65f, _captionColor);
            SetVisible(false);
        }

        private void OnEnable()
        {
            _phaseChannel?.Register(OnPhase);
            _ghostChannel?.Register(OnGhost);
            _cueChannel?.Register(OnCue);
            _chordChanged?.Register(OnChordChanged);
            _evaluationChannel?.Register(OnEvaluated);
        }

        private void OnDisable()
        {
            _phaseChannel?.Unregister(OnPhase);
            _ghostChannel?.Unregister(OnGhost);
            _cueChannel?.Unregister(OnCue);
            _chordChanged?.Unregister(OnChordChanged);
            _evaluationChannel?.Unregister(OnEvaluated);
        }

        private void LateUpdate()
        {
            if (_head == null)
            {
                return;
            }

            Vector3 forward = _head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                return;
            }

            forward.Normalize();
            Vector3 wanted = _head.position + forward * _distanceMetres + Vector3.up * _heightOffsetMetres;

            if (!_hasTargetPosition)
            {
                _targetPosition = wanted;
                _hasTargetPosition = true;
            }
            else
            {
                float k = _followEaseSeconds <= 0f
                    ? 1f
                    : 1f - Mathf.Exp(-Time.deltaTime / _followEaseSeconds);
                _targetPosition = Vector3.Lerp(_targetPosition, wanted, k);
            }

            transform.position = _targetPosition;
            transform.rotation = Quaternion.LookRotation(_targetPosition - _head.position, Vector3.up);
        }

        private void OnPhase(LessonPhaseInfo info)
        {
            _lessonActive = true;
            SetVisible(true);
            _banner.text = NameOf(info.Mode);
            _caption.text = string.Empty;
            _asked = null;
            RefreshPrompt();
        }

        private void OnGhost(GhostFrame frame)
        {
            // Ghost hidden = Test Yourself / Compose: no pose is being shown (§3.8).
            _asked = frame.Visible ? frame.DemonstratedPose : null;
            RefreshPrompt();
        }

        private void OnChordChanged(ChordChange change)
        {
            _held = change.CurrentFunction;
            RefreshPrompt();
        }

        private void OnCue(CueAction action)
        {
            switch (action.Kind)
            {
                case CueActionKind.ShowText:
                    _caption.text = action.Text ?? string.Empty;
                    break;

                case CueActionKind.HideText:
                    _caption.text = string.Empty;
                    break;
            }
        }

        private void OnEvaluated(AttemptResult result)
        {
            // §3.7: feedback is deferred to the end of an attempt, never mid-phrase.
            _caption.text = result.IsEmpty
                ? string.Empty
                : $"{result.OnTimeCount} on time · {result.CloseCount} close · {result.OffCount} off";
        }

        private void RefreshPrompt()
        {
            if (!_lessonActive)
            {
                return;
            }

            if (_asked is not { } wanted)
            {
                _prompt.text = string.Empty;
                return;
            }

            bool matched = _held == wanted;
            _prompt.text = matched ? $"{PoseName(wanted)}  ✓" : PoseName(wanted);
            _prompt.color = matched ? _matchedColor : _promptColor;
        }

        private void SetVisible(bool visible)
        {
            _banner.gameObject.SetActive(visible);
            _prompt.gameObject.SetActive(visible);
            _caption.gameObject.SetActive(visible);
        }

        /// <summary>The pose named the way the learner was taught it — no theory jargon (§1.5).</summary>
        private static string PoseName(ChordFunction function) => function switch
        {
            ChordFunction.Two => "Open palm, facing right",
            ChordFunction.Five => "Fist",
            ChordFunction.One => "Open palm, facing down",
            _ => string.Empty,
        };

        private static string NameOf(LearningMode mode) => mode switch
        {
            LearningMode.GestureLearning => "Learn the shapes",
            LearningMode.WatchAndListen => "Watch and listen",
            LearningMode.TryYourself => "Your turn",
            LearningMode.TestYourself => "Test yourself",
            LearningMode.ComposeOnTheFly => "Play freely",
            _ => string.Empty,
        };

        private TextMesh CreateLine(string lineName, Vector3 localPosition, float scale, Color color)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, worldPositionStays: false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;

            TextMesh text = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.font = font;
            text.fontSize = _fontSize;
            text.characterSize = _characterSize * scale;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            text.text = string.Empty;

            if (font != null && go.TryGetComponent(out MeshRenderer renderer))
            {
                renderer.sharedMaterial = font.material;
            }

            return text;
        }
    }
}
