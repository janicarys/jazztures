using Jazztures.Core.Ports;
using UnityEngine.InputSystem;

namespace Jazztures.Input
{
    /// <summary>
    /// Desktop keyboard stand-in for the left hand (M2/M3 development, CLAUDE.md §5).
    /// Z / X / C press = the ii / V / I pose; nothing pressed = no pose. Holding Space
    /// reports a synthetic downward speed, standing in for a chord strike (ADR-0025).
    /// Tracking is always reported High. Real gesture input is
    /// <see cref="MetaXRHandPoseSource"/>.
    ///
    /// <para>
    /// The candidate still flows through the full <c>GestureInterpreter</c> — hold time,
    /// confirming frames, debounce — and the synthetic speed through the full
    /// <c>ChordStrikeDetector</c>, so the whole selection+articulation path is exercised on
    /// the desktop, not only on device.
    /// </para>
    /// </summary>
    public sealed class KeyboardHandPoseSource : IHandPoseSource
    {
        /// <summary>
        /// Synthetic downward speed reported while Space is held — comfortably over the
        /// default strike enter threshold. Debug-only; not a study parameter.
        /// </summary>
        public const float StrikeSpeedMetresPerSecond = 0.9f;

        public HandPoseFrame CurrentFrame
        {
            get
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null)
                {
                    return HandPoseFrame.Untracked;
                }

                HandPoseCandidate candidate = HandPoseCandidate.None;
                if (keyboard.zKey.isPressed)
                {
                    candidate = HandPoseCandidate.Ii;
                }
                else if (keyboard.xKey.isPressed)
                {
                    candidate = HandPoseCandidate.V;
                }
                else if (keyboard.cKey.isPressed)
                {
                    candidate = HandPoseCandidate.I;
                }

                float verticalSpeed = keyboard.spaceKey.isPressed ? -StrikeSpeedMetresPerSecond : 0f;

                return new HandPoseFrame(candidate, TrackingQuality.High, TrackingQuality.High, verticalSpeed);
            }
        }
    }
}
