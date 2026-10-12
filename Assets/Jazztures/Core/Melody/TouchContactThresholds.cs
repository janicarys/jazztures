using System;

namespace Jazztures.Core.Melody
{
    /// <summary>
    /// The speeds and counts <see cref="TouchContact"/> decides a touch from (ADR-0045).
    /// All `[TUNABLE]` and unmeasured for the right hand: the tap, settle and frame values
    /// start from the left-hand strike detector's, which were tuned against real tracking
    /// noise on the same sensor (ADR-0028/0030). Calibrate at M8 from the timing drills.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct TouchContactThresholds
    {
        /// <summary>
        /// Fingertip speed that makes a fresh entry deliberate. Equals
        /// <see cref="MelodyEngine.EntryVelocityGateMetresPerSecond"/>, which still gates the note.
        /// </summary>
        public const float DefaultEntrySpeed = 0.08f;

        /// <summary>
        /// Fingertip speed that counts as a tap <i>inside</i> the volume. Well above
        /// <see cref="DefaultEntrySpeed"/> because there is no entry edge to vouch for
        /// intent here: a resting hand's tracking jitter must not read as a tap.
        /// </summary>
        public const float DefaultTapSpeed = 0.25f;

        /// <summary>After a note, the fingertip must slow to this before another tap can fire.</summary>
        public const float DefaultSettleSpeed = 0.10f;

        /// <summary>Consecutive frames at or below <see cref="SettleSpeed"/> that re-arm a tap.</summary>
        public const int DefaultSettleFrames = 3;

        public static TouchContactThresholds Default =>
            new TouchContactThresholds(DefaultEntrySpeed, DefaultTapSpeed, DefaultSettleSpeed, DefaultSettleFrames);

        public TouchContactThresholds(float entrySpeed, float tapSpeed, float settleSpeed, int settleFrames)
        {
            if (float.IsNaN(entrySpeed) || entrySpeed < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(entrySpeed), entrySpeed, "Must be a non-negative number.");
            }

            if (float.IsNaN(tapSpeed) || tapSpeed < entrySpeed)
            {
                throw new ArgumentOutOfRangeException(nameof(tapSpeed), tapSpeed, "Must be at least the entry speed.");
            }

            if (float.IsNaN(settleSpeed) || settleSpeed < 0f || settleSpeed >= tapSpeed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settleSpeed), settleSpeed, "Must be non-negative and below the tap speed (hysteresis).");
            }

            if (settleFrames < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(settleFrames), settleFrames, "Must be at least 1.");
            }

            EntrySpeed = entrySpeed;
            TapSpeed = tapSpeed;
            SettleSpeed = settleSpeed;
            SettleFrames = settleFrames;
        }

        public float EntrySpeed { get; }

        public float TapSpeed { get; }

        public float SettleSpeed { get; }

        public int SettleFrames { get; }
    }
}
