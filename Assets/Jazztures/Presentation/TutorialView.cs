using System.Text;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The tutorial's one line of instruction (ADR-0053; CLAUDE.md §3.10). Plain world-space text that
    /// soft-follows the learner and <b>stays until the learner has done what it asks</b> — unlike the
    /// lesson HUD's captions, which fade on their own — and cross-fades when the line changes, so a
    /// newcomer who has never worn a headset is never left guessing whether the line has changed.
    /// A <b>dumb view</b>: it shows a string and reads nothing.
    ///
    /// <para>
    /// Sits below eye line, under the lesson menu, so reading it neither tilts the head nor pulls the
    /// hands out of the tracking cone (ADR-0020). Built like <see cref="LessonHud"/> — runtime
    /// <see cref="TextMesh"/>, no Canvas (ADR-0021).
    /// </para>
    /// </summary>
    public sealed class TutorialView : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("The centre-eye / head transform. Leave empty to stay where it is placed.")]
        [SerializeField] private Transform _head;
        [Tooltip("Distance in front of the learner, metres.")]
        [Min(0.2f)] [SerializeField] private float _distanceMetres = 0.8f;
        [Tooltip("Height relative to the head, metres, while no menu is on screen (the first lines): near eye line, " +
                 "where the menu will appear, so the line is read without looking down.")]
        [SerializeField] private float _heightWithoutMenuMetres = -0.05f;
        [Tooltip("Height relative to the head, metres, once the menu is up: below eye line, under the menu's footer.")]
        [SerializeField] private float _heightOffsetMetres = -0.34f;
        [Min(0f)] [SerializeField] private float _followEaseSeconds = 0.4f;
        [Tooltip("The panel never trails the point it belongs at by more than this, metres. Within it the panel eases " +
                 "softly; beyond it, it is carried along with the head, so it is never far out of view - after a quick " +
                 "turn, or when it first appears (ADR-0056).")]
        [Min(0f)] [SerializeField] private float _maxTrailMetres = 0.2f;

        [Header("Type")]
        [Min(0.001f)] [SerializeField] private float _characterSize = 0.0065f;
        [Min(8)] [SerializeField] private int _fontSize = 90;
        [Tooltip("Characters per line before wrapping (TextMesh has no wrap of its own).")]
        [Min(8)] [SerializeField] private int _wrapChars = 30;
        [SerializeField] private Color _color = new Color(0.96f, 0.95f, 0.92f, 1f);

        [Header("Fade between lines")]
        [Tooltip("Seconds the old line takes to fade (and drift up) before the next one.")]
        [Min(0.01f)] [SerializeField] private float _fadeOutSeconds = 0.4f;
        [Tooltip("Seconds the new line takes to fade in (rising into place).")]
        [Min(0.01f)] [SerializeField] private float _fadeInSeconds = 0.6f;
        [Tooltip("How far the line drifts as it fades, metres: up on the way out, up into place on the way in. 0 = a plain fade.")]
        [Min(0f)] [SerializeField] private float _driftMetres = 0.025f;

        private enum Phase
        {
            Idle,
            Out,
            In,
        }

        private TextMesh _text;
        private Phase _phase;
        private float _alpha;
        private string _pending;
        private bool _hideWhenFaded;
        private Vector3 _targetPosition;
        private bool _hasTargetPosition;
        private bool _menuVisible;

        public bool IsShown { get; private set; }

        /// <summary>
        /// Tell the view whether the menu is on screen. The line sits near eye line until it is, then eases down
        /// beneath it, so it never overlaps the cards and never looks stranded below an empty view.
        /// </summary>
        public void SetMenuVisible(bool visible) => _menuVisible = visible;

        private void Awake()
        {
            var go = new GameObject("Line");
            go.transform.SetParent(transform, worldPositionStays: false);
            _text = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.font = font;
            _text.fontSize = _fontSize;
            _text.characterSize = _characterSize;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.text = string.Empty;

            if (font != null && go.TryGetComponent(out MeshRenderer renderer))
            {
                renderer.sharedMaterial = font.material;
            }

            _text.gameObject.SetActive(false);
        }

        /// <summary>Show <paramref name="text"/>; if a different line is up, cross-fade to it.</summary>
        public void Show(string text)
        {
            text = Wrap(text, _wrapChars);
            _hideWhenFaded = false;

            if (!IsShown)
            {
                IsShown = true;
                _hasTargetPosition = false;
                _text.text = text;
                _text.gameObject.SetActive(true);
                _alpha = 0f;
                _phase = Phase.In;
                ApplyColor();
                return;
            }

            if (text == _text.text && _phase != Phase.Out)
            {
                return;
            }

            _pending = text;
            _phase = Phase.Out;
        }

        /// <summary>Fade the line away and hide.</summary>
        public void Hide()
        {
            if (!IsShown)
            {
                return;
            }

            _hideWhenFaded = true;
            _pending = null;
            _phase = Phase.Out;
        }

        private void Update()
        {
            if (!IsShown)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            switch (_phase)
            {
                case Phase.Out:
                    _alpha = Mathf.Max(0f, _alpha - dt / _fadeOutSeconds);
                    if (_alpha <= 0f)
                    {
                        if (_hideWhenFaded)
                        {
                            IsShown = false;
                            _phase = Phase.Idle;
                            _text.gameObject.SetActive(false);
                            return;
                        }

                        _text.text = _pending ?? _text.text;
                        _phase = Phase.In;
                    }

                    break;

                case Phase.In:
                    _alpha = Mathf.Min(1f, _alpha + dt / _fadeInSeconds);
                    if (_alpha >= 1f)
                    {
                        _phase = Phase.Idle;
                    }

                    break;
            }

            ApplyColor();
        }

        private void LateUpdate()
        {
            if (!IsShown || _head == null)
            {
                return;
            }

            float height = _menuVisible ? _heightOffsetMetres : _heightWithoutMenuMetres;
            PanelFollow.Follow(
                transform, _head, _distanceMetres, height, _followEaseSeconds, _maxTrailMetres,
                Time.deltaTime, ref _targetPosition, ref _hasTargetPosition);
        }

        private void ApplyColor()
        {
            // Eased, so the fade reads as a fade and not as a linear dimming; the drift follows the same curve.
            float eased = _alpha * _alpha * (3f - 2f * _alpha);

            Color c = _color;
            c.a *= eased;
            _text.color = c;

            float drift = _driftMetres * (1f - eased);
            float y = _phase == Phase.Out ? drift : -drift; // leaving drifts up; arriving rises from below
            _text.transform.localPosition = new Vector3(0f, y, 0f);
        }

        /// <summary>Break <paramref name="text"/> onto lines of at most <paramref name="maxChars"/>, on spaces.</summary>
        private static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length + 8);
            int lineLength = 0;
            foreach (string word in text.Split(' '))
            {
                if (lineLength > 0 && lineLength + 1 + word.Length > maxChars)
                {
                    sb.Append('\n');
                    lineLength = 0;
                }
                else if (lineLength > 0)
                {
                    sb.Append(' ');
                    lineLength++;
                }

                sb.Append(word);
                lineLength += word.Length;
            }

            return sb.ToString();
        }
    }
}
