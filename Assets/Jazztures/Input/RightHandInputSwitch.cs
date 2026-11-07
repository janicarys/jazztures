using Jazztures.Core.Melody;
using UnityEngine;

namespace Jazztures.Input
{
    /// <summary>
    /// Chooses which right-hand input plays the melody (ADR-0078): the finger-bend input (ADR-0050, unchanged)
    /// or the palm swing. Assign this as <c>LessonFlowController</c>'s melody input: the lesson flow enables
    /// and disables it as it does any melody input, and this turns the chosen one on and off with it. Changing
    /// <see cref="Mode"/> in the inspector or at runtime needs no code change.
    /// </summary>
    public sealed class RightHandInputSwitch : MonoBehaviour
    {
        [SerializeField] private RightHandInputMode _mode = RightHandInputMode.FingerBend;

        [SerializeField] private FingerBendBinder _fingerBend;

        [SerializeField] private PalmSwingInputSource _palmSwing;

        public RightHandInputMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value)
                {
                    return;
                }

                _mode = value;
                Apply();
            }
        }

        private void OnEnable() => Apply();

        private void OnDisable()
        {
            if (_fingerBend != null)
            {
                _fingerBend.enabled = false;
            }

            if (_palmSwing != null)
            {
                _palmSwing.enabled = false;
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_fingerBend != null)
            {
                _fingerBend.enabled = _mode == RightHandInputMode.FingerBend;
            }

            if (_palmSwing != null)
            {
                _palmSwing.enabled = _mode == RightHandInputMode.PalmSwing;
            }
        }
    }
}
