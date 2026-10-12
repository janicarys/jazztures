using System;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The in-VR lesson selector surface (CLAUDE.md §3.9, §3.10) — a plain vertical list of
    /// world-space rows the learner <b>points at and pinches</b> to choose, the same
    /// point-and-pinch as the rest of the Quest's UI. A <b>dumb view</b>: it takes rows of
    /// strings and raises <see cref="Chosen"/>; it knows nothing about lessons or sessions
    /// (that lives in <c>Jazztures.App</c>, which can see <c>Jazztures.Lessons</c> — this
    /// assembly cannot).
    ///
    /// <para>
    /// Built like <see cref="LessonHud"/> — runtime <see cref="TextMesh"/>, soft-follow, no
    /// Canvas / EventSystem / TextMeshPro / SDK interactor prefab (ADR-0021). Aiming is the
    /// hand's own pointer ray (<see cref="IHand.GetPointerPose"/> — the ray every Meta ray
    /// interactor is fed); selecting is an index pinch on the hovered row. A pinch tends to
    /// tug the ray, so for a brief grace window the last-hovered row stays selectable.
    /// Number keys <c>1</c>..<c>9</c> pick a row at the desk, matching
    /// <c>KeyboardMelodyInput</c> / <c>KeyboardHandPoseSource</c>.
    /// </para>
    ///
    /// <para>
    /// Not the melody instrument: the lesson flow disables the touch-target binder while the
    /// selector is open, so this is the only thing reading a pinch off the right hand here.
    /// </para>
    /// </summary>
    public sealed class LessonSelector : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("The right Interaction SDK Hand — the learner aims its pinch ray at a row and pinches.")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Header("Placement")]
        [SerializeField] private Transform _head;
        [Tooltip("Distance in front of the learner, metres. Point-and-pinch reaches any " +
                 "distance — this only has to stay comfortably readable.")]
        [Min(0.2f)] [SerializeField] private float _distanceMetres = 0.6f;
        [SerializeField] private float _heightOffsetMetres = -0.05f;
        [Min(0f)] [SerializeField] private float _followEaseSeconds = 0.4f;

        [Header("Layout")]
        [Min(0.03f)] [SerializeField] private float _rowSpacingMetres = 0.1f;
        [Tooltip("Row hit-band width, metres — the ray may cross anywhere across it.")]
        [Min(0.05f)] [SerializeField] private float _rowWidthMetres = 0.4f;

        [Header("Pointer")]
        [Tooltip("Aim-ray thickness, metres.")]
        [Min(0.001f)] [SerializeField] private float _rayWidthMetres = 0.006f;
        [Tooltip("Drawn ray length when it is not crossing a row, metres.")]
        [Min(0.1f)] [SerializeField] private float _rayLengthMetres = 2.5f;
        [Tooltip("After the ray leaves a row it stays pinch-selectable for this long — a " +
                 "pinch usually tugs the ray off the target.")]
        [Min(0f)] [SerializeField] private float _hoverGraceSeconds = 0.18f;
        [Tooltip("Shortest gap between two pinch-selects, seconds — debounces a noisy pinch " +
                 "and stops a held pinch from cascading through menus.")]
        [Min(0.05f)] [SerializeField] private float _selectCooldownSeconds = 0.4f;
        [SerializeField] private Color _rayColor = new Color(0.55f, 0.70f, 1f, 0.5f);
        [SerializeField] private Color _rayFocusColor = new Color(1f, 0.95f, 0.7f, 0.95f);

        [Header("Side control — optional +/- stepper beside the rows")]
        [Tooltip("Side length of each +/- button's square plate, metres.")]
        [Min(0.04f)] [SerializeField] private float _sideButtonSizeMetres = 0.09f;
        [Tooltip("Clear gap between the row band and the buttons, metres.")]
        [Min(0f)] [SerializeField] private float _sideGapMetres = 0.05f;
        [Tooltip("Vertical distance from the readout to the centre of each button, metres.")]
        [Min(0.04f)] [SerializeField] private float _sideButtonPitchMetres = 0.11f;
        [Tooltip("Extra reach around each button where the ray still counts as on it, metres.")]
        [Min(0f)] [SerializeField] private float _sideHitMarginMetres = 0.015f;
        [Tooltip("Readout text size relative to a row's.")]
        [Range(0.2f, 1f)] [SerializeField] private float _sideReadoutScale = 0.45f;
        [SerializeField] private Color _sideDisabledColor = new Color(0.45f, 0.45f, 0.48f, 0.6f);

        [Header("Footer note — optional text under the rows (e.g. the last timing result)")]
        [Tooltip("Footer text size relative to a row's.")]
        [Range(0.2f, 1f)] [SerializeField] private float _footerScale = 0.4f;
        [Tooltip("Characters per line before the footer wraps (TextMesh has no wrap of its own).")]
        [Min(10)] [SerializeField] private int _footerWrapChars = 44;
        [Tooltip("Gap between the last row and the footer, metres.")]
        [Min(0f)] [SerializeField] private float _footerGapMetres = 0.07f;

        [Header("Visual")]
        [Tooltip("Optional. A translucent material for the plate behind each row. Left empty, " +
                 "a plain translucent plate is generated so the ray still has something to read against.")]
        [SerializeField] private Material _plateMaterial;

        [Header("Type")]
        [Min(0.001f)] [SerializeField] private float _characterSize = 0.006f;
        [Min(8)] [SerializeField] private int _fontSize = 90;
        [SerializeField] private Color _idleColor = new Color(0.80f, 0.80f, 0.84f, 1f);
        [SerializeField] private Color _focusColor = new Color(1f, 0.97f, 0.85f, 1f);
        [SerializeField] private Color _titleColor = new Color(0.62f, 0.66f, 0.72f, 1f);
        [SerializeField] private Color _plateIdleColor = new Color(0.16f, 0.17f, 0.20f, 0.55f);
        [SerializeField] private Color _plateFocusColor = new Color(0.30f, 0.32f, 0.24f, 0.85f);

        /// <summary>The row index the learner chose. Row order matches the strings passed to <see cref="Show"/>.</summary>
        public event Action<int> Chosen;

        /// <summary>
        /// The learner pinched a side button: −1 for the lower ("-") one, +1 for the upper
        /// ("+") one. What it steps is the caller's business — this is a dumb view.
        /// </summary>
        public event Action<int> SideStepped;

        public bool IsShown { get; private set; }

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorIdLegacy = Shader.PropertyToID("_Color");

        private IHand _hand;
        private TextMesh _title;
        private readonly List<TextMesh> _rows = new List<TextMesh>();
        private readonly List<Renderer> _plates = new List<Renderer>();
        private MaterialPropertyBlock _mpb;
        private LineRenderer _ray;
        private Material _runtimeMaterial;
        private int _rowCount;

        private int _focused = -1;
        private int _lastHoverRow = -1;
        private float _lastHoverTime = -999f;
        private float _lastSelectTime = -999f;
        private bool _wasPinching;

        // Side stepper: built on first use, so a scene that never calls SetSideControl pays nothing.
        private Renderer _sidePlusPlate;
        private Renderer _sideMinusPlate;
        private TextMesh _sidePlusText;
        private TextMesh _sideMinusText;
        private TextMesh _sideReadout;
        private bool _sideEnabled;
        private bool _canStepDown = true;
        private bool _canStepUp = true;
        private int _sideFocused;       // 0 none, −1 the "-" button, +1 the "+" button
        private int _lastSideHover;
        private float _lastSideHoverTime = -999f;

        private TextMesh _footer;
        private string _footerText = string.Empty;

        private Vector3 _targetPosition;
        private bool _hasTargetPosition;
        private float _shownWithoutHandSeconds;
        private bool _warnedNoHand;

        private void Awake()
        {
            _hand = _rightHand as IHand;
            _mpb = new MaterialPropertyBlock();
            _runtimeMaterial = RuntimeMaterial.Unlit("LessonSelector (runtime)");
            _title = CreateLine("Title", 1f, _titleColor);
            _ray = CreateRay();
            SetShown(false);

            if (_hand == null)
            {
                Debug.LogError(
                    $"{nameof(LessonSelector)}: '{(_rightHand == null ? "<none>" : _rightHand.GetType().Name)}' " +
                    $"is not an {nameof(IHand)} — point-and-pinch can't work. Number keys 1-9 still pick a row.", this);
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
        }

        /// <summary>Display <paramref name="rows"/> (top to bottom) under an optional <paramref name="title"/>.</summary>
        public void Show(IReadOnlyList<string> rows, string title = null)
        {
            EnsureRows(rows.Count);
            _title.text = title ?? string.Empty;

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < rows.Count;
                _rows[i].gameObject.SetActive(used);
                if (_plates.Count > i && _plates[i] != null)
                {
                    _plates[i].gameObject.SetActive(used);
                }

                if (used)
                {
                    _rows[i].text = rows[i];
                    _rows[i].color = _idleColor;
                }
            }

            _rowCount = rows.Count;
            _focused = -1;
            _lastHoverRow = -1;
            _sideFocused = 0;
            _lastSideHover = 0;
            _wasPinching = false;
            _hasTargetPosition = false;
            _shownWithoutHandSeconds = 0f;
            _warnedNoHand = false;
            SetShown(true);
            Layout();
            Repaint();
        }

        public void Hide() => SetShown(false);

        /// <summary>
        /// Show a "+" / "-" pair with a <paramref name="readout"/> between them, to the right
        /// of the rows (multi-line text is fine). Pinching a button raises <see cref="SideStepped"/>;
        /// a button whose direction is unavailable is dimmed and ignored. Safe to call again to
        /// update the readout. Stays until <see cref="ClearSideControl"/>.
        /// </summary>
        public void SetSideControl(string readout, bool canStepDown = true, bool canStepUp = true)
        {
            EnsureSideControl();
            _sideEnabled = true;
            _canStepDown = canStepDown;
            _canStepUp = canStepUp;
            _sideReadout.text = readout ?? string.Empty;
            SetShown(IsShown);
            LayoutSide();
            Repaint();
        }

        /// <summary>Remove the side control (e.g. on a screen where it does not apply).</summary>
        public void ClearSideControl()
        {
            _sideEnabled = false;
            _sideFocused = 0;
            _lastSideHover = 0;
            SetShown(IsShown);
        }

        /// <summary>
        /// Show a short, wrapped, non-interactive note under the rows — a deferred result
        /// (§3.7), shown once something is over rather than mid-way. An empty or null
        /// <paramref name="text"/> clears it. Stays until changed.
        /// </summary>
        public void SetFooter(string text)
        {
            _footerText = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
            if (_footer == null && _footerText.Length > 0)
            {
                _footer = CreateLine("Footer", _footerScale, _titleColor);
                _footer.anchor = TextAnchor.UpperCenter;
            }

            if (_footer != null)
            {
                _footer.text = WrapFooter(_footerText, _footerWrapChars);
                SetShown(IsShown);
                LayoutFooter();
            }
        }

        public void ClearFooter() => SetFooter(null);

        private void LayoutFooter()
        {
            if (_footer == null)
            {
                return;
            }

            float lastRowY = -(_rowCount - 1) * 0.5f * _rowSpacingMetres;
            _footer.transform.localPosition = new Vector3(
                0f, lastRowY - _rowSpacingMetres * 0.5f - _footerGapMetres, 0f);
        }

        /// <summary>Break <paramref name="text"/> onto lines of at most <paramref name="maxChars"/>, on spaces.</summary>
        private static string WrapFooter(string text, int maxChars)
        {
            if (text.Length == 0)
            {
                return text;
            }

            var sb = new System.Text.StringBuilder(text.Length + 8);
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

        /// <summary>Choose a row directly (a test hook / editor shortcut).</summary>
        public void SelectRow(int index)
        {
            if (IsShown && index >= 0 && index < _rowCount)
            {
                Chosen?.Invoke(index);
            }
        }

        private void Update()
        {
            if (!IsShown)
            {
                _ray.enabled = false;
                return;
            }

            if (DigitKeyChose())
            {
                return;
            }

            bool tracked = _hand != null && _hand.IsTrackedDataValid;
            WarnIfHandMissing(tracked);

            int hover = -1;
            int sideHover = 0;
            if (tracked && TryPointer(out Vector3 origin, out Vector3 direction))
            {
                Vector3 localOrigin = transform.InverseTransformPoint(origin);
                Vector3 localDir = transform.InverseTransformDirection(direction);
                hover = RowUnderRay(localOrigin, localDir, _rowCount,
                    _rowSpacingMetres, _rowWidthMetres * 0.5f, out Vector3 localHit);

                // The side buttons sit outside the row band, so the ray is on a row or a button, never both.
                if (hover < 0 && _sideEnabled && TryPanelPoint(localOrigin, localDir, out Vector3 panelHit))
                {
                    sideHover = SideButtonAt(panelHit);
                    if (sideHover != 0)
                    {
                        localHit = panelHit;
                    }
                }

                DrawRay(origin, direction, hover >= 0 || sideHover != 0, localHit);

                if (hover >= 0)
                {
                    _lastHoverRow = hover;
                    _lastHoverTime = Time.unscaledTime;
                }

                if (sideHover != 0)
                {
                    _lastSideHover = sideHover;
                    _lastSideHoverTime = Time.unscaledTime;
                }

                if (PinchChose(hover, sideHover))
                {
                    return;
                }
            }
            else
            {
                _ray.enabled = false;
                _wasPinching = false; // don't carry a stale pinch across a tracking gap
            }

            _focused = hover;
            _sideFocused = sideHover;
            Repaint();
        }

        private bool DigitKeyChose()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            for (int i = 0; i < _rowCount && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    ResetFocus();
                    Chosen?.Invoke(i);
                    return true;
                }
            }

            return false;
        }

        private bool PinchChose(int hover, int sideHover)
        {
            bool confident = _hand.GetFingerIsHighConfidence(HandFinger.Index)
                          && _hand.GetFingerIsHighConfidence(HandFinger.Thumb);
            bool pinching = confident && _hand.GetIndexFingerIsPinching();
            bool started = pinching && !_wasPinching;
            _wasPinching = pinching;

            if (!started)
            {
                return false;
            }

            int row = hover;
            int side = sideHover;
            if (row < 0 && side == 0)
            {
                // The pinch tugged the ray off its target — take whichever one it left most recently.
                bool rowRecent = Time.unscaledTime - _lastHoverTime <= _hoverGraceSeconds;
                bool sideRecent = _sideEnabled && Time.unscaledTime - _lastSideHoverTime <= _hoverGraceSeconds;
                if (rowRecent && (!sideRecent || _lastHoverTime >= _lastSideHoverTime))
                {
                    row = _lastHoverRow;
                }
                else if (sideRecent)
                {
                    side = _lastSideHover;
                }
            }

            if (Time.unscaledTime - _lastSelectTime < _selectCooldownSeconds)
            {
                return false;
            }

            if (row >= 0 && row < _rowCount)
            {
                _lastSelectTime = Time.unscaledTime;
                ResetFocus();
                Chosen?.Invoke(row);
                return true;
            }

            if (_sideEnabled && ((side < 0 && _canStepDown) || (side > 0 && _canStepUp)))
            {
                _lastSelectTime = Time.unscaledTime;
                SideStepped?.Invoke(side);
                return true;
            }

            return false;
        }

        private void WarnIfHandMissing(bool tracked)
        {
            if (tracked)
            {
                _shownWithoutHandSeconds = 0f;
                return;
            }

            _shownWithoutHandSeconds += Time.deltaTime;
            if (_shownWithoutHandSeconds > 3f && !_warnedNoHand)
            {
                _warnedNoHand = true;
                Debug.LogWarning(
                    $"{nameof(LessonSelector)}: no tracked right hand for 3 s — point at a row and pinch " +
                    "to choose. Over Quest Link, enable hand tracking in the Meta Quest Link app " +
                    "(Settings ▸ Beta). Number keys 1-9 also pick a row.", this);
            }
        }

        /// <summary>
        /// The hand's aim ray: the pointer pose every Meta ray interactor uses, or — when
        /// the platform supplies none — a ray straight down the index finger.
        /// </summary>
        private bool TryPointer(out Vector3 origin, out Vector3 direction)
        {
            if (_hand.IsPointerPoseValid && _hand.GetPointerPose(out Pose pointer))
            {
                origin = pointer.position;
                direction = pointer.rotation * Vector3.forward;
                return true;
            }

            if (_hand.GetJointPose(HandJointId.HandIndex1, out Pose knuckle)
                && _hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
            {
                origin = knuckle.position;
                direction = tip.position - knuckle.position;
                if (direction.sqrMagnitude > 1e-6f)
                {
                    direction.Normalize();
                    return true;
                }
            }

            origin = default;
            direction = default;
            return false;
        }

        /// <summary>
        /// Row index whose band the ray crosses on the panel plane (local z = 0), or -1. The
        /// panel is never scaled, so <paramref name="localOrigin"/> / <paramref name="localDir"/>
        /// come straight from <c>Transform.InverseTransform*</c> with the direction still unit length.
        /// Bands are contiguous (± half the spacing) so a slightly sloppy aim still lands on a row.
        /// </summary>
        internal static int RowUnderRay(
            Vector3 localOrigin, Vector3 localDir, int rowCount, float rowSpacing, float halfWidth,
            out Vector3 localHit)
        {
            localHit = default;
            if (rowCount <= 0 || Mathf.Abs(localDir.z) < 1e-5f)
            {
                return -1;
            }

            float t = -localOrigin.z / localDir.z;
            if (t <= 0f)
            {
                return -1; // the panel is behind the ray
            }

            localHit = localOrigin + t * localDir;
            if (Mathf.Abs(localHit.x) > halfWidth)
            {
                return -1;
            }

            float top = (rowCount - 1) * 0.5f * rowSpacing;
            for (int i = 0; i < rowCount; i++)
            {
                float rowY = top - i * rowSpacing;
                if (Mathf.Abs(localHit.y - rowY) <= rowSpacing * 0.5f)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Where the ray crosses the panel plane (local z = 0), in panel space — or false when
        /// the ray is parallel to it or the panel is behind. The same intersection
        /// <see cref="RowUnderRay"/> does, without the row banding.
        /// </summary>
        private static bool TryPanelPoint(Vector3 localOrigin, Vector3 localDir, out Vector3 localHit)
        {
            localHit = default;
            if (Mathf.Abs(localDir.z) < 1e-5f)
            {
                return false;
            }

            float t = -localOrigin.z / localDir.z;
            if (t <= 0f)
            {
                return false;
            }

            localHit = localOrigin + t * localDir;
            return true;
        }

        /// <summary>Panel-space x of the side buttons' centre line.</summary>
        private float SideX => _rowWidthMetres * 0.5f + _sideGapMetres + _sideButtonSizeMetres * 0.5f;

        /// <summary>+1 over the "+" button, −1 over the "-" button, 0 over neither.</summary>
        private int SideButtonAt(Vector3 localHit)
        {
            float half = _sideButtonSizeMetres * 0.5f + _sideHitMarginMetres;
            if (Mathf.Abs(localHit.x - SideX) > half)
            {
                return 0;
            }

            if (Mathf.Abs(localHit.y - _sideButtonPitchMetres) <= half)
            {
                return 1;
            }

            if (Mathf.Abs(localHit.y + _sideButtonPitchMetres) <= half)
            {
                return -1;
            }

            return 0;
        }

        private void DrawRay(Vector3 origin, Vector3 direction, bool onTarget, Vector3 localHit)
        {
            Vector3 end = onTarget
                ? transform.TransformPoint(localHit + Vector3.back * 0.006f) // sit just in front of the text
                : origin + direction * _rayLengthMetres;

            _ray.enabled = true;
            _ray.widthMultiplier = _rayWidthMetres;
            _ray.SetPosition(0, origin);
            _ray.SetPosition(1, end);

            Color near = onTarget ? _rayFocusColor : _rayColor;
            Color far = near;
            far.a *= 0.25f;
            _ray.startColor = near;
            _ray.endColor = far;
        }

        private void Repaint()
        {
            for (int i = 0; i < _rowCount; i++)
            {
                float k = i == _focused ? 1f : 0f;
                _rows[i].color = Color.Lerp(_idleColor, _focusColor, k);

                if (_plates.Count > i && _plates[i] != null)
                {
                    Write(_plates[i], Color.Lerp(_plateIdleColor, _plateFocusColor, k));
                }
            }

            if (_sideEnabled && _sidePlusPlate != null)
            {
                PaintSideButton(_sidePlusPlate, _sidePlusText, 1, _canStepUp);
                PaintSideButton(_sideMinusPlate, _sideMinusText, -1, _canStepDown);
                _sideReadout.color = _titleColor;
            }
        }

        private void PaintSideButton(Renderer plate, TextMesh glyph, int direction, bool available)
        {
            float k = available && _sideFocused == direction ? 1f : 0f;
            Write(plate, available ? Color.Lerp(_plateIdleColor, _plateFocusColor, k) : _plateIdleColor);
            glyph.color = available ? Color.Lerp(_idleColor, _focusColor, k) : _sideDisabledColor;
        }

        private void LateUpdate()
        {
            if (!IsShown || _head == null)
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
                float k = _followEaseSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / _followEaseSeconds);
                _targetPosition = Vector3.Lerp(_targetPosition, wanted, k);
            }

            transform.position = _targetPosition;
            transform.rotation = Quaternion.LookRotation(_targetPosition - _head.position, Vector3.up);
        }

        private void Layout()
        {
            float top = (_rowCount - 1) * 0.5f * _rowSpacingMetres;
            _title.transform.localPosition = new Vector3(0f, top + _rowSpacingMetres * 1.3f, 0f);

            for (int i = 0; i < _rows.Count; i++)
            {
                float y = top - i * _rowSpacingMetres;
                _rows[i].transform.localPosition = new Vector3(0f, y, 0f);
                if (_plates.Count > i && _plates[i] != null)
                {
                    _plates[i].transform.localPosition = new Vector3(0f, y, 0.01f);
                    _plates[i].transform.localScale = new Vector3(_rowWidthMetres, _rowSpacingMetres * 0.9f, 1f);
                }
            }

            LayoutSide();
            LayoutFooter();
        }

        private void LayoutSide()
        {
            if (_sidePlusPlate == null)
            {
                return;
            }

            float x = SideX;
            var plateScale = new Vector3(_sideButtonSizeMetres, _sideButtonSizeMetres, 1f);

            _sidePlusPlate.transform.localPosition = new Vector3(x, _sideButtonPitchMetres, 0.01f);
            _sidePlusPlate.transform.localScale = plateScale;
            _sideMinusPlate.transform.localPosition = new Vector3(x, -_sideButtonPitchMetres, 0.01f);
            _sideMinusPlate.transform.localScale = plateScale;

            _sidePlusText.transform.localPosition = new Vector3(x, _sideButtonPitchMetres, 0f);
            _sideMinusText.transform.localPosition = new Vector3(x, -_sideButtonPitchMetres, 0f);
            _sideReadout.transform.localPosition = new Vector3(x, 0f, 0f);
        }

        private void EnsureSideControl()
        {
            if (_sidePlusPlate != null)
            {
                return;
            }

            _sidePlusPlate = CreatePlate("SidePlusPlate");
            _sideMinusPlate = CreatePlate("SideMinusPlate");
            _sidePlusText = CreateLine("SidePlus", 1.3f, _idleColor);
            _sidePlusText.text = "+";
            _sideMinusText = CreateLine("SideMinus", 1.3f, _idleColor);
            _sideMinusText.text = "-";
            _sideReadout = CreateLine("SideReadout", _sideReadoutScale, _titleColor);
        }

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                _rows.Add(CreateLine($"Row{_rows.Count}", 1f, _idleColor));
                _plates.Add(CreatePlate(_rows.Count - 1));
            }
        }

        private void ResetFocus()
        {
            _focused = -1;
            Repaint();
        }

        private void SetShown(bool shown)
        {
            IsShown = shown;
            _title.gameObject.SetActive(shown && !string.IsNullOrEmpty(_title.text));
            for (int i = 0; i < _rows.Count; i++)
            {
                bool on = shown && i < _rowCount;
                _rows[i].gameObject.SetActive(on);
                if (_plates.Count > i && _plates[i] != null)
                {
                    _plates[i].gameObject.SetActive(on);
                }
            }

            if (_footer != null)
            {
                _footer.gameObject.SetActive(shown && _footerText.Length > 0);
            }

            if (_sidePlusPlate != null)
            {
                bool sideOn = shown && _sideEnabled;
                _sidePlusPlate.gameObject.SetActive(sideOn);
                _sideMinusPlate.gameObject.SetActive(sideOn);
                _sidePlusText.gameObject.SetActive(sideOn);
                _sideMinusText.gameObject.SetActive(sideOn);
                _sideReadout.gameObject.SetActive(sideOn);
            }

            if (_ray != null && !shown)
            {
                _ray.enabled = false;
            }
        }

        private void Write(Renderer r, Color color)
        {
            _mpb.Clear();
            _mpb.SetColor(ColorId, color);
            _mpb.SetColor(ColorIdLegacy, color);
            r.SetPropertyBlock(_mpb);
        }

        private LineRenderer CreateRay()
        {
            var go = new GameObject("Pointer Ray");
            go.transform.SetParent(transform, worldPositionStays: false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthMultiplier = _rayWidthMetres;
            line.sharedMaterial = _runtimeMaterial;
            line.enabled = false;
            return line;
        }

        private Renderer CreatePlate(int index) => CreatePlate($"Plate{index}");

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
    }
}
