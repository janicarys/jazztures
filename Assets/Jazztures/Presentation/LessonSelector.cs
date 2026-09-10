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
            _wasPinching = false;
            _hasTargetPosition = false;
            _shownWithoutHandSeconds = 0f;
            _warnedNoHand = false;
            SetShown(true);
            Layout();
            Repaint();
        }

        public void Hide() => SetShown(false);

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
            if (tracked && TryPointer(out Vector3 origin, out Vector3 direction))
            {
                Vector3 localOrigin = transform.InverseTransformPoint(origin);
                Vector3 localDir = transform.InverseTransformDirection(direction);
                hover = RowUnderRay(localOrigin, localDir, _rowCount,
                    _rowSpacingMetres, _rowWidthMetres * 0.5f, out Vector3 localHit);

                DrawRay(origin, direction, hover, localHit);

                if (hover >= 0)
                {
                    _lastHoverRow = hover;
                    _lastHoverTime = Time.unscaledTime;
                }

                if (PinchChose(hover))
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

        private bool PinchChose(int hover)
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
            if (row < 0 && Time.unscaledTime - _lastHoverTime <= _hoverGraceSeconds)
            {
                row = _lastHoverRow; // the pinch tugged the ray off — take the row it left
            }

            if (row < 0 || row >= _rowCount
                || Time.unscaledTime - _lastSelectTime < _selectCooldownSeconds)
            {
                return false;
            }

            _lastSelectTime = Time.unscaledTime;
            ResetFocus();
            Chosen?.Invoke(row);
            return true;
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

        private void DrawRay(Vector3 origin, Vector3 direction, int hover, Vector3 localHit)
        {
            Vector3 end = hover >= 0
                ? transform.TransformPoint(localHit + Vector3.back * 0.006f) // sit just in front of the text
                : origin + direction * _rayLengthMetres;

            _ray.enabled = true;
            _ray.widthMultiplier = _rayWidthMetres;
            _ray.SetPosition(0, origin);
            _ray.SetPosition(1, end);

            Color near = hover >= 0 ? _rayFocusColor : _rayColor;
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

        private Renderer CreatePlate(int index)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Plate{index}";
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
