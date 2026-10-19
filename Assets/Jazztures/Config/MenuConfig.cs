using System.Collections.Generic;
using Jazztures.Core.Menu;
using Jazztures.Core.Timing;
using Jazztures.Core.Tutorial;
using UnityEngine;

namespace Jazztures.Config
{
    /// <summary>
    /// The single tuning asset for the gesture menu (ADR-0051). `[TUNABLE]` throughout —
    /// <b>none of it has been tried on a headset</b>; pilot-calibrate at M8 and mirror the result
    /// in <c>Docs/CALIBRATION.md</c>. The motion and fist values feed
    /// <see cref="MenuGestureRecognizer"/> via <see cref="ToThresholds"/>; the rest decides which
    /// cards the menu offers.
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Config/Menu", fileName = "MenuConfig")]
    public sealed class MenuConfig : ScriptableObject
    {
        [Header("Flick — sideways, moves the highlight")]
        [Tooltip("Sideways hand speed that starts a flick.")]
        [Min(0f)] [SerializeField] private float _flickEnterSpeedMetresPerSecond = 0.7f;

        [Tooltip("Sideways speed the hand must fall to before another flick or strike can arm (Schmitt exit).")]
        [Min(0f)] [SerializeField] private float _flickExitSpeedMetresPerSecond = 0.25f;

        [Header("Strike — downward, chooses the highlighted card")]
        [Tooltip("Downward hand speed that starts a strike.")]
        [Min(0f)] [SerializeField] private float _strikeEnterSpeedMetresPerSecond = 0.45f;

        [Tooltip("Downward speed the hand must fall to before another flick or strike can arm (Schmitt exit).")]
        [Min(0f)] [SerializeField] private float _strikeExitSpeedMetresPerSecond = 0.18f;

        [Header("Shared by flick and strike")]
        [Tooltip("How much faster the primary axis must be than the other. A diagonal swipe counts as neither.")]
        [Min(1f)] [SerializeField] private float _axisDominanceRatio = 1.8f;

        [Tooltip("Consecutive quiet frames before the next flick or strike can arm — a stroke's own rebound must not fire twice (ADR-0030 pattern).")]
        [Min(1)] [SerializeField] private int _settleFrames = 3;

        [Tooltip("Minimum gap between two flicks/strikes.")]
        [Min(0f)] [SerializeField] private float _minInterGestureSeconds = 0.25f;

        [Tooltip("A flick the OTHER way inside this window after a flick is the hand returning, and is swallowed.")]
        [Min(0f)] [SerializeField] private float _oppositeFlickLockoutSeconds = 0.5f;

        [Tooltip("After the menu appears (a lesson ending, the right hand mid-way through striking targets) flicks and strikes are ignored this long.")]
        [Min(0f)] [SerializeField] private float _entryGraceSeconds = 0.6f;

        [Tooltip("Consecutive High-quality frames required before gestures count again after a tracking loss (§3.5).")]
        [Min(1)] [SerializeField] private int _highFramesToResumeAfterLoss = 3;

        [Header("Fist — held, goes back to the main menu from anywhere")]
        [Tooltip("Closure at which a fist begins (0 open .. 1 tight). §3.4's 'curled' default is 0.75.")]
        [Range(0f, 1f)] [SerializeField] private float _fistEnterAmount = 0.75f;

        [Tooltip("Closure below which the hand counts as open. Must be lower than the enter edge (hysteresis).")]
        [Range(0f, 1f)] [SerializeField] private float _fistExitAmount = 0.55f;

        [Tooltip("How long a fist must be held to go home while a menu is open.")]
        [Min(0f)] [SerializeField] private float _fistHoldSecondsInMenu = 0.35f;

        [Tooltip("How long a fist must be held to leave a running lesson or free play. Longer: the right hand is busy playing, " +
                 "and being thrown out mid-phrase is far worse than a slow exit. Never shorter than the menu hold.")]
        [Min(0f)] [SerializeField] private float _fistHoldSecondsInRun = 1.0f;

        [Tooltip("Consecutive open-hand frames tolerated inside a fist hold without cancelling it.")]
        [Min(0)] [SerializeField] private int _fistMissToleranceFrames = 2;

        [Header("Tutorial (ADR-0053)")]
        [Tooltip("Run the tutorial on startup: hands in view, then the three menu gestures, in darkness until the " +
                 "menu opens. Untick to go straight to the lesson menu (and the sunrise). Each of the five study " +
                 "sessions starts the app, so untick it for returning participants if the protocol says so.")]
        [SerializeField] private bool _showTutorial = true;

        [Tooltip("A step that waits on the learner's hands is shown at least this long, even if they are already " +
                 "doing it, so every line can be read.")]
        [Min(0f)] [SerializeField] private float _tutorialMinStepSeconds = 1.5f;

        [Tooltip("A hand is 'in front of you' when tracked and within this many degrees of where you are looking. " +
                 "A hand held at chest height is about 35 degrees.")]
        [Range(5f, 90f)] [SerializeField] private float _tutorialInViewMaxDegrees = 50f;

        [Tooltip("A hand is 'out of view' when not tracked, or at least this many degrees off where you are looking. " +
                 "The Quest keeps tracking hands hanging at your sides, so tracking loss alone cannot be the test. " +
                 "A hand at your side is 80 degrees or more. Must exceed the in-front angle.")]
        [Range(10f, 180f)] [SerializeField] private float _tutorialOutOfViewMinDegrees = 60f;

        [Tooltip("If a hands-in-view, hands-out-of-view or thumb-up step is still unmet after this long, move on " +
                 "rather than strand the learner behind something the hardware cannot see. 0 = never. Gesture " +
                 "steps (fist, flick, strike) never time out.")]
        [Min(0f)] [SerializeField] private float _tutorialPostureTimeoutSeconds = 20f;

        [Tooltip("How long both hands must stay in view for 'hands in front of you' to count.")]
        [Min(0f)] [SerializeField] private float _tutorialInViewHoldSeconds = 0.5f;

        [Tooltip("How long both hands must stay out of view for 'hands to your side' to count.")]
        [Min(0f)] [SerializeField] private float _tutorialOutOfViewHoldSeconds = 0.6f;

        [Tooltip("The most closed any finger may be (0 open .. 1 curled) for the right hand to count as open.")]
        [Range(0.05f, 1f)] [SerializeField] private float _tutorialOpenHandMaxClosure = 0.35f;

        [Tooltip("Thumb within this many degrees of straight up starts 'thumb up'.")]
        [Range(1f, 90f)] [SerializeField] private float _tutorialThumbUpEnterDegrees = 35f;

        [Tooltip("The thumb must fall past this to lose 'thumb up' (hysteresis: wider than the enter angle).")]
        [Range(2f, 180f)] [SerializeField] private float _tutorialThumbUpExitDegrees = 50f;

        [Tooltip("How long an open hand with its thumb up must be held.")]
        [Min(0f)] [SerializeField] private float _tutorialThumbUpHoldSeconds = 0.6f;

        [Tooltip("A gesture arriving this soon after a step begins is the tail of the last step's motion; ignored.")]
        [Min(0f)] [SerializeField] private float _tutorialEventGraceSeconds = 0.5f;

        [Header("What the menu offers")]
        [Tooltip("Free play at the end of the main menu (§3.8 Compose-on-the-Fly). A tester affordance, not part of the " +
                 "learner's task — untick it for a study build (§3.10: visual restraint).")]
        [SerializeField] private bool _showFreePlay = true;

        [Tooltip("The tempo card beside a lesson's modes. Untick it for a study build: a learner-chosen tempo changes how " +
                 "hard the onset windows are and so confounds timing scores across participants (§3.7).")]
        [SerializeField] private bool _showTempoCard = true;

        [Tooltip("Tempos the tempo card steps through when struck, wrapping from the fastest to the slowest. " +
                 "Empty = no tempo card. §3.6 treats anything above ~100 BPM as unvalidated.")]
        [SerializeField] private float[] _tempoPresetsBpm = { 60f, 70f, 80f, 90f, 100f };

        [Tooltip("Let the presets exceed the validated 100 BPM ceiling (up to the 240 BPM typo guard).")]
        [SerializeField] private bool _allowUnvalidatedTempo;

        public bool ShowFreePlay => _showFreePlay;

        public bool ShowTempoCard => _showTempoCard;

        public bool ShowTutorial => _showTutorial;

        public TutorialThresholds ToTutorialThresholds() => new TutorialThresholds(
            _tutorialMinStepSeconds,
            _tutorialInViewHoldSeconds,
            _tutorialOutOfViewHoldSeconds,
            _tutorialOpenHandMaxClosure,
            _tutorialThumbUpEnterDegrees,
            Mathf.Max(_tutorialThumbUpExitDegrees, _tutorialThumbUpEnterDegrees + 1f),
            _tutorialThumbUpHoldSeconds,
            _tutorialEventGraceSeconds,
            _tutorialInViewMaxDegrees,
            Mathf.Max(_tutorialOutOfViewMinDegrees, _tutorialInViewMaxDegrees + 1f),
            _tutorialPostureTimeoutSeconds);

        public MenuGestureThresholds ToThresholds() => new MenuGestureThresholds(
            _flickEnterSpeedMetresPerSecond,
            _flickExitSpeedMetresPerSecond,
            _strikeEnterSpeedMetresPerSecond,
            _strikeExitSpeedMetresPerSecond,
            _axisDominanceRatio,
            _settleFrames,
            _minInterGestureSeconds,
            _oppositeFlickLockoutSeconds,
            _entryGraceSeconds,
            _fistEnterAmount,
            _fistExitAmount,
            _fistHoldSecondsInMenu,
            Mathf.Max(_fistHoldSecondsInRun, _fistHoldSecondsInMenu),
            _fistMissToleranceFrames,
            _highFramesToResumeAfterLoss);

        /// <summary>
        /// The presets the tempo card cycles, clamped into <see cref="TempoRange"/> (so a typo in
        /// the Inspector cannot offer 600 BPM) — empty when the card is switched off.
        /// </summary>
        public IReadOnlyList<double> TempoPresets()
        {
            var presets = new List<double>();
            if (!_showTempoCard || _tempoPresetsBpm == null)
            {
                return presets;
            }

            foreach (float bpm in _tempoPresetsBpm)
            {
                if (bpm > 0f)
                {
                    presets.Add(TempoRange.Clamp(bpm, _allowUnvalidatedTempo));
                }
            }

            return presets;
        }

        private void OnValidate()
        {
            // Keep the Schmitt edges ordered however the sliders are dragged.
            _flickEnterSpeedMetresPerSecond = Mathf.Max(_flickEnterSpeedMetresPerSecond, _flickExitSpeedMetresPerSecond + 0.01f);
            _strikeEnterSpeedMetresPerSecond = Mathf.Max(_strikeEnterSpeedMetresPerSecond, _strikeExitSpeedMetresPerSecond + 0.01f);
            _fistEnterAmount = Mathf.Max(_fistEnterAmount, _fistExitAmount + 0.01f);
            _fistHoldSecondsInRun = Mathf.Max(_fistHoldSecondsInRun, _fistHoldSecondsInMenu);
            _tutorialThumbUpExitDegrees = Mathf.Max(_tutorialThumbUpExitDegrees, _tutorialThumbUpEnterDegrees + 1f);
            _tutorialOutOfViewMinDegrees = Mathf.Max(_tutorialOutOfViewMinDegrees, _tutorialInViewMaxDegrees + 1f);
        }
    }
}
