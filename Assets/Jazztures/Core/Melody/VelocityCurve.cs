namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Maps a fingertip entry speed (metres per second) to a MIDI velocity (CLAUDE.md
    /// §3.3): linear from <see cref="MinSpeed"/>..<see cref="MaxSpeed"/> onto
    /// <see cref="MinVelocity"/>..<see cref="MaxVelocity"/>, clamped, and never below
    /// <see cref="AbsoluteFloor"/> — a novice's timid gesture must not read as silence.
    /// All four bounds are `[TUNABLE]` (see <c>Docs/CALIBRATION.md</c>).
    /// </summary>
    public static class VelocityCurve
    {
        /// <summary>Speeds at or below this map to <see cref="MinVelocity"/>. Equals the melody entry gate.</summary>
        public const float MinSpeed = 0.15f;

        /// <summary>Speeds at or above this map to <see cref="MaxVelocity"/>.</summary>
        public const float MaxSpeed = 1.5f;

        public const byte MinVelocity = 40;

        public const byte MaxVelocity = 110;

        /// <summary>Hard floor — no note-on is ever quieter than this (§3.3).</summary>
        public const byte AbsoluteFloor = 30;

        public static byte FromSpeed(float metresPerSecond)
        {
            float t = (metresPerSecond - MinSpeed) / (MaxSpeed - MinSpeed);
            if (t < 0f)
            {
                t = 0f;
            }
            else if (t > 1f)
            {
                t = 1f;
            }

            int velocity = (int)(MinVelocity + t * (MaxVelocity - MinVelocity) + 0.5f);
            return (byte)(velocity < AbsoluteFloor ? AbsoluteFloor : velocity);
        }
    }
}
