using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The prototype's whole interface for the palm-swing input (ADR-0078), kept in the right hand's lane in the
    /// <b>central</b> view (Session 4: peripheral cues cause head turns and lost focus). Four things and a hint:
    /// <list type="bullet">
    ///   <item>a faint vertical <b>trigger line</b>, which flashes on each crossing;</item>
    ///   <item>a <b>note marker</b>: the letter of the last note played (the start pitch's letter flashes on a reset);</item>
    ///   <item>a <b>direction indicator</b> beside it: ▲ · ● · ▼, the <i>held</i> one lit — what the next swing will do;</item>
    ///   <item>an <b>at-limit</b> bar above or below the marker when a step ran off the end of the range;</item>
    ///   <item>with height zones on, small <b>zone bars</b>, the current one lit, so an octave jump is never a surprise.</item>
    /// </list>
    /// When the gate is closed everything dims and a short hint says what pose is wanted. No note highways, no
    /// targets, no colour-only information (CLAUDE.md §3.10). Reads the pure <see cref="PalmSwingPipeline"/>; never
    /// writes to it. The lane's position comes from <see cref="SetLane"/>, so this assembly needs nothing from Input.
    /// </summary>
    public sealed class PalmSwingView : MonoBehaviour
    {
        private const float LineHeightMetres = 0.45f;
        private const float LineWidthMetres = 0.004f;
        private const float MarkerHeightMetres = 0.09f;
        private const float FlashSeconds = 0.18f;
        private const float DimAlpha = 0.25f;

        [SerializeField] private Transform _head;

        [Tooltip("Hint shown while the gate is closed. Plain words: no theory vocabulary.")]
        [SerializeField] private string _hint = "open palm, facing left";

        private PalmSwingPipeline _pipeline;
        private LineRenderer _line;
        private TextMesh _marker;
        private TextMesh _hintText;
        private MeshRenderer _up;
        private MeshRenderer _same;
        private MeshRenderer _down;
        private MeshRenderer _limitBar;
        private MeshRenderer[] _zoneBars;
        private Transform _pivot;
        private Material _template;
        private bool _hasLane;
        private float _flashLineUntil = float.NegativeInfinity;
        private float _flashMarkerUntil = float.NegativeInfinity;
        private Vector3 _laneOrigin;
        private Vector3 _laneRight = Vector3.right;

        /// <summary>Wire to the pipeline whose state is shown. Safe to call once, before or after enabling.</summary>
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
        /// Place the lane: <paramref name="origin"/> is where the trigger line stands (the palm's resting
        /// position), <paramref name="right"/> the horizontal left-to-right axis. The visuals face the head.
        /// </summary>
        public void SetLane(Vector3 origin, Vector3 right)
        {
            _hasLane = true;
            _laneOrigin = origin;
            _laneRight = Vector3.ProjectOnPlane(right, Vector3.up).normalized;
        }

        private void Awake()
        {
            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform;
            }

            _template = RuntimeMaterial.Unlit("Palm Swing");
            _pivot = new GameObject("Palm Swing Lane").transform;
            _pivot.SetParent(transform, worldPositionStays: false);

            _line = new GameObject("Trigger Line").AddComponent<LineRenderer>();
            _line.transform.SetParent(_pivot, worldPositionStays: false);
            _line.useWorldSpace = false;
            _line.positionCount = 2;
            _line.SetPosition(0, new Vector3(0f, -LineHeightMetres * 0.5f, 0f));
            _line.SetPosition(1, new Vector3(0f, LineHeightMetres * 0.5f, 0f));
            _line.widthMultiplier = LineWidthMetres;
            _line.sharedMaterial = _template;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _marker = MakeText("Note Marker", font, 90, new Vector3(0f, LineHeightMetres * 0.5f + MarkerHeightMetres, 0f), TextAnchor.MiddleCenter);
            _hintText = MakeText("Gate Hint", font, 50, new Vector3(0f, -LineHeightMetres * 0.5f - 0.05f, 0f), TextAnchor.UpperCenter);
            _hintText.text = _hint;

            Vector3 indicator = new Vector3(0.09f, LineHeightMetres * 0.5f + MarkerHeightMetres, 0f);
            _up = MakeShape("Direction Up", Triangle(up: true), indicator + new Vector3(0f, 0.035f, 0f), 0.03f);
            _same = MakeShape("Direction Same", Disc(), indicator, 0.016f);
            _down = MakeShape("Direction Down", Triangle(up: false), indicator + new Vector3(0f, -0.035f, 0f), 0.03f);

            Vector3 markerPos = _marker.transform.localPosition;
            _limitBar = MakeShape("At Limit Bar", Quad(), markerPos, 1f);
            _limitBar.transform.localScale = new Vector3(0.08f, 0.008f, 1f);

            _zoneBars = new MeshRenderer[3];
            for (int z = 0; z < _zoneBars.Length; z++)
            {
                _zoneBars[z] = MakeShape($"Zone Bar {z}", Quad(), markerPos + new Vector3(-0.09f, (z - 1) * 0.022f, 0f), 1f);
                _zoneBars[z].transform.localScale = new Vector3(0.035f, 0.01f, 1f);
            }

            _pivot.gameObject.SetActive(false);
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
            if (_pipeline == null || _pivot == null)
            {
                return;
            }

            _pivot.gameObject.SetActive(_hasLane);
            if (!_hasLane)
            {
                return;
            }

            _pivot.position = _laneOrigin;
            _pivot.rotation = Quaternion.LookRotation(Vector3.Cross(_laneRight, Vector3.up), Vector3.up);

            if (_head != null)
            {
                // Face the head, yaw only, so the line stays vertical and the text reads.
                Vector3 toHead = Vector3.ProjectOnPlane(_head.position - _pivot.position, Vector3.up);
                if (toHead.sqrMagnitude > 1e-4f)
                {
                    _pivot.rotation = Quaternion.LookRotation(-toHead.normalized, Vector3.up);
                }
            }

            float now = Time.unscaledTime;
            float gate = _pipeline.GateOpen ? 1f : DimAlpha;

            float lineFlash = Mathf.Clamp01((_flashLineUntil - now) / FlashSeconds);
            SetLineColor(new Color(1f, 1f, 1f, Mathf.Lerp(0.22f, 0.95f, lineFlash) * gate));

            float markerFlash = Mathf.Clamp01((_flashMarkerUntil - now) / FlashSeconds);
            _marker.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.85f, 1f, markerFlash) * gate);
            _marker.transform.localScale = Vector3.one * (0.01f * (1f + 0.35f * markerFlash));

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
                _limitBar.transform.localPosition = _marker.transform.localPosition + new Vector3(0f, side * 0.045f, 0f);
                SetShape(_limitBar, true, gate);
            }

            _hintText.gameObject.SetActive(!_pipeline.GateOpen);
            _hintText.color = new Color(1f, 1f, 1f, 0.8f);
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

        private TextMesh MakeText(string name, Font font, int size, Vector3 localPosition, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_pivot, worldPositionStays: false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * 0.01f;
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
            go.transform.SetParent(_pivot, worldPositionStays: false);
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
