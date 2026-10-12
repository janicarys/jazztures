using System;
using System.Collections.Generic;
using Jazztures.Core.Drills;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;
using UnityEngine;

namespace Jazztures.Lessons
{
    /// <summary>
    /// A timing-drill preset as data (ADR-0043): metronome settings, where the learner is
    /// expected to play, which hand is scored, and what harmony sounds underneath. Like a
    /// <see cref="LessonDefinition"/>, a new preset is a new asset — no C# change (§3.9).
    /// Listed in the <see cref="LessonCatalog"/>, shown on the "Timing test" menu, and baked
    /// into the pure <see cref="TimingDrill"/> by <see cref="BuildDrill"/>.
    ///
    /// <para>
    /// Every value here is `[TUNABLE]` and part of a measuring instrument, not of the lesson
    /// curriculum — change one and the numbers from earlier runs are no longer comparable.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Timing Drill", fileName = "TimingDrill")]
    public sealed class TimingDrillDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The menu row, and the drill name in the CSV. Keep it short.")]
        [SerializeField] private string _displayName = "New drill";

        [Header("Metronome")]
        [Tooltip("Within the validated 40–100 BPM range (§3.6).")]
        [Range(40f, 100f)]
        [SerializeField] private float _tempoBpm = 80f;

        [Min(1)]
        [SerializeField] private int _beatsPerBar = 4;

        [Tooltip("Off-beat clicks. The quarter pulse is never swung.")]
        [SerializeField] private MetronomeSubdivision _subdivision = MetronomeSubdivision.None;

        [Tooltip("0.5 = straight. Swings the off-beat click and any every-eighth onsets.")]
        [Range(0.5f, 0.85f)]
        [SerializeField] private float _swingRatio = 0.5f;

        [Tooltip("Bars of click before the first expected onset (ADR-0043: 1).")]
        [Min(0)]
        [SerializeField] private int _countInBars = 1;

        [Tooltip("Scored bars, after the count-in.")]
        [Min(1)]
        [SerializeField] private int _bars = 4;

        [Tooltip("Sound the click. Off only for a no-metronome control run.")]
        [SerializeField] private bool _metronomeAudible = true;

        [Header("What is played and scored")]
        [Tooltip("Off makes a metronome-only run: nothing is expected, scored or written to a CSV.")]
        [SerializeField] private bool _scoringEnabled = true;

        [Tooltip("Whose playing is captured. Left = chord strikes, Right = melody notes.")]
        [SerializeField] private Handedness _scoredHand = Handedness.Right;

        [Tooltip("Which hands' surfaces are live during the run (a right-hand drill hides the " +
                 "left-hand plate and vice versa).")]
        [SerializeField] private ActiveHands _activeHands = ActiveHands.Right;

        [Tooltip("Where the scored hand is expected to play, within the scored bars.")]
        [SerializeField] private OnsetPatternSpec _pattern = new OnsetPatternSpec();

        [Header("Harmony")]
        [Tooltip("None: the learner's left hand supplies it (or there is none). Fixed: one chord, " +
                 "struck on every bar line. Timeline: the chord list below. A left-hand drill must be None.")]
        [SerializeField] private DrillHarmonyKind _harmony = DrillHarmonyKind.None;

        [SerializeField] private ChordFunction _fixedChord = ChordFunction.One;

        [Tooltip("Timeline only. Beats are from the very start of the run, the count-in included.")]
        [SerializeField] private List<ChordEntry> _chords = new List<ChordEntry>();

        [Tooltip("Sound the scripted chords (decision 3: audible by default). Off keeps the melody " +
                 "following the chord without playing it.")]
        [SerializeField] private bool _harmonyAudible = true;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;

        public Handedness ScoredHand => _scoredHand;

        public ActiveHands ActiveHands => _activeHands;

        public bool ScoringEnabled => _scoringEnabled;

        /// <summary>Bake this preset into the pure drill. Throws if it does not make a valid drill.</summary>
        public TimingDrill BuildDrill()
        {
            var builder = new TimingDrillBuilder()
                .Named(DisplayName)
                .WithTempo(TempoRange.ClampToTempo(_tempoBpm, allowUnvalidated: false))
                .WithBeatsPerBar(Mathf.Max(1, _beatsPerBar))
                .WithSubdivision(_subdivision)
                .WithSwing(Mathf.Approximately(_swingRatio, 0.5f) ? SwingRatio.Straight : new SwingRatio(_swingRatio))
                .WithCountInBars(Mathf.Max(0, _countInBars))
                .WithBars(Mathf.Max(1, _bars))
                .WithMetronomeAudible(_metronomeAudible)
                .WithScoring(_scoringEnabled)
                .ScoringHand(_scoredHand)
                .WithPattern(_pattern != null ? _pattern.ToPattern() : OnsetPattern.None)
                .WithHarmonyAudible(_harmonyAudible);

            switch (_harmony)
            {
                case DrillHarmonyKind.Fixed:
                    builder.FixedChord(_fixedChord);
                    break;

                case DrillHarmonyKind.Timeline:
                    foreach (ChordEntry chord in _chords)
                    {
                        builder.ChordAt(chord.beat, chord.function);
                    }

                    break;
            }

            return builder.Build();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            try
            {
                BuildDrill();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{name}] drill does not bake cleanly: {e.Message}", this);
            }
        }
#endif

        [Serializable]
        private sealed class ChordEntry
        {
            [Min(0f)] public float beat;
            public ChordFunction function;
        }
    }
}
