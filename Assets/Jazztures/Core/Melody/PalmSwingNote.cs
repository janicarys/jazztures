using Jazztures.Core.Music;

namespace Jazztures.Core.Melody
{
    /// <summary>One note the palm swing asks for (ADR-0078).</summary>
    public readonly struct PalmSwingNote
    {
        public PalmSwingNote(
            Pitch pitch, Pitch cursorPitch, StepDirection sampledDirection, SwingDirection swing,
            int zone, double time, float speedMetresPerSecond, bool atLimit)
        {
            Pitch = pitch;
            CursorPitch = cursorPitch;
            SampledDirection = sampledDirection;
            Swing = swing;
            Zone = zone;
            Time = time;
            SpeedMetresPerSecond = speedMetresPerSecond;
            AtLimit = atLimit;
        }

        /// <summary>What to sound: the cursor's pitch plus any octave shift.</summary>
        public Pitch Pitch { get; }

        /// <summary>The cursor's pitch before the octave shift.</summary>
        public Pitch CursorPitch { get; }

        /// <summary>The held direction read from before the crossing (what the learner meant).</summary>
        public StepDirection SampledDirection { get; }

        public SwingDirection Swing { get; }

        /// <summary>The register zone the note was read in; -1 when height zones are off.</summary>
        public int Zone { get; }

        /// <summary>When the hand crossed the line.</summary>
        public double Time { get; }

        /// <summary>Lateral speed at the crossing: feed this to the velocity curve.</summary>
        public float SpeedMetresPerSecond { get; }

        /// <summary>The step wanted to go past the end of the range and was clamped.</summary>
        public bool AtLimit { get; }
    }
}
