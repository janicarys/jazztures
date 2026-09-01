using Jazztures.Core.Ports;
using UnityEngine.InputSystem;

namespace Jazztures.Input
{
    /// <summary>
    /// Desktop keyboard stand-in for the left hand (M2/M3 development, CLAUDE.md §5).
    /// Z / X / C press = the ii / V / I pose; nothing pressed = no pose. Tracking is
    /// always reported High. Real gesture input is <see cref="MetaXRHandPoseSource"/>.
    ///
    /// <para>
    /// The candidate still flows through the full <c>GestureInterpreter</c> — hold time,
    /// confirming frames, debounce — so the temporal path is exercised on the desktop.
    /// </para>
    /// </summary>
    public sealed class KeyboardHandPoseSource : IHandPoseSource
    {
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

                return new HandPoseFrame(candidate, TrackingQuality.High, TrackingQuality.High);
            }
        }
    }
}
