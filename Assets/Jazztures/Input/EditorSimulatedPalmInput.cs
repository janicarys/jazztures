#if UNITY_EDITOR
using Jazztures.Core.Melody;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Jazztures.Input
{
    /// <summary>
    /// EDITOR ONLY (ADR-0078): a keyboard-and-mouse stand-in for the right hand, so the whole palm-swing chain —
    /// pipeline, melody engine, sampler, sandbox logger — runs in Play mode without a headset. It implements the
    /// same <see cref="IPalmSwingSampleSource"/> the headset reader does.
    ///
    /// <para>
    /// Up / Down arrow: tilt the fingers up / down in 15° steps (two taps reach Up or Down at the default
    /// thresholds); N: level. Space: toggle the gate (hand open and palm left). A / D: move left / right
    /// (hold Shift for a fast swing); Z / C snap to the left / right of the line at once. W / S: hand height.
    /// Holding the right mouse button, mouse X / Y move the hand too. R: re-centre.
    /// </para>
    /// </summary>
    public sealed class EditorSimulatedPalmInput : MonoBehaviour, IPalmSwingSampleSource
    {
        [SerializeField] private float _tiltStepDegrees = 15f;
        [SerializeField] private float _maxTiltDegrees = 60f;
        [SerializeField] private float _swingMetresPerSecond = 0.8f;
        [SerializeField] private float _fastMultiplier = 3f;
        [SerializeField] private float _heightMetresPerSecond = 0.25f;
        [SerializeField] private float _mouseMetresPerPixel = 0.002f;
        [SerializeField] private float _snapMetres = 0.1f;
        [SerializeField] private float _maxLateralMetres = 0.25f;
        [SerializeField] private float _maxHeightMetres = 0.3f;
        [SerializeField] private bool _showOverlay = true;

        private float _tilt;
        private float _lateral = -0.08f;
        private float _height;
        private bool _gate = true;

        public void Recenter()
        {
            _lateral = 0f;
            _height = 0f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                _tilt = Mathf.Min(_maxTiltDegrees, _tilt + _tiltStepDegrees);
            }

            if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                _tilt = Mathf.Max(-_maxTiltDegrees, _tilt - _tiltStepDegrees);
            }

            if (keyboard.nKey.wasPressedThisFrame)
            {
                _tilt = 0f;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                _gate = !_gate;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                Recenter();
            }

            float dt = Time.unscaledDeltaTime;
            float speed = _swingMetresPerSecond * (keyboard.leftShiftKey.isPressed ? _fastMultiplier : 1f);
            if (keyboard.dKey.isPressed)
            {
                _lateral += speed * dt;
            }

            if (keyboard.aKey.isPressed)
            {
                _lateral -= speed * dt;
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                _lateral = _snapMetres;
            }

            if (keyboard.zKey.wasPressedThisFrame)
            {
                _lateral = -_snapMetres;
            }

            if (keyboard.wKey.isPressed)
            {
                _height += _heightMetresPerSecond * dt;
            }

            if (keyboard.sKey.isPressed)
            {
                _height -= _heightMetresPerSecond * dt;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _lateral += delta.x * _mouseMetresPerPixel;
                _height += delta.y * _mouseMetresPerPixel;
            }

            _lateral = Mathf.Clamp(_lateral, -_maxLateralMetres, _maxLateralMetres);
            _height = Mathf.Clamp(_height, -_maxHeightMetres, _maxHeightMetres);
        }

        public PalmSwingSample Read() => new PalmSwingSample(0.0, _gate, _tilt, _lateral, _height);

        private void OnGUI()
        {
            if (!_showOverlay)
            {
                return;
            }

            GUI.Label(
                new Rect(10, 10, 520, 60),
                $"Simulated palm: tilt {_tilt:0}°  x {_lateral * 100f:0} cm  height {_height * 100f:0} cm  gate {(_gate ? "OPEN" : "closed")}\n" +
                "↑/↓ tilt · N level · A/D (Shift) swing · Z/C snap · W/S height · Space gate · R recentre");
        }
    }
}
#endif
