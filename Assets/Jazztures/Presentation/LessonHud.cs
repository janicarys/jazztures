using System.Text;
using Jazztures.Core.Evaluation;
using Jazztures.Core.Lessons;
using Jazztures.Events;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The lesson caption surface (CLAUDE.md §3.9, §3.10). One line of world-space text
    /// that appears at a phase boundary — the mode the learner is entering, or an authored
    /// cue — and then <b>fades on its own</b>. The play field is otherwise text-free: the
    /// ghost hand (<see cref="GhostHandView"/>, ADR-0012) carries the instruction, so the
    /// old mode banner and pose-name prompt are gone.
    ///
    /// <para>
    /// Built from Unity's builtin font via runtime <see cref="TextMesh"/> (ADR-0021) —
    /// which has no word wrap of its own, so this class wraps (the actual cause of the
    /// "too much text" sprawl: a 66-char caption was rendering as one ~1 m line).
    /// </para>
    ///
    /// <para>Direction rule (§2.3): reads channels only, never raises.</para>
    /// </summary>
    public sealed class LessonHud : MonoBehaviour
    {
        [Header("Channels")]
        [SerializeField] private LessonPhaseChannel _phaseChannel;
        [SerializeField] private CueActionChannel _cueChannel;
        [SerializeField] private EvaluationResultChannel _evaluationChannel;

        [Header("Placement")]
        [Tooltip("The centre-eye / head transform. Leave empty to stay where it is placed.")]
        [SerializeField] private Transform _head;

        [Tooltip("Distance in front of the learner, metres. Beyond the touch targets.")]
        [Min(0.2f)] [SerializeField] private float _distanceMetres = 1.1f;

        [Tooltip("Height relative to the head, metres. Below eye line so reading it does not " +
                 "pull the head up and the hands out of the tracking cone (ADR-0020).")]
        [SerializeField] private float _heightOffsetMetres = -0.15f;

        [Tooltip("Seconds for the panel to ease to a new position. Soft-follow, not head-locked.")]
        [Min(0f)] [SerializeField] private float _followEaseSeconds = 0.5f;

        [Header("Caption lifecycle")]
        [Tooltip("Characters per line before wrapping (TextMesh has no wrap of its own).")]
        [Min(8)] [SerializeField] private int _wrapChars = 34;

        [Tooltip("Seconds a caption stays fully opaque before fading.")]
        [Min(0f)] [SerializeField] private float _holdSeconds = 3.5f;

        [Tooltip("Seconds a caption takes to fade out.")]
        [Min(0.1f)] [SerializeField] private float _fadeSeconds = 0.8f;

        [Header("Type")]
        [Min(0.001f)] [SerializeField] private float _characterSize = 0.006f;
        [Min(8)] [SerializeField] private int _fontSize = 90;
        [SerializeField] private Color _captionColor = new Color(0.82f, 0.82f, 0.86f, 1f);

        private TextMesh _caption;
        private Vector3 _targetPosition;
        private bool _hasTargetPosition;

        private float _captionSetAt = -999f;
        private float _captionHold;
        private bool _captionImportant; // an authored cue / result — a routine phase label must not stomp it

        private void Awake()
        {
            _caption = CreateLine("Caption", Vector3.zero, 1f, _captionColor);
            _caption.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            _phaseChannel?.Register(OnPhase);
            _cueChannel?.Register(OnCue);
            _evaluationChannel?.Register(OnEvaluated);
        }

        private void OnDisable()
        {
            _phaseChannel?.Unregister(OnPhase);
            _cueChannel?.Unregister(OnCue);
            _evaluationChannel?.Unregister(OnEvaluated);
        }

        private void Update()
        {
            if (_caption.text.Length == 0)
            {
                return;
            }

            float age = Time.time - _captionSetAt;
            float alpha;
            if (age < _captionHold)
            {
                alpha = 1f;
            }
            else if (age < _captionHold + _fadeSeconds)
            {
                alpha = 1f - (age - _captionHold) / _fadeSeconds;
            }
            else
            {
                _caption.text = string.Empty;
                _caption.gameObject.SetActive(false);
                return;
            }

            Color c = _captionColor;
            c.a *= alpha;
            _caption.color = c;
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

        // A phase label is routine — it must not stomp an authored closing cue that fired
        // in the same frame (AdvancePhase runs EndCurrentPhase before the phase change).
        private void OnPhase(LessonPhaseInfo info) => Show(NameOf(info.Mode), _holdSeconds, important: false);

        private void OnCue(CueAction action)
        {
            switch (action.Kind)
            {
                case CueActionKind.ShowText:
                    Show(action.Text ?? string.Empty, _holdSeconds, important: true);
                    break;

                case CueActionKind.HideText:
                    // Start the fade now rather than holding.
                    _captionHold = 0f;
                    _captionSetAt = Time.time;
                    break;
            }
        }

        private void OnEvaluated(AttemptResult result)
        {
            // §3.7: feedback is deferred to the end of an attempt, never mid-phrase.
            if (result.IsEmpty)
            {
                return;
            }

            Show(
                $"{result.OnTimeCount} on time · {result.CloseCount} close · {result.OffCount} off",
                _holdSeconds * 1.6f,
                important: true);
        }

        private void Show(string text, float holdSeconds, bool important)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Yield if an important caption is still holding.
            if (!important && _captionImportant && _caption.text.Length > 0
                && Time.time - _captionSetAt < _captionHold)
            {
                return;
            }

            _caption.text = Wrap(text, _wrapChars);
            _caption.color = _captionColor;
            _caption.gameObject.SetActive(true);
            _captionSetAt = Time.time;
            _captionHold = holdSeconds;
            _captionImportant = important;
        }

        /// <summary>Break <paramref name="text"/> onto lines of at most <paramref name="maxChars"/>, on spaces.</summary>
        private static string Wrap(string text, int maxChars)
        {
            string[] words = text.Split(' ');
            var sb = new StringBuilder(text.Length + 8);
            int lineLen = 0;

            foreach (string word in words)
            {
                if (lineLen > 0 && lineLen + 1 + word.Length > maxChars)
                {
                    sb.Append('\n');
                    lineLen = 0;
                }
                else if (lineLen > 0)
                {
                    sb.Append(' ');
                    lineLen++;
                }

                sb.Append(word);
                lineLen += word.Length;
            }

            return sb.ToString();
        }

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
