using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// Hand pitch angle (degrees, + = fingers up) at which <see cref="DirectionClassifier"/> switches
    /// state. `[TUNABLE]`, unmeasured. Up is lower than down because the wrist bends less far toward the
    /// thumb. Entering needs the angle to pass the level; leaving needs it to fall back by
    /// <see cref="HysteresisDegrees"/> (the Schmitt rule, CLAUDE.md §3.4).
    /// </summary>
    public readonly struct DirectionThresholds
    {
        public const float DefaultUpDegrees = 20f;
        public const float DefaultDownDegrees = -25f;
        public const float DefaultHysteresisDegrees = 5f;

        public DirectionThresholds(float upDegrees, float downDegrees, float hysteresisDegrees)
        {
            if (hysteresisDegrees < 0f || float.IsNaN(hysteresisDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(hysteresisDegrees), hysteresisDegrees, "Must be >= 0.");
            }

            // The two "Same" exits must not cross, or Same would have no width.
            if (upDegrees - hysteresisDegrees <= downDegrees + hysteresisDegrees)
            {
                throw new ArgumentException("Up and down thresholds leave no room for 'Same' once the hysteresis is applied.");
            }

            UpDegrees = upDegrees;
            DownDegrees = downDegrees;
            HysteresisDegrees = hysteresisDegrees;
        }

        public static DirectionThresholds Default =>
            new DirectionThresholds(DefaultUpDegrees, DefaultDownDegrees, DefaultHysteresisDegrees);

        public float UpDegrees { get; }

        public float DownDegrees { get; }

        public float HysteresisDegrees { get; }
    }
}
