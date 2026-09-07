using Jazztures.Core.Music;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// One of the ten mid-air melody targets (CLAUDE.md §3.1, ADR-0005). Its
    /// <see cref="Index"/> / <see cref="Degree"/> / octave are fixed for the session; only
    /// <see cref="Pitch"/> changes, on every chord change — "re-pitched, never
    /// re-arranged". Spawned and positioned by <see cref="TouchTargetRig"/>; re-pitched and
    /// struck by <see cref="TouchTargetBinder"/>.
    ///
    /// <para>Visuals here are a placeholder — a tinted sphere. Proper visual design is M6
    /// polish, alongside the ghost hands (ADR-0012).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TouchTarget : MonoBehaviour
    {
        [SerializeField] private Renderer _renderer;

        [Header("Placeholder tints")]
        [SerializeField] private Color _restColor = new Color(0.30f, 0.55f, 0.85f, 0.65f);
        [SerializeField] private Color _highlightColor = new Color(0.55f, 0.80f, 1.00f, 0.85f);
        [SerializeField] private Color _struckColor = new Color(1.00f, 0.95f, 0.70f, 1.00f);
        [Min(0.01f)] [SerializeField] private float _flashDecaySeconds = 0.18f;

        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorIdLegacy = Shader.PropertyToID("_Color");

        private float _flash; // 1 at the moment of a strike, decays to 0
        private bool _highlighted;

        /// <summary>Stable slot, 0..9.</summary>
        public int Index { get; private set; }

        public ScaleDegree Degree { get; private set; }

        public int OctaveOffset { get; private set; }

        /// <summary>MIDI note the target currently sounds, or -1 before the first chord.</summary>
        public int Pitch { get; private set; } = -1;

        /// <summary>Trigger-sphere radius in world metres (§3.3).</summary>
        public float Radius { get; private set; } = 0.035f;

        public bool IsSounding => Pitch >= 0;

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<Renderer>();
            }

            _mpb = new MaterialPropertyBlock();
            ApplyTint();
        }

        /// <summary>Called once by the rig at spawn.</summary>
        public void Configure(int index, float radius)
        {
            Index = index;
            Radius = radius;
            transform.localScale = Vector3.one * (radius * 2f);
            name = $"TouchTarget {index}";
        }

        /// <summary>Called on every chord change with this slot's new tone (or cleared).</summary>
        public void RePitch(ChordTarget target)
        {
            Degree = target.Degree;
            OctaveOffset = target.OctaveOffset;
            Pitch = target.Pitch.Midi;
            SetVisible(true);
        }

        public void Clear()
        {
            Pitch = -1;
            SetVisible(false);
        }

        public bool Contains(Vector3 worldPoint) =>
            (worldPoint - transform.position).sqrMagnitude <= Radius * Radius;

        public void SetVisible(bool visible)
        {
            if (_renderer != null)
            {
                _renderer.enabled = visible;
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
            ApplyTint();
        }

        /// <summary>Flash the target — a fingertip just triggered it.</summary>
        public void Strike()
        {
            _flash = 1f;
            ApplyTint();
        }

        private void Update()
        {
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - Time.deltaTime / _flashDecaySeconds);
                ApplyTint();
            }
        }

        private void ApplyTint()
        {
            if (_renderer == null)
            {
                return;
            }

            Color baseColor = _highlighted ? _highlightColor : _restColor;
            Color color = Color.Lerp(baseColor, _struckColor, _flash);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, color);
            _mpb.SetColor(ColorIdLegacy, color);
            _renderer.SetPropertyBlock(_mpb);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.80f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
    }
}
