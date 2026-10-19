using System;

namespace Jazztures.Core.Menu
{
    /// <summary>
    /// The parameters <see cref="MenuGestureRecognizer"/> uses (ADR-0051). All `[TUNABLE]` and
    /// <b>not yet measured on a headset</b> — the Unity <c>Config/MenuConfig.asset</c> mirrors
    /// these and produces one of these structs. Pilot-calibrate at M8 and record the result in
    /// <c>Docs/CALIBRATION.md</c>; until then these are an engineer's guesses, not findings.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct MenuGestureThresholds
    {
        public MenuGestureThresholds(
            float flickEnterSpeedMetresPerSecond,
            float flickExitSpeedMetresPerSecond,
            float strikeEnterSpeedMetresPerSecond,
            float strikeExitSpeedMetresPerSecond,
            float axisDominanceRatio,
            int settleFrames,
            double minInterGestureSeconds,
            double oppositeFlickLockoutSeconds,
            double entryGraceSeconds,
            float fistEnterAmount,
            float fistExitAmount,
            double fistHoldSecondsInMenu,
            double fistHoldSecondsInRun,
            int fistMissToleranceFrames,
            int highFramesToResumeAfterLoss)
        {
            Require(flickExitSpeedMetresPerSecond >= 0f, nameof(flickExitSpeedMetresPerSecond));
            Require(flickEnterSpeedMetresPerSecond > flickExitSpeedMetresPerSecond, nameof(flickEnterSpeedMetresPerSecond));
            Require(strikeExitSpeedMetresPerSecond >= 0f, nameof(strikeExitSpeedMetresPerSecond));
            Require(strikeEnterSpeedMetresPerSecond > strikeExitSpeedMetresPerSecond, nameof(strikeEnterSpeedMetresPerSecond));
            Require(axisDominanceRatio >= 1f, nameof(axisDominanceRatio));
            Require(settleFrames >= 1, nameof(settleFrames));
            Require(minInterGestureSeconds >= 0.0, nameof(minInterGestureSeconds));
            Require(oppositeFlickLockoutSeconds >= 0.0, nameof(oppositeFlickLockoutSeconds));
            Require(entryGraceSeconds >= 0.0, nameof(entryGraceSeconds));
            Require(fistExitAmount >= 0f, nameof(fistExitAmount));
            Require(fistEnterAmount > fistExitAmount && fistEnterAmount <= 1f, nameof(fistEnterAmount));
            Require(fistHoldSecondsInMenu >= 0.0, nameof(fistHoldSecondsInMenu));
            Require(fistHoldSecondsInRun >= fistHoldSecondsInMenu, nameof(fistHoldSecondsInRun));
            Require(fistMissToleranceFrames >= 0, nameof(fistMissToleranceFrames));
            Require(highFramesToResumeAfterLoss >= 1, nameof(highFramesToResumeAfterLoss));

            FlickEnterSpeedMetresPerSecond = flickEnterSpeedMetresPerSecond;
            FlickExitSpeedMetresPerSecond = flickExitSpeedMetresPerSecond;
            StrikeEnterSpeedMetresPerSecond = strikeEnterSpeedMetresPerSecond;
            StrikeExitSpeedMetresPerSecond = strikeExitSpeedMetresPerSecond;
            AxisDominanceRatio = axisDominanceRatio;
            SettleFrames = settleFrames;
            MinInterGestureSeconds = minInterGestureSeconds;
            OppositeFlickLockoutSeconds = oppositeFlickLockoutSeconds;
            EntryGraceSeconds = entryGraceSeconds;
            FistEnterAmount = fistEnterAmount;
            FistExitAmount = fistExitAmount;
            FistHoldSecondsInMenu = fistHoldSecondsInMenu;
            FistHoldSecondsInRun = fistHoldSecondsInRun;
            FistMissToleranceFrames = fistMissToleranceFrames;
            HighFramesToResumeAfterLoss = highFramesToResumeAfterLoss;
        }

        /// <summary>Sideways hand speed that starts a flick. Schmitt enter edge. Default 0.7 m/s.</summary>
        public float FlickEnterSpeedMetresPerSecond { get; }

        /// <summary>Sideways speed the hand must fall to before another gesture can arm. Default 0.25 m/s.</summary>
        public float FlickExitSpeedMetresPerSecond { get; }

        /// <summary>Downward hand speed that starts a strike. Schmitt enter edge. Default 0.45 m/s.</summary>
        public float StrikeEnterSpeedMetresPerSecond { get; }

        /// <summary>Downward speed the hand must fall to before another gesture can arm. Default 0.18 m/s.</summary>
        public float StrikeExitSpeedMetresPerSecond { get; }

        /// <summary>
        /// How much faster the primary axis must be than the other for a motion to count. A
        /// diagonal swipe is neither a flick nor a strike — silence is a recoverable error, a
        /// wrong choice is not (§3.4). Default 1.8.
        /// </summary>
        public float AxisDominanceRatio { get; }

        /// <summary>
        /// Consecutive quiet frames needed before the next flick or strike can arm (the ADR-0030
        /// pattern): a stroke decelerating, rebounding or being retraced must not fire again.
        /// Default 3.
        /// </summary>
        public int SettleFrames { get; }

        /// <summary>Minimum gap between two flicks/strikes. Default 250 ms.</summary>
        public double MinInterGestureSeconds { get; }

        /// <summary>
        /// After a flick, a flick the <i>other way</i> inside this window is taken to be the hand
        /// returning, and swallowed. Without it every flick would be followed by its own return
        /// stroke undoing it. Default 500 ms.
        /// </summary>
        public double OppositeFlickLockoutSeconds { get; }

        /// <summary>
        /// After the recogniser is re-armed — a menu appearing as a lesson ends, with the right
        /// hand mid-way through striking touch targets — flicks and strikes are ignored for this
        /// long, and until the hand has settled. Default 600 ms.
        /// </summary>
        public double EntryGraceSeconds { get; }

        /// <summary>Fist closure at which a fist begins. Schmitt enter edge. Default 0.75 (§3.4's "curled").</summary>
        public float FistEnterAmount { get; }

        /// <summary>Fist closure below which the hand counts as open again. Must be lower than the enter edge. Default 0.55.</summary>
        public float FistExitAmount { get; }

        /// <summary>How long a fist must be held to go home while a menu is open. Default 350 ms.</summary>
        public double FistHoldSecondsInMenu { get; }

        /// <summary>
        /// How long a fist must be held to leave a running lesson or free play. Longer than in
        /// a menu: the right hand is busy playing, and being thrown out mid-phrase is far worse
        /// than a slow exit. Default 1000 ms.
        /// </summary>
        public double FistHoldSecondsInRun { get; }

        /// <summary>Consecutive open-hand frames tolerated inside a fist hold without cancelling it. Default 2.</summary>
        public int FistMissToleranceFrames { get; }

        /// <summary>Consecutive High-quality frames required before gestures count again after a tracking loss (§3.5.3). Default 3.</summary>
        public int HighFramesToResumeAfterLoss { get; }

        /// <summary>The ADR-0051 engineering defaults. Pilot-calibrated at M8.</summary>
        public static MenuGestureThresholds Default => new MenuGestureThresholds(
            flickEnterSpeedMetresPerSecond: 0.7f,
            flickExitSpeedMetresPerSecond: 0.25f,
            strikeEnterSpeedMetresPerSecond: 0.45f,
            strikeExitSpeedMetresPerSecond: 0.18f,
            axisDominanceRatio: 1.8f,
            settleFrames: 3,
            minInterGestureSeconds: 0.25,
            oppositeFlickLockoutSeconds: 0.5,
            entryGraceSeconds: 0.6,
            fistEnterAmount: 0.75f,
            fistExitAmount: 0.55f,
            fistHoldSecondsInMenu: 0.35,
            fistHoldSecondsInRun: 1.0,
            fistMissToleranceFrames: 2,
            highFramesToResumeAfterLoss: 3);

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
