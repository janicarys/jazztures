using System;
using System.Collections.Generic;
using Jazztures.Core.Lessons;
using Jazztures.Core.Timing;
using UnityEngine;

namespace Jazztures.Lessons
{
    /// <summary>
    /// A lesson made of steps run one after another (ADR-0081), as data: each step's cards, its four bars of pattern text
    /// (<see cref="SwingPatternText"/>: one slot per eighth, a degree 1..9 or '-'), how many leading bars are straight, and the
    /// modes its four bars run in. The progression, comping, swing and the C major regions are the sequence's. Editing a
    /// pattern is editing text in the inspector: no recompile. <see cref="BuildStep"/> bakes a step into a run-time
    /// <see cref="LessonDefinition"/> the ordinary <c>LessonRunner</c> plays, so nothing about running a phase is new.
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Lesson Sequence", fileName = "LessonSequence")]
    public sealed class LessonSequence : ScriptableObject
    {
        private const int HintSlot = 2; // the straight / swing word; clear of the other cue slots

        [Tooltip("Fixed for the whole sequence: the learner cannot step it. Session 4 found 80 too fast for beginners.")]
        [Min(20f)]
        [SerializeField] private float _tempoBpm = 60f;

        [Tooltip("Where the off-beat eighth falls in a swung bar, as a fraction of the beat. 2/3 is triplet swing.")]
        [Range(0.5f, 0.85f)]
        [SerializeField] private float _swingRatio = 0.6667f;

        [Tooltip("The lowest right-hand note, MIDI (60 = C4). The regions are the C major scale up from here.")]
        [Range(36, 96)]
        [SerializeField] private int _lowestMidi = 60;

        [Tooltip("How many height regions the right hand has in this sequence (9 = C4 up to D5).")]
        [Range(2, 16)]
        [SerializeField] private int _regionCount = 9;

        [Tooltip("Seconds each card stays up before the next.")]
        [Min(1f)]
        [SerializeField] private float _cardSeconds = 6f;

        [SerializeField] private List<Step> _steps = new List<Step>();

        [Tooltip("Shown after the last step, then back to the menu. Plain words, no note names.")]
        [TextArea(1, 3)]
        [SerializeField] private List<string> _closingCards = new List<string>();

        public int StepCount => _steps.Count;

        public float TempoBpm => _tempoBpm;

        /// <summary>The closing cards joined by a line break, for the post-run screen.</summary>
        public string ClosingText => string.Join("\n", _closingCards);

        public string StepName(int index) => _steps[index].name;

        /// <summary>The steps as pure data, the form the tests and the builder read.</summary>
        public IReadOnlyList<SwingStepSpec> ToSpecs()
        {
            var specs = new List<SwingStepSpec>(_steps.Count);
            foreach (Step step in _steps)
            {
                specs.Add(new SwingStepSpec(step.name, step.cards, step.bars, step.straightBars, step.modes));
            }

            return specs;
        }

        /// <summary>A run-time lesson for step <paramref name="index"/>. The caller owns it (and should Destroy it when done).</summary>
        public LessonDefinition BuildStep(int index)
        {
            SwingStepSpec spec = ToSpecs()[index];
            var swing = _swingRatio <= 0.5001f ? SwingRatio.Straight : new SwingRatio(_swingRatio);

            var slides = new List<ConceptSlide>();
            foreach (string card in spec.Cards)
            {
                if (!string.IsNullOrWhiteSpace(card))
                {
                    slides.Add(new ConceptSlide(card, _cardSeconds));
                }
            }

            LessonScript script = BuildScript(spec);

            var definition = CreateInstance<LessonDefinition>();
            definition.hideFlags = HideFlags.HideAndDontSave;
            definition.ConfigureStep(
                $"SW{index}",
                "Swing",
                slides,
                spec.Modes,
                _tempoBpm,
                _swingRatio,
                LessonPitchSetKind.CMajorScale,
                _lowestMidi,
                _regionCount,
                tempoBpm => SwingLessonBuilder.BuildTimeline(spec, new Tempo(tempoBpm), swing),
                script);
            return definition;
        }

        // The listening step labels each half with one word, through the HUD's ordinary caption.
        private static LessonScript BuildScript(SwingStepSpec spec)
        {
            var builder = new LessonScriptBuilder();
            if (spec.StraightBars > 0)
            {
                builder.Cue(CueTrigger.AtBeat(0.0), CueAction.ShowText("Straight", HintSlot));
                builder.Cue(
                    CueTrigger.AtBeat(spec.StraightBars * SwingLessonBuilder.BeatsPerBar), CueAction.ShowText("Swing", HintSlot));
            }

            return builder.Build();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            try
            {
                foreach (SwingStepSpec spec in ToSpecs())
                {
                    SwingLessonBuilder.BuildTimeline(spec, new Tempo(_tempoBpm), SwingRatio.Straight);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{name}] a step does not bake: {e.Message}", this);
            }
        }
#endif

        [Serializable]
        private sealed class Step
        {
            public string name = "Step";

            [TextArea(1, 3)] public List<string> cards = new List<string>();

            [Tooltip("Four bars, one text line each: eight eighth slots, a degree 1..9 (1 = C4 ... 9 = D5) or '-' for a rest. " +
                     "E.g. \"1 2 3 4 - - - -\" is four notes on beats 1 and 2, then rest.")]
            public List<string> bars = new List<string> { string.Empty, string.Empty, string.Empty, string.Empty };

            [Tooltip("The first this-many bars are straight (even eighths); the rest swing. The listening step uses 2.")]
            [Range(0, 4)] public int straightBars;

            [Tooltip("The four bars are played once per entry, in order: Watch And Listen, Try Yourself, and so on.")]
            public List<LearningMode> modes = new List<LearningMode>
            {
                LearningMode.WatchAndListen, LearningMode.TryYourself, LearningMode.WatchAndListen, LearningMode.TryYourself,
            };
        }
    }
}
