using System.Collections.Generic;
using Jazztures.Config;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// Spawns the ten <see cref="TouchTarget"/>s in a 2×5 grid (octave × scale degree,
    /// matching the <see cref="ChordToneSet"/> slot order) and keeps the whole grid
    /// body-anchored in front of the learner (ADR-0015): it tracks head position and yaw
    /// but ignores pitch/roll, and only eases to a new facing once the head has turned
    /// past a threshold for a dwell. All the timing is in the pure
    /// <see cref="LazyRecenter"/>; this component just feeds it head yaw and applies the
    /// result.
    /// </summary>
    public sealed class TouchTargetRig : MonoBehaviour
    {
        [Tooltip("Tuning for geometry and the lazy-recenter follow (ADR-0015).")]
        [SerializeField] private MelodyConfig _config;

        [Tooltip("The centre-eye / head transform to anchor against.")]
        [SerializeField] private Transform _head;

        [Tooltip("Optional. A styled target prefab (needs a Renderer). If empty, a sphere is generated.")]
        [SerializeField] private GameObject _targetPrefab;

        [Tooltip("Material for generated spheres — a transparent unlit so alpha and the " +
                 "chord colour read as authored. Ignored when a prefab is supplied.")]
        [SerializeField] private Material _targetMaterial;

        private readonly List<TouchTarget> _targets = new List<TouchTarget>(ChordToneSet.TargetCount);
        private LazyRecenter _recenter;
        private float _lastYawRadians;
        private bool _ready;

        public IReadOnlyList<TouchTarget> Targets => _targets;

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
            float spacing = _config.InterTargetSpacingMetres;
            float radius = _config.TargetRadiusMetres;
            float halfDepth = _config.TargetDepthMetres * 0.5f;
            float midDegree = (ChordToneSet.DegreesPerOctave - 1) / 2f;

            for (int slot = 0; slot < ChordToneSet.TargetCount; slot++)
            {
                int octave = slot / ChordToneSet.DegreesPerOctave;
                int degree = slot % ChordToneSet.DegreesPerOctave;

                GameObject go = _targetPrefab != null
                    ? Instantiate(_targetPrefab, transform)
                    : GenerateSphere();
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localPosition = new Vector3(
                    (degree - midDegree) * spacing,
                    (octave - 0.5f) * spacing,
                    0f);
                go.transform.localRotation = Quaternion.identity;

                TouchTarget target = go.GetComponent<TouchTarget>();
                if (target == null)
                {
                    target = go.AddComponent<TouchTarget>();
                }

                target.Configure(slot, radius, halfDepth);
                target.Clear(); // visible from the start (§3.1); just not soundable yet
                _targets.Add(target);
            }
        }

        private GameObject GenerateSphere()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            if (_targetMaterial != null && go.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = _targetMaterial;
            }

            return go;
        }

        private void ApplyAnchor(float yawRadians)
        {
            Quaternion facing = Quaternion.AngleAxis(yawRadians * Mathf.Rad2Deg, Vector3.up);
            Vector3 origin = _head.position + Vector3.up * _config.AnchorHeightOffsetMetres;
            Vector3 offset =
                facing * Vector3.forward * _config.ReachDistanceMetres +
                facing * Vector3.right * _config.AnchorLateralOffsetMetres;
            transform.SetPositionAndRotation(origin + offset, facing);
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
    }
}
