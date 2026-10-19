using Jazztures.Core.Gesture;
using Jazztures.Core.Melody;
using Oculus.Interaction.Input;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// A single virtual object the left hand must touch to articulate the currently
    /// selected chord (ADR-0039) — a third articulation commit, alongside the strike
    /// (ADR-0025) and the pinch (ADR-0038). Contact is plain volume containment, the same idea
    /// the right hand's ten melody targets use (ADR-0016/0018), rather than a velocity threshold
    /// or the SDK's own pinch flag. The volume is a <see cref="StrikePlateVolume"/> built from
    /// the plate's own visual (ADR-0048), so what the learner sees is what they hit.
    ///
    /// <para>
    /// <b>Any part of the hand can touch; only the hand's own motion strikes</b> (ADR-0047). The
    /// plate reports "inside" when any of <c>Contact Joints</c> — fingertips, knuckles, the
    /// pinky-side palm, the wrist — is in the volume: a chop reaches the plate with whatever leads
    /// (the pinky edge of a sideways hand, the heel of a fingers-up hand, the fingertips of a flat
    /// press), and testing one fingertip made the learner push several centimetres past a visual
    /// contact. But the strike <i>speed</i> is the peak <b>downward</b> speed of the palm and wrist
    /// (<c>Speed Joints</c>) — never the fingertips, which move fast whenever the hand opens or
    /// closes, and never sideways or upward motion. Taking the speed from the fastest joint inside
    /// made changing pose near the plate read as a strike.
    /// </para>
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

        [Tooltip("The plate's visual. The hit volume is built from THIS renderer (ADR-0048): its " +
                 "footprint, and its top face as the surface. Resize or move the visual and the " +
                 "hitbox follows — they cannot drift apart. It also flashes on a hit. Leave empty " +
                 "to use the first Renderer under this object.")]
        [SerializeField] private Renderer _renderer;

        [Tooltip("How far the hit area extends past the visual's edges on every side, metres.")]
        [Min(0f)] [SerializeField] private float _footprintMarginMetres = 0.02f;

        [Tooltip("How far ABOVE the visual's top surface still counts, metres. A joint's centre is " +
                 "about a centimetre inside the skin, so ~2 cm makes the touch land when the skin " +
                 "meets the plate. Raise it and the hitbox feels higher than the visual.")]
        [Min(0f)] [SerializeField] private float _hoverAboveSurfaceMetres = 0.02f;

        [Tooltip("How far BELOW the visual's top surface still counts, metres. Generous on purpose — " +
                 "depth is the one axis a person cannot judge in mid-air VR (ADR-0018), so a hand " +
                 "that overshoots the plate must still count.")]
        [Min(0.01f)] [SerializeField] private float _depthBelowSurfaceMetres = 0.10f;

        [Tooltip("Recent frames to take the peak downward speed over — mirrors the strike's own " +
                 "sampling (ADR-0028).")]
        [Min(1)] [SerializeField] private int _speedSampleFrames = 3;

        [Tooltip("WHERE: which parts of the hand count as touching the plate. ANY of these inside the " +
                 "volume is a touch (ADR-0047). The hand reaches the plate with whatever part leads — " +
                 "the pinky edge of a sideways ii chop, the heel of a fingers-up chop, the fingertips " +
                 "of a flat I press, the knuckles of a V fist. Remove a joint here if it sets the plate " +
                 "off by accident.")]
        [SerializeField] private HandJointId[] _contactJoints =
        {
            HandJointId.HandIndexTip,
            HandJointId.HandMiddleTip,
            HandJointId.HandRingTip,
            HandJointId.HandPinkyTip,
            HandJointId.HandThumbTip,
            HandJointId.HandIndex1,
            HandJointId.HandMiddle1,
            HandJointId.HandRing1,
            HandJointId.HandPinky1,
            HandJointId.HandPinky0,
            HandJointId.HandWristRoot,
        };

        [Tooltip("HOW HARD: the strike speed is the peak DOWNWARD speed of these joints — the palm and " +
                 "wrist, which move when the hand moves but NOT when the fingers open or close. " +
                 "Using the fingertips here made every pose change near the plate read as a strike " +
                 "(ADR-0047 addendum). Only downward motion counts: the plate is struck from above.")]
        [SerializeField] private HandJointId[] _speedJoints =
        {
            HandJointId.HandWristRoot,
            HandJointId.HandIndex1,
            HandJointId.HandMiddle1,
            HandJointId.HandRing1,
            HandJointId.HandPinky1,
            HandJointId.HandPinky0,
        };

        [Tooltip("Seconds for the hit flash to decay back to the base colour.")]
        [Min(0.01f)] [SerializeField] private float _flashDecaySeconds = 0.18f;

        [Tooltip("Base colour, always visible (§3.1's 'a stable spatial map', applied to a single object).")]
        [SerializeField] private Color _baseColor = new Color(0.58f, 0.58f, 0.62f, 0.5f);

        [Tooltip("Colour at the moment of a hit.")]
        [SerializeField] private Color _flashColor = new Color(1f, 0.96f, 0.85f, 1f);

        private IHand _left;
        private StrikePlateVolume _volume;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorIdLegacy = Shader.PropertyToID("_Color");

        // One downward-speed window per speed joint (the palm and wrist).
        private DownwardSpeedWindow[] _speedWindows;
        private float _flash;

        private bool _cachedInside;
        private float _cachedEntrySpeed;
        private int _cachedFrameNumber = -1;

        /// <summary>Whether any contact joint is inside the volume, as of the last <see cref="Sense"/> call this frame.</summary>
        public bool CurrentlyInside => _cachedInside;

        /// <summary>The hand's peak downward speed, metres per second (0 when it is not moving down), as of the last <see cref="Sense"/> call this frame.</summary>
        public float CurrentEntrySpeed => _cachedEntrySpeed;

        /// <summary>
        /// The middle of the visual's top face in world space - where a hand lands on the plate.
        /// The ghost hand (ADR-0052) aims for this when it demonstrates a strike. The plate is only
        /// ever yawed (ADR-0040), so the renderer's axis-aligned bounds give the top face exactly.
        /// </summary>
        public Vector3 TopCenterWorld
        {
            get
            {
                if (_renderer == null)
                {
                    return transform.position;
                }

                Bounds bounds = _renderer.bounds;
                return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            }
        }

        private void Awake()
        {
            _left = Resolve<IHand>(_leftHand, nameof(_leftHand));

            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<Renderer>();
            }

            _volume = BuildVolume();

            if (_contactJoints == null || _contactJoints.Length == 0)
            {
                _contactJoints = new[] { HandJointId.HandIndexTip };
            }

            if (_speedJoints == null || _speedJoints.Length == 0)
            {
                _speedJoints = new[] { HandJointId.HandWristRoot };
            }

            _speedWindows = new DownwardSpeedWindow[_speedJoints.Length];
            for (int j = 0; j < _speedWindows.Length; j++)
            {
                _speedWindows[j] = new DownwardSpeedWindow(Mathf.Max(1, _speedSampleFrames));
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

            if (_left == null || !_left.IsTrackedDataValid)
            {
                for (int j = 0; j < _speedWindows.Length; j++)
                {
                    _speedWindows[j].Reset();
                }

                _cachedInside = false;
                _cachedEntrySpeed = 0f;
                return;
            }

            float dt = Time.deltaTime;

            // HOW HARD: the hand's downward speed, from the palm and wrist only. Fingertips are
            // left out on purpose — opening or closing the hand moves them fast while the hand
            // itself is still, and that must not read as a strike (ADR-0047 addendum).
            float handDownwardSpeed = 0f;
            for (int j = 0; j < _speedJoints.Length; j++)
            {
                if (!_left.GetJointPose(_speedJoints[j], out Pose speedPose))
                {
                    _speedWindows[j].Reset();
                    continue;
                }

                float speed = _speedWindows[j].Update(speedPose.position.y, dt);
                if (speed > handDownwardSpeed)
                {
                    handDownwardSpeed = speed;
                }
            }

            // WHERE: any contact joint in the volume. The leading edge varies with the pose.
            Quaternion inverse = Quaternion.Inverse(transform.rotation);
            bool anyInside = false;
            for (int j = 0; j < _contactJoints.Length && !anyInside; j++)
            {
                if (!_left.GetJointPose(_contactJoints[j], out Pose pose))
                {
                    continue;
                }

                Vector3 local = inverse * (pose.position - transform.position);
                anyInside = _volume.Contains(local.x, local.y, local.z);
            }

            _cachedInside = anyInside;
            _cachedEntrySpeed = handDownwardSpeed;
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

        /// <summary>
        /// The hit volume, in this object's frame (local Z up once <see cref="ChordStrikeTargetAnchor"/>
        /// has laid the plate flat), built around the visual's top surface (ADR-0048). The visual's
        /// bounds are taken relative to this transform, so the result does not depend on where the
        /// plate currently is in the world — only on how the visual sits within it.
        /// </summary>
        private StrikePlateVolume BuildVolume()
        {
            const float FallbackHalfExtentMetres = 0.10f; // a 20 cm plate, if there is no visual to read

            float minX = -FallbackHalfExtentMetres, maxX = FallbackHalfExtentMetres;
            float minY = -FallbackHalfExtentMetres, maxY = FallbackHalfExtentMetres;
            float surfaceZ = 0f;

            if (_renderer != null)
            {
                Matrix4x4 toPlate = transform.worldToLocalMatrix * _renderer.transform.localToWorldMatrix;
                Bounds bounds = _renderer.localBounds;

                var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 sign = new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f);
                    Vector3 point = toPlate.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, sign));
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }

                minX = min.x;
                maxX = max.x;
                minY = min.y;
                maxY = max.y;
                surfaceZ = max.z; // the visual's top face: the surface a hand lands on
            }

            return StrikePlateVolume.AroundSurface(
                minX, maxX, minY, maxY, surfaceZ,
                _footprintMarginMetres, _hoverAboveSurfaceMetres, _depthBelowSurfaceMetres);
        }

        private void OnDrawGizmosSelected()
        {
            StrikePlateVolume volume = BuildVolume();
            Gizmos.color = new Color(0.55f, 0.80f, 1f, 0.5f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(
                new Vector3((volume.MinX + volume.MaxX) * 0.5f, (volume.MinY + volume.MaxY) * 0.5f, (volume.MinZ + volume.MaxZ) * 0.5f),
                new Vector3(volume.MaxX - volume.MinX, volume.MaxY - volume.MinY, volume.MaxZ - volume.MinZ));
        }
    }
}
