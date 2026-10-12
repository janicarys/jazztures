using System.Collections.Generic;
using Jazztures.Config;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// Spawns the ten <see cref="TouchTarget"/>s on an arc centred on the learner's right
    /// shoulder (ADR-0019): five scale-degree columns swept around the pivot, two octave rows
    /// (near = lower, far = higher). Within a row every target is the same distance from the
    /// shoulder, so the outer degrees are no harder to reach than the centre and a glissando
    /// is a single shoulder rotation. Each target lies flat, face up, and is struck from
    /// above (ADR-0049).
    ///
    /// <para>
    /// The rig is body-anchored at the shoulder pivot and keeps its facing with the pure
    /// <see cref="LazyRecenter"/> (ADR-0015): it tracks head position and yaw but ignores
    /// pitch/roll, and only eases to a new facing once the head has turned past a threshold
    /// for a dwell.
    /// </para>
    ///
    /// <para>
    /// Runs at DefaultExecutionOrder(-10) so its per-frame re-anchor lands before
    /// <see cref="TouchTargetBinder"/> reads the target transforms for its hit test —
    /// otherwise a fast reach mid-recenter is tested against one-frame-stale positions.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class TouchTargetRig : MonoBehaviour
    {
        // Paddle cosmetics (ADR-0046) — how it is drawn, not how it behaves, so not [TUNABLE].
        // Ignored when the look is Prefab.
        private const float PadHalfThicknessMetres = 0.006f; // a 1.2 cm pad
        private const float RailRadiusMetres = 0.004f;

        [Tooltip("Tuning for geometry and the lazy-recenter follow (ADR-0015).")]
        [SerializeField] private MelodyConfig _config;

        [Tooltip("The centre-eye / head transform to anchor against.")]
        [SerializeField] private Transform _head;

        [Tooltip("Optional. A styled target prefab or scene object (needs a Renderer), used only " +
                 "when MelodyConfig's Target Look is Prefab. With the default Paddle look this " +
                 "slot is ignored — the paddle is generated.")]
        [SerializeField] private GameObject _targetPrefab;

        [Tooltip("Material for the generated paddle — a transparent unlit so alpha reads as " +
                 "authored. Ignored when the prefab look is used.")]
        [SerializeField] private Material _targetMaterial;

        private readonly List<TouchTarget> _targets = new List<TouchTarget>(ChordToneSet.TargetCount);
        private LazyRecenter _recenter;
        private float _lastYawRadians;
        private bool _ready;

        public IReadOnlyList<TouchTarget> Targets => _targets;

        /// <summary>
        /// Show or hide the ten stop markers. The lesson flow hides them on the selector
        /// screen — the melody instrument is not part of the menu (§3.10).
        /// </summary>
        public void SetTargetsVisible(bool visible)
        {
            for (int i = 0; i < _targets.Count; i++)
            {
                if (_targets[i] != null)
                {
                    _targets[i].gameObject.SetActive(visible);
                }
            }
        }

        private void Awake()
        {
            if (_config == null || _head == null)
            {
                Debug.LogError($"{nameof(TouchTargetRig)}: assign {nameof(_config)} and {nameof(_head)}.", this);
                enabled = false;
                return;
            }

            SpawnTargets();
        }

        private void Start()
        {
            _lastYawRadians = ReadHeadYaw();
            _recenter = new LazyRecenter(_config.ToRecenterSettings(), _lastYawRadians);
            ApplyAnchor(_lastYawRadians);
            _ready = true;
        }

        private void LateUpdate()
        {
            if (!_ready)
            {
                return;
            }

            float committed = (float)_recenter.Update(ReadHeadYaw(), Time.deltaTime);
            ApplyAnchor(committed);

            // The note labels are laid flat on their pads, upright for the learner, after the
            // anchor has moved the targets (ADR-0044, ADR-0049).
            Vector3 viewer = _head.position;
            for (int i = 0; i < _targets.Count; i++)
            {
                _targets[i].FaceLabel(viewer);
            }
        }

        /// <summary>Snap the grid to the learner's current facing (e.g. a recenter input).</summary>
        public void ForceRecenter()
        {
            if (_ready)
            {
                float yaw = ReadHeadYaw();
                _recenter.Reset(yaw);
                ApplyAnchor(yaw);
            }
        }

        private void SpawnTargets()
        {
            float radius = _config.TargetRadiusMetres;
            float halfDepth = _config.TargetDepthMetres * 0.5f;

            bool usePrefab = _config.TargetLook == TargetLook.Prefab;
            if (usePrefab && _targetPrefab == null)
            {
                Debug.LogWarning(
                    $"{nameof(TouchTargetRig)}: Target Look is Prefab but no Target Prefab is assigned — " +
                    "drawing the paddle instead.", this);
                usePrefab = false;
            }

            // The padded half-thickness cannot exceed the volume it travels in.
            float padHalfThickness = Mathf.Min(PadHalfThicknessMetres, halfDepth * 0.5f);

            for (int slot = 0; slot < ChordToneSet.TargetCount; slot++)
            {
                int octave = slot / ChordToneSet.DegreesPerOctave;
                int degree = slot % ChordToneSet.DegreesPerOctave;

                TouchTarget target;
                GameObject go;
                if (usePrefab)
                {
                    go = Instantiate(_targetPrefab, transform);
                    target = go.GetComponent<TouchTarget>() ?? go.AddComponent<TouchTarget>();
                }
                else
                {
                    go = GeneratePaddle(radius, halfDepth, padHalfThickness,
                        out Renderer pad, out Renderer rail, out Transform padRoot);
                    target = go.AddComponent<TouchTarget>();
                    target.BindGeneratedVisual(pad, rail, padRoot, halfDepth, padHalfThickness);
                }

                go.transform.SetParent(transform, worldPositionStays: false);

                LocalPlacement(octave, degree, out Vector3 localPosition, out Quaternion localRotation);
                go.transform.localPosition = localPosition;
                go.transform.localRotation = localRotation;

                target.Configure(slot, radius, halfDepth);
                target.ConfigureLabel(_config.ShowNoteLabels, _config.LabelCharacterSize, _config.LabelColor);
                target.Clear(); // visible from the start (§3.1); just not soundable yet
                _targets.Add(target);
            }
        }

        /// <summary>
        /// One target's place on the shoulder arc (ADR-0019, ADR-0049). Column
        /// <paramref name="degree"/> is swept by <c>ColumnAngleDegrees</c> about the pivot's up
        /// axis. The targets lie flat, face up, and are struck from above, so their local +Z —
        /// the depth axis <see cref="TouchTarget.Contains"/> tests along — points straight
        /// <b>down</b>, the way the fingertip travels. That is also why the two octave rows are
        /// <b>near and far</b> rather than stacked: a pad stacked above another would sit in the
        /// path of a fingertip coming down onto the lower one. The far row is the higher octave.
        /// </summary>
        private void LocalPlacement(int octave, int degree, out Vector3 position, out Quaternion rotation)
        {
            float midDegree = (ChordToneSet.DegreesPerOctave - 1) / 2f;
            float angleDeg = (degree - midDegree) * _config.ColumnAngleDegrees;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            float reach = _config.ReachDistanceMetres + (octave - 0.5f) * _config.RowSpacingMetres;

            position = new Vector3(
                Mathf.Sin(angleRad) * reach,
                0f,
                Mathf.Cos(angleRad) * reach);
            rotation = Quaternion.Euler(90f, angleDeg, 0f); // +Z straight down, yawed with its column
        }

        /// <summary>
        /// The default target visual (ADR-0046): a flat pad at the volume's own radius, resting
        /// at its near, learner-facing end, on a faint rail the length of the volume. The pad
        /// sits on a unit-scale root (<paramref name="padRoot"/>) that
        /// <see cref="TouchTarget.SetFingerDepth"/> slides along the rail, so the note label can
        /// ride on it without inheriting the cylinder's non-uniform scale. Replaces the earlier
        /// translucent tube, whose "entry rim" was drawn at the far end of the volume and whose
        /// depth disc was a separate object from the thing being pushed.
        /// </summary>
        private GameObject GeneratePaddle(
            float radius, float halfDepth, float padHalfThickness,
            out Renderer pad, out Renderer rail, out Transform padRoot)
        {
            var root = new GameObject("Target");

            rail = MakeCylinder("Rail", root.transform, RailRadiusMetres, halfDepth);

            padRoot = new GameObject("PadRoot").transform;
            padRoot.SetParent(root.transform, worldPositionStays: false);
            padRoot.localPosition = new Vector3(0f, 0f, -halfDepth + padHalfThickness);

            pad = MakeCylinder("Pad", padRoot, radius, padHalfThickness);
            pad.sortingOrder = 1; // over the rail inside the transparent queue

            return root;
        }

        /// <summary>
        /// A cylinder primitive re-aligned to the target's local Z (its approach axis) and
        /// scaled to <paramref name="radius"/> across the face by ±<paramref name="halfLength"/>
        /// along Z. Unity's cylinder is Y-aligned, half-height 1, radius 0.5.
        /// </summary>
        private Renderer MakeCylinder(string name, Transform parent, float radius, float halfLength)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            if (go.TryGetComponent(out Collider collider))
            {
                Destroy(collider);
            }

            Transform t = go.transform;
            t.SetParent(parent, worldPositionStays: false);
            t.localRotation = Quaternion.Euler(90f, 0f, 0f);
            t.localScale = new Vector3(radius * 2f, halfLength, radius * 2f);

            var renderer = go.GetComponent<Renderer>();
            if (_targetMaterial != null)
            {
                renderer.sharedMaterial = _targetMaterial;
            }

            return renderer;
        }

        private void ApplyAnchor(float yawRadians)
        {
            // The rig sits AT the shoulder pivot; the arc radius (reach) is applied
            // per-target in LocalPlacement, not here (ADR-0019).
            Quaternion facing = Quaternion.AngleAxis(yawRadians * Mathf.Rad2Deg, Vector3.up);
            Vector3 pivot =
                _head.position +
                Vector3.up * _config.ShoulderHeightOffsetMetres +
                facing * Vector3.right * _config.ShoulderLateralOffsetMetres;
            transform.SetPositionAndRotation(pivot, facing);
        }

        private float ReadHeadYaw()
        {
            Vector3 forward = _head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                return _lastYawRadians; // looking straight up or down — hold the last heading
            }

            _lastYawRadians = Mathf.Atan2(forward.x, forward.z);
            return _lastYawRadians;
        }

        private void OnDrawGizmosSelected()
        {
            if (_config == null)
            {
                return;
            }

            // Draw the arc and each target volume in the pivot's frame, so the layout is
            // checkable in the editor without the headset (ADR-0019). At edit time the rig
            // has not been anchored yet, so estimate the pivot from the head.
            Quaternion facing;
            Vector3 pivot;
            if (Application.isPlaying || _head == null)
            {
                facing = transform.rotation;
                pivot = transform.position;
            }
            else
            {
                Vector3 flat = _head.forward;
                flat.y = 0f;
                facing = flat.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(flat, Vector3.up) : Quaternion.identity;
                pivot = _head.position
                    + Vector3.up * _config.ShoulderHeightOffsetMetres
                    + facing * Vector3.right * _config.ShoulderLateralOffsetMetres;
            }

            float radius = _config.TargetRadiusMetres;
            float halfDepth = _config.TargetDepthMetres * 0.5f;
            var pivotFrame = Matrix4x4.TRS(pivot, facing, Vector3.one);

            Gizmos.matrix = pivotFrame;
            Gizmos.color = new Color(0.4f, 0.6f, 0.9f, 0.25f);
            for (int row = 0; row < 2; row++)
            {
                DrawRing(_config.ReachDistanceMetres + (row - 0.5f) * _config.RowSpacingMetres);
            }

            for (int slot = 0; slot < ChordToneSet.TargetCount; slot++)
            {
                LocalPlacement(slot / ChordToneSet.DegreesPerOctave, slot % ChordToneSet.DegreesPerOctave,
                    out Vector3 p, out Quaternion r);

                Gizmos.matrix = pivotFrame * Matrix4x4.TRS(p, r, Vector3.one);
                Gizmos.color = new Color(0.55f, 0.80f, 1f, 0.6f);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(radius * 2f, radius * 2f, halfDepth * 2f));
            }
        }

        /// <summary>A horizontal ring about the pivot, in the current Gizmos matrix.</summary>
        private static void DrawRing(float radius)
        {
            const int Segments = 48;
            Vector3 previous = new Vector3(0f, 0f, radius);
            for (int i = 1; i <= Segments; i++)
            {
                float a = i * (Mathf.PI * 2f / Segments);
                Vector3 next = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
