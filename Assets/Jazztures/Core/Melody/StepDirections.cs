namespace Jazztures.Core.Melody
{
    /// <summary>Helpers for putting any right-hand input on the palm swing's up / same / down scale (ADR-0078).</summary>
    public static class StepDirections
    {
        /// <summary>
        /// The direction a note moved from the one before it. For scoring the finger-bend input in the same drill: each
        /// press counts as up, same or down by its pitch movement. The first note (no previous) is <see cref="StepDirection.Same"/>.
        /// </summary>
        public static StepDirection FromPitchMovement(int previousMidi, int currentMidi)
        {
            if (previousMidi < 0 || currentMidi == previousMidi)
            {
                return StepDirection.Same;
            }

            return currentMidi > previousMidi ? StepDirection.Up : StepDirection.Down;
        }
    }
}
