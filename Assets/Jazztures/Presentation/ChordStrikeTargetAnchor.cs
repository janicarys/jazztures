using Jazztures.Core.Melody;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// Body-anchors the left-hand strike plate (ADR-0040) the same way
    /// <see cref="TouchTargetRig"/> anchors the melody arc (ADR-0015): tracks head position
    /// and yaw only — ignoring pitch/roll — through the pure, headlessly-tested
    /// <see cref="LazyRecenter"/>, easing to a new facing only once the head has diverged
    /// past a threshold and stayed there for a dwell. Uses <see cref="LazyRecenterSettings.Default"/>
    /// unchanged — the same body, the same "how much yaw drift before recentering" question
    /// the melody arc already answered; nothing here justifies a second guess.
    ///
    /// <para>
    /// Mirrored to the left, at the same height as melody's shoulder-arc (ADR-0040 revision:
    /// shipped lower at first, moved up to match melody's level so both hands operate at the
    /// same height).
    /// </para>
    ///
    /// <para>
    /// Unlike <see cref="TouchTargetRig"/>, this does not rely on Unity's <c>LateUpdate</c>
    /// ordering: it exposes <see cref="Reanchor"/>, which
    /// <c>PerformanceCompositionRoot.Update()</c> calls explicitly, immediately before
    /// <see cref="ChordStrikeTarget.Sense"/> — the same "call it, don't hope for ordering"
    /// pattern <c>Sense()</c> itself already documents (Unity gives no cross-component
    /// ordering guarantee).
    /// </para>
    ///
    /// <para>
    /// The plate is a flat disc struck from above, not a radial approach volume like a
    /// melody target: <see cref="TargetVolume"/>'s local Z is always its approach axis
    /// (ADR-0016/0018), so <see cref="ApplyAnchor"/> fixes the transform's rotation so local
    /// Z points straight up, laying the disc horizontal. Because the containment test is a
    /// circular cylinder, that rotation is arbitrary about the vertical axis — unlike the
    /// melody arc, the plate never needs to track head yaw for its <em>orientation</em>,
    /// only its position.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(ChordStrikeTarget))]
    public sealed class ChordStrikeTargetAnchor : MonoBehaviour
    {
        [Tooltip("The centre-eye / head transform to anchor against.")]
        [SerializeField] private Transform _head;

        [Tooltip("Vertical offset from head height, metres (negative = below). Matches "
            + "melody's own shoulder-arc height (MelodyConfig.ShoulderHeightOffsetMetres, "
            + "-0.25 m, ADR-0015) so both hands operate at the same level — see ADR-0040.")]
        [SerializeField] private float _heightOffsetMetres = -0.25f;

        [Tooltip("Lateral offset from head, metres (negative = the learner's left) — "
            + "mirrors melody's rightward bias (ADR-0020) onto the other hand.")]
        [SerializeField] private float _lateralOffsetMetres = -0.20f;

        [Tooltip("Forward offset from head, metres — how far in front of the body the plate sits.")]
        [SerializeField] private float _forwardOffsetMetres = 0.20f;

        private LazyRecenter _recenter;
        private float _lastYawRadians;
        private bool _ready;

        private void Start()
        {
            if (_head == null)
            {
                Debug.LogError($"{nameof(ChordStrikeTargetAnchor)}: assign {nameof(_head)}.", this);
                enabled = false;
                return;
            }

            _lastYawRadians = ReadHeadYaw();
            _recenter = new LazyRecenter(LazyRecenterSettings.Default, _lastYawRadians);
            ApplyAnchor(_lastYawRadians);
            _ready = true;
        }

        /// <summary>
        /// Re-anchor for this frame. Call once, before reading the sibling
        /// <see cref="ChordStrikeTarget"/>'s <c>Sense()</c> result.
        /// </summary>
        public void Reanchor(float deltaTimeSeconds)
        {
            if (!_ready)
            {
                return;
            }

            float committed = (float)_recenter.Update(ReadHeadYaw(), deltaTimeSeconds);
            ApplyAnchor(committed);
        }

        /// <summary>Snap the plate to the learner's current facing (e.g. a recenter input).</summary>
        public void ForceRecenter()
        {
            if (!_ready)
            {
                return;
            }

            float yaw = ReadHeadYaw();
            _recenter.Reset(yaw);
            ApplyAnchor(yaw);
        }

        private void ApplyAnchor(float yawRadians)
        {
            Quaternion facing = Quaternion.AngleAxis(yawRadians * Mathf.Rad2Deg, Vector3.up);
            Vector3 pivot =
                _head.position +
                Vector3.up * _heightOffsetMetres +
                facing * new Vector3(_lateralOffsetMetres, 0f, _forwardOffsetMetres);

            // Local Z is TargetVolume's approach axis — point it straight up so the disc
            // lies flat, struck from above. Rotationally symmetric about that axis, so no
            // yaw term belongs here.
            transform.SetPositionAndRotation(pivot, Quaternion.Euler(-90f, 0f, 0f));
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
            if (_head == null)
            {
                return;
            }

            Vector3 flat = _head.forward;
            flat.y = 0f;
            Quaternion facing = flat.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(flat, Vector3.up)
                : Quaternion.identity;
            Vector3 pivot =
                _head.position +
                Vector3.up * _heightOffsetMetres +
                facing * new Vector3(_lateralOffsetMetres, 0f, _forwardOffsetMetres);

            Gizmos.color = new Color(1f, 0.6f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(pivot, 0.05f);
            Gizmos.DrawLine(_head.position, pivot);
        }
    }
}
