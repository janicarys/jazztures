using Jazztures.Config;
using Jazztures.Input;
using Jazztures.Presentation;
using UnityEngine;

namespace Jazztures.App
{
    /// <summary>
    /// Glue between the palm-swing input and its central-view visuals (ADR-0078). <c>Presentation</c> must not know
    /// <c>Input</c> and the reverse, so this component, in the assembly that knows both, hands the view the pipeline,
    /// each note, and where the lane is.
    ///
    /// <para>
    /// On a headset the lane is the one the hand reader set when the gate first opened (the palm's resting position). In
    /// the editor simulation, or before the hand has been seen, the trigger line stands in the right lane in front of the
    /// head, at <c>Lane Offset Right</c> and <c>Lane Distance</c> from <see cref="PalmSwingSettings"/>.
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
            if (_headsetSource != null && _headsetSource.LaneSet && !_palmSwing.UsesSimulation)
            {
                _view.SetLane(_headsetSource.LaneOrigin, _headsetSource.LaneRight);
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
