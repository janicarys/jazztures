namespace Jazztures.Core.Melody
{
    /// <summary>
    /// One frame of right-hand state for <see cref="PalmSwingPipeline"/> (ADR-0078), already in the lane
    /// frame and already reduced to numbers — the Unity adapter owns the tracking maths, Core never sees a vector.
    /// </summary>
    public readonly struct PalmSwingSample
    {
        public PalmSwingSample(double time, bool gateOpen, float pitchAngleDegrees, float lateralMetres, float heightMetres)
        {
            Time = time;
            GateOpen = gateOpen;
            PitchAngleDegrees = pitchAngleDegrees;
            LateralMetres = lateralMetres;
            HeightMetres = heightMetres;
        }

        /// <summary>Seconds on the musical clock.</summary>
        public double Time { get; }

        /// <summary>Tracking is good, the palm faces left and the hand is open.</summary>
        public bool GateOpen { get; }

        /// <summary>Fingers above the horizontal, degrees, in world space. + = up.</summary>
        public float PitchAngleDegrees { get; }

        /// <summary>Palm centre along the lane's left-right axis, 0 = the trigger line, + = right.</summary>
        public float LateralMetres { get; }

        /// <summary>Palm centre above the lane origin.</summary>
        public float HeightMetres { get; }
    }
}
