using Jazztures.Config;
using Jazztures.Core.Melody;
using Jazztures.Diagnostics;
using Oculus.Interaction.Input;
using Oculus.Interaction.PoseDetection;
using UnityEngine;

namespace Jazztures.Input
{
    /// <summary>
    /// EXPLORATION (ADR-0050, branch <c>explore/finger-bend-keys</c>): the right hand plays by
    /// bending one finger at a time, like pressing a key — thumb = root, index = 3rd, middle = 5th,
    /// ring = 7th, pinky = 9th — instead of touching mid-air targets. A drop-in alternative to
    /// <c>TouchTargetBinder</c>: it is handed the same <see cref="MelodyEngine"/>, so the chord
    /// tones are still the left hand's chord's, the velocity curve and retrigger cooldown still
    /// apply, and the lesson flow can switch to it as its melody input.
    ///
    /// <para>
    /// This reverses ADR-0005 / ADR-0018 (the co-design sessions rejected individual finger bends).
    /// It is a branch experiment to find out whether a <i>modest</i> bend works where the earlier,
    /// deeper curl did not; it is not a thesis decision and must not be merged as one.
    /// </para>
    ///
    /// <para>
    /// <b>What is measured.</b> The Interaction SDK's own <c>FingerShapes</c> features (CLAUDE.md
    /// §3.4 — no hand-rolled joint math), blended the way the SDK's own grab detection does it:
    /// <c>(2 × curl + flexion) / 3</c> for the four fingers, curl alone for the thumb, normalised with
    /// <c>PalmGrabAPI.CURL_RANGE</c>. Curl is the bend of the finger's <i>middle</i> joints; flexion
    /// is the bend at the <i>knuckle</i>. A key press is mostly a knuckle movement, with the finger
    /// staying fairly straight, so curl alone barely moved for it — the first version sounded only when
    /// the fingertips were curled, which is why presses seemed to work "sometimes". The decision of what
    /// counts as a press is the pure, tested <see cref="FingerBendDetector"/>. Loudness is the finger's
    /// tip speed through the bend, the same quantity the touch targets already feed the melody engine.
    /// </para>
    /// </summary>
    public sealed class FingerBendBinder : MonoBehaviour
    {
        [Tooltip("The right Interaction SDK Hand component.")]
        [SerializeField] private MonoBehaviour _rightHand;

        [Tooltip("Tuning for the bend levels and the speed window (ADR-0050).")]
        [SerializeField] private MelodyConfig _config;

        [Tooltip("Hold off judging presses on any frame where the SDK reports ANY of the five fingers as " +
                 "low-confidence. Off by default: confidence tends to drop on the very finger being " +
                 "bent (it folds toward the palm and hides itself), and one flickering finger, the " +
                 "thumb especially, then silences all five. Turn it on to compare. With JAZZTURES_DIAG set " +
                 "the log shows how often each finger is low-confidence.")]
        [SerializeField] private bool _requireHighConfidence;

        // Thumb, index, middle, ring, pinky — also the melody slot each finger plays (slots 0..4 are
        // the lower octave: root, 3rd, 5th, 7th, 9th). One octave only for now.
        // TODO(OPEN): the upper octave (slots 5..9) has no finger; see ADR-0050.
        private static readonly HandFinger[] Fingers =
        {
            HandFinger.Thumb, HandFinger.Index, HandFinger.Middle, HandFinger.Ring, HandFinger.Pinky,
        };

        private static readonly HandJointId[] Tips =
        {
            HandJointId.HandThumbTip, HandJointId.HandIndexTip, HandJointId.HandMiddleTip,
            HandJointId.HandRingTip, HandJointId.HandPinkyTip,
        };

        // The SDK's own PalmGrabAPI.CURL_RANGE, copied verbatim — flat and fully bent, in the
        // degrees FingerShapes reports. Not new tunables.
        private static readonly float[] BendFlat = { 190f, 180f, 180f, 180f, 180f };
        private static readonly float[] BendFull = { 220f, 250f, 250f, 250f, 245f };

        // A gap this long between updates (tracking loss) and the remembered finger positions are
        // stale: start over rather than judge a press against them.
        private const double MaxGapSeconds = 0.25;
        private const int DefaultSpeedSampleFrames = 3;
        private const double SummarySeconds = 3.0;

        private readonly FingerShapes _shapes = new FingerShapes();
        private readonly float[] _bends = new float[FingerBendDetector.FingerCount];
        private readonly Vector3[] _previousTips = new Vector3[FingerBendDetector.FingerCount];
        private readonly bool[] _hasPreviousTip = new bool[FingerBendDetector.FingerCount];
        private readonly bool[] _wasBlocked = new bool[FingerBendDetector.FingerCount];
        private readonly int[] _lowConfidenceFrames = new int[FingerBendDetector.FingerCount];

        private MelodyEngine _melody;
        private IHand _hand;
        private FingerBendDetector _detector;
        private float[,] _speedSamples;
        private int _speedSampleFrames = DefaultSpeedSampleFrames;
        private int _speedCursor;
        private double _lastUpdateTime = double.NegativeInfinity;
        private int _summaryFrames;
        private double _nextSummaryTime;
        private float _thumbPalmarMetres;      // diagnostics: the thumb tip's offset toward the palm side

        /// <summary>Wire the domain up. Call once, from the composition root's <c>Awake</c>.</summary>
        public void Bind(MelodyEngine melody) => _melody = melody;

        private void Awake()
        {
            _hand = _rightHand as IHand;
            if (_hand == null)
            {
                Debug.LogError(
                    $"{nameof(FingerBendBinder)}: '{(_rightHand == null ? "<none>" : _rightHand.GetType().Name)}' " +
                    $"is not an {nameof(IHand)}.", this);
                enabled = false;
                return;
            }

            FingerBendThresholds thresholds = _config != null
                ? _config.ToFingerBendThresholds()
                : FingerBendThresholds.Default;
            _detector = new FingerBendDetector(thresholds);

            _speedSampleFrames = _config != null ? Mathf.Max(1, _config.SpeedSampleFrames) : DefaultSpeedSampleFrames;
            _speedSamples = new float[FingerBendDetector.FingerCount, _speedSampleFrames];
        }

        private void OnDisable()
        {
            // The lesson flow turns this off between exercises: come back with fresh finger positions.
            ForgetHand();
        }

        private void LateUpdate()
        {
            // A failed joint read makes the SDK's features report 0 — "flat" — which would look like
            // every finger snapping open and then bending back. Never feed those frames in.
            if (_hand == null || !_hand.IsTrackedDataValid || !_hand.GetJointPosesFromWrist(out _))
            {
                ForgetHand();
                return;
            }

            double now = Time.unscaledTimeAsDouble;
            float dt = Time.deltaTime;
            _speedCursor = (_speedCursor + 1) % _speedSampleFrames;
            SampleTipSpeeds(dt);

            bool allConfident = CountConfidence(now);
            if (_requireHighConfidence && !allConfident)
            {
                return;
            }

            if (now - _lastUpdateTime > MaxGapSeconds)
            {
                _detector.Reset();
            }

            double elapsed = double.IsNegativeInfinity(_lastUpdateTime) ? dt : now - _lastUpdateTime;
            _lastUpdateTime = now;

            for (int f = 0; f < Fingers.Length; f++)
            {
                _bends[f] = ReadBend(f);
            }

            _detector.Update(_bends, elapsed);

            for (int f = 0; f < Fingers.Length; f++)
            {
                bool blocked = _detector.Blocked(f);
                if (blocked && !_wasBlocked[f])
                {
                    int other = _detector.BlockedBy(f);
                    string who = other >= 0 ? Fingers[other].ToString() : "another finger";
                    DiagLog.Log(
                        _detector.WasDragged(f)
                            ? $"[FingerBend] {Fingers[f]} rose {_detector.LastDelta(f):0.00} but {who} was still moving - dragged along, ignored (ADR-0068)"
                            : $"[FingerBend] {Fingers[f]} bent {_detector.LastDelta(f):0.00} but {who} moved nearly as much — ignored");
                }

                _wasBlocked[f] = blocked;

                // Why a note did NOT sound, and how far each one that did was curled: together they show how far this hand
                // actually curls each finger, which the press levels (ADR-0073, ADR-0074) were chosen without knowing.
                if (_detector.NearMiss(f))
                {
                    int driver = _detector.NearMissDrivenBy(f);
                    DiagLog.Log(
                        $"[FingerBend] {Fingers[f]} NEAR MISS rose {_detector.NearMissPeak(f):0.00}, needs {_detector.PressLevel(f):0.00} - " +
                        (driver >= 0
                            ? $"{Fingers[driver]} was pressed and moving, so probably dragged along"
                            : "on its own: a curl too small to sound"));
                }

                if (_detector.Released(f))
                {
                    DiagLog.Log($"[FingerBend] {Fingers[f]} STROKE peak {_detector.ReleasePeak(f):0.00} (needs {_detector.PressLevel(f):0.00})");
                }

                if (!_detector.JustPressed(f))
                {
                    continue;
                }

                // A deliberate bend is the intent here, so it is not also held to the melody
                // engine's entry gate (which exists to stop a resting hand firing notes): a slow
                // press sounds at the quietest allowed velocity rather than being dropped.
                float speed = Mathf.Max(PeakSpeed(f), MelodyEngine.EntryVelocityGateMetresPerSecond);
                bool sounded = _melody != null && _melody.TriggerTarget(f, speed);
                DiagLog.Log(
                    $"[FingerBend] {Fingers[f]} · rise {_detector.LastDelta(f):0.00} · {speed:0.00} m/s · " +
                    (f == 0 ? $"thumb tip {_thumbPalmarMetres * 1000f:0} mm from the back-of-hand plane (palm side is more) · " : string.Empty) +
                    $"{(sounded ? "note" : _melody == null ? "no melody engine" : "gated (no chord held, or retrigger cooldown)")}");
            }
        }

        /// <summary>
        /// 0 (flat) .. 1 (fully bent) for one finger: the SDK's curl, blended with knuckle flexion for
        /// the four fingers exactly as its own grab detection does.
        /// </summary>
        private float ReadBend(int f)
        {
            float degrees = _shapes.GetCurlValue(Fingers[f], _hand);
            if (f != 0)
            {
                degrees = (degrees * 2f + _shapes.GetFlexionValue(Fingers[f], _hand)) / 3f;
            }

            float span = BendFull[f] - BendFlat[f];
            if (f == 0 && _config != null)
            {
                span = _config.BendThumbCurlSpanDegrees; // wider than the SDK's 30 degrees: see the config tooltip
            }

            float bend = Mathf.Clamp01((degrees - BendFlat[f]) / span);
            return f == 0 ? bend + ThumbPressBend() : bend;
        }

        /// <summary>
        /// The thumb's second way of bending. Curl only sees the thumb tuck in; pressing it DOWN, as
        /// one does instinctively for a key, swings the whole thumb toward the palm side of the hand
        /// and leaves its middle joints nearly straight. So the thumb's bend is the sum of the two:
        /// either motion raises it, and the detector — which only looks at change — treats the
        /// result like any other finger's. Measured as the tip's offset toward the palm in the
        /// wrist's own frame (the SDK's palmar axis), so it does not matter which way the hand faces.
        /// The absolute offset means nothing; only how much it changes does.
        /// </summary>
        private float ThumbPressBend()
        {
            float span = _config != null ? _config.BendThumbPressSpanMetres : 0f;
            if (Mathf.Abs(span) < 1e-4f || !_hand.GetJointPosesFromWrist(out ReadOnlyHandJointPoses poses))
            {
                _thumbPalmarMetres = 0f;
                return 0f;
            }

            Vector3 palmar = _hand.Handedness == Handedness.Left ? Constants.LeftPalmar : Constants.RightPalmar;
            _thumbPalmarMetres = Vector3.Dot(poses[(int)HandJointId.HandThumbTip].position, palmar);
            return _thumbPalmarMetres / span;
        }

        private void ForgetHand()
        {
            _detector?.Reset();
            _lastUpdateTime = double.NegativeInfinity;
            for (int f = 0; f < _hasPreviousTip.Length; f++)
            {
                _hasPreviousTip[f] = false;
                _wasBlocked[f] = false;
            }
        }

        /// <summary>
        /// Counts, per finger, the frames the SDK called low-confidence, and every few seconds says so
        /// in the diagnostic log. Returns whether all five are high-confidence this frame.
        /// </summary>
        private bool CountConfidence(double now)
        {
            bool all = true;
            _summaryFrames++;
            for (int f = 0; f < Fingers.Length; f++)
            {
                if (!_hand.GetFingerIsHighConfidence(Fingers[f]))
                {
                    _lowConfidenceFrames[f]++;
                    all = false;
                }
            }

            if (now >= _nextSummaryTime)
            {
                if (_nextSummaryTime > 0.0 && _summaryFrames > 0)
                {
                    DiagLog.Log(
                        "[FingerBend] low-confidence frames, last few seconds — " +
                        $"thumb {Percent(0)} · index {Percent(1)} · middle {Percent(2)} · ring {Percent(3)} · pinky {Percent(4)}" +
                        (_requireHighConfidence ? " (judging is held off on those frames)" : " (still judged)"));
                }

                _nextSummaryTime = now + SummarySeconds;
                _summaryFrames = 0;
                System.Array.Clear(_lowConfidenceFrames, 0, _lowConfidenceFrames.Length);
            }

            return all;
        }

        private string Percent(int finger) =>
            _summaryFrames > 0 ? $"{100 * _lowConfidenceFrames[finger] / _summaryFrames}%" : "–";

        private void SampleTipSpeeds(float dt)
        {
            for (int f = 0; f < Tips.Length; f++)
            {
                if (!_hand.GetJointPose(Tips[f], out Pose pose))
                {
                    _hasPreviousTip[f] = false;
                    _speedSamples[f, _speedCursor] = 0f;
                    continue;
                }

                float instant = _hasPreviousTip[f] && dt > 0f
                    ? Vector3.Distance(pose.position, _previousTips[f]) / dt
                    : 0f;
                _previousTips[f] = pose.position;
                _hasPreviousTip[f] = true;
                _speedSamples[f, _speedCursor] = instant;
            }
        }

        private float PeakSpeed(int finger)
        {
            float peak = 0f;
            for (int s = 0; s < _speedSampleFrames; s++)
            {
                if (_speedSamples[finger, s] > peak)
                {
                    peak = _speedSamples[finger, s];
                }
            }

            return peak;
        }
    }
}
