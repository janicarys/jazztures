using System;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The in-VR lesson selector surface (CLAUDE.md §3.9, §3.10) — a plain vertical list of
    /// world-space rows the learner dwells a fingertip on to choose. A <b>dumb view</b>: it
    /// takes rows of strings and raises <see cref="Chosen"/>; it knows nothing about
    /// lessons or sessions (that lives in <c>Jazztures.App</c>, which can see
    /// <c>Jazztures.Lessons</c> — this assembly cannot).
    ///
    /// <para>
    /// Built like <see cref="LessonHud"/> — runtime <see cref="TextMesh"/>, soft-follow, no
    /// Canvas / EventSystem / TextMeshPro / SDK interactor (ADR-0021). Selection is a
    /// forgiving <b>dwell</b> inside a per-row box (a menu needs no depth judgment, and
    /// dwell keeps the selector clear of the melody pinch). Number keys <c>1</c>..<c>9</c>
    /// pick a row at the desk, matching <c>KeyboardMelodyInput</c> / <c>KeyboardHandPoseSource</c>.
    /// </para>
    /// </summary>
    public sealed class LessonSelector : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("The right Interaction SDK Hand — the learner dwells its index fingertip in a row.")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Header("Placement")]
        [SerializeField] private Transform _head;
        [Min(0.2f)] [SerializeField] private float _distanceMetres = 0.75f;
        [SerializeField] private float _heightOffsetMetres = -0.05f;
        [Min(0f)] [SerializeField] private float _followEaseSeconds = 0.4f;

        [Header("Layout")]
        [Min(0.03f)] [SerializeField] private float _rowSpacingMetres = 0.11f;
        [Tooltip("Row hit-box width, metres — reach a fingertip anywhere across it.")]
        [Min(0.05f)] [SerializeField] private float _rowWidthMetres = 0.34f;
        [Tooltip("Row hit-box depth along the approach axis, metres — no precise depth needed.")]
        [Min(0.02f)] [SerializeField] private float _reachDepthMetres = 0.10f;
        [Tooltip("Seconds the fingertip must dwell in a row to choose it.")]
        [Min(0.1f)] [SerializeField] private float _dwellSeconds = 0.5f;

        [Header("Visual")]
        [Tooltip("Optional. A translucent material for a plate behind each row — gives the " +
                 "fingertip something to aim at. Leave empty for text only.")]
        [SerializeField] private Material _plateMaterial;

        [Header("Type")]
        [Min(0.001f)] [SerializeField] private float _characterSize = 0.006f;
        [Min(8)] [SerializeField] private int _fontSize = 90;
        [SerializeField] private Color _idleColor = new Color(0.80f, 0.80f, 0.84f, 1f);
        [SerializeField] private Color _focusColor = new Color(1f, 0.97f, 0.85f, 1f);
        [SerializeField] private Color _titleColor = new Color(0.62f, 0.66f, 0.72f, 1f);
        [SerializeField] private Color _plateIdleColor = new Color(0.16f, 0.17f, 0.20f, 0.55f);
        [SerializeField] private Color _plateFocusColor = new Color(0.30f, 0.32f, 0.24f, 0.8f);

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
        private int _rowCount;

        private int _focused = -1;
        private float _dwell;
        private Vector3 _targetPosition;
        private bool _hasTargetPosition;

        private void Awake()
        {
            _hand = _rightHand as IHand;
            _mpb = new MaterialPropertyBlock();
            _title = CreateLine("Title", 1f, _titleColor);
            SetShown(false);
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
            _dwell = 0f;
            _hasTargetPosition = false;
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
                return;
            }

            // Desk fallback: number keys pick a row (matches KeyboardMelodyInput).
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                for (int i = 0; i < _rowCount && i < 9; i++)
                {
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                    {
                        int chosen = i;
                        ResetFocus();
                        Chosen?.Invoke(chosen);
                        return;
                    }
                }
            }

            int nearest = FingertipRow();

            if (nearest != _focused)
            {
                _focused = nearest;
                _dwell = 0f;
            }
            else if (_focused >= 0)
            {
                _dwell += Time.deltaTime;
                if (_dwell >= _dwellSeconds)
                {
                    int chosen = _focused;
                    ResetFocus();
                    Chosen?.Invoke(chosen);
                    return;
                }
            }

            Repaint();
        }

        /// <summary>Which row the right index fingertip is inside, or -1.</summary>
        private int FingertipRow()
        {
            if (_hand == null || !_hand.IsTrackedDataValid
                || !_hand.GetJointPose(HandJointId.HandIndexTip, out Pose tip))
            {
                return -1;
            }

            Vector3 local = transform.InverseTransformPoint(tip.position);
            if (Mathf.Abs(local.x) > _rowWidthMetres * 0.5f || Mathf.Abs(local.z) > _reachDepthMetres * 0.5f)
            {
                return -1;
            }

            float top = (_rowCount - 1) * 0.5f * _rowSpacingMetres;
            for (int i = 0; i < _rowCount; i++)
            {
                float rowY = top - i * _rowSpacingMetres;
                if (Mathf.Abs(local.y - rowY) <= _rowSpacingMetres * 0.5f)
                {
                    return i;
                }
            }

            return -1;
        }

        private void Repaint()
        {
            for (int i = 0; i < _rowCount; i++)
            {
                float k = i == _focused ? Mathf.Clamp01(_dwell / _dwellSeconds) : 0f;
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
                _plates.Add(_plateMaterial != null ? CreatePlate(_rows.Count - 1) : null);
            }
        }

        private void ResetFocus()
        {
            _focused = -1;
            _dwell = 0f;
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
        }

        private void Write(Renderer r, Color color)
        {
            _mpb.Clear();
            _mpb.SetColor(ColorId, color);
            _mpb.SetColor(ColorIdLegacy, color);
            r.SetPropertyBlock(_mpb);
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
            renderer.sharedMaterial = _plateMaterial;
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
