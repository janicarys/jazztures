using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using UnityEngine.InputSystem;

namespace Jazztures.Input
{
    /// <summary>
    /// Desktop keyboard stand-in for the right hand until mid-air touch targets land in
    /// M4. A S D F G strike the lower-octave chord tones, H J K L ; the upper octave.
    /// Call <see cref="Poll"/> once per frame.
    /// </summary>
    public sealed class KeyboardMelodyInput
    {
        /// <summary>Synthetic entry speed for a keyed note. `[TUNABLE]` (debug only).</summary>
        public const float KeyedEntrySpeed = 0.8f;

        private static readonly Key[] TargetKeys =
        {
            Key.A, Key.S, Key.D, Key.F, Key.G,
            Key.H, Key.J, Key.K, Key.L, Key.Semicolon,
        };

        public void Poll(MelodyEngine melody)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            for (int i = 0; i < TargetKeys.Length && i < ChordToneSet.TargetCount; i++)
            {
                if (keyboard[TargetKeys[i]].wasPressedThisFrame)
                {
                    melody.TriggerTarget(i, KeyedEntrySpeed);
                }
            }
        }
    }
}
