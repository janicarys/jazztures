using Jazztures.Core.Gesture;
using Jazztures.Core.Harmony;
using Jazztures.Core.Ports;
using Jazztures.Events;
using UnityEngine;

namespace Jazztures.App
{
    /// <summary>
    /// The one place the domain's plain C# events are forwarded onto ScriptableObject
    /// event channels for presentation (CLAUDE.md §2.3). Enforces the direction rule:
    /// this raises, presentation only registers.
    ///
    /// <para>
    /// <see cref="NoteTriggeredChannel"/> is not handled here — it is fed by a
    /// <c>ChannelNoteSink</c> inside the composite sink so it sees the exact note stream.
    /// </para>
    /// </summary>
    public sealed class DomainEventBridge : MonoBehaviour
    {
        [SerializeField] private ChordChangedChannel _chordChanged;
        [SerializeField] private GestureStateChannel _gestureState;
        [SerializeField] private TrackingQualityChannel _trackingQuality;

        private IHandPoseSource _poseSource;
        private GesturePhase _phase = GesturePhase.Suppressed;
        private TrackingQuality _left = TrackingQuality.NotTracked;
        private TrackingQuality _right = TrackingQuality.NotTracked;
        private bool _bound;

        /// <summary>Wire the domain up. Call once, from the composition root's <c>Awake</c>.</summary>
        public void Bind(HarmonyEngine harmony, GestureInterpreter interpreter, IHandPoseSource poseSource)
        {
            _poseSource = poseSource;
            _bound = true;

            if (_chordChanged != null && harmony != null)
            {
                harmony.ChordChanged += change => _chordChanged.Raise(change);
            }

            if (interpreter != null)
            {
                interpreter.PhaseChanged += phase =>
                {
                    _phase = phase;
                    RaiseGestureState();
                };
            }
        }

        private void Update()
        {
            if (!_bound || _poseSource == null)
            {
                return;
            }

            HandPoseFrame frame = _poseSource.CurrentFrame;

            if (frame.LeftTracking != _left)
            {
                _left = frame.LeftTracking;
                if (_trackingQuality != null)
                {
                    _trackingQuality.Raise(new HandTrackingState(Handedness.Left, _left));
                }

                RaiseGestureState();
            }

            if (frame.RightTracking != _right)
            {
                _right = frame.RightTracking;
                if (_trackingQuality != null)
                {
                    _trackingQuality.Raise(new HandTrackingState(Handedness.Right, _right));
                }
            }
        }

        private void RaiseGestureState()
        {
            if (_gestureState != null)
            {
                _gestureState.Raise(new GestureState(Handedness.Left, _phase, _left));
            }
        }
    }
}
