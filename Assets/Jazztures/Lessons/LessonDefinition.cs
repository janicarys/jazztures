using System;
using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Music;
using Jazztures.Core.Timing;
using UnityEngine;

namespace Jazztures.Lessons
{
    /// <summary>
    /// A lesson as data (CLAUDE.md §3.9 — "lessons are ScriptableObject assets; adding
    /// Lesson 9 must require no C# changes"). Holds the authored plan, the musical
    /// timeline, and the cue track, and bakes each into its pure <c>Jazztures.Core</c>
    /// form via <see cref="BuildPlan"/> / <see cref="BuildTimeline"/> / <see cref="BuildScript"/>.
    ///
    /// <para>
    /// The timeline is hand-authored here for M5. The ADR-0011 SMF importer (M6) will
    /// populate the same chord / note / marker lists from a <c>.mid</c> file.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Jazztures/Lesson Definition", fileName = "Lesson")]
    public sealed class LessonDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id = "L0";
        [SerializeField] private string _title = "Untitled lesson";

        [Tooltip("Plain-language theory shown alongside the exercise. No jargon without explanation (§1.5).")]
        [TextArea(2, 6)]
        [SerializeField] private string _conceptExplanation = string.Empty;

        [Tooltip("The opening explanation as a sequence of captions, each held for its own time. The first phrase " +
                 "waits until they have all been shown. When empty, the single explanation above is shown " +
                 "instead, for the HUD's default hold, without delaying the lesson.")]
        [SerializeField] private List<ConceptSlideEntry> _conceptSlides = new List<ConceptSlideEntry>();

        [Header("Musical setting")]
        [Min(20f)]
        [SerializeField] private float _tempoBpm = 80f;

        [Tooltip("0.5 = straight eighths. 0.66 ≈ 2:1 swing. L1–L3 are straight; L4 introduces swing (§3.6).")]
        [Range(0.5f, 0.85f)]
        [SerializeField] private float _swingRatio = 0.5f;

        [Min(1)]
        [SerializeField] private int _beatsPerBar = 4;

        [SerializeField] private ActiveHands _hands = ActiveHands.Left;

        [Tooltip("Click on the beat for each phase (MetronomeVoice; sound set in MetronomeSettings). " +
                 "The phrase starts a short pre-roll after the phase begins so the first click " +
                 "can be scheduled ahead (ADR-0043).")]
        [SerializeField] private bool _useMetronome;

        [Tooltip("Score the learner against a steady pulse as well as any melody notes — for a " +
                 "lesson with expected onsets but no notes to carry them (L2: chords struck on " +
                 "the beat). Runs from beat 0 to the end of the timeline. Test Yourself reports " +
                 "how the strikes lined up (§3.7).")]
        [SerializeField] private OnsetPatternSpec _scoredPulse = new OnsetPatternSpec();

        [Header("Mode phases — run in this order (§3.8)")]
        [SerializeField] private List<LearningMode> _modes = new List<LearningMode>
        {
            LearningMode.WatchAndListen,
            LearningMode.TryYourself,
        };

        [Header("Timeline — left-hand chord changes")]
        [SerializeField] private List<ChordEntry> _chords = new List<ChordEntry>();

        [Header("Timeline — right-hand melody targets")]
        [SerializeField] private List<NoteEntry> _notes = new List<NoteEntry>();

        [Header("Timeline — named markers (for cue triggers)")]
        [SerializeField] private List<MarkerEntry> _markers = new List<MarkerEntry>();

        [Header("Cue track — text / visual / control (ADR-0011)")]
        [SerializeField] private List<CueEntry> _cues = new List<CueEntry>();

        public LessonId LessonId => new LessonId(_id);

        public string Title => _title;

        public bool UseMetronome => _useMetronome;

        /// <summary>The modes this lesson offers, in authored order (§3.8) — what its mode menu lists.</summary>
        public IReadOnlyList<LearningMode> Modes => _modes;

        /// <summary>The authored tempo, in BPM — the default a learner-chosen tempo overrides (ADR-0051).</summary>
        public float TempoBpm => _tempoBpm;

        /// <summary>The whole lesson: every authored mode, in order.</summary>
        public LessonPlan BuildPlan() => BuildPlan(_modes, _tempoBpm);

        /// <summary>
        /// A lesson reduced to one mode at <paramref name="tempoBpm"/> (ADR-0051): the menu starts
        /// a lesson one mode at a time, at a tempo the learner may have stepped. The timeline,
        /// metronome and onset windows all follow the plan's tempo, so pair this with
        /// <see cref="BuildTimeline(double)"/> at the same tempo.
        /// </summary>
        public LessonPlan BuildPlan(LearningMode onlyMode, double tempoBpm) =>
            BuildPlan(new[] { onlyMode }, tempoBpm);

        private LessonPlan BuildPlan(IEnumerable<LearningMode> modes, double tempoBpm) => new LessonPlan(
            new LessonId(_id),
            _title,
            _conceptExplanation ?? string.Empty,
            modes,
            new Tempo(tempoBpm),
            BuildSwing(),
            _hands,
            BuildConceptSlides());

        private IEnumerable<ConceptSlide> BuildConceptSlides()
        {
            foreach (ConceptSlideEntry entry in _conceptSlides)
            {
                if (!string.IsNullOrWhiteSpace(entry.text))
                {
                    yield return new ConceptSlide(entry.text, Mathf.Max(0f, entry.seconds));
                }
            }
        }

        public LessonTimeline BuildTimeline() => BuildTimeline(_tempoBpm);

        public LessonTimeline BuildTimeline(double tempoBpm)
        {
            var builder = new LessonTimelineBuilder()
                .WithTempo(new Tempo(tempoBpm))
                .WithSwing(BuildSwing())
                .WithBeatsPerBar(Mathf.Max(1, _beatsPerBar))
                .WithScoredPulse(_scoredPulse != null ? _scoredPulse.ToPattern() : OnsetPattern.None);

            foreach (ChordEntry chord in _chords)
            {
                builder.Chord(chord.beat, chord.function);
            }

            foreach (NoteEntry note in _notes)
            {
                builder.Note(note.beat, note.targetIndex, (byte)Mathf.Clamp(note.velocity, 1, 127));
            }

            foreach (MarkerEntry marker in _markers)
            {
                builder.Marker(marker.beat, marker.name);
            }

            return builder.Build();
        }

        public LessonScript BuildScript()
        {
            var builder = new LessonScriptBuilder();
            foreach (CueEntry cue in _cues)
            {
                builder.Cue(BuildTrigger(cue), BuildAction(cue));
            }

            return builder.Build();
        }

        private SwingRatio BuildSwing() =>
            Mathf.Approximately(_swingRatio, 0.5f) ? SwingRatio.Straight : new SwingRatio(_swingRatio);

        private static CueTrigger BuildTrigger(CueEntry cue)
        {
            switch (cue.triggerKind)
            {
                case CueTriggerKind.AtBeat:
                    return CueTrigger.AtBeat(Mathf.Max(0f, cue.beat));

                case CueTriggerKind.AtMarker:
                    return CueTrigger.AtMarker(cue.marker);

                default:
                    switch (cue.learnerAction)
                    {
                        case LearnerAction.PhraseStarted:
                            return CueTrigger.WhenPhraseStarts();
                        case LearnerAction.ChordConfirmed:
                            return CueTrigger.WhenChordConfirmed(
                                cue.narrowToFunction ? cue.function : (ChordFunction?)null);
                        case LearnerAction.MelodyNotePlayed:
                            return CueTrigger.WhenNotePlayed(
                                cue.narrowToTarget ? cue.targetIndex : (int?)null);
                        default:
                            return CueTrigger.WhenAttemptCompleted();
                    }
            }
        }

        private static CueAction BuildAction(CueEntry cue)
        {
            switch (cue.actionKind)
            {
                case CueActionKind.ShowText:
                    return CueAction.ShowText(cue.text ?? string.Empty, cue.slot);
                case CueActionKind.HideText:
                    return CueAction.HideText(cue.slot);
                case CueActionKind.HighlightTarget:
                    return CueAction.HighlightTarget(Mathf.Clamp(cue.actionTargetIndex, 0, ChordToneSet.TargetCount - 1));
                case CueActionKind.ClearHighlights:
                    return CueAction.ClearHighlights();
                case CueActionKind.SetTensionColor:
                    return CueAction.SetTensionColor(cue.color);
                case CueActionKind.WaitForInput:
                    return CueAction.WaitForInput();
                case CueActionKind.SetScoring:
                    return CueAction.SetScoring(cue.flag);
                default:
                    return CueAction.AdvancePhase();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_chords.Count == 0 && _notes.Count == 0)
            {
                return;
            }

            try
            {
                BuildTimeline();
                BuildScript();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{name}] lesson does not yet bake cleanly: {e.Message}", this);
            }
        }
#endif

        [Serializable]
        private sealed class ConceptSlideEntry
        {
            [TextArea(2, 6)] public string text = string.Empty;
            [Min(0f)] public float seconds = 6f;
        }

        [Serializable]
        private sealed class ChordEntry
        {
            [Min(0f)] public float beat;
            public ChordFunction function;
        }

        [Serializable]
        private sealed class NoteEntry
        {
            [Min(0f)] public float beat;
            [Range(0, 9)] public int targetIndex;
            [Range(1, 127)] public int velocity = 90;
        }

        [Serializable]
        private sealed class MarkerEntry
        {
            [Min(0f)] public float beat;
            public string name = string.Empty;
        }

        [Serializable]
        private sealed class CueEntry
        {
            [Header("Trigger")]
            public CueTriggerKind triggerKind;
            [Min(0f)] public float beat;
            public string marker = string.Empty;
            public LearnerAction learnerAction;
            public bool narrowToFunction;
            public ChordFunction function;
            public bool narrowToTarget;
            [Range(0, 9)] public int targetIndex;

            [Header("Action")]
            public CueActionKind actionKind;
            [TextArea(1, 3)] public string text = string.Empty;
            public int slot;
            [Range(0, 9)] public int actionTargetIndex;
            public TensionColor color;
            public bool flag;
        }
    }
}
