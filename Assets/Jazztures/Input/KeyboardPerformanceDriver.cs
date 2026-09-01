using System;
using Jazztures.Core.Harmony;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using UnityEngine.InputSystem;

namespace Jazztures.Input
{
    /// <summary>
    /// A desktop keyboard stand-in for hand tracking, for milestone M2 (CLAUDE.md §5:
    /// "Drive it from a keyboard-input debug adapter, not from hand tracking"). Real
    /// gesture input arrives in M3.
    ///
    /// <list type="bullet">
    ///   <item>Z / X / C — hold ii / V / I (left hand). Release the key to release the chord.</item>
    ///   <item>A S D F G — lower-octave root/3rd/5th/7th/9th (right hand).</item>
    ///   <item>H J K L ; — upper-octave root/3rd/5th/7th/9th.</item>
    /// </list>
    ///
    /// Call <see cref="Poll"/> once per frame.
    /// </summary>
    public sealed class KeyboardPerformanceDriver
    {
        /// <summary>Fixed synthetic entry speed for a keyed melody note. `[TUNABLE]` (debug only).</summary>
        public const float KeyedEntrySpeed = 0.8f;

        private static readonly Key[] ChordKeys = { Key.Z, Key.X, Key.C };
        private static readonly ChordFunction[] ChordFunctions =
        {
            ChordFunction.Two, ChordFunction.Five, ChordFunction.One,
        };

        private static readonly Key[] TargetKeys =
        {
            Key.A, Key.S, Key.D, Key.F, Key.G,
            Key.H, Key.J, Key.K, Key.L, Key.Semicolon,
        };

        private readonly HarmonyEngine _harmony;
        private readonly MelodyEngine _melody;

        public KeyboardPerformanceDriver(HarmonyEngine harmony, MelodyEngine melody)
        {
            _harmony = harmony ?? throw new ArgumentNullException(nameof(harmony));
            _melody = melody ?? throw new ArgumentNullException(nameof(melody));
        }

        public void Poll()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            PollChord(keyboard);
            PollMelody(keyboard);
        }

        private void PollChord(Keyboard keyboard)
        {
            ChordFunction? held = null;
            for (int i = 0; i < ChordKeys.Length; i++)
            {
                if (keyboard[ChordKeys[i]].isPressed)
                {
                    held = ChordFunctions[i];
                }
            }

            _harmony.SetHeldFunction(held);
        }

        private void PollMelody(Keyboard keyboard)
        {
            for (int i = 0; i < TargetKeys.Length && i < ChordToneSet.TargetCount; i++)
            {
                if (keyboard[TargetKeys[i]].wasPressedThisFrame)
                {
                    _melody.TriggerTarget(i, KeyedEntrySpeed);
                }
            }
        }
    }
}
