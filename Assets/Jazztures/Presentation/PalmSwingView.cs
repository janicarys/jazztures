using Jazztures.Core.Melody;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The swing-slice right hand's whole interface (ADR-0080, built on ADR-0078), small and kept with the hand, in the
    /// <b>central</b> view (Session 4: peripheral cues cause head turns and lost focus): a long, fixed vertical
    /// <b>trigger line</b>, which flashes on each crossing, and a small dot at the <b>palm's height</b> on it (lateral
    /// position from where the fingers point). <b>Nothing names a note</b>: no letters, no region labels, no highlight of
    /// the region the hand is in; the only feedback for which note was chosen is the sound. When the gate is closed
    /// everything dims and a short hint says what pose is wanted. With <see cref="SetDebugGuides"/> on (developer tuning
    /// only) faint lines mark the region boundaries; still no names. Reads the pure <see cref="SwingSlicesPipeline"/>;
    /// never writes to it. Where things stand comes from <see cref="SetLane"/> and <see cref="SetPointer"/>.
    /// </summary>
    public sealed class PalmSwingView : MonoBehaviour
    {
        private const float LineWidthMetres = 0.003f;
        private const float FlashSeconds = 0.18f;
        private const float DimAlpha = 0.25f;
        private const float GuideWidthMetres = 0.04f;
        private const int MaxGuides = 17;

        [SerializeField] private Transform _head;

        [Tooltip("Scales the cursor. 1 is small; the trigger line is not scaled.")]
        [Min(0.3f)] [SerializeField] private float _scale = 1f;

        [Tooltip("How tall the trigger line is, metres. It stays where it was set; only the cursor moves up and down.")]
        [Min(0.05f)] [SerializeField] private float _lineHeightMetres = 0.5f;

        [Tooltip("Time constant of the easing on the cursor's movement, seconds, on top of the input smoothing. 0 = none.")]
        [Min(0f)] [SerializeField] private float _cursorFollowSeconds = 0.05f;

        [Tooltip("Hint shown while the gate is closed. Plain words: no theory vocabulary.")]
        [SerializeField] private string _hint = "open palm, facing left";

        private SwingSlicesPipeline _pipeline;
        private Transform _lineRoot;
        private Transform _cluster;
        private LineRenderer _line;
        private MeshRenderer _cursor;
        private TextMesh _hintText;
        private LineRenderer[] _guides;
        private bool _debugGuides;
        private Material _template;
        private bool _hasLane;
        private bool _shown;
        private bool _hasPointer;
        private Vector3 _laneOrigin;
        private Vector3 _laneRight = Vector3.right;
        private Vector3 _pointer;
        private Vector3 _shownPointer;
        private float _flashLineUntil = float.NegativeInfinity;

        /// <summary>Wire to the pipeline whose state is shown. Safe to call before or after enabling, and again for a new pipeline.</summary>
        public void Bind(SwingSlicesPipeline pipeline)
        {
            _pipeline = pipeline;

            // The line must be long enough to hold the whole stack of regions.
            float stack = pipeline.Layout.RegionCount * pipeline.Layout.RegionHeightMetres + 0.05f;
            _lineHeightMetres = Mathf.Max(_lineHeightMetres, stack);
            if (_line != null)
            {
                _line.SetPosition(0, new Vector3(0f, -_lineHeightMetres * 0.5f, 0f));
                _line.SetPosition(1, new Vector3(0f, _lineHeightMetres * 0.5f, 0f));
            }
        }

        /// <summary>Show or hide the whole interface. Hidden outside lessons, like the left hand's strike plane.</summary>
        public void SetShown(bool shown) => _shown = shown;

        /// <summary>Developer tuning only: draw faint lines at the region boundaries. No names, ever.</summary>
        public void SetDebugGuides(bool on) => _debugGuides = on;

        /// <summary>A note was sounded: the line flashes. Which note is not shown.</summary>
        public void OnNote(SwingSlicesNote note) => _flashLineUntil = Time.unscaledTime + FlashSeconds;

        /// <summary>
        /// Place the trigger line: <paramref name="origin"/> is where its middle stands, <paramref name="right"/> the horizontal left-to-right axis.
        /// </summary>
        public void SetLane(Vector3 origin, Vector3 right)
        {
            _hasLane = true;
            _laneOrigin = origin;
            _laneRight = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
            if (!_hasPointer)
            {
                _pointer = origin;
                _shownPointer = origin;
            }
        }

        /// <summary>Where the hand is pointing now, in world space: the cursor goes here.</summary>
        public void SetPointer(Vector3 world)
        {
            if (!_hasPointer)
            {
                _shownPointer = world;
            }

            _hasPointer = true;
            _pointer = world;
        }

        private void Awake()
        {
            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform;
            }

            _template = RuntimeMaterial.Unlit("Palm Swing");

            _lineRoot = new GameObject("Trigger Line").transform;
            _lineRoot.SetParent(transform, worldPositionStays: false);
            _line = _lineRoot.gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.positionCount = 2;
            _line.SetPosition(0, new Vector3(0f, -_lineHeightMetres * 0.5f, 0f));
            _line.SetPosition(1, new Vector3(0f, _lineHeightMetres * 0.5f, 0f));
            _line.widthMultiplier = LineWidthMetres;
            _line.sharedMaterial = _template;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            _cluster = new GameObject("Palm Swing Cursor").transform;
            _cluster.SetParent(transform, worldPositionStays: false);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _cursor = MakeShape("Cursor", Disc(), Vector3.zero, 0.008f);
            _hintText = MakeText("Gate Hint", font, 50, new Vector3(0f, -0.035f, 0f), TextAnchor.UpperCenter, 0.0045f);
            _hintText.text = _hint;

            _guides = new LineRenderer[MaxGuides];
            for (int g = 0; g < _guides.Length; g++)
            {
                var guide = new GameObject($"Region Guide {g}");
                guide.transform.SetParent(_lineRoot, worldPositionStays: false);
                LineRenderer renderer = guide.AddComponent<LineRenderer>();
                renderer.useWorldSpace = false;
                renderer.positionCount = 2;
                renderer.widthMultiplier = LineWidthMetres * 0.5f;
                renderer.sharedMaterial = _template;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                _guides[g] = renderer;
            }

            _lineRoot.gameObject.SetActive(false);
            _cluster.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_pipeline == null || _lineRoot == null)
            {
                return;
            }

            bool visible = _hasLane && _shown;
            _lineRoot.gameObject.SetActive(visible);
            _cluster.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            // The line stays where the lane put it; only the cursor moves with the hand, eased a little.
            float follow = _cursorFollowSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / _cursorFollowSeconds);
            _shownPointer = Vector3.Lerp(_shownPointer, _pointer, follow);
            Face(_lineRoot, _laneOrigin);
            Face(_cluster, _shownPointer);
            _cluster.localScale = Vector3.one * _scale;

            float now = Time.unscaledTime;
            float gate = _pipeline.GateOpen ? 1f : DimAlpha;

            float lineFlash = Mathf.Clamp01((_flashLineUntil - now) / FlashSeconds);
            SetLineColor(new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 0.95f, lineFlash) * gate));

            SetShape(_cursor, true, gate);
            UpdateGuides(gate);

            _hintText.gameObject.SetActive(!_pipeline.GateOpen);
            _hintText.color = new Color(1f, 1f, 1f, 0.8f);
        }

        // Stand `root` at `position`, turned to face the head about the vertical (so a vertical line stays vertical and
        // text reads); with no head, square to the lane.
        private void Face(Transform root, Vector3 position)
        {
            root.position = position;
            Vector3 away = _head != null
                ? Vector3.ProjectOnPlane(position - _head.position, Vector3.up)
                : Vector3.Cross(_laneRight, Vector3.up);
            if (away.sqrMagnitude > 1e-4f)
            {
                root.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            }
        }

        // Developer tuning only: a faint short line at every region boundary of the stack. Never a name or a highlight.
        private void UpdateGuides(float gate)
        {
            int edges = _debugGuides ? _pipeline.Layout.RegionCount + 1 : 0;
            for (int g = 0; g < _guides.Length; g++)
            {
                bool shown = g < edges;
                _guides[g].enabled = shown;
                if (!shown)
                {
                    continue;
                }

                float y = _pipeline.Layout.EdgeOf(g);
                _guides[g].SetPosition(0, new Vector3(-GuideWidthMetres * 0.5f, y, 0f));
                _guides[g].SetPosition(1, new Vector3(GuideWidthMetres * 0.5f, y, 0f));
                var color = new Color(1f, 1f, 1f, 0.15f * gate);
                _guides[g].startColor = color;
                _guides[g].endColor = color;
            }
        }

        private void SetLineColor(Color color)
        {
            _line.startColor = color;
            _line.endColor = color;
        }

        private static void SetShape(MeshRenderer shape, bool lit, float gate)
        {
            shape.material.color = new Color(1f, 1f, 1f, (lit ? 0.95f : 0.18f) * gate);
        }

        private TextMesh MakeText(string name, Font font, int size, Vector3 localPosition, TextAnchor anchor, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_cluster, worldPositionStays: false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * scale;
            TextMesh text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = size;
            text.characterSize = 1f;
            text.anchor = anchor;
            text.alignment = TextAlignment.Center;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return text;
        }

        private MeshRenderer MakeShape(string name, Mesh mesh, Vector3 localPosition, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_cluster, worldPositionStays: false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _template;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static Mesh Disc()
        {
            const int segments = 16;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }

            return Build("Disc", vertices, triangles);
        }

        private static Mesh Build(string name, Vector3[] vertices, int[] triangles)
        {
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            var colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }

            mesh.colors = colors;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
