using Jazztures.Config;
using Jazztures.Core.Lessons;
using Jazztures.Events;
using Jazztures.Input;
using Jazztures.Presentation;
using UnityEngine;

namespace Jazztures.App
{
    /// <summary>
    /// Glue between the swing-slice input and its central-view visuals (ADR-0080, built on ADR-0078). <c>Presentation</c> must not know
    /// <c>Input</c> and the reverse, so this component, in the assembly that knows both, hands the view the pipeline,
    /// each note, and where the lane is.
    ///
    /// <para>
    /// The trigger line stands where the hand reader set the lane when the pose had settled, and the cursor follows where
    /// the hand is pointing. Until the hand
    /// has been seen, the trigger line stands in the right lane in front of the head, at <c>Lane Offset Right</c> and
    /// <c>Lane Distance</c> from <see cref="PalmSwingSettings"/>, so the "open palm, facing left" hint has somewhere to be.
    /// </para>
    /// </summary>
    public sealed class PalmSwingViewBinder : MonoBehaviour
    {
        [SerializeField] private PalmSwingInputSource _palmSwing;

        [Tooltip("The headset hand reader, whose lane frame places the view on a device.")]
        [SerializeField] private MetaXRPalmSwingSampleSource _headsetSource;

        [SerializeField] private PalmSwingView _view;
        [SerializeField] private PalmSwingSettings _settings;
        [SerializeField] private Transform _head;

        [Tooltip("The lesson runner's ghost frames: the circles for the lesson's notes come from them (ADR-0081). Optional.")]
        [SerializeField] private GhostFrameChannel _ghostChannel;

        private void OnEnable() => _ghostChannel?.Register(OnGhost);

        private void OnDisable() => _ghostChannel?.Unregister(OnGhost);

        private void OnGhost(GhostFrame frame)
        {
            if (_view == null)
            {
                return;
            }

            if (frame.Visible && frame.NoteCues != null)
            {
                _view.SetCues(frame.NoteCues, frame.MelodyBeat);
            }
            else
            {
                _view.SetCues(null, 0.0);
            }
        }

        private void Start()
        {
            if (_head == null && Camera.main != null)
            {
                _head = Camera.main.transform;
            }

            if (_palmSwing == null || _view == null || _palmSwing.Pipeline == null)
            {
                Debug.LogWarning($"{nameof(PalmSwingViewBinder)}: the palm-swing input is not bound, so there is nothing to show.", this);
                enabled = false;
                return;
            }

            _view.Bind(_palmSwing.Pipeline);
            _view.SetDebugGuides(_settings != null && _settings.ShowRegionGuidesDebug);
            _palmSwing.NotePlayed += _view.OnNote;
        }

        private void OnDestroy()
        {
            if (_palmSwing != null && _view != null)
            {
                _palmSwing.NotePlayed -= _view.OnNote;
            }
        }

        private void LateUpdate()
        {
            // The lesson flow enables the right-hand input only while a lesson (or free play) that uses the right
            // hand is running, so the interface follows it: absent from the menu and the tutorial.
            bool active = _palmSwing.isActiveAndEnabled;
            _view.SetShown(active);
            if (!active)
            {
                return;
            }

            if (_headsetSource != null && _headsetSource.LaneSet)
            {
                _view.SetLane(_headsetSource.LaneOrigin, _headsetSource.LaneRight);
                if (_headsetSource.PointerValid)
                {
                    _view.SetPointer(_headsetSource.PointerWorld);
                }

                return;
            }

            if (_head == null || _settings == null)
            {
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f)
            {
                return;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 origin = _head.position + forward * _settings.LaneDistanceMetres
                + right * _settings.LaneOffsetRightMetres + Vector3.down * 0.3f;
            _view.SetLane(origin, right);
        }
    }
}
