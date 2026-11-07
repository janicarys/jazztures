using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The prototype's whole interface for the palm-swing input (ADR-0078), small and kept with the hand, in the
    /// <b>central</b> view (Session 4: peripheral cues cause head turns and lost focus). It is two things:
    /// <list type="bullet">
    ///   <item>a short vertical <b>trigger line</b>, which stays where it is sideways (it is what the hand crosses) but
    ///   rides at the hand's height, and flashes on each crossing;</item>
    ///   <item>a small <b>cursor</b> that follows where the hand is pointing, with beside it the <b>note letter</b> (the
    ///   last note played; the start pitch's letter flashes on a reset), the <b>direction</b> ▲ · ● · ▼ with the
    ///   <i>held</i> one lit (what the next swing will do), an <b>at-limit</b> bar when a step ran off the range, and,
    ///   with height zones on, <b>zone bars</b> with the current one lit.</item>
    /// </list>
    /// When the gate is closed everything dims and a short hint says what pose is wanted. No note highways, no targets,
    /// no colour-only information (CLAUDE.md §3.10). Reads the pure <see cref="PalmSwingPipeline"/>; never writes to it.
    /// Where things stand comes from <see cref="SetLane"/> and <see cref="SetPointer"/>, so this assembly needs
    /// nothing from Input.
    /// </summary>
    public sealed class PalmSwingView : MonoBehaviour
    {
        private const float LineHeightMetres = 0.12f;
        private const float LineWidthMetres = 0.002f;
        private const float FlashSeconds = 0.18f;
        private const float DimAlpha = 0.25f;

        // Offsets of the widgets from the cursor, metres, in the view's own up/right.
        private const float MarkerUp = 0.05f;
        private const float DirectionRight = 0.045f;
        private const float DirectionStep = 0.02f;
        private const float ZoneRight = -0.05f;

        [SerializeField] private Transform _head;

        [Tooltip("Scales the whole display about the cursor. 1 is small; the trigger line is not scaled.")]
        [Min(0.3f)] [SerializeField] private float _scale = 1f;

        [Tooltip("Hint shown while the gate is closed. Plain words: no theory vocabulary.")]
        [SerializeField] private string _hint = "open palm, facing left";

        private PalmSwingPipeline _pipeline;
        private Transform _lineRoot;
        private Transform _cluster;
        private LineRenderer _line;
        private MeshRenderer _cursor;
        private TextMesh _marker;
        private TextMesh _hintText;
        private MeshRenderer _up;
        private MeshRenderer _same;
        private MeshRenderer _down;
        private MeshRenderer _limitBar;
        private MeshRenderer[] _zoneBars;
        private Material _template;
        private bool _hasLane;
        private bool _hasPointer;
        private Vector3 _laneOrigin;
        private Vector3 _laneRight = Vector3.right;
        private Vector3 _pointer;
        private float _flashLineUntil = float.NegativeInfinity;
        private float _flashMarkerUntil = float.NegativeInfinity;

        /// <summary>Wire to the pipeline whose state is shown. Safe to call before or after enabling, and again for a new pipeline.</summary>
        public void Bind(PalmSwingPipeline pipeline)
        {
            if (_pipeline != null)
            {
                _pipeline.PhraseReset -= OnReset;
            }

            _pipeline = pipeline;
            pipeline.PhraseReset += OnReset;
            ShowLetter(pipeline.CursorPitch);
        }

        /// <summary>A note was sounded (crossing flash, and the marker takes its letter).</summary>
        public void OnNote(PalmSwingNote note)
        {
            _flashLineUntil = Time.unscaledTime + FlashSeconds;
            ShowLetter(note.CursorPitch);
        }

        /// <summary>
        /// Place the trigger line: <paramref name="origin"/> is where it stands (its horizontal position and depth; the
        /// line rides at the hand's height), <paramref name="right"/> the horizontal left-to-right axis.
        /// </summary>
        public void SetLane(Vector3 origin, Vector3 right)
        {
            _hasLane = true;
            _laneOrigin = origin;
            _laneRight = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
            if (!_hasPointer)
            {
                _pointer = origin;
            }
        }

        /// <summary>Where the hand is pointing now, in world space: the cursor goes here.</summary>
        public void SetPointer(Vector3 world)
        {
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
            _line.SetPosition(0, new Vector3(0f, -LineHeightMetres * 0.5f, 0f));
            _line.SetPosition(1, new Vector3(0f, LineHeightMetres * 0.5f, 0f));
            _line.widthMultiplier = LineWidthMetres;
            _line.sharedMaterial = _template;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            _cluster = new GameObject("Palm Swing Cursor").transform;
            _cluster.SetParent(transform, worldPositionStays: false);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _cursor = MakeShape("Cursor", Disc(), Vector3.zero, 0.008f);
            _marker = MakeText("Note Marker", font, 90, new Vector3(0f, MarkerUp, 0f), TextAnchor.MiddleCenter, 0.0045f);
            _hintText = MakeText("Gate Hint", font, 50, new Vector3(0f, -0.035f, 0f), TextAnchor.UpperCenter, 0.0045f);
            _hintText.text = _hint;

            var indicator = new Vector3(DirectionRight, MarkerUp, 0f);
            _up = MakeShape("Direction Up", Triangle(up: true), indicator + new Vector3(0f, DirectionStep, 0f), 0.014f);
            _same = MakeShape("Direction Same", Disc(), indicator, 0.008f);
            _down = MakeShape("Direction Down", Triangle(up: false), indicator + new Vector3(0f, -DirectionStep, 0f), 0.014f);

            _limitBar = MakeShape("At Limit Bar", Quad(), new Vector3(0f, MarkerUp, 0f), 1f);
            _limitBar.transform.localScale = new Vector3(0.035f, 0.004f, 1f);

            _zoneBars = new MeshRenderer[3];
            for (int z = 0; z < _zoneBars.Length; z++)
            {
                _zoneBars[z] = MakeShape($"Zone Bar {z}", Quad(), new Vector3(ZoneRight, MarkerUp + (z - 1) * 0.011f, 0f), 1f);
                _zoneBars[z].transform.localScale = new Vector3(0.018f, 0.005f, 1f);
            }

            _lineRoot.gameObject.SetActive(false);
            _cluster.gameObject.SetActive(false);
            if (_pipeline != null)
            {
                ShowLetter(_pipeline.CursorPitch);
            }
        }

        private void OnDestroy()
        {
            if (_pipeline != null)
            {
                _pipeline.PhraseReset -= OnReset;
            }
        }

        private void LateUpdate()
        {
            if (_pipeline == null || _lineRoot == null)
            {
                return;
            }

            _lineRoot.gameObject.SetActive(_hasLane);
            _cluster.gameObject.SetActive(_hasLane);
            if (!_hasLane)
            {
                return;
            }

            // The line stands where the lane puts it, but at the height of the hand; the cursor is where the hand points.
            Vector3 linePosition = new Vector3(_laneOrigin.x, _pointer.y, _laneOrigin.z);
            Face(_lineRoot, linePosition);
            Face(_cluster, _pointer);
            _cluster.localScale = Vector3.one * _scale;

            float now = Time.unscaledTime;
            float gate = _pipeline.GateOpen ? 1f : DimAlpha;

            float lineFlash = Mathf.Clamp01((_flashLineUntil - now) / FlashSeconds);
            SetLineColor(new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 0.95f, lineFlash) * gate));

            SetShape(_cursor, true, gate);

            float markerFlash = Mathf.Clamp01((_flashMarkerUntil - now) / FlashSeconds);
            _marker.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.85f, 1f, markerFlash) * gate);
            _marker.transform.localScale = Vector3.one * (0.0045f * (1f + 0.35f * markerFlash));

            StepDirection held = _pipeline.HeldDirection;
            SetShape(_up, held == StepDirection.Up, gate);
            SetShape(_same, held == StepDirection.Same, gate);
            SetShape(_down, held == StepDirection.Down, gate);

            bool zones = _pipeline.Zone >= 0;
            for (int z = 0; z < _zoneBars.Length; z++)
            {
                bool shown = zones && z < _pipeline.ZoneCount;
                _zoneBars[z].gameObject.SetActive(shown);
                if (shown)
                {
                    SetShape(_zoneBars[z], z == _pipeline.Zone, gate);
                }
            }

            bool atLimit = _pipeline.AtLimit;
            _limitBar.gameObject.SetActive(atLimit);
            if (atLimit)
            {
                // Above the marker for a climb that ran out, below it for a descent.
                float side = held == StepDirection.Down ? -1f : 1f;
                _limitBar.transform.localPosition = new Vector3(0f, MarkerUp + side * 0.03f, 0f);
                SetShape(_limitBar, true, gate);
            }

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

        private void OnReset(PhraseResetReason reason, Pitch startPitch)
        {
            _flashMarkerUntil = Time.unscaledTime + FlashSeconds * 2f;
            ShowLetter(startPitch);
        }

        private void ShowLetter(Pitch pitch)
        {
            if (_marker != null)
            {
                _marker.text = PitchNames.Of(pitch.Class);
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

        private static Mesh Triangle(bool up)
        {
            float y = up ? 0.5f : -0.5f;
            return Build("Triangle", new[] { new Vector3(0f, y, 0f), new Vector3(-0.5f, -y, 0f), new Vector3(0.5f, -y, 0f) }, new[] { 0, 1, 2 });
        }

        private static Mesh Quad() => Build(
            "Quad",
            new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) },
            new[] { 0, 1, 2, 0, 2, 3 });

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
