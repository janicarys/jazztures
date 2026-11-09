using System;
using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Melody;
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

        [Tooltip("One or two plain sentences shown under this lesson's card while it is highlighted in the menu: " +
                 "what the learner will do, not the theory. Keep it to about 80 characters (two lines). No jargon (§1.5).")]
        [TextArea(1, 3)]
        [SerializeField] private string _menuDescription = string.Empty;

        [Tooltip("What the menu card says about the hands, overriding the one worked out from Hands (for example 'Left, right or both' " +
                 "for a lesson the learner can do either way). Empty uses Hands.")]
        [SerializeField] private string _handsLabel = string.Empty;

        [Tooltip("Plain-language theory shown alongside the exercise. No jargon without explanation (§1.5).")]
        [TextArea(2, 6)]
        [SerializeField] private string _conceptExplanation = string.Empty;

        [Tooltip("The opening explanation as a sequence of captions, each held for its own time. The first phrase " +
                 "waits until they have all been shown. When empty, the single explanation above is shown " +
                 "instead, for the HUD's default hold, without delaying the lesson.")]
        [SerializeField] private List<ConceptSlideEntry> _conceptSlides = new List<ConceptSlideEntry>();

        [Header("Menu card (ADR-0081)")]
        [Tooltip("Shown on the menu as 'Coming soon' and cannot be opened or started. A placeholder for a lesson not built yet.")]
        [SerializeField] private bool _locked;

        [Tooltip("Choosing the card starts the lesson at once: no mode screen and no tempo card. The lesson runs all its modes " +
                 "itself, in order, at its own tempo.")]
        [SerializeField] private bool _startsDirectly;

        [Tooltip("The steps this card runs, one after another. Empty for an ordinary lesson. When set, this asset is only the " +
                 "menu card: its own timeline is unused.")]
        [SerializeField] private LessonSequence _sequence;

        [Header("Right hand regions (ADR-0081)")]
        [Tooltip("Fixed notes for the swing-slice regions instead of the active chord's tones; they do not retune on a chord " +
                 "change. The default leaves the swing-slices behaviour unchanged.")]
        [SerializeField] private LessonPitchSetKind _pitchSetKind = LessonPitchSetKind.ChordTonesDefault;

        [Tooltip("CMajorScale: the lowest MIDI note (60 = C4).")]
        [Range(36, 96)]
        [SerializeField] private int _pitchSetLowestMidi = 60;

        [Tooltip("CMajorScale: how many height regions.")]
        [Range(2, 16)]
        [SerializeField] private int _pitchSetRegionCount = 9;

        [Tooltip("FixedList: the pitch of each region, bottom to top (MIDI, ascending).")]
        [SerializeField] private List<int> _pitchSetFixed = new List<int>();

        [Tooltip("The system's chords sound exactly on this lesson's own beats, and there is no left ghost hand striking them. " +
                 "Without it, a lesson whose chords are system-played delays them by the ghost's strike time so the sound meets " +
                 "the hand. The Swing lesson's band must stay on the metronome's grid.")]
        [SerializeField] private bool _chordsOnTheBeat;

        [Tooltip("Click on every beat of the silent lead-in too, so the learner hears the count before each phase. " +
                 "Needs the metronome on.")]
        [SerializeField] private bool _audibleCountIn;

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

        [Tooltip("An extra, quieter click on the \"and\" between beats (MetronomeSettings ▸ Off Beat Volume Scale). " +
                 "Swung eighths puts it where this lesson's swing ratio puts the \"and\", so the learner hears where an " +
                 "off-beat note lands. Only used when the metronome is on; None is the on-the-beat pulse alone.")]
        [SerializeField] private MetronomeSubdivision _metronomeSubdivision = MetronomeSubdivision.None;

        [Tooltip("Score the learner against a steady pulse as well as any melody notes — for a " +
                 "lesson with expected onsets but no notes to carry them (L2: chords struck on " +
                 "the beat). Runs from beat 0 to the end of the timeline. Test Yourself reports " +
                 "how the strikes lined up (§3.7).")]
        [SerializeField] private OnsetPatternSpec _scoredPulse = new OnsetPatternSpec();

        [Header("Call and response (ADR-0077) - leave 0 for an ordinary lesson")]
        [Tooltip("Length of one round in beats. With System Turn Beats it splits the lesson into repeating rounds in which the " +
                 "system plays first and the learner answers. 0 = no rounds: the system plays the whole timeline (every " +
                 "lesson before ADR-0077). Only a Compose-on-the-Fly phase uses it.")]
        [Min(0f)]
        [SerializeField] private float _roundBeats;

        [Tooltip("How much of each round the system plays, in beats from the round's start. Must be shorter than Round Beats. " +
                 "Chords and notes authored after this in a round are never played by the system, and the learner's left " +
                 "hand is ignored while the system is playing.")]
        [Min(0f)]
        [SerializeField] private float _systemTurnBeats;

        [Tooltip("Make the lesson at least this many beats long even if its last note or chord comes earlier - the learner's " +
                 "last turn has no notes of its own to mark its end. 0 = end at the last event.")]
        [Min(0f)]
        [SerializeField] private float _minimumLengthBeats;

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

        // Set only by LessonSequence when it makes a step at run time (a step is data in the sequence asset, not an asset of its own).
        private Func<double, LessonTimeline> _timelineFactory;
        private LessonScript _scriptOverride;

        /// <summary>
        /// Make this definition a run-time step of a sequence (ADR-0081): its notes and chords come from
        /// <paramref name="timeline"/>, its cues from <paramref name="script"/>, not from the serialised lists.
        /// </summary>
        internal void ConfigureStep(
            string id, string title, IEnumerable<ConceptSlide> slides, IEnumerable<LearningMode> modes, float tempoBpm,
            float swingRatio, LessonPitchSetKind pitchSetKind, int lowestMidi, int regionCount,
            Func<double, LessonTimeline> timeline, LessonScript script)
        {
            _id = id;
            _title = title;
            _tempoBpm = tempoBpm;
            _swingRatio = swingRatio;
            _beatsPerBar = SwingLessonBuilder.BeatsPerBar;
            _hands = ActiveHands.Right;
            _useMetronome = true;
            _audibleCountIn = true;
            _chordsOnTheBeat = true;
            _startsDirectly = true;
            _metronomeSubdivision = MetronomeSubdivision.None;
            _modes = new List<LearningMode>(modes);
            _conceptSlides = new List<ConceptSlideEntry>();
            foreach (ConceptSlide slide in slides)
            {
                _conceptSlides.Add(new ConceptSlideEntry { text = slide.Text, seconds = slide.Seconds });
            }

            _pitchSetKind = pitchSetKind;
            _pitchSetLowestMidi = lowestMidi;
            _pitchSetRegionCount = regionCount;
            _timelineFactory = timeline;
            _scriptOverride = script;
        }

        public LessonId LessonId => new LessonId(_id);

        public string Title => _title;

        /// <summary>What the menu shows under this lesson's card while it is highlighted; empty shows nothing.</summary>
        public string MenuDescription => _menuDescription ?? string.Empty;

        /// <summary>The card's own words for the hands, or empty to derive them from <see cref="Hands"/>.</summary>
        public string HandsLabel => _handsLabel ?? string.Empty;

        /// <summary>Which hands the learner plays in this lesson.</summary>
        public ActiveHands Hands => _hands;

        public bool UseMetronome => _useMetronome;

        /// <summary>The metronome also clicks through the lead-in, so the learner hears the count before each phase.</summary>
        public bool AudibleCountIn => _audibleCountIn && _useMetronome;

        /// <summary>The system's chords sound on the lesson's beats with no ghost strike (no playback lead, no left ghost).</summary>
        public bool ChordsOnTheBeat => _chordsOnTheBeat;

        /// <summary>A placeholder card: "Coming soon", never started.</summary>
        public bool IsLocked => _locked;

        /// <summary>No mode screen: choosing the card starts the lesson, which runs all its modes.</summary>
        public bool StartsDirectly => _startsDirectly;

        /// <summary>The steps this card runs in turn, or null for an ordinary lesson.</summary>
        public LessonSequence Sequence => _sequence;

        /// <summary>The lesson's own region pitches for the swing-slice right hand, or null for the swing-slices default.</summary>
        public LessonPitchSet PitchSet
        {
            get
            {
                switch (_pitchSetKind)
                {
                    case LessonPitchSetKind.CMajorScale:
                        return LessonPitchSet.CMajorScale(_pitchSetLowestMidi, _pitchSetRegionCount);
                    case LessonPitchSetKind.FixedList:
                        return _pitchSetFixed != null && _pitchSetFixed.Count > 0
                            ? LessonPitchSet.Fixed(_pitchSetFixed.ToArray())
                            : null;
                    default:
                        return null;
                }
            }
        }

        /// <summary>Whether the lesson's metronome also clicks the "and" between beats, and where (straight or swung).</summary>
        public MetronomeSubdivision MetronomeSubdivision => _metronomeSubdivision;

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
            if (_timelineFactory != null)
            {
                return _timelineFactory(tempoBpm);
            }

            var builder = new LessonTimelineBuilder()
                .WithTempo(new Tempo(tempoBpm))
                .WithSwing(BuildSwing())
                .WithBeatsPerBar(Mathf.Max(1, _beatsPerBar))
                .WithScoredPulse(_scoredPulse != null ? _scoredPulse.ToPattern() : OnsetPattern.None)
                .WithTurns(BuildTurns())
                .WithMinimumDuration(Mathf.Max(0f, _minimumLengthBeats));

            foreach (ChordEntry chord in _chords)
            {
                builder.Chord(chord.beat, chord.function);
            }

            foreach (NoteEntry note in _notes)
            {
                if (note.midiPitch >= 0)
                {
                    builder.PitchedNote(note.beat, note.midiPitch, (byte)Mathf.Clamp(note.velocity, 1, 127), note.straight);
                }
                else
                {
                    builder.Note(note.beat, note.targetIndex, (byte)Mathf.Clamp(note.velocity, 1, 127));
                }
            }

            foreach (MarkerEntry marker in _markers)
            {
                builder.Marker(marker.beat, marker.name);
            }

            return builder.Build();
        }

        public LessonScript BuildScript()
        {
            if (_scriptOverride != null)
            {
                return _scriptOverride;
            }

            var builder = new LessonScriptBuilder();
            foreach (CueEntry cue in _cues)
            {
                builder.Cue(BuildTrigger(cue), BuildAction(cue));
            }

            return builder.Build();
        }

        private TurnWindow BuildTurns() =>
            _roundBeats > 0f && _systemTurnBeats > 0f && _systemTurnBeats < _roundBeats
                ? new TurnWindow(_roundBeats, _systemTurnBeats)
                : TurnWindow.None;

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

            [Tooltip("A fixed MIDI pitch, whatever the chord (-1 = use Target Index, the chord-tone slot).")]
            [Range(-1, 127)] public int midiPitch = -1;

            [Tooltip("Played exactly where it is written even in a swung lesson.")]
            public bool straight;
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
