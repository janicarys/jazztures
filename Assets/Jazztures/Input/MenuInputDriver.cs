using Jazztures.Core.Menu;
using Jazztures.Core.Ports;
using UnityEngine;

namespace Jazztures.Input
{
    /// <summary>
    /// Feeds the menu recogniser once per frame (ADR-0051): reads the right hand from an
    /// <see cref="IMenuHandSource"/> and hands the frame to the <see cref="MenuGestureRecognizer"/>
    /// the composition layer built. A thin adapter — it owns no thresholds and decides nothing.
    ///
    /// <para>
    /// At a desk the keyboard (<see cref="KeyboardMenuHandSource"/>) stands in: it wins on any frame a
    /// menu key is down, and carries the whole session when the hand is not tracked at all, so
    /// the menu works in the Editor without a headset. A headset build has no keyboard and reads
    /// only the hand.
    /// </para>
    /// </summary>
    public sealed class MenuInputDriver : MonoBehaviour
    {
        [Tooltip("A MetaXRMenuHandSource. Optional — leave empty to use the desk keyboard alone " +
                 "(arrows = flick, Down/Enter = strike, hold Backspace = fist).")]
        [SerializeField] private MonoBehaviour _handSource;

        [Tooltip("Editor / Link only: let the keyboard drive the menu.")]
        [SerializeField] private bool _deskKeys = true;

        private readonly KeyboardMenuHandSource _keys = new KeyboardMenuHandSource();

        private IMenuHandSource _hand;
        private MenuGestureRecognizer _recognizer;
        private bool _handSeen; // the headset has reported a hand at least once this session

        /// <summary>
        /// The frame fed to the recogniser on the most recent <c>Update</c> — what the tutorial reads to see
        /// whether the learner's hands are in view, open, thumb up.
        /// </summary>
        public MenuHandFrame LastFrame { get; private set; } = MenuHandFrame.Untracked;

        /// <summary>Hand the recogniser in. Call once, from the composition layer, before the first <c>Update</c>.</summary>
        public void Bind(MenuGestureRecognizer recognizer) => _recognizer = recognizer;

        private void Awake()
        {
            _hand = _handSource as IMenuHandSource;
            if (_handSource != null && _hand == null)
            {
                Debug.LogWarning(
                    $"{nameof(MenuInputDriver)}: '{_handSource.GetType().Name}' is not an {nameof(IMenuHandSource)}; " +
                    "the menu will use the keyboard only.", this);
            }
        }

        private void Update()
        {
            if (_recognizer == null)
            {
                return;
            }

            MenuHandFrame frame = _hand != null ? _hand.CurrentFrame : MenuHandFrame.Untracked;

            if (frame.Tracking != TrackingQuality.NotTracked || frame.LeftTracking != TrackingQuality.NotTracked)
            {
                _handSeen = true;
            }

            // The keyboard is a desk tool: in the Editor only, and it stands in for the hands only until real
            // hands have appeared. Once they have, a hand that is NotTracked is the learner moving it away -
            // the tutorial asks for exactly that - and must not be papered over with a pretend one.
            if (Application.isEditor
                && _deskKeys
                && _keys.TryRead(out MenuHandFrame keyFrame, out bool anyKey)
                && (anyKey || !_handSeen))
            {
                frame = keyFrame;
            }

            LastFrame = frame;
            _recognizer.Feed(frame);
        }
    }
}
