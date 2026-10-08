using UnityEngine.InputSystem;

namespace Jazztures.Input
{
    /// <summary>
    /// Desk keys for the free-play metronome (ADR-0043): <c>M</c> switches it on or off,
    /// <c>-</c> / <c>=</c> change the tempo (by <c>MetronomeSettings.TempoStepBpm</c>),
    /// <c>B</c> cycles the subdivision. Editor / Link only — a Quest build has no keyboard,
    /// so every poll reports nothing there. Call <see cref="Poll"/> once per frame.
    /// </summary>
    public sealed class KeyboardMetronomeInput
    {
        /// <summary>
        /// Read this frame's key presses. <paramref name="tempoSteps"/> is −1, 0 or +1
        /// (slower / unchanged / faster). Returns true when any key was pressed.
        /// </summary>
        public bool Poll(out bool toggle, out int tempoSteps, out bool cycleSubdivision)
        {
            toggle = false;
            tempoSteps = 0;
            cycleSubdivision = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            toggle = keyboard[Key.M].wasPressedThisFrame;
            cycleSubdivision = keyboard[Key.B].wasPressedThisFrame;

            if (keyboard[Key.Minus].wasPressedThisFrame || keyboard[Key.NumpadMinus].wasPressedThisFrame)
            {
                tempoSteps--;
            }

            if (keyboard[Key.Equals].wasPressedThisFrame || keyboard[Key.NumpadPlus].wasPressedThisFrame)
            {
                tempoSteps++;
            }

            return toggle || cycleSubdivision || tempoSteps != 0;
        }
    }
}
