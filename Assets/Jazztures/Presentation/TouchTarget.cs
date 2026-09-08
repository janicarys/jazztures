using Jazztures.Core.Melody;
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
    /// <para>
    /// The trigger volume is a cylinder along the target's local Z — generous in depth,
    /// tight in the XY face (ADR-0018): depth is the axis a person cannot judge in mid-air,
    /// while the face still selects scale degree and octave.
    /// </para>
    ///
    /// <para>
    /// The target is <b>always visible</b> (§3.1). Its base colour is driven by
    /// <see cref="TensionColorDriver"/> from the tension palette (§3.10); this component
    /// only layers the hover glow, the chord-change highlight and the strike flash on top.
    /// Visibility and playability are decoupled: a target showing the neutral colour still
    /// cannot fire a note (<see cref="IsSounding"/> gates <see cref="TouchTargetBinder"/>).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TouchTarget : MonoBehaviour
    {
        [SerializeField] private Renderer _renderer;

        [Tooltip("Seconds for a strike flash to decay back to the base colour.")]
        [Min(0.01f)] [SerializeField] private float _flashDecaySeconds = 0.18f;

        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorIdLegacy = Shader.PropertyToID("_Color");

        // Base colour from the tension driver, plus the overlays this component owns.
        private Color _baseColor = new Color(0.58f, 0.58f, 0.62f, 0.35f);
        private Color _struckColor = new Color(1f, 0.96f, 0.85f, 1f);
        private float _highlightBoost = 0.35f;

        private TargetVolume _volume;
        private float _flash; // 1 at the moment of a strike, decays to 0
        private bool _highlighted;
        private bool _hovered;

        /// <summary>Stable slot, 0..9.</summary>
        public int Index { get; private set; }

        public ScaleDegree Degree { get; private set; }

        public int OctaveOffset { get; private set; }

        /// <summary>MIDI note the target currently sounds, or -1 before the first chord.</summary>
        public int Pitch { get; private set; } = -1;

        /// <summary>Radius of the target's circular face, world metres (§3.3).</summary>
        public float Radius => _volume.Radius;

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
        public void Configure(int index, float radius, float halfDepth)
        {
            Index = index;
            _volume = new TargetVolume(radius, halfDepth);
            transform.localScale = Vector3.one * (radius * 2f);
            name = $"TouchTarget {index}";
        }

        /// <summary>Called once by <see cref="TensionColorDriver"/> — the palette's overlay style.</summary>
        public void SetOverlayStyle(Color struckColor, float highlightBoost)
        {
            _struckColor = struckColor;
            _highlightBoost = Mathf.Clamp01(highlightBoost);
            ApplyTint();
        }

        /// <summary>Called every frame by <see cref="TensionColorDriver"/> with the eased chord colour.</summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            ApplyTint();
        }

        /// <summary>Called on every chord change with this slot's new tone (or cleared).</summary>
        public void RePitch(ChordTarget target)
        {
            Degree = target.Degree;
            OctaveOffset = target.OctaveOffset;
            Pitch = target.Pitch.Midi;
        }

        /// <summary>Chord released — the target stays visible but can no longer sound.</summary>
        public void Clear()
        {
            Pitch = -1;
        }

        /// <summary>
        /// True when <paramref name="worldPoint"/> lies within the trigger volume, or
        /// within <paramref name="volumeScale"/>× it (the binder uses a scaled test to
        /// drive the hover glow). Uses the target's rotation only — the mesh is scaled, so
        /// a full inverse-transform would divide the offset by that scale.
        /// </summary>
        public bool Contains(Vector3 worldPoint, float volumeScale = 1f)
        {
            Vector3 local = Quaternion.Inverse(transform.rotation) * (worldPoint - transform.position);
            float inv = volumeScale > 0f ? 1f / volumeScale : 1f;
            return _volume.Contains(local.x * inv, local.y * inv, local.z * inv);
        }

        public void SetHighlighted(bool highlighted)
        {
            _highlighted = highlighted;
            ApplyTint();
        }

        /// <summary>A fingertip is near but has not entered — glow, so aim is learnable.</summary>
        public void SetHovered(bool hovered)
        {
            if (_hovered == hovered)
            {
                return;
            }

            _hovered = hovered;
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

            Color color = _baseColor;
            if (_highlighted)
            {
                color = Color.Lerp(color, Color.white, _highlightBoost);
            }

            if (_hovered)
            {
                color = Color.Lerp(color, Color.white, 0.5f * _highlightBoost);
                color.a = Mathf.Max(color.a, _baseColor.a + 0.15f);
            }

            if (_flash > 0f)
            {
                color = Color.Lerp(color, _struckColor, _flash);
            }

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, color);
            _mpb.SetColor(ColorIdLegacy, color);
            _renderer.SetPropertyBlock(_mpb);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.80f, 1f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(
                Vector3.zero,
                new Vector3(_volume.Radius * 2f, _volume.Radius * 2f, _volume.HalfDepth * 2f));
        }
    }
}
