using UnityEngine;
using UnityEngine.Rendering;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The sky (ADR-0053): <b>complete darkness</b> while the tutorial teaches the learner to show their
    /// hands, then — the moment the lesson menu opens — a <b>warm sunrise</b> that comes up over a few
    /// seconds and stays for the rest of the session.
    ///
    /// <para>
    /// It is a vertex-coloured dome that follows the head, not a skybox shader. A dome needs only
    /// <c>Sprites/Default</c> — already in every build because the menu's plates use it — whereas a new
    /// skybox shader would be code that can only be compiled on a machine with the Editor open; and the
    /// palette is plain serialized colours that can be tuned live. Darkness is the cameras clearing to
    /// black (which also stops the scene's own skybox from drawing); the sunrise is the dome fading in
    /// over that black.
    /// </para>
    ///
    /// <para>
    /// The gradient runs nadir → rose ground → gold horizon → coral → rose → violet → deep blue zenith, with a
    /// soft sun glow low on the horizon <i>straight ahead of the learner at the moment of the reveal</i>, so
    /// the first thing the sunrise does is appear where they are already looking. The dome draws in the
    /// <c>Background</c> queue, before everything else, with no depth write; it is never in front of the
    /// menu, the hands or the ghosts.
    /// </para>
    ///
    /// <para>
    /// Presentation only (§2.3): the app tells it <see cref="SetDark"/> or <see cref="Reveal"/>; it reads
    /// nothing back. Visual restraint (§3.10): there is no sky detail, only light.
    /// </para>
    /// </summary>
    public sealed class SkyDome : MonoBehaviour
    {
        [Header("Rig")]
        [Tooltip("The centre-eye / head transform. The dome follows its position (never its rotation). Falls back to Camera.main.")]
        [SerializeField] private Transform _head;

        [Tooltip("The cameras that draw the scene. They are set to clear to black so the sky starts dark and the " +
                 "scene's own skybox stops drawing. Empty = Camera.main.")]
        [SerializeField] private Camera[] _cameras = new Camera[0];

        [Header("Dome")]
        [Tooltip("Distance of the dome from the head, metres. Beyond everything else in the scene.")]
        [Min(10f)] [SerializeField] private float _radiusMetres = 80f;
        [Range(8, 96)] [SerializeField] private int _segments = 48;
        [Range(4, 48)] [SerializeField] private int _rings = 24;

        [Tooltip("Optional. A material whose shader honours vertex colour and a '_Color' tint. Empty = a plain translucent one is generated.")]
        [SerializeField] private Material _material;

        [Header("Sunrise palette (sRGB)")]
        [SerializeField] private Color _zenith = new Color(0.16f, 0.17f, 0.42f, 1f);
        [SerializeField] private Color _upper = new Color(0.45f, 0.35f, 0.62f, 1f);
        [SerializeField] private Color _mid = new Color(0.80f, 0.46f, 0.55f, 1f);
        [SerializeField] private Color _coral = new Color(0.99f, 0.62f, 0.45f, 1f);
        [SerializeField] private Color _horizon = new Color(1.00f, 0.77f, 0.46f, 1f);
        [SerializeField] private Color _belowHorizon = new Color(0.55f, 0.28f, 0.28f, 1f);
        [SerializeField] private Color _nadir = new Color(0.11f, 0.07f, 0.11f, 1f);

        [Header("Sun glow")]
        [SerializeField] private Color _sunGlow = new Color(1.00f, 0.86f, 0.55f, 1f);
        [Tooltip("Height of the sun above the horizon, degrees.")]
        [Range(-5f, 30f)] [SerializeField] private float _sunElevationDegrees = 4f;
        [Tooltip("Radius of the bright core, degrees.")]
        [Min(0.5f)] [SerializeField] private float _sunCoreDegrees = 5f;
        [Tooltip("Radius of the soft halo, degrees.")]
        [Min(1f)] [SerializeField] private float _sunHaloDegrees = 32f;
        [Range(0f, 1.5f)] [SerializeField] private float _sunStrength = 0.9f;

        [Header("Reveal")]
        [Tooltip("Seconds for the sunrise to come up from black. Long on purpose: it is dawn, not a light switch, " +
                 "and the learner is busy with the next instruction while it rises.")]
        [Min(0.1f)] [SerializeField] private float _revealSeconds = 12f;

        [Tooltip("Shapes the rise. 1 = an even fade (which still looks like it jumps at the start, because the screen " +
                 "brightens faster than the numbers); higher keeps the first seconds dim and lets the light gather. " +
                 "2 is a gentle dawn.")]
        [Range(1f, 4f)] [SerializeField] private float _riseCurve = 2f;

        [Header("Overall brightness")]
        [Tooltip("How bright the finished sunrise is, relative to the palette above (1 = as written). Below 1 darkens " +
                 "the whole sky - colours, glow and all - without changing its hues.")]
        [Range(0.05f, 1.5f)] [SerializeField] private float _brightness = 0.55f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // Elevation stops, degrees. How the sky is drawn, not how anything behaves - cosmetic.
        private const float NadirDeg = -90f;
        private const float BelowDeg = -22f;
        private const float HorizonDeg = 0f;
        private const float CoralDeg = 9f;
        private const float MidDeg = 26f;
        private const float UpperDeg = 56f;
        private const float ZenithDeg = 90f;

        private MeshRenderer _renderer;
        private Material _runtimeMaterial;
        private MaterialPropertyBlock _mpb;
        private float _alpha;       // 0 dark .. 1 fully risen
        private float _revealT;
        private bool _revealing;
        private bool _built;

        /// <summary>True once the sunrise has begun (or been shown outright).</summary>
        public bool IsRevealed { get; private set; }

        private void Awake()
        {
            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform;
            }

            BuildDome();
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
            }
        }

        /// <summary>Complete darkness: the cameras clear to black and the sunrise is put away.</summary>
        public void SetDark()
        {
            ClearToBlack();
            IsRevealed = false;
            _revealing = false;
            _revealT = 0f;
            SetAlpha(0f);
        }

        /// <summary>Bring the sunrise up from black, with the sun straight ahead of the learner.</summary>
        public void Reveal()
        {
            if (IsRevealed)
            {
                return;
            }

            ClearToBlack();
            FaceSunAhead();
            IsRevealed = true;
            _revealing = true;
            _revealT = 0f;
        }

        /// <summary>The sunrise at once — for a session that skips the tutorial and opens straight on the menu.</summary>
        public void RevealImmediately()
        {
            ClearToBlack();
            FaceSunAhead();
            IsRevealed = true;
            _revealing = false;
            _revealT = 1f;
            SetAlpha(1f);
        }

        private void Update()
        {
            if (!_revealing)
            {
                return;
            }

            _revealT = Mathf.Min(1f, _revealT + Time.unscaledDeltaTime / _revealSeconds);

            // Smootherstep: slow to start, slow to settle. Then bent by the rise curve so the start stays dim.
            float t = _revealT;
            float eased = t * t * t * (t * (t * 6f - 15f) + 10f);
            SetAlpha(Mathf.Pow(eased, _riseCurve));

            if (_revealT >= 1f)
            {
                _revealing = false;
            }
        }

        private void LateUpdate()
        {
            if (_head != null)
            {
                transform.position = _head.position; // never its rotation: the horizon stays level
            }
        }

        private void ClearToBlack()
        {
            if (_cameras != null && _cameras.Length > 0)
            {
                foreach (Camera camera in _cameras)
                {
                    if (camera != null)
                    {
                        Blacken(camera);
                    }
                }
            }
            else if (Camera.main != null)
            {
                Blacken(Camera.main);
            }
        }

        private static void Blacken(Camera camera)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
        }

        /// <summary>Turn the dome so its sun (local +Z) is where the learner is facing now.</summary>
        private void FaceSunAhead()
        {
            if (_head == null)
            {
                return;
            }

            transform.rotation = GhostHandView.YawOnly(_head);
        }

        private void SetAlpha(float alpha)
        {
            _alpha = alpha;
            if (_renderer == null)
            {
                return;
            }

            // The tint carries both: its alpha is the fade-in, its rgb the overall brightness (the vertex colours
            // are multiplied by it, so < 1 darkens the whole sky evenly).
            float b = _brightness;
            _renderer.enabled = alpha > 0.001f;
            _mpb.SetColor(ColorId, new Color(b, b, b, alpha));
            _mpb.SetColor(BaseColorId, new Color(b, b, b, alpha));
            _renderer.SetPropertyBlock(_mpb);
        }

        // --- the dome ---------------------------------------------------------------------------

        private void BuildDome()
        {
            if (_built)
            {
                return;
            }

            _built = true;
            _mpb = new MaterialPropertyBlock();

            var go = new GameObject("Dome");
            go.transform.SetParent(transform, worldPositionStays: false);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildMesh();

            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            if (_material != null)
            {
                _renderer.sharedMaterial = _material;
            }
            else
            {
                _runtimeMaterial = RuntimeMaterial.Unlit("SkyDome (runtime)");
                _runtimeMaterial.renderQueue = (int)RenderQueue.Background; // before everything, so nothing is ever behind it
                _renderer.sharedMaterial = _runtimeMaterial;
            }
        }

        private Mesh BuildMesh()
        {
            int columns = _segments + 1;
            int rows = _rings + 1;
            var vertices = new Vector3[columns * rows];
            var colors = new Color[columns * rows];
            var triangles = new int[_segments * _rings * 6];

            Vector3 sun = SunDirection();
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

            for (int r = 0; r < rows; r++)
            {
                float polar = Mathf.PI * r / _rings;            // 0 at the zenith, π at the nadir
                float y = Mathf.Cos(polar);
                float ring = Mathf.Sin(polar);

                for (int s = 0; s < columns; s++)
                {
                    float azimuth = 2f * Mathf.PI * s / _segments; // 0 = straight ahead (+Z)
                    var direction = new Vector3(ring * Mathf.Sin(azimuth), y, ring * Mathf.Cos(azimuth));
                    int index = r * columns + s;
                    vertices[index] = direction * _radiusMetres;

                    Color c = Sample(direction, sun);
                    colors[index] = linear ? c.linear : c;
                }
            }

            int t = 0;
            for (int r = 0; r < _rings; r++)
            {
                for (int s = 0; s < _segments; s++)
                {
                    int a = r * columns + s;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;

                    // Both windings would draw; this one faces inward for a culling material.
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
                }
            }

            var mesh = new Mesh { name = "SkyDome" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * _radiusMetres * 2.5f); // never culled
            return mesh;
        }

        private Vector3 SunDirection()
        {
            float elevation = _sunElevationDegrees * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Sin(elevation), Mathf.Cos(elevation)); // azimuth 0: straight ahead
        }

        /// <summary>The sky's colour looking along <paramref name="direction"/>: the gradient plus the sun glow.</summary>
        private Color Sample(Vector3 direction, Vector3 sun)
        {
            float elevation = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg;

            Color c;
            if (elevation < BelowDeg) c = Between(_nadir, _belowHorizon, NadirDeg, BelowDeg, elevation);
            else if (elevation < HorizonDeg) c = Between(_belowHorizon, _horizon, BelowDeg, HorizonDeg, elevation);
            else if (elevation < CoralDeg) c = Between(_horizon, _coral, HorizonDeg, CoralDeg, elevation);
            else if (elevation < MidDeg) c = Between(_coral, _mid, CoralDeg, MidDeg, elevation);
            else if (elevation < UpperDeg) c = Between(_mid, _upper, MidDeg, UpperDeg, elevation);
            else c = Between(_upper, _zenith, UpperDeg, ZenithDeg, elevation);

            float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction, sun), -1f, 1f)) * Mathf.Rad2Deg;
            float core = Mathf.Exp(-(angle * angle) / (_sunCoreDegrees * _sunCoreDegrees));
            float halo = Mathf.Exp(-(angle * angle) / (_sunHaloDegrees * _sunHaloDegrees)) * 0.5f;
            float glow = (core + halo) * _sunStrength;

            c.r = Mathf.Min(1f, c.r + _sunGlow.r * glow);
            c.g = Mathf.Min(1f, c.g + _sunGlow.g * glow);
            c.b = Mathf.Min(1f, c.b + _sunGlow.b * glow);
            c.a = 1f;
            return c;
        }

        private static Color Between(Color from, Color to, float fromDeg, float toDeg, float deg)
        {
            float t = Mathf.Clamp01((deg - fromDeg) / (toDeg - fromDeg));
            t = t * t * (3f - 2f * t);
            return Color.Lerp(from, to, t);
        }
    }
}
