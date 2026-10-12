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
    /// while the face still selects scale degree and octave. Local +Z points <b>away</b> from
    /// the learner (the arc is built outward from the shoulder), so a fingertip reaching out
    /// meets the volume at its -Z end. The generated visual is a <b>paddle</b> (ADR-0046): a
    /// flat pad resting at that near end, on a faint rail the length of the volume. It says
    /// "push me": while a fingertip is inside, the pad is carried along the rail in front of it
    /// (<see cref="SetFingerDepth"/>), making the depth visible, and springs back on release.
    /// The pad is drawn at the volume's own radius, so what is pushed is what triggers.
    /// </para>
    ///
    /// <para>
    /// The target transform is never scaled — the visual children carry the volume's
    /// dimensions — so <see cref="Contains"/> stays a rotation-only inverse and the hit
    /// test is provably independent of how the target is drawn.
    /// </para>
    ///
    /// <para>
    /// The target is <b>always visible</b> (§3.1), in one neutral colour that does not
    /// follow the chord (ADR-0044). What the target plays is written on it instead — the
    /// <b>note label</b>, a letter name such as "E". This component only layers the touch
    /// feedback on top of the neutral base: the hover glow, the strike flash, the pinch
    /// "armed" glow and the ghost hand's demonstration glow. Visibility and playability are
    /// decoupled: a target with no label cannot fire a note (<see cref="IsSounding"/> gates
    /// <see cref="TouchTargetBinder"/>).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TouchTarget : MonoBehaviour
    {
        [Tooltip("The body renderer. The generated paddle's pad; for a supplied prefab, whatever it draws.")]
        [SerializeField] private Renderer _renderer;

        [Tooltip("Paddle only: the faint rail the pad slides along, drawing the push travel. Optional.")]
        [SerializeField] private Renderer _railRenderer;

        [Tooltip("Seconds for a strike flash to decay back to the base colour.")]
        [Min(0.01f)] [SerializeField] private float _flashDecaySeconds = 0.18f;

        // Paddle cosmetics (ADR-0046) — how it is drawn, not how it behaves, so not [TUNABLE].
        private const float PadMinAlpha = 0.8f;       // a solid pad reads as a thing to push
        private const float RailAlpha = 0.18f;        // the rail is a hint, not a second object
        private const float PadReturnSeconds = 0.06f; // spring-back time constant
        private const float LabelStandOffMetres = 0.004f;

        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorIdLegacy = Shader.PropertyToID("_Color");

        // Neutral base colour (set once from the palette), plus the touch overlays this component owns.
        private Color _baseColor = new Color(0.58f, 0.58f, 0.62f, 0.35f);
        private Color _struckColor = new Color(1f, 0.96f, 0.85f, 1f);
        private float _highlightBoost = 0.35f; // how far the hover glow pushes toward white

        private TargetVolume _volume;
        private float _flash; // 1 at the moment of a strike, decays to 0
        private bool _hovered;

        // The paddle's moving part: a unit-scale root the pad (and its label) ride on.
        private Transform _pad;
        private float _padHalfThickness;
        private float _padRestZ;   // resting at the near face, toward the learner
        private float _padMaxZ;    // bottomed out against the far face
        private float _padZ;
        private float _padTargetZ;

        // The note label (ADR-0044): the letter this target currently plays.
        private TextMesh _label;
        private string _noteName = string.Empty;
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
            name = $"TouchTarget {index}";
        }

        /// <summary>
        /// Wired by the rig when it generates the paddle (ADR-0046): a flat <paramref name="pad"/>
        /// resting at the learner's end of the trigger volume, a faint <paramref name="rail"/>
        /// along the push travel, and <paramref name="padRoot"/> — a unit-scale transform the pad
        /// (and the note label) ride on. A supplied prefab is not bound here and has no moving part.
        /// </summary>
        public void BindGeneratedVisual(
            Renderer pad, Renderer rail, Transform padRoot, float halfDepth, float padHalfThickness)
        {
            _renderer = pad;
            _railRenderer = rail;
            _pad = padRoot;
            _padHalfThickness = padHalfThickness;

            // +Z points away from the learner (the arc is built outward from the shoulder), so
            // the pad rests on the near, -Z end and is pushed toward +Z.
            _padRestZ = -halfDepth + padHalfThickness;
            _padMaxZ = halfDepth - padHalfThickness;
            _padZ = _padTargetZ = _padRestZ;
            SetPadZ(_padRestZ);

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

        /// <summary>
        /// Called once by <see cref="TensionColorDriver"/> with the neutral colour. The base
        /// colour does not follow the chord (ADR-0044); the note label says what changed.
        /// </summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            ApplyTint();
        }

        /// <summary>
        /// Create (or hide) this target's note label. Called once by the rig at spawn, for
        /// generated and prefab targets alike. The label is a fixed-colour letter drawn above
        /// the tube; <see cref="FaceLabel"/> keeps it turned toward the learner.
        /// </summary>
        public void ConfigureLabel(bool show, float characterSize, Color color)
        {
            if (!show)
            {
                if (_label != null)
                {
                    _label.gameObject.SetActive(false);
                }

                return;
            }

            if (_label == null)
            {
                var go = new GameObject("Label");

                // On the paddle the label rides the pad, just in front of its learner-facing
                // face; on a supplied prefab it sits at the target's centre.
                Transform parent = _pad != null ? _pad : transform;
                go.transform.SetParent(parent, worldPositionStays: false);
                go.transform.localPosition = _pad != null
                    ? new Vector3(0f, 0f, -(_padHalfThickness + LabelStandOffMetres))
                    : Vector3.zero;

                // The label is sized in metres, so it must not inherit the target's scale. A
                // supplied prefab is often a scaled primitive (a 0.05 sphere shrank the first
                // labels twentyfold); undo whatever the parent chain applies.
                Vector3 inherited = parent.lossyScale;
                go.transform.localScale = new Vector3(
                    InverseScale(inherited.x), InverseScale(inherited.y), InverseScale(inherited.z));

                _label = go.AddComponent<TextMesh>();
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _label.font = font;
                _label.fontSize = 90;
                _label.anchor = TextAnchor.MiddleCenter;
                _label.alignment = TextAlignment.Center;

                if (font != null && go.TryGetComponent(out MeshRenderer labelRenderer))
                {
                    labelRenderer.sharedMaterial = font.material;
                    labelRenderer.sortingOrder = 20; // above the translucent tube, rim and depth disc
                }
            }

            _label.gameObject.SetActive(true);
            _label.characterSize = characterSize;
            _label.color = color;
            _label.text = _noteName;
        }

        /// <summary>
        /// Turn the label to face <paramref name="viewerPosition"/>, kept upright. The
        /// target itself faces outward along the approach axis, so without this the letter
        /// would be edge-on or mirrored from where the learner stands. Cheap enough for the
        /// rig to call on all ten targets every frame.
        /// </summary>
        public void FaceLabel(Vector3 viewerPosition)
        {
            if (_label == null)
            {
                return;
            }

            Vector3 away = _label.transform.position - viewerPosition;
            if (away.sqrMagnitude > 1e-6f)
            {
                _label.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
        }

        /// <summary>Called on every chord change with this slot's new tone (or cleared).</summary>
        public void RePitch(ChordTarget target)
        {
            Degree = target.Degree;
            OctaveOffset = target.OctaveOffset;
            Pitch = target.Pitch.Midi;
            SetNoteName(PitchNames.Of(target.Pitch.Class));
        }

        /// <summary>Chord released — the target stays visible but can no longer sound, and shows no note.</summary>
        public void Clear()
        {
            Pitch = -1;
            SetNoteName(string.Empty);
        }

        private static float InverseScale(float scale) =>
            Mathf.Abs(scale) > 1e-6f ? 1f / scale : 1f;

        private void SetNoteName(string noteName)
        {
            _noteName = noteName;
            if (_label != null)
            {
                _label.text = noteName;
            }
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
        /// Push the paddle (ADR-0046): while a fingertip is <paramref name="inside"/> the volume
        /// the pad is carried along the approach axis in front of the fingertip, so the learner
        /// sees the push — a cue on the one axis mid-air VR cannot show (ADR-0018, D2). It
        /// bottoms out against the far end of its travel, and springs back when the finger
        /// leaves. Called every frame by <see cref="TouchTargetBinder"/> with the deepest
        /// fingertip inside; a no-op on a supplied prefab, which has no pad.
        /// </summary>
        public void SetFingerDepth(bool inside, float localZ)
        {
            if (_pad == null)
            {
                return;
            }

            // The pad's learner-facing face meets the fingertip, so its centre leads the tip by
            // half its thickness. Never behind its resting place.
            _padTargetZ = inside
                ? Mathf.Clamp(localZ + _padHalfThickness, _padRestZ, _padMaxZ)
                : _padRestZ;
        }

        private void SetPadZ(float z)
        {
            _padZ = z;
            Vector3 p = _pad.localPosition;
            p.z = z;
            _pad.localPosition = p;
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

            if (_pad != null && !Mathf.Approximately(_padZ, _padTargetZ))
            {
                if (_padTargetZ > _padZ)
                {
                    SetPadZ(_padTargetZ); // being pushed: it stays under the fingertip, no lag
                }
                else
                {
                    float k = 1f - Mathf.Exp(-Time.deltaTime / PadReturnSeconds);
                    SetPadZ(Mathf.Lerp(_padZ, _padTargetZ, k)); // released: spring back
                }
            }
        }

        private void ApplyTint()
        {
            if (_renderer == null)
            {
                return;
            }

            Color color = _baseColor;

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

            if (_pad != null)
            {
                // A solid pad reads as a thing to push; the neutral base alone is a faint ghost.
                color.a = Mathf.Max(color.a, PadMinAlpha);
            }

            Write(_renderer, color);

            if (_railRenderer != null)
            {
                // The rail never reacts — it only draws the travel, not a second signal (§3.10).
                Color rail = _baseColor;
                rail.a = RailAlpha;
                Write(_railRenderer, rail);
            }
        }

        private void Write(Renderer target, Color color)
        {
            // Clear, not GetPropertyBlock: the shared _mpb is reused across the pad and the
            // rail in one ApplyTint pass, and nothing else writes a block on these
            // renderers, so the two colour keys are the whole block.
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
