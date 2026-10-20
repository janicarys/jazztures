using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The menu surface (CLAUDE.md §3.10; ADR-0051): a single row of cards that scrolls under a
    /// fixed highlight, so the card you would choose is always dead centre and its neighbours
    /// trail off either side. A <b>dumb view</b> — it takes labels and a highlighted index and
    /// draws them; it reads no input and raises no events. What the cards mean, and what moves
    /// the highlight, is the menu navigator's business (<c>Jazztures.Core.Menu</c>), driven by
    /// the right hand's flick, strike and fist.
    ///
    /// <para>
    /// Built like <see cref="LessonHud"/> — runtime <see cref="TextMesh"/>, soft-follow in front
    /// of the learner, no Canvas / EventSystem / TextMeshPro (ADR-0021). It replaces the
    /// point-and-pinch <c>LessonSelector</c>; the whole pointer ray and side-stepper went with it.
    /// </para>
    ///
    /// <para>
    /// <b>Feedback.</b> With no haptics (§3.10) the menu has to say what the hand just did, so it
    /// answers every gesture and nothing else: a <b>transition</b> when the screen changes (the
    /// old cards drift and fade, the new ones rise or settle in — see <see cref="MenuTransition"/>),
    /// a <b>pulse</b> on the highlighted card when it is struck, and a <b>bump</b> when a flick
    /// cannot move the highlight at all (a one-card row; any longer row wraps), so "recognised,
    /// nowhere to go" is distinguishable from "not recognised". All short, all subtle, all on <see cref="Time.unscaledDeltaTime"/>.
    /// </para>
    ///
    /// <para>
    /// It also draws the one cue the fist needs: a thin bar that fills while the fist is held
    /// (<see cref="SetHoldProgress"/>), visible whether or not any cards are, because the fist
    /// also leaves a running lesson, where a hold with no feedback would just feel like latency.
    /// Visual restraint (§3.10): the bar exists only while a fist is closing.
    /// </para>
    /// </summary>
    public sealed class MenuView : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("The centre-eye / head transform. Leave empty to stay where it is placed.")]
        [SerializeField] private Transform _head;
        [Tooltip("Distance in front of the learner, metres.")]
        [Min(0.2f)] [SerializeField] private float _distanceMetres = 0.8f;
        [SerializeField] private float _heightOffsetMetres = -0.05f;
        [Min(0f)] [SerializeField] private float _followEaseSeconds = 0.4f;
        [Tooltip("The panel never trails the point it belongs at by more than this, metres. Within it the panel eases " +
                 "softly; beyond it, it is carried along with the head, so it is never far out of view - after a quick " +
                 "turn, or when it first appears (ADR-0056).")]
        [Min(0f)] [SerializeField] private float _maxTrailMetres = 0.2f;

        [Header("Cards")]
        [Min(0.1f)] [SerializeField] private float _cardWidthMetres = 0.30f;
        [Min(0.05f)] [SerializeField] private float _cardHeightMetres = 0.17f;
        [Min(0f)] [SerializeField] private float _cardGapMetres = 0.04f;
        [Tooltip("How many neighbours are drawn either side of the highlighted card.")]
        [Range(1, 4)] [SerializeField] private int _visibleEachSide = 2;
        [Tooltip("Size of a card one step away from the highlight, relative to the highlighted one.")]
        [Range(0.3f, 1f)] [SerializeField] private float _neighbourScale = 0.72f;
        [Tooltip("Seconds for the row to glide to a new highlight. 0 = jump.")]
        [Min(0f)] [SerializeField] private float _scrollEaseSeconds = 0.08f;
        [Tooltip("Characters per line inside a card (TextMesh has no wrap of its own).")]
        [Min(4)] [SerializeField] private int _wrapChars = 10;

        [Header("Title and footer")]
        [Range(0.2f, 1f)] [SerializeField] private float _titleScale = 0.8f;
        [Range(0.2f, 1f)] [SerializeField] private float _footerScale = 0.7f;
        [Tooltip("Characters per line before the footer wraps.")]
        [Min(10)] [SerializeField] private int _footerWrapChars = 44;
        [Tooltip("Gap between the cards and the title / footer, metres.")]
        [Min(0f)] [SerializeField] private float _captionGapMetres = 0.1f;

        [Header("Transition — changing screen (ADR-0051)")]
        [Tooltip("Seconds the old cards take to fade and drift away.")]
        [Min(0f)] [SerializeField] private float _fadeOutSeconds = 0.10f;
        [Tooltip("Seconds the new cards take to fade and settle in.")]
        [Min(0f)] [SerializeField] private float _fadeInSeconds = 0.18f;
        [Tooltip("How far the cards drift up or down during a transition, metres. 0 = a plain fade.")]
        [Min(0f)] [SerializeField] private float _slideMetres = 0.06f;

        [Header("Feedback — strike pulse and end-of-row bump")]
        [Tooltip("Seconds the highlighted card's pop takes to settle after a strike.")]
        [Min(0.01f)] [SerializeField] private float _pulseSeconds = 0.22f;
        [Tooltip("How much bigger the highlighted card swells at the peak of a strike pulse (0.12 = 12%).")]
        [Range(0f, 0.4f)] [SerializeField] private float _pulseScale = 0.12f;
        [SerializeField] private Color _pulseColor = Color.white;
        [Tooltip("How far, in cards, the row is nudged when a flick runs into the end of it.")]
        [Range(0f, 0.5f)] [SerializeField] private float _bumpCards = 0.2f;
        [Tooltip("Seconds the row takes to spring back from a bump.")]
        [Min(0.01f)] [SerializeField] private float _bumpSeconds = 0.14f;

        [Header("Fist hold bar")]
        [Min(0.05f)] [SerializeField] private float _holdBarWidthMetres = 0.30f;
        [Min(0.002f)] [SerializeField] private float _holdBarHeightMetres = 0.012f;
        [SerializeField] private Color _holdBarColor = new Color(1f, 0.82f, 0.45f, 0.9f);

        [Header("Visual")]
        [Tooltip("Optional. A translucent material for the plate behind each card. Left empty, a plain " +
                 "translucent plate is generated.")]
        [SerializeField] private Material _plateMaterial;

        [Header("Type")]
        [Min(0.001f)] [SerializeField] private float _characterSize = 0.005f;
        [Min(8)] [SerializeField] private int _fontSize = 90;
        [SerializeField] private Color _idleColor = new Color(0.80f, 0.80f, 0.84f, 1f);
        [SerializeField] private Color _focusColor = new Color(1f, 0.97f, 0.85f, 1f);
        [SerializeField] private Color _titleColor = new Color(0.62f, 0.66f, 0.72f, 1f);
        [SerializeField] private Color _plateIdleColor = new Color(0.16f, 0.17f, 0.20f, 0.55f);
        [SerializeField] private Color _plateFocusColor = new Color(0.30f, 0.32f, 0.24f, 0.85f);

        public bool IsShown { get; private set; }

        private enum Phase
        {
            Idle,
            Out,
            In,
        }

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorIdLegacy = Shader.PropertyToID("_Color");

        private readonly List<TextMesh> _labels = new List<TextMesh>();
        private readonly List<Renderer> _plates = new List<Renderer>();

        private MaterialPropertyBlock _mpb;
        private Material _runtimeMaterial;
        private TextMesh _title;
        private TextMesh _footer;
        private Renderer _holdBar;

        private int _count;
        private int _selected;
        private float _scroll;       // where the row is drawn; unbounded, wrapped when laid out
        private float _scrollTarget; // where it is gliding to - the highlight, by the short way round
        private float _holdProgress;

        // Transition: the whole content (cards, title, footer) fades by _alpha and sits _offsetY
        // above or below its place. The new content is held in _pending until the old has faded out.
        private Phase _phase;
        private float _phaseT;
        private int _direction = 1; // +1 deeper (old drifts up), -1 shallower (old sinks)
        private float _alpha = 1f;
        private float _offsetY;
        private bool _hasPending;
        private readonly List<string> _pendingLabels = new List<string>();
        private int _pendingSelected;
        private string _pendingTitle;
        private string _pendingFooter;

        private float _pulse; // 1 at a strike, decaying to 0
        private float _bump;  // cards; signed, decaying to 0

        private Vector3 _targetPosition;
        private bool _hasTargetPosition;

        private float Pitch => _cardWidthMetres + _cardGapMetres;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            _runtimeMaterial = RuntimeMaterial.Unlit("MenuView (runtime)");

            _title = CreateLine("Title", _titleScale, _titleColor);
            _footer = CreateLine("Footer", _footerScale, _titleColor);
            _holdBar = CreatePlate("HoldBar");
            Write(_holdBar, _holdBarColor);
            _holdBar.gameObject.SetActive(false);

            Hide();
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
        }

        /// <summary>
        /// Draw <paramref name="labels"/> left to right with <paramref name="selected"/> highlighted.
        /// With <see cref="MenuTransition.None"/> this is the same screen redrawn — the row glides to
        /// the highlight and a card's label may change in place. Otherwise it is a different screen:
        /// the old cards drift and fade out, then these rise or settle in
        /// (<see cref="MenuTransition"/>). A view that was hidden just fades in.
        /// </summary>
        public void Show(
            IReadOnlyList<string> labels,
            int selected,
            string title = null,
            string footer = null,
            MenuTransition transition = MenuTransition.None)
        {
            int direction = transition == MenuTransition.Shallower ? -1 : 1;

            if (!IsShown)
            {
                ApplyContent(labels, selected, title, footer, snap: true);
                IsShown = true;
                _hasPending = false;
                BeginPhase(Phase.In, 0f, direction);
                RefreshAll();
                return;
            }

            if (_phase == Phase.Out)
            {
                // Still fading the old screen away: only the destination matters now.
                StashPending(labels, selected, title, footer);
                return;
            }

            if (transition == MenuTransition.None)
            {
                ApplyContent(labels, selected, title, footer, snap: false);
                RefreshAll();
                return;
            }

            StashPending(labels, selected, title, footer);
            // From mid-fade-in, start fading out from the alpha already reached rather than popping to full.
            BeginPhase(Phase.Out, _phase == Phase.In ? 1f - _phaseT : 0f, direction);
            RefreshAll();
        }

        public void Hide()
        {
            IsShown = false;
            _hasPending = false;
            _phase = Phase.Idle;
            UpdateFade();
            ApplyLayout();
        }

        /// <summary>Move the highlight to <paramref name="index"/>; the row glides to it.</summary>
        public void Select(int index)
        {
            _selected = Mathf.Clamp(index, 0, Mathf.Max(0, _count - 1));
            RetargetScroll();
        }

        /// <summary>
        /// Acknowledge a strike: the highlighted card swells and flashes, then settles. Call it as
        /// the strike arrives — on a card that stays (the tempo card) it is the whole answer, and on
        /// one that leads to another screen it is seen for the instant before the transition.
        /// </summary>
        public void Pulse()
        {
            _pulse = 1f;
            if (IsShown)
            {
                ApplyLayout();
            }
        }

        /// <summary>
        /// Acknowledge a flick that ran into the end of the row: the row is nudged
        /// <paramref name="direction"/> cards' worth (+1 toward later cards) and springs back, so the
        /// learner can tell "recognised, nowhere to go" from "not recognised".
        /// </summary>
        public void Bump(int direction)
        {
            _bump = Mathf.Sign(direction) * _bumpCards;
            if (IsShown)
            {
                ApplyLayout();
            }
        }

        /// <summary>
        /// How far through a fist hold the hand is, 0..1. Anything above 0 draws the bar; it is
        /// shown even when the cards are hidden (a fist also leaves a running lesson).
        /// </summary>
        public void SetHoldProgress(float progress)
        {
            _holdProgress = Mathf.Clamp01(progress);
            UpdateHoldBar();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_pulse > 0f)
            {
                _pulse = Mathf.Max(0f, _pulse - dt / _pulseSeconds);
            }

            if (_bump != 0f)
            {
                float decay = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, _bumpSeconds * 0.5f));
                _bump = Mathf.Abs(_bump) < 0.002f ? 0f : _bump - _bump * decay;
            }

            if (!IsShown)
            {
                return;
            }

            bool animating = _pulse > 0f || _bump != 0f;

            switch (_phase)
            {
                case Phase.Out:
                    _phaseT += dt / Mathf.Max(0.001f, _fadeOutSeconds);
                    if (_phaseT >= 1f)
                    {
                        CommitPending();
                        BeginPhase(Phase.In, 0f, _direction);
                    }

                    animating = true;
                    break;

                case Phase.In:
                    _phaseT += dt / Mathf.Max(0.001f, _fadeInSeconds);
                    if (_phaseT >= 1f)
                    {
                        _phase = Phase.Idle;
                        _phaseT = 1f;
                    }

                    animating = true;
                    break;
            }

            float gap = _scrollTarget - _scroll;
            if (gap != 0f)
            {
                float k = _scrollEaseSeconds <= 0f ? 1f : 1f - Mathf.Exp(-dt / _scrollEaseSeconds);
                _scroll += gap * k;
                if (Mathf.Abs(_scrollTarget - _scroll) < 0.001f)
                {
                    _scroll = _scrollTarget;
                }

                // Keep the numbers small however many times the row has gone round.
                if (_count > 0)
                {
                    float shift = Mathf.Floor(_scrollTarget / _count) * _count;
                    _scroll -= shift;
                    _scrollTarget -= shift;
                }

                animating = true;
            }

            if (animating)
            {
                UpdateFade();
                ApplyLayout();
            }
        }

        private void LateUpdate()
        {
            if ((!IsShown && _holdProgress <= 0f) || _head == null)
            {
                return;
            }

            PanelFollow.Follow(
                transform, _head, _distanceMetres, _heightOffsetMetres, _followEaseSeconds, _maxTrailMetres,
                Time.deltaTime, ref _targetPosition, ref _hasTargetPosition);
        }

        // --- content and transition ---------------------------------------------------------

        private void ApplyContent(
            IReadOnlyList<string> labels, int selected, string title, string footer, bool snap)
        {
            EnsureCards(labels.Count);
            _count = labels.Count;

            for (int i = 0; i < _count; i++)
            {
                _labels[i].text = Wrap(labels[i], _wrapChars);
            }

            _selected = Mathf.Clamp(selected, 0, Mathf.Max(0, _count - 1));
            if (snap)
            {
                _scroll = _selected;
                _scrollTarget = _selected;
                _hasTargetPosition = false;
                _bump = 0f;
            }
            else
            {
                RetargetScroll();
            }

            _title.text = title ?? string.Empty;
            _footer.text = string.IsNullOrWhiteSpace(footer) ? string.Empty : Wrap(footer, _footerWrapChars);
        }

        /// <summary>
        /// Glide to the highlight the short way round: from the last card to the first is one step
        /// forward, not a sweep back across the whole row.
        /// </summary>
        private void RetargetScroll() => _scrollTarget = _scroll + WrapOffset(_selected - _scroll, _count);

        /// <summary>
        /// <paramref name="offset"/> (cards from the highlight) wrapped onto the ring of
        /// <paramref name="count"/> cards, into (−count/2, count/2]: every card is drawn once, at its
        /// nearest position to the highlight.
        /// </summary>
        private static float WrapOffset(float offset, int count)
        {
            if (count <= 1)
            {
                return offset;
            }

            float half = count * 0.5f;
            return offset - count * Mathf.Ceil((offset - half) / count);
        }

        private void StashPending(IReadOnlyList<string> labels, int selected, string title, string footer)
        {
            _pendingLabels.Clear();
            for (int i = 0; i < labels.Count; i++)
            {
                _pendingLabels.Add(labels[i]);
            }

            _pendingSelected = selected;
            _pendingTitle = title;
            _pendingFooter = footer;
            _hasPending = true;
        }

        private void CommitPending()
        {
            if (!_hasPending)
            {
                return;
            }

            ApplyContent(_pendingLabels, _pendingSelected, _pendingTitle, _pendingFooter, snap: true);
            _hasPending = false;
        }

        private void BeginPhase(Phase phase, float startT, int direction)
        {
            _phase = phase;
            _phaseT = startT;
            _direction = direction;
            UpdateFade();
        }

        /// <summary>Alpha is linear in the phase's progress (so a fade can be reversed without a pop); the drift is eased.</summary>
        private void UpdateFade()
        {
            switch (_phase)
            {
                case Phase.Out:
                    _alpha = 1f - Mathf.Clamp01(_phaseT);
                    _offsetY = _direction * _slideMetres * Smooth(_phaseT);
                    break;

                case Phase.In:
                    _alpha = Mathf.Clamp01(_phaseT);
                    _offsetY = -_direction * _slideMetres * (1f - Smooth(_phaseT));
                    break;

                default:
                    _alpha = 1f;
                    _offsetY = 0f;
                    break;
            }
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void RefreshAll()
        {
            LayoutCaptions();
            UpdateFade();
            ApplyLayout();
        }

        // --- layout ---------------------------------------------------------------------------

        /// <summary>Place and colour every card for the current scroll, fade, drift, pulse and bump.</summary>
        private void ApplyLayout()
        {
            bool hasTitle = IsShown && _title.text.Length > 0;
            bool hasFooter = IsShown && _footer.text.Length > 0;
            _title.gameObject.SetActive(hasTitle);
            _footer.gameObject.SetActive(hasFooter);

            float captionHalf = _cardHeightMetres * 0.5f + _captionGapMetres;
            _title.transform.localPosition = new Vector3(0f, captionHalf + _offsetY, 0f);
            _footer.transform.localPosition = new Vector3(0f, -captionHalf + _offsetY, 0f);
            _title.color = WithAlpha(_titleColor, _alpha);
            _footer.color = WithAlpha(_titleColor, _alpha);

            float scroll = _scroll + _bump;

            for (int i = 0; i < _labels.Count; i++)
            {
                float offset = WrapOffset(i - scroll, _count);
                float distance = Mathf.Abs(offset);
                bool visible = IsShown && i < _count && distance <= _visibleEachSide + 0.6f;

                _labels[i].gameObject.SetActive(visible);
                _plates[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                float near = Mathf.Clamp01(distance);
                float size = Mathf.Lerp(1f, _neighbourScale, near);
                float focus = 1f - near;
                float fade = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(distance - 1f)) * _alpha;

                // The card on the far side of the ring changes sides as the row turns; fade it out as it
                // gets there so it never visibly jumps. (Two cards: the other one is always that card.)
                if (_count > 2)
                {
                    fade *= Mathf.Clamp01((_count * 0.5f - distance) / 0.5f);
                }

                // The pulse belongs to the highlighted card only, and fades with its distance from it.
                float pulse = _pulse * focus;
                size *= 1f + _pulseScale * pulse;

                float x = offset * Pitch;
                _labels[i].transform.localPosition = new Vector3(x, _offsetY, 0f);
                _labels[i].transform.localScale = new Vector3(size, size, 1f);
                _plates[i].transform.localPosition = new Vector3(x, _offsetY, 0.01f);
                _plates[i].transform.localScale = new Vector3(_cardWidthMetres * size, _cardHeightMetres * size, 1f);

                Color text = Color.Lerp(Color.Lerp(_idleColor, _focusColor, focus), _pulseColor, pulse);
                text.a *= fade;
                _labels[i].color = text;

                Color plate = Color.Lerp(_plateIdleColor, _plateFocusColor, focus);
                plate = Color.Lerp(plate, _pulseColor, pulse * 0.35f);
                plate.a = Mathf.Lerp(_plateIdleColor.a, _plateFocusColor.a, focus) * fade;
                Write(_plates[i], plate);
            }
        }

        private void LayoutCaptions() => UpdateHoldBar();

        private void UpdateHoldBar()
        {
            if (_holdBar == null)
            {
                return;
            }

            bool on = _holdProgress > 0.001f;
            _holdBar.gameObject.SetActive(on);
            if (!on)
            {
                return;
            }

            float width = _holdBarWidthMetres * _holdProgress;
            float y = -(_cardHeightMetres * 0.5f + _captionGapMetres * 0.5f);
            _holdBar.transform.localPosition = new Vector3(-_holdBarWidthMetres * 0.5f + width * 0.5f, y, 0.005f);
            _holdBar.transform.localScale = new Vector3(width, _holdBarHeightMetres, 1f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        private void EnsureCards(int count)
        {
            while (_labels.Count < count)
            {
                _labels.Add(CreateLine($"Card{_labels.Count}", 1f, _idleColor));
                _plates.Add(CreatePlate($"Plate{_plates.Count}"));
            }
        }

        private void Write(Renderer renderer, Color color)
        {
            _mpb.Clear();
            _mpb.SetColor(ColorId, color);
            _mpb.SetColor(ColorIdLegacy, color);
            renderer.SetPropertyBlock(_mpb);
        }

        private Renderer CreatePlate(string plateName)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = plateName;
            if (go.TryGetComponent(out Collider collider))
            {
                Destroy(collider);
            }

            go.transform.SetParent(transform, worldPositionStays: false);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = _plateMaterial != null ? _plateMaterial : _runtimeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Write(renderer, _plateIdleColor);
            return renderer;
        }

        private TextMesh CreateLine(string lineName, float scale, Color color)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, worldPositionStays: false);
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

        /// <summary>
        /// Break <paramref name="text"/> onto lines of at most <paramref name="maxChars"/>, on
        /// spaces, keeping any line breaks already in it.
        /// </summary>
        private static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length + 8);
            int lineLength = 0;

            foreach (string paragraph in text.Split('\n'))
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                    lineLength = 0;
                }

                foreach (string word in paragraph.Split(' '))
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
            }

            return sb.ToString();
        }
    }
}
