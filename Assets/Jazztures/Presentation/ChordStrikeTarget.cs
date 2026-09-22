using Jazztures.Core.Melody;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// A single virtual object the left hand must touch to articulate the currently
    /// selected chord (ADR-0039) — a third articulation commit, alongside the strike
    /// (ADR-0025) and the pinch (ADR-0038). Reuses <see cref="TargetVolume"/>, the exact
    /// proven containment math the right hand's ten melody targets already use
    /// (ADR-0016/0018), rather than a velocity threshold or the SDK's own pinch flag —
    /// this is the one commit mechanism in this project's history built from a piece that
    /// has already worked reliably on this hardware, just for the other hand.
    /// </summary>
    /// <remarks>
    /// Senses itself: assign the left <see cref="IHand"/> here directly, rather than
    /// relying on some other component's <c>Update()</c> to have run first. Unity does not
    /// guarantee <c>Update()</c> order between components, so <see cref="Sense"/> is called
    /// explicitly by <c>PerformanceCompositionRoot.Update()</c> and memoizes per
    /// <see cref="Time.frameCount"/> — the same pattern <c>MetaXRHandPoseSource.CurrentFrame</c>
    /// and <c>MetaXRHandPostureSource.CurrentFrame</c> already use for the identical reason.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ChordStrikeTarget : MonoBehaviour
    {
        [Tooltip("The left Interaction SDK Hand component.")]
        [SerializeField] private MonoBehaviour _leftHand;

        [Tooltip("Radius of the target's circular face, metres.")]
        [Min(0.01f)] [SerializeField] private float _radiusMetres = 0.06f;

        [Tooltip("Half the target's depth along its local Z (the approach axis), metres. "
            + "Generous on purpose — depth is the one axis a person cannot judge in mid-air "
            + "VR (ADR-0018), same reasoning as the melody targets.")]
        [Min(0.01f)] [SerializeField] private float _halfDepthMetres = 0.08f;

        [Tooltip("Recent frames to take the peak entry speed over — mirrors the right "
            + "hand's own fingertip-speed sampling (ADR-0018).")]
        [Min(1)] [SerializeField] private int _speedSampleFrames = 3;

        [Tooltip("Which fingertip must enter the volume. Index by default, matching the melody targets' primary finger.")]
        [SerializeField] private HandJointId _fingertip = HandJointId.HandIndexTip;

        [Tooltip("Optional — purely cosmetic, so the learner can find the target in mid-air. Flashes briefly on a hit.")]
        [SerializeField] private Renderer _renderer;

        [Tooltip("Seconds for the hit flash to decay back to the base colour.")]
        [Min(0.01f)] [SerializeField] private float _flashDecaySeconds = 0.18f;

        [Tooltip("Base colour, always visible (§3.1's 'a stable spatial map', applied to a single object).")]
        [SerializeField] private Color _baseColor = new Color(0.58f, 0.58f, 0.62f, 0.5f);

        [Tooltip("Colour at the moment of a hit.")]
        [SerializeField] private Color _flashColor = new Color(1f, 0.96f, 0.85f, 1f);

        private IHand _left;
        private TargetVolume _volume;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorIdLegacy = Shader.PropertyToID("_Color");

        private bool _hasPreviousTip;
        private Vector3 _previousTip;
        private float[] _speedSamples;
        private int _speedCursor;
        private float _flash;

        private bool _cachedInside;
        private float _cachedEntrySpeed;
        private int _cachedFrameNumber = -1;

        /// <summary>Whether the fingertip is inside the volume, as of the last <see cref="Sense"/> call this frame.</summary>
        public bool CurrentlyInside => _cachedInside;

        /// <summary>The peak entry speed, metres per second, as of the last <see cref="Sense"/> call this frame.</summary>
        public float CurrentEntrySpeed => _cachedEntrySpeed;

        private void Awake()
        {
            _left = Resolve<IHand>(_leftHand, nameof(_leftHand));
            _volume = new TargetVolume(_radiusMetres, _halfDepthMetres);
            _speedSamples = new float[Mathf.Max(1, _speedSampleFrames)];

            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<Renderer>();
            }

            if (_renderer != null)
            {
                _mpb = new MaterialPropertyBlock();
                Write(_baseColor);
            }
        }

        /// <summary>
        /// Compute this frame's containment/speed, memoized per <see cref="Time.frameCount"/>
        /// so it is safe to call from more than one place in the same frame. Call once per
        /// frame from the composition root before reading <see cref="CurrentlyInside"/>/
        /// <see cref="CurrentEntrySpeed"/>.
        /// </summary>
        public void Sense()
        {
            int frame = Time.frameCount;
            if (frame == _cachedFrameNumber)
            {
                return;
            }

            _cachedFrameNumber = frame;

            if (_left == null || !_left.IsTrackedDataValid || !_left.GetJointPose(_fingertip, out Pose tip))
            {
                _hasPreviousTip = false;
                _cachedInside = false;
                _cachedEntrySpeed = 0f;
                return;
            }

            float dt = Time.deltaTime;
            float instant = _hasPreviousTip && dt > 0f
                ? Vector3.Distance(tip.position, _previousTip) / dt
                : 0f;
            _previousTip = tip.position;
            _hasPreviousTip = true;

            _speedCursor = (_speedCursor + 1) % _speedSamples.Length;
            _speedSamples[_speedCursor] = instant;

            float peak = 0f;
            for (int i = 0; i < _speedSamples.Length; i++)
            {
                if (_speedSamples[i] > peak)
                {
                    peak = _speedSamples[i];
                }
            }

            Vector3 local = Quaternion.Inverse(transform.rotation) * (tip.position - transform.position);
            _cachedInside = _volume.Contains(local.x, local.y, local.z);
            _cachedEntrySpeed = peak;
        }

        /// <summary>Flash the target — a fingertip just triggered it. Purely cosmetic.</summary>
        public void Flash()
        {
            _flash = 1f;
        }

        private void Update()
        {
            if (_flash > 0f && _renderer != null)
            {
                _flash = Mathf.Max(0f, _flash - Time.deltaTime / _flashDecaySeconds);
                Write(Color.Lerp(_baseColor, _flashColor, _flash));
            }
        }

        private void Write(Color color)
        {
            _mpb.Clear();
            _mpb.SetColor(ColorId, color);
            _mpb.SetColor(ColorIdLegacy, color);
            _renderer.SetPropertyBlock(_mpb);
        }

        private T Resolve<T>(MonoBehaviour behaviour, string field) where T : class
        {
            if (behaviour == null)
            {
                Debug.LogError($"{nameof(ChordStrikeTarget)}: '{field}' is not assigned.", this);
                return null;
            }

            if (behaviour is T typed)
            {
                return typed;
            }

            Debug.LogError(
                $"{nameof(ChordStrikeTarget)}: '{field}' ({behaviour.GetType().Name}) is not an {typeof(T).Name}.",
                this);
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.80f, 1f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(_radiusMetres * 2f, _radiusMetres * 2f, _halfDepthMetres * 2f));
        }
    }
}
