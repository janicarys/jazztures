using Jazztures.Core.Music;

namespace Jazztures.Core.Melody
{
    /// <summary>One note the swing-slice right hand asks for (explore/swing-slices).</summary>
    public readonly struct SwingSlicesNote
    {
        public SwingSlicesNote(Pitch pitch, int region, SwingDirection swing, double time, float speedMetresPerSecond)
        {
            Pitch = pitch;
            Region = region;
            Swing = swing;
            Time = time;
            SpeedMetresPerSecond = speedMetresPerSecond;
        }

        public Pitch Pitch { get; }

        /// <summary>The height region the palm was in just before the swing, 0 = lowest.</summary>
        public int Region { get; }

        public SwingDirection Swing { get; }

        /// <summary>When the hand crossed the line.</summary>
        public double Time { get; }

        /// <summary>Lateral speed at the crossing: feed this to the velocity curve.</summary>
        public float SpeedMetresPerSecond { get; }
    }
}
