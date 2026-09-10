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
    /// while the face still selects scale degree and octave. The generated visual draws
    /// that volume <b>at its true extent</b> — a translucent tube, a brighter rim at the
    /// near (+Z) face marking the entry threshold, and a disc that slides to a fingertip's
    /// depth while it is inside. Before this the target was drawn as a small sphere, ~2×
    /// shorter than the volume it stood for, on the one axis with no depth cue.
    /// </para>
    ///
    /// <para>
    /// The target transform is never scaled — the visual children carry the volume's
    /// dimensions — so <see cref="Contains"/> stays a rotation-only inverse and the hit
    /// test is provably independent of how the target is drawn.
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
        [Tooltip("The tube renderer — the trigger volume drawn at its true extent.")]
        [SerializeField] private Renderer _renderer;

        [Tooltip("Ring at the near (+Z) face: the entry threshold made visible. Optional.")]
        [SerializeField] private Renderer _mouthRenderer;

        [Tooltip("Thin disc that slides along the approach axis to a fingertip's depth while "
            + "it is inside — a cue on the one axis mid-air VR cannot show (ADR-0018). Optional.")]
        [SerializeField] private Transform _depthIndicator;

        [Tooltip("Colour of the depth disc — a fingertip 'cursor', kept clear of the chord "
            + "palette and the warm strike flash so it reads as position, not state.")]
        [SerializeField] private Color _depthIndicatorColor = new Color(0.5f, 0.9f, 1f, 0.85f);

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
        private float _halfDepth;
        private Renderer _depthIndicatorRenderer;
        private bool _depthShown;
        private float _flash; // 1 at the moment of a strike, decays to 0
        private bool _highlighted;
        private bool _hovered;
        private bool _armed;        // pinch spike: this is the stop a pinch will fire
        private bool _demonstrated; // ghost hand: the lesson demo is playing this stop right now

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

            CacheDepthIndicator();

            _mpb = new MaterialPropertyBlock();
            ApplyTint();
        }

        /// <summary>
        /// Called once by the rig at spawn. The target transform is never scaled — the
        /// visual children carry the volume's dimensions.
        /// </summary>
        public void Configure(int index, float radius, float halfDepth)
        {
            Index = index;
            _volume = new TargetVolume(radius, halfDepth);
            _halfDepth = halfDepth;
            name = $"TouchTarget {index}";
        }

        /// <summary>
        /// Wired by the rig when it generates the default visual (tube + rim + depth disc).
        /// A supplied prefab assigns these in the inspector instead.
        /// </summary>
        public void BindGeneratedVisual(Renderer tube, Renderer mouth, Transform depthIndicator)
        {
            _renderer = tube;
            _mouthRenderer = mouth;
            _depthIndicator = depthIndicator;
            CacheDepthIndicator();
            if (_mpb != null)
            {
                ApplyTint();
            }
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
        /// <paramref name="worldPoint"/> expressed in this target's local frame. Rotation
        /// only: the target transform is never scaled (its visual children are), so an
        /// inverse rotation is the whole transform.
        /// </summary>
        public Vector3 ToLocal(Vector3 worldPoint) =>
            Quaternion.Inverse(transform.rotation) * (worldPoint - transform.position);

        /// <summary>
        /// True when a point already in this target's local frame lies within the trigger
        /// volume, or within <paramref name="volumeScale"/>× it (the binder uses a scaled
        /// test for the hover glow).
        /// </summary>
        public bool ContainsLocal(Vector3 local, float volumeScale = 1f)
        {
            float inv = volumeScale > 0f ? 1f / volumeScale : 1f;
            return _volume.Contains(local.x * inv, local.y * inv, local.z * inv);
        }

        /// <summary>True when <paramref name="worldPoint"/> lies within the trigger volume, or <paramref name="volumeScale"/>× it.</summary>
        public bool Contains(Vector3 worldPoint, float volumeScale = 1f) =>
            ContainsLocal(ToLocal(worldPoint), volumeScale);

        /// <summary>
        /// Show the depth disc at <paramref name="localZ"/> along the approach axis (clamped
        /// to the volume), or hide it when <paramref name="inside"/> is false. Called every
        /// frame by <see cref="TouchTargetBinder"/> with the deepest fingertip inside.
        /// </summary>
        public void SetFingerDepth(bool inside, float localZ)
        {
            if (_depthIndicator == null)
            {
                return;
            }

            if (inside != _depthShown)
            {
                _depthShown = inside;
                _depthIndicator.gameObject.SetActive(inside);
                if (inside && _depthIndicatorRenderer != null)
                {
                    Write(_depthIndicatorRenderer, _depthIndicatorColor); // re-assert after re-enable
                }
            }

            if (inside)
            {
                Vector3 p = _depthIndicator.localPosition;
                p.z = Mathf.Clamp(localZ, -_halfDepth, _halfDepth);
                _depthIndicator.localPosition = p;
            }
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

        /// <summary>Pinch spike: mark this as the stop a pinch will fire — a strong, persistent glow.</summary>
        public void SetArmed(bool armed)
        {
            if (_armed == armed)
            {
                return;
            }

            _armed = armed;
            ApplyTint();
        }

        /// <summary>
        /// Ghost hand (ADR-0012): the lesson demonstration is sounding this stop right now.
        /// A distinct, warm look — separate from <see cref="SetArmed"/> ("a pinch will fire
        /// this"), since in Try Yourself both are live at once.
        /// </summary>
        public void SetDemonstrated(bool demonstrated)
        {
            if (_demonstrated == demonstrated)
            {
                return;
            }

            _demonstrated = demonstrated;
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

        private void CacheDepthIndicator()
        {
            if (_depthIndicator == null)
            {
                return;
            }

            _depthIndicatorRenderer = _depthIndicator.GetComponent<Renderer>();
            _depthShown = false;
            _depthIndicator.gameObject.SetActive(false);
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

            if (_armed)
            {
                color = Color.Lerp(color, Color.white, 0.6f);
                color.a = Mathf.Clamp01(Mathf.Max(color.a, _baseColor.a) + 0.35f);
            }

            if (_demonstrated)
            {
                // The demo's own colour — warm, toward the strike tint, held steady.
                color = Color.Lerp(color, _struckColor, 0.55f);
                color.a = Mathf.Clamp01(Mathf.Max(color.a, _baseColor.a) + 0.4f);
            }

            if (_flash > 0f)
            {
                color = Color.Lerp(color, _struckColor, _flash);
            }

            Write(_renderer, color);

            if (_mouthRenderer != null)
            {
                // The rim reads as a brighter edge of the same colour — it marks the
                // threshold without becoming a second information channel (§3.10).
                Color rim = Color.Lerp(color, Color.white, 0.35f);
                rim.a = Mathf.Clamp01(color.a + 0.3f);
                Write(_mouthRenderer, rim);
            }

            if (_depthIndicatorRenderer != null)
            {
                Write(_depthIndicatorRenderer, _depthIndicatorColor);
            }
        }

        private void Write(Renderer target, Color color)
        {
            // Clear, not GetPropertyBlock: the shared _mpb is reused across the tube, rim
            // and depth disc in one ApplyTint pass, and nothing else writes a block on
            // these renderers, so the two colour keys are the whole block.
            _mpb.Clear();
            _mpb.SetColor(ColorId, color);
            _mpb.SetColor(ColorIdLegacy, color);
            target.SetPropertyBlock(_mpb);
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
