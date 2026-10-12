using System;
using System.Collections.Generic;
using Jazztures.Core.Harmony;
using Jazztures.Core.Lessons;
using Jazztures.Core.Ports;
using Jazztures.Core.Timing;

namespace Jazztures.Core.Drills
{
    /// <summary>
    /// A timing drill as a pure plan (ADR-0043): a metronome run of a few bars, a count-in,
    /// a rule for where the learner is expected to play, which hand is scored, and what
    /// harmony sounds underneath. It is the measuring instrument for comparing right-hand
    /// mechanics against a beat — it does no playing itself. The runner turns it into a
    /// metronome, a harmony playback and a note capture, all anchored to one shared start.
    ///
    /// <para>
    /// <b>Time origin.</b> Every time here is seconds from the drill's shared start
    /// <c>t0</c>, which is beat 0 of the <b>count-in</b>. The scored region starts
    /// <see cref="CountInSeconds"/> later; expected onsets are never inside the count-in.
    /// Scoring is unchanged: <see cref="Evaluation.OnsetScorer.Evaluate"/>.
    /// </para>
    ///
    /// <para>Immutable. Build one with <see cref="TimingDrillBuilder"/>.</para>
    /// </summary>
    public sealed class TimingDrill
    {
        private readonly TimelineChord[] _chordChanges;

        internal TimingDrill(
            string name,
            Tempo tempo,
            int beatsPerBar,
            SwingRatio swing,
            MetronomeSubdivision subdivision,
            int bars,
            int countInBars,
            OnsetPattern pattern,
            Handedness scoredHand,
            bool scoringEnabled,
            bool metronomeAudible,
            DrillHarmonyKind harmonyKind,
            ChordFunction? fixedChord,
            TimelineChord[] chordChanges,
            bool harmonyAudible)
        {
            Name = name;
            Tempo = tempo;
            BeatsPerBar = beatsPerBar;
            Swing = swing;
            Subdivision = subdivision;
            Bars = bars;
            CountInBars = countInBars;
            Pattern = pattern;
            ScoredHand = scoredHand;
            ScoringEnabled = scoringEnabled;
            MetronomeAudible = metronomeAudible;
            HarmonyKind = harmonyKind;
            FixedChord = fixedChord;
            _chordChanges = chordChanges;
            HarmonyAudible = harmonyAudible;
        }

        public string Name { get; }

        public Tempo Tempo { get; }

        public int BeatsPerBar { get; }

        public SwingRatio Swing { get; }

        /// <summary>The metronome's subdivision clicks (the quarter pulse is never swung, §3.6).</summary>
        public MetronomeSubdivision Subdivision { get; }

        /// <summary>Scored bars — after the count-in.</summary>
        public int Bars { get; }

        public int CountInBars { get; }

        /// <summary>Where the learner is expected to play, within the scored bars.</summary>
        public OnsetPattern Pattern { get; }

        /// <summary>Whose playing is captured and scored.</summary>
        public Handedness ScoredHand { get; }

        /// <summary>False for a metronome-only run: nothing is expected, scored or written out.</summary>
        public bool ScoringEnabled { get; }

        /// <summary>Whether the click is sounded (it is silent only for a no-metronome control run).</summary>
        public bool MetronomeAudible { get; }

        public DrillHarmonyKind HarmonyKind { get; }

        public ChordFunction? FixedChord { get; }

        /// <summary>The authored chord changes (beats from the shared start); empty unless <see cref="DrillHarmonyKind.Timeline"/>.</summary>
        public IReadOnlyList<TimelineChord> ChordChanges => _chordChanges;

        /// <summary>Whether the scripted chords are sounded (they are always <i>selected</i>, so the melody still follows them).</summary>
        public bool HarmonyAudible { get; }

        public int TotalBars => CountInBars + Bars;

        public int TotalBeats => TotalBars * BeatsPerBar;

        /// <summary>Beat, from the shared start, where the scored region begins.</summary>
        public double ScoredStartBeat => CountInBars * BeatsPerBar;

        public double CountInSeconds => Tempo.BeatsToSeconds(ScoredStartBeat);

        /// <summary>Length of the whole run, count-in included, in seconds.</summary>
        public double TotalSeconds => Tempo.BeatsToSeconds(TotalBeats);

        /// <summary>
        /// The expected onsets in straight beats from the shared start — within the scored
        /// bars only. Empty when scoring is off.
        /// </summary>
        public double[] ExpectedOnsetBeats() =>
            ScoringEnabled
                ? Pattern.BeatsBetween(ScoredStartBeat, TotalBeats, BeatsPerBar)
                : Array.Empty<double>();

        /// <summary>
        /// <see cref="ExpectedOnsetBeats"/> in seconds from the shared start, with the drill's
        /// swing applied (so an every-eighth pattern's off-beats swing) — ready for
        /// <see cref="Evaluation.OnsetScorer.Evaluate"/>.
        /// </summary>
        public double[] ExpectedOnsetSeconds()
        {
            double[] beats = ExpectedOnsetBeats();
            var seconds = new double[beats.Length];
            for (int i = 0; i < beats.Length; i++)
            {
                seconds[i] = SwingQuantizer.SwingToSeconds(beats[i], Swing, Tempo);
            }

            return seconds;
        }

        /// <summary>A new, un-started metronome with this drill's grid. The caller starts it at the shared start.</summary>
        public Metronome CreateMetronome() =>
            new Metronome(Tempo, BeatsPerBar, Subdivision, Swing);

        /// <summary>
        /// The scripted harmony as a timeline — for <see cref="ScriptedHarmony"/> (selection) and
        /// <see cref="TimelinePlayback"/> (sound) — or null when the drill has none. A fixed
        /// chord is struck on every bar line (a piano chord decays; one strike would be silent
        /// by bar 3), and the timeline runs to the end of the drill so the last chord rings out.
        /// </summary>
        public LessonTimeline? BuildHarmonyTimeline()
        {
            if (HarmonyKind == DrillHarmonyKind.None)
            {
                return null;
            }

            LessonTimelineBuilder builder = new LessonTimelineBuilder()
                .WithTempo(Tempo)
                .WithSwing(Swing)
                .WithBeatsPerBar(BeatsPerBar)
                .WithMinimumDuration(TotalBeats);

            if (HarmonyKind == DrillHarmonyKind.Fixed)
            {
                for (int bar = 0; bar < TotalBars; bar++)
                {
                    builder.Chord(bar * BeatsPerBar, FixedChord!.Value);
                }
            }
            else
            {
                foreach (TimelineChord change in _chordChanges)
                {
                    builder.Chord(change.Beat.Position, change.Function);
                }
            }

            return builder.Build();
        }

        public override string ToString() =>
            $"\"{Name}\" {Tempo}, {CountInBars}+{Bars} bars, {ScoredHand} hand {Pattern}";
    }
}
