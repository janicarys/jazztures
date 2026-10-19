using Jazztures.Core.Ports;
using UnityEngine.InputSystem;

namespace Jazztures.Input
{
    /// <summary>
    /// Desktop keyboard stand-in for the right hand in the menu (ADR-0051), the counterpart of
    /// <see cref="KeyboardHandPoseSource"/>. Left / Right arrow = a flick that way, Down arrow or
    /// Enter = a strike, hold Backspace = a fist. For the tutorial (ADR-0053): hold Tab = both hands out
    /// of view, hold T = the right hand open with the thumb up; otherwise both hands are in view with the
    /// right one open and the thumb forward. Each key reports a synthetic speed comfortably
    /// over the default thresholds, so the whole recogniser — settling, spacing, the fist hold —
    /// is exercised at the desk, not only on the headset. Debug-only; not a study parameter.
    /// </summary>
    public sealed class KeyboardMenuHandSource
    {
        /// <summary>Synthetic sideways speed while an arrow key is held.</summary>
        public const float FlickSpeedMetresPerSecond = 1.2f;

        /// <summary>Synthetic downward speed while Down / Enter is held.</summary>
        public const float StrikeSpeedMetresPerSecond = 0.9f;

        /// <summary>
        /// This frame's keyboard hand. False when there is no keyboard (a headset build);
        /// <paramref name="anyKey"/> says whether one of the menu keys is down. The frame always
        /// reports High tracking — a keyboard does not occlude.
        /// </summary>
        public bool TryRead(out MenuHandFrame frame, out bool anyKey)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                frame = MenuHandFrame.Untracked;
                anyKey = false;
                return false;
            }

            bool left = keyboard.leftArrowKey.isPressed;
            bool right = keyboard.rightArrowKey.isPressed;
            bool down = keyboard.downArrowKey.isPressed || keyboard.enterKey.isPressed;
            bool fist = keyboard.backspaceKey.isPressed;
            bool away = keyboard.tabKey.isPressed;
            bool thumbUp = keyboard.tKey.isPressed;

            anyKey = left || right || down || fist || away || thumbUp;
            TrackingQuality tracking = away ? TrackingQuality.NotTracked : TrackingQuality.High;
            frame = new MenuHandFrame(
                tracking,
                left ? FlickSpeedMetresPerSecond : 0f,
                right ? FlickSpeedMetresPerSecond : 0f,
                down ? StrikeSpeedMetresPerSecond : 0f,
                fist ? 1f : 0f,
                tracking,
                maxFingerClosure: fist ? 1f : 0f,
                thumbUpAngleDegrees: thumbUp ? 0f : 90f,
                viewAngleDegrees: away ? 180f : 20f,
                leftViewAngleDegrees: away ? 180f : 20f);
            return true;
        }
    }
}
