using System;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jazztures.Presentation
{
    /// <summary>
    /// One "Exit" control that floats above the learner during any lesson or free play.
    /// Reach a hand up to it and pinch to leave — overhead, out of the play space, and a
    /// deliberate pinch, so it is close to impossible to trigger by accident while the
    /// hands work in front of the body (CLAUDE.md §3.10 — one justified affordance).
    ///
    /// <para>
    /// A dumb view: it raises <see cref="Exited"/> and nothing else. The flow controller
    /// owns what leaving means. Never touches domain state (§2.3).
    /// </para>
    /// </summary>
    public sealed class ExitButton : MonoBehaviour
    {
        [Header("Anchor")]
        [Tooltip("The centre-eye / head transform. The button rides above it. Falls back to Camera.main.")]
        [SerializeField] private Transform _head;
        [Tooltip("Height above the head, metres — clear of eye line, still an easy reach up.")]
        [Min(0.1f)] [SerializeField] private float _heightMetres = 0.34f;
        [Tooltip("Forward of the head, metres — slightly ahead so a glance up finds it.")]
        [SerializeField] private float _forwardMetres = 0.12f;
        [Min(0f)] [SerializeField] private float _followEaseSeconds = 0.25f;

        [Header("Hands (either can press it)")]
        [SerializeField] private MonoBehaviour _rightHand;
        [SerializeField] private MonoBehaviour _leftHand;

        [Header("Activation")]
        [Tooltip("How close the pinch (thumb/index midpoint) must be to the button to arm it, metres.")]
        [Min(0.04f)] [SerializeField] private float _reachRadiusMetres = 0.13f;
        [Tooltip("Ignore a second exit for this long, seconds.")]
        [Min(0.1f)] [SerializeField] private float _cooldownSeconds = 0.8f;

        [Header("Look")]
        [Min(0.02f)] [SerializeField] private float _discRadiusMetres = 0.055f;
        [Min(0.001f)] [SerializeField] private float _characterSize = 0.004f;
        [SerializeField] private Color _idleColor = new Color(0.46f, 0.13f, 0.15f, 0.66f);
        [SerializeField] private Color _armedColor = new Color(1f, 0.42f, 0.36f, 0.96f);
        [SerializeField] private Color _textColor = new Color(1f, 0.95f, 0.92f, 1f);

        /// <summary>Raised when the learner reaches up and pinches the button.</summary>
        public event Action Exited;

        public bool IsShown { get; private set; }

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorIdLegacy = Shader.PropertyToID("_Color");

        private IHand _right;
        private IHand _left;
        private Transform _visual;
        private Renderer _disc;
        private TextMesh _label;
        private Material _material;
        private MaterialPropertyBlock _mpb;

        private Vector3 _targetPosition;
        private bool _hasTarget;
        private bool _wasArmedPinch;
        private bool _armed;
        private float _lastExitTime = -999f;

        private void Awake()
        {
            _right = _rightHand as IHand;
            _left = _leftHand as IHand;
            _mpb = new MaterialPropertyBlock();
            _material = RuntimeMaterial.Unlit("ExitButton (runtime)");

            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform;
            }

            Build();
            SetShown(false);

            if (_right == null && _left == null)
            {
                Debug.LogWarning(
                    $"{nameof(ExitButton)}: neither hand is an {nameof(IHand)} — the button will show " +
                    "but cannot be pinched.", this);
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        public void Show() => SetShown(true);

        public void Hide() => SetShown(false);

        private void Update()
        {
            if (!IsShown || _head == null)
            {
                return;
            }

            Follow();

            bool armed = false;
            bool armedPinch = false;
            Evaluate(_right, ref armed, ref armedPinch);
            Evaluate(_left, ref armed, ref armedPinch);

            if (armed != _armed)
            {
                _armed = armed;
                Paint();
            }

            bool edge = armedPinch && !_wasArmedPinch;
            _wasArmedPinch = armedPinch;

            if (edge && Time.unscaledTime - _lastExitTime >= _cooldownSeconds)
            {
                _lastExitTime = Time.unscaledTime;
                Exited?.Invoke();
            }
        }

        /// <summary>
        /// A hand arms the button when its pinch point (thumb/index midpoint) is within
        /// <see cref="_reachRadiusMetres"/> of the centre; it presses when that hand is
        /// also confidently pinching.
        /// </summary>
        private void Evaluate(IHand hand, ref bool armed, ref bool armedPinch)
        {
            if (hand == null || !hand.IsTrackedDataValid
                || !hand.GetJointPose(HandJointId.HandThumbTip, out Pose thumb)
                || !hand.GetJointPose(HandJointId.HandIndexTip, out Pose index))
            {
                return;
            }

            Vector3 point = (thumb.position + index.position) * 0.5f;
            if ((point - transform.position).sqrMagnitude > _reachRadiusMetres * _reachRadiusMetres)
            {
                return;
            }

            armed = true;

            bool confident = hand.GetFingerIsHighConfidence(HandFinger.Index)
                          && hand.GetFingerIsHighConfidence(HandFinger.Thumb);
            if (confident && hand.GetIndexFingerIsPinching())
            {
                armedPinch = true;
            }
        }

        private void Follow()
        {
            Vector3 forward = _head.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 1e-6f ? Vector3.forward : forward.normalized;

            Vector3 wanted = _head.position + Vector3.up * _heightMetres + forward * _forwardMetres;

            if (!_hasTarget)
            {
                _targetPosition = wanted;
                _hasTarget = true;
            }
            else
            {
                float k = _followEaseSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / _followEaseSeconds);
                _targetPosition = Vector3.Lerp(_targetPosition, wanted, k);
            }

            transform.position = _targetPosition;

            Vector3 fromHead = _targetPosition - _head.position;
            if (fromHead.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(fromHead.normalized, Vector3.up);
            }
        }

        private void SetShown(bool shown)
        {
            IsShown = shown;
            if (_visual != null)
            {
                _visual.gameObject.SetActive(shown);
            }

            if (shown)
            {
                _hasTarget = false;
                _wasArmedPinch = true; // require a fresh pinch — never fire on a pinch carried in from the selector
                _armed = false;
                Paint();
            }
        }

        private void Build()
        {
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, worldPositionStays: false);

            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = "Disc";
            if (disc.TryGetComponent(out Collider collider))
            {
                Destroy(collider);
            }

            disc.transform.SetParent(_visual, worldPositionStays: false);
            disc.transform.localScale = new Vector3(_discRadiusMetres * 2f, _discRadiusMetres * 2f, 1f);
            _disc = disc.GetComponent<Renderer>();
            _disc.sharedMaterial = _material;
            _disc.shadowCastingMode = ShadowCastingMode.Off;
            _disc.receiveShadows = false;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_visual, worldPositionStays: false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.003f); // toward the viewer
            _label = labelGo.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.font = font;
            _label.text = "Exit";
            _label.fontSize = 80;
            _label.characterSize = _characterSize;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = _textColor;
            if (font != null && labelGo.TryGetComponent(out MeshRenderer labelRenderer))
            {
                labelRenderer.sharedMaterial = font.material;
            }

            Paint();
        }

        private void Paint()
        {
            if (_disc == null)
            {
                return;
            }

            Color color = _armed ? _armedColor : _idleColor;
            _mpb.Clear();
            _mpb.SetColor(ColorId, color);
            _mpb.SetColor(ColorIdLegacy, color);
            _disc.SetPropertyBlock(_mpb);

            if (_label != null)
            {
                _label.color = _armed ? Color.white : _textColor;
            }
        }
    }
}
