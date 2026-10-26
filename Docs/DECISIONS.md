# Architecture Decision Record — Jazztures

Every `[OPEN]` resolution and every deviation from the thesis prose is logged here.
Format: newest first. Each entry states the decision, the context, and — where relevant —
what must change in the thesis text (Chapter 6 in particular).

Status legend: **Accepted** · **Superseded** · **Proposed**

---

## ADR-0077 — An Antecedent–consequent lesson (lesson 5): Compose on the Fly only, the system plays the first half of every round

**Date:** 2026-10-22 · **Status:** Accepted (student's call, from `Docs/handoff/2026-10-22-lesson-structure.md`) — **Core change and tests done
(876 pass); the Unity-side wiring (`LessonRunner`, `PerformanceCompositionRoot`, `LessonDefinition`) compiles only by inspection and the
lesson has not been opened in the Editor or tried on a headset** · **Milestone:** M6 (early) · **Changes the thesis: yes** — see the end.

### What the student decided
- Antecedent–consequent has **one mode, Compose on the Fly** - no Watch and Listen, no Try Yourself (the handoff had assumed both).
- **The system plays ii and V in bars 1-2** along with the antecedent melody; the learner answers in bars 3-4, striking I with the left hand
  and playing the right hand.
- **The learner's left hand is ignored while the system has its turn.** The full version was chosen over a right-hand-only lesson.

### What was built
- **`Core/Lessons/TurnWindow`** - a round of `RoundBeats` of which the first `SystemTurnBeats` are the system's. `default` is "no turns", so
  every earlier lesson is unchanged. `LessonTimeline.Turns`, set by `LessonTimelineBuilder.WithTurns`.
- **`TimelinePlayback`** plays only what falls in the system's turn: a note or chord authored in the learner's part is skipped, and a chord
  stops ringing where the turn ends instead of running to the next chord.
- **`ScriptedHarmony`** selects only in the system's turn (`IsSelecting`), and reports `BlocksLeftHandStrikes`. It is the same script that
  already selects the chords of a right-hand lesson (ADR-0063); the lead-in counts as the system's turn.
- **`PerformanceCompositionRoot`**: the left hand's selection is ignored while `IsSelecting` (it used `IsRunning`, which only says a script
  exists), and **a strike is dropped while `BlocksLeftHandStrikes`** - otherwise the learner's strike would sound the script's chord on top
  of the system's. When the turn ends the root hands the selection back to whatever the left hand holds, as it already did when a script
  stopped; an idle left hand means no chord until the learner makes one. Right-hand lessons and drills have no turns and are unaffected.
- **`LessonRunner`**: Compose on the Fly with turns plays the timeline and starts the script. Without turns it still logs "running dry"
  (the `[OPEN]` backing track, §7). The phrase ends at the lesson's `Minimum Length Beats`, because the learner's last turn has no notes.
- **`LessonDefinition`**: `Round Beats`, `System Turn Beats`, `Minimum Length Beats` (all 0 by default).
- **`Lesson_L5_AntecedentConsequent.asset`** (id `L5`, both hands, Compose on the Fly, 70 BPM, swing 0.66, metronome on), after Guided play.
  Four rounds of ii | V | I | I (16 beats; 64 in all). The system plays the first 8 beats; the pre-authored antecedents use thumb, index and
  middle only (fingers 0-2):

  | Round | Antecedent (beat: finger) | Ends on |
  |---|---|---|
  | 1 up-up-down | 0:0 1:1 2:2 2.5:1 - 4:1 5:2 5.5:1 | the 3rd of G7 (B), unresolved |
  | 2 "and"s on the beat pairs | 0:0 0.5:1 2:2 2.5:1 - 4:0 4.5:1 6:2 | the 5th (D), open |
  | 3 stepping down | 0:2 1:1 1.5:0 - 4:2 5:1 5.5:2 | the 5th (D), open |
  | 4 repeated rhythm | 0:1 1:1 1.5:2 - 4:1 5:1 5.5:2 | the 5th (D), open |

  Every antecedent has at least one swung "and" in each bar (swing 0.66, so the .5 notes land at 0.66). The rule from ADR-0071 holds: an
  "and" is never followed by another note within a beat (the short 291 ms gap), and the beat-to-"and" step (566 ms) is the only short one.
  **The metronome does not click the swung "and"** (`Metronome Subdivision: None`, as in every lesson, ADR-0071): it was switched on for
  this lesson alone and switched back so that no lesson has it, until someone can hear on a headset whether it helps.

  No chord is authored at beat 8: the learner makes the I. The learner's notes are heard and logged and **not scored** (there is no correct
  consequent).

### Not done, and worth knowing
- **No example answer.** Without Watch and Listen a novice never hears a consequent. The window is the same for every round, so a first
  round in which the system plays both halves is not data - it would need a per-round window. The last opening slide tells them to end
  on the thumb (the home note) instead.
- **The "your turn" cue is text**: "Listen." at the start, then a count-in "3", "2", "1" on beats 5, 6, 7 and "Go!" on beat 8 of each round (the
  count overlaps the last notes of the antecedent, which end by beat 6, and the metronome keeps the pulse). The handoff asked for something unmistakable that is not text (ghost fading,
  letters brightening); the cue track cannot do that today and it needs a headset to judge.
- **Chord-name label and lanes** in Compose on the Fly have not been looked at for a lesson (they exist for free play).
- **The title is the jargon** the teacher suggested, so the first slide defines "antecedent" and "consequent" in a sentence (§1.5).
  The menu card reads `5. Antecedent–consequent`; Free play is therefore `6.`.
- No telemetry for the analysis the handoff mentions (ends on a chord tone, echoes the rhythm, range): M7.

### Thesis and paper
§3.8 says Compose on the Fly's system plays "backing only"; here it plays the authored phrase. §3.9's L8 ("System plays 2 bars, user answers
2 bars", W&L + CotF) is the proposal's version of this lesson. `CLAUDE.md` is not edited; Chapters 4 and 6 need matching wording if the
adviser accepts the lesson set.

---

## ADR-0076 — A Guided play lesson (lesson 4): both hands, the Swing patterns; Swing's notes rewritten as up-up-down; Free play numbered

**Date:** 2026-10-22 · **Status:** Accepted (student's requests) — **data plus two one-line presentation changes; not opened in the
Editor, not tried on a headset** · **Milestone:** M6 (early) · **Amends:** ADR-0071 / ADR-0073 (Swing's note table is no longer the
shipped pattern) · **Changes the thesis:** yes — it adds a lesson §3.9 does not list; see the end. This records work that was done
before any ADR and sat uncommitted; the lesson-set direction it belongs to is in `Docs/handoff/2026-10-22-lesson-structure.md`, whose
adviser sign-off is still pending.

### What the student asked for, in order
1. Make the notes in Swing follow patterns like ImproVisAR's sequences ("up-up-down").
2. Add a "guided play" lesson after Swing, using both hands and the same patterns.
3. Make Free play the last item, though it has no modes.
4. The left ghost "transitions way too slowly".

### What was built
- **`Lesson_L3_Swing.asset` - notes only.** Beats, the "and" timings, chords, markers and cues are unchanged; only each note's finger
  changed. Fingers are 0 thumb … 4 pinky. Bar shape is *up, up, down* (finger a, a+1, a+2, a+1), moved one step up each bar (a = 0, 1, 2):
  - Passes 1-3 (on the beat; the first "and"; the late "and") use the **same three bars each time**, so only the timing is new - the
    one-new-dimension rule (§1.5).
  - Pass 4 (the running line, 7 notes a bar) plays two up-up-down cells back to back: bars at a = 0, 1, 0.
  57 notes, as before.
- **`Lesson_L4_GuidedPlay.asset`** (id `L4`, **hands Both**, Watch and Listen then Try Yourself, 70 BPM, swing 0.66, metronome on),
  listed after Swing in Session 1. It is Swing's chords and notes, copied, with its own opening slides and cues: the left hand makes each
  chord shape and strikes as the bar begins, the right plays the notes. Because both hands are active the learner selects the chords
  (ADR-0063); nothing is system-played. The note data is **a copy, not a reference** - editing Swing's patterns later does not move L4.
- **Free play is numbered** - `LessonFlowController.LabelOf` reads `{lessons + 1}. Free play`. It was already the last card on the main
  menu (ADR-0066); only the number is new. This amends ADR-0066's "Free play ... not numbered".
- **Left ghost morph 1.5 → 0.5 beats** (`GhostHandView._morphBeats`, in the scene and as the code default). At 70 BPM 1.5 beats is ~1.3 s,
  a third of a bar spent mid-change. `[TUNABLE]`, a guess, unseen. The ghost still starts changing when the chord changes, not before it,
  so it shows the new shape only as the bar begins; showing it ahead is a separate change to when the pose is announced and is not done.

### Where this departs from the handoff's L4 notes (surfaced, not fixed)
The handoff asks L4 to favour thumb / index / middle, keep the left hand's load low (one chord per bar or slower, strike on beat 1 only,
slow tempo) and notes this is the first time the left hand plays in time. As built:
- **All five fingers are used**, including the pinky on the beat (beats 10, 22, 34, ...) and the ring on a swung "and" (22.5, 34.5). The
  ring/pinky input was made easier by ADR-0073/0074 on a model, and ADR-0075's diagnostics have not been read from a headset.
- **Pass 4 steps between neighbouring fingers at the short swung gap** (e.g. 36.5 index → 37 middle, 291 ms at 70 BPM). ADR-0071 chose its
  pattern so that gap was always between non-neighbours, because ADR-0068's drag veto makes neighbouring-finger runs the weak spot.
  Swing's pattern had already lost that property when the student edited it before this change, and the up-up-down shape does not
  restore it.
- **The left hand strikes every bar (4 beats, ~3.4 s) at 70 BPM**, which is the handoff's "one chord per bar" limit, not below it, and
  there are three chord shapes in a row with no prior timed left-hand lesson (ADR-0069 removed Timing).
Any of these may be wrong on a headset; the fix would be data (a thumb/index/middle copy of the pattern, a slower tempo, strikes every
two bars).

### Not done
- No Test Yourself in either lesson, so nothing scores timing (the handoff's gap 1).
- No `CLAUDE.md` change; Chapters 4 and 6 are untouched.

### Thesis and paper
§3.9's table lists eight lessons with Swing as L4 and Timing as L2; the repository now has Chord tones, Chords, Swing, Guided play and
Free play. Guided play is an addition (the proposal's list has nothing like it), and the L5 antecedent-consequent lesson in the handoff is
built in ADR-0077. Both need matching wording in Chapters 4 and 6 if the adviser accepts the set.

---

## ADR-0075 — Finger-bend diagnostics: log the curls too small to sound, and how far each pressed finger was curled

**Date:** 2026-10-21 · **Status:** Accepted (student asked for it) — **Core change and tests done (850 pass); the log lines exist only in a
build with `JAZZTURES_DIAG`, and have not been seen on a headset** · **Milestone:** M6 (early) · **Changes the thesis:** nothing.

### Why
ADR-0073 and ADR-0074 set the ring and pinky's press level from a model, because nothing on the Quest recorded how far a hand actually
curls a finger. The student's own diagnosis was "I can't curl them enough consistently". This makes that measurable, so the next
adjustment can come from this hand's numbers instead of the model's.

### What it logs (only with `JAZZTURES_DIAG`; the detector's press decision is untouched)
`FingerBendDetector` now also reports, per finger and per frame:
- **`NearMiss`** - a free finger rose past **0.6 of its press level** (`NearMissFraction`, clear of the +-0.03 tracking noise) and fell
  back below half its peak without ever reaching the press level. It carries `NearMissPeak` (how far it got) and `NearMissDrivenBy`
  (the pressed, still-moving finger that was dragging it, or -1 for a weak curl of its own). A finger that reached the press level and
  was *blocked* is not a near miss - it is already reported as blocked - and neither is anything pressed.
- **`Released`** / **`ReleasePeak`** - when a pressed finger lets go, how far it had risen from the trough it pressed from: the whole stroke.
- **`PressLevel(finger)`** - what each finger needed (so a log can say "rose 0.10, needs 0.13").

`FingerBendBinder` writes them as `[FingerBend] Ring NEAR MISS rose 0.10, needs 0.13 - on its own: a curl too small to sound` (or `- Middle was
pressed and moving, so probably dragged along`) and `[FingerBend] Ring STROKE peak 0.31 (needs 0.13)`, next to the lines it already logs for
each press and each blocked press (isolation, or the drag veto). Together: how far each finger curls when it sounds, how far when it does not,
and which of the three reasons (too small / isolation / drag veto) took each note.

### Tests
6 new, all in `FingerBendDetectorTests`: a curl short of the press level is a near miss and says how far it got; a pressed curl is not,
and its stroke is reported at release; tracking noise is never a near miss; a finger blocked above the press level is not; a neighbour dragged
below the press level is reported with the finger that dragged it; the ring is judged against its own lower level. Two deliberate breakages
of the rule (never reporting; counting a blocked finger) each failed tests.

### How to run it
1. Add `JAZZTURES_DIAG` to the Android scripting defines (Edit > Project Settings > Player > Other Settings > Scripting Define Symbols, or
   the Android build profile's scripting defines), build, play the Swing lesson. **Remove it afterwards:** it also turns on the per-note
   `[DIAG] Send/StartNote` lines, the posture and touch-target logs, and the left hand's strike-speed line.
2. Pull the log straight after: `adb logcat -d`. The `[FingerBend]` lines are one per event, so they do not flood the buffer the way the
   per-frame pose logger did (ADR-0065).
3. A scratch script (`bend_log_summary.py`, in the session scratchpad - not in the repo) tallies per finger: strokes that sounded (10th
   percentile and median peak), curls too small on their own (and how big), near misses a neighbour explains, and refusals by isolation and
   by the drag veto.

### Not done
No per-learner calibration, which is what the numbers would justify. The press and isolation scales are still the model's.

---

## ADR-0074 — Ring and pinky: a smaller isolation margin for the fingers that drag each other, and the ring and pinky get their own, lower press level

**Date:** 2026-10-21 · **Status:** Accepted (student's report) — **Core rule and tests done (844 pass); the values are chosen from a model, not
measured, and have not been tried on a headset** · **Milestone:** M6 (early) · **Amends:** ADR-0073 (its isolation scale, 0.6, is now 0.3, and
the ring and pinky no longer share the middle finger's press scale) · **Changes the thesis:** nothing.

### What the student reported
With ADR-0073 in, ring and pinky notes still occasionally did not sound, "likely because I can't curl them enough consistently to sound
them": the bend sometimes never reached the press level.

### What the model says (same harness as ADR-0073; still a model, not a measurement)
I added **weak strokes** to it: with 40% probability a ring or pinky stroke rises only 0.18-0.32 ("weak"), or 0.10-0.22 ("very weak"),
instead of 0.40-0.65, and an **idle hand** run (resting posture, +-0.03 noise plus occasional single-finger spikes) to price any lowering of
the press level in notes nobody played. Two separate things limit a weak curl:
1. **The isolation lead, not the press level, for moderately weak curls.** A weak curl leads the neighbours it drags by only a little, so
   the absolute margin (0.06 after ADR-0073) blocks it even when it clears the press level. Lowering the press level alone left weak-stroke
   hits at 78-79%; lowering the margin to x0.3 (0.03) moved them to 83% (1.0x ring/pinky coupling) and from 57% to 79% (1.3x), with
   **no rise** in notes sounded on the wrong finger and none in idle presses. Below x0.3 returned almost nothing.
2. **The press level, for very weak curls** (the case the student describes). With the smaller margin already in, lowering the ring and
   pinky's press level alone to x0.65 lifted very-weak-stroke hits from 51% to 65% (47% to 57% at 1.3x coupling); x0.55 to 75% (65%). **The cost is stray presses:** in the model's idle
   run, 0.6 presses a minute at x0.75, 3.0 at x0.65, 6.8 at x0.55. **That idle noise is my invented spike process, not tracking data** (the
   student has not reported notes sounding by themselves at the current level), so read it as "each step down trades missed notes for stray
   ones at a rate I cannot yet price", not as a count.

Final choice (normal / weak / very weak curls, 1.0x coupling; the model's hit rates):

| | ring | pinky | weak-stroke hit | wrong-finger notes /100 runs |
|---|---|---|---|---|
| ADR-0073 values | 98 / 88 / 74% | 100 / 98 / 82% | 78% (0.18-0.32), 40% (0.10-0.22) | 46 / 37 / 31 |
| ADR-0074 values | 99 / 92 / 84% | 100 / 100 / 94% | 85%, 65% | 39 / 31 / 28 |

### What changed
- **`DefaultCoupledIsolationScale` 0.6 -> 0.3** (`MelodyConfig` field `Bend Coupled Isolation Scale`): with the scene's isolation margin 0.10, two
  of the four fingers need to lead each other by 0.03, not 0.06. The coupled-stroke stress test passes at this value (300 of 300 strokes,
  0 neighbours sounded) and a case for it was added.
- **`FingerBendThresholds.RingPinkyPressScale`** (new; falls back to `CoupledPressScale` when not given, so every earlier caller is
  unchanged) and `MelodyConfig` field **`Bend Ring Pinky Press Scale`**, default **0.65**: the ring and pinky press at 0.13 (0.20 x 0.65) while
  the middle finger keeps `Bend Coupled Press Scale` 0.75 (0.15) and the thumb and index 0.20. Release level is checked against the lowest.
- **Tests:** 844 (3 new): the ring and pinky press at their own lower level while the middle does not; the ring/pinky scale falls back
  to the coupled one; bad scales are rejected, including a release level above the ring/pinky press level.
- `Docs/CALIBRATION.md` rows updated.

### How to tune it, on a headset
`Config/MelodyConfig.asset` ▸ Finger bend: **`Bend Ring Pinky Press Scale`** is the one to move. Lower (toward 0.5) if ring and pinky notes
are still missed; raise (toward 0.75) if they sound by themselves, or a neighbour sounds as you curl. Nothing here has been calibrated: it is
the M8 job.

### Not done
- **Nothing measures how far the student can actually curl** - ADR-0075 adds the near-miss and stroke-peak log (behind `JAZZTURES_DIAG`)
  that would show it; it needs a build with that define.
- **No per-learner calibration** of each finger's range, which is the principled fix for hands that differ.
- The thumb and index are unchanged.

---

## ADR-0073 — The middle, ring and pinky are asked for less: a smaller press level and a smaller isolation margin, for the fingers that drag each other

**Date:** 2026-10-21 · **Status:** Accepted (student's call); **its isolation scale and the ring/pinky press scale are amended by ADR-0074** — **Core rule and tests done (841 pass); the scale values are chosen from a
model, not measured, and have not been tried on a headset** · **Milestone:** M6 (early) · **Supersedes:** ADR-0072 (the Swing lesson is
back to five fingers) · **Changes the thesis:** nothing.

### The request
After ADR-0072 cut the ring and pinky out of the Swing lesson, the student said that was drastic and asked whether the ring and pinky
notes should instead be made *easier to actuate*. They are right: the system's design puts one chord tone on each of five fingers
(§1.3, ADR-0050), so a finger that cannot be played reliably is an input problem to fix, not a lesson to avoid. ADR-0072 was my own
guess, made without evidence, and is withdrawn; **`Lesson_L3_Swing.asset` is back to the five-finger pattern the student played**, so
the next run tests the input change on the same notes.

### What the evidence is, and is not
There is still **no record of the headset run**: `[FingerBend]` diagnostics are compiled out without `JAZZTURES_DIAG`, and there is no
telemetry writer yet. So I built a **model** and read the detector against it. It is a scratch harness (`bendsim`, not in the repo):
`FingerBendDetector` at 60 Hz driven through the Swing lesson's 57 notes at their swung onsets. Each note is a stroke of its finger
(0.40-0.65 rise over 5-8 frames, held 6-19 frames, released over 6-9); the other fingers are dragged along by amounts falling off with
distance, one or two frames late, with 0.02 of tracking noise; the ring and pinky optionally drag each other 1.3x as hard. A note
"hits" if its finger presses within -0.20 s / +0.25 s of its swung onset. 100 runs per setting. **It tests how the detector's rules
behave under that coupling; it does not say how many notes a real hand would lose, and every number below is the model's.**

What it showed for the scene's hand-adjusted levels (press 0.20, release 0.05, isolation margin 0.10 - see CALIBRATION):

| | index | middle | ring | pinky | neighbour sounded, /100 runs |
|---|---|---|---|---|---|
| scene asset (0.20 / 0.10), no scaling - Swing lesson | 99% | 88-90% | **73-88%** | **83-99%** | 64-73 |
| scene + coupled scales (x0.75 press, x0.6 margin) | 99% | 93-94% | 93-98% | 96-100% | 54-81 |
| code defaults (0.14 / 0.06) | 99% | 97% | 93-97% | 96-100% | 18-46 |

(Ranges are the two ring/pinky coupling strengths, 1.0x and 1.3x.) The same model gives the **Chord tones** lesson 100% for every
finger at the scene's levels unless the ring/pinky coupling is 1.3x (then 88-89%), which fits notes there being slow and far apart.
The misses at the scene's levels, classed by the detector's own state: mostly **isolation** (another free finger rose within 0.10 of
the finger meant, hardest on the ring), then the **drag veto** (a held finger still moving, judged against the same 0.10), then a
**press level** too high for a finger already pulled up by a neighbour. The ring and pinky lose most because they drag each other
hardest, and an "and" is played against the clock, so the curl is quick.

### What changed
- **`FingerBendThresholds`** gains `CoupledPressScale` and `CoupledIsolationScale` (default 1 = no change, so every existing caller
  and test behaves as before) and three methods the detector uses: `PressDeltaFor(finger)` - the press level x the press scale for the
  **middle, ring and pinky** (they share one flexor muscle belly); and `IsolationMarginFor(finger, other)` - the margin x the isolation
  scale when **both** are fingers (index..pinky; the thumb moves on its own and keeps the full margin). The release level must stay below
  every finger's own press level, so a scale that would break that is rejected.
- **`FingerBendDetector`** uses them: the press level per finger, the isolation test per pair (the finger it is weakest against is the
  one reported as blocking), and the drag veto's "did it out-rise the driver's stroke by the margin" per pair. With equal margins this is
  exactly the old rule, which the unchanged tests show.
- **`MelodyConfig`** has `Bend Coupled Press Scale` (default 0.75) and `Bend Coupled Isolation Scale` (default 0.6). The scene asset
  does not list them, so they take these defaults at once: with the scene's press 0.20 / margin 0.10 the middle, ring and pinky press at
  0.15, and two fingers need to lead each other by 0.06. The thumb and index are untouched, so they are as hard to fire by accident as
  before. `[TUNABLE]`, **unmeasured**; chosen as the first setting that lifted ring and pinky to the mid-90s in the model without raising
  neighbour presses there.
- **Tests (9 new, 841 total):** coupled fingers press on a smaller bend than the index; the scales are neutral by default; two
  mutually dragging fingers give the lead to the finger that led by more than the scaled margin (and, as a control, not without the
  scale); the thumb keeps the full margin (a mutation check confirmed this test fails if it does not); a stroke that drags its
  neighbours still sounds only the finger meant, three ways, with the scaled thresholds; the coupled-stroke stress test also runs at the
  scaled values; bad scales are rejected.

### What the model also says, which I have not acted on
The code defaults (0.14 / 0.06) did better than the scene's hand-adjusted 0.20 / 0.10 on every measure, including neighbours sounded.
Those scene values were raised "by feel" (CALIBRATION) - I believe before the drag veto (ADR-0068) existed, when neighbours cascaded -
but the record does not say why, and I did not change the student's values. If ring and pinky are still hard, **the next thing to try is
the code defaults on the config itself**, with `JAZZTURES_DIAG` on for one run so refused presses are logged.

### Not verified
Everything about a real hand: whether the middle, ring and pinky now sound when meant, and whether anything sounds that was not meant
(notes sounded by mistake are audible, and a looser input trades against them). The thumb is not changed.

---

## ADR-0072 — The Swing lesson plays three fingers, not five: the ring and pinky are off its notes

**Date:** 2026-10-21 · **Status:** **Superseded by ADR-0073** — the student found removing fingers too drastic and asked for the ring and
pinky to be made easier to actuate instead; the lesson is back to five fingers. Kept as the record of the first guess. (Was: data only; the
new pattern had not been tried on a headset) · **Milestone:** M6 (early) · **Amends:** ADR-0071's note patterns (its table, the 57-note count, and the claim
about which steps are between non-neighbouring fingers are no longer true) · **Changes the thesis:** nothing beyond ADR-0071.

### What the student reported
After playing the Swing lesson (ADR-0071), the student said they struggled to hit some notes with their ring and pinky fingers.

### What I could and could not establish
**No log of the run exists to read.** The app's `[FingerBend]` diagnostic lines are compiled out unless the build defines
`JAZZTURES_DIAG` (`DiagLog`), this build did not, and there is no on-device telemetry writer yet (M7), so there is no record of which
notes were missed, late or refused. What the code and the pattern show:
- ADR-0071's passes 2–4 put a **swung "and" on the ring finger** (all three passes) and, in pass 4, a **pinky** and two more ring
  notes in a running line. I checked that the fastest step (291 ms) fell between non-neighbouring fingers, but **I never asked
  whether the fingers on the late notes were ones a novice can curl alone.** The ring and pinky are the least independent: curling one
  tends to pull the other (and the middle finger) with it, and `FingerBendDetector` only accepts a press when that finger rose
  `IsolationMargin` (0.06) more than every free finger, so a quick curl that drags its neighbour is *refused*, silently. On an "and" the
  learner is curling against the clock, which is when a quick curl is most likely.
- This is a **hypothesis from the mechanism, not a measurement.** Whether it was refused presses, late ones or wrong ones is not known.

### What changed — data only
`Lesson_L3_Swing.asset` was regenerated with **thumb, index and middle only** (root, 3rd, 5th), 54 notes:

| Pass | Notes in each bar (beat: finger) |
|---|---|
| 1 | 0 thumb · 1 index · 2 middle · 3 index |
| 2 | the same, then **2.5** index |
| 3 | 0 thumb, **0.5** index · 2 middle, **2.5** index |
| 4 | 0 thumb, **0.5** index, 1 middle · 2 thumb, **2.5** index, 3 middle |

Every swung "and" is on the index finger. The shortest step the lesson plays is 291 ms, index → middle, in pass 4 (the pair of
neighbours with the most independence); no finger repeats within 700 ms; ring and pinky play nothing. This also matches the thesis
rule that one new dimension is introduced while the rest is held still (§1.5): Chord tones teaches the five fingers, and Swing reuses
three of them while the rhythm is new. The pass-4 cue was reworded to match the new line ("Now each 'and' leads straight into the next
beat. Long, short.").

### Not done, deliberately
- **No change to `FingerBendDetector`.** A per-finger isolation margin for the ring and pinky is the likely engineering fix if they
  stay hard in Chord tones, but it would change input for every lesson on a guess; it wants the diagnostic data first.
- **No `JAZZTURES_DIAG` define was added** (that is a `ProjectSettings` change on a file with uncommitted edits). A development build
  with it on would log each refused press and who dragged whom (`[FingerBend] … ignored`), readable over `adb` after one run.

### Still unverified
Whether index → middle at 291 ms is comfortable, whether the thumb on beats 0 and 2 is reliable (it is held to two confirming frames),
and whether the cause was isolation at all.

---

## ADR-0071 — A Swing lesson (lesson 3): right hand, system-played ii-V-I, notes on the beat and then on the "and"

**Date:** 2026-10-21 · **Status:** Accepted (student's call, from a teammate's suggestion); **its note patterns are amended by ADR-0072** —
**authored data plus one small field; not run
in the Editor, not tried on a headset** (the Lessons assembly compiles in a scratch project and the 832 Core tests pass) ·
**Milestone:** M6 (early) · **Changes the thesis: yes** — see the end.

### The request
A teammate suggested a lesson that has the learner perform a melody over a ii-V-I, first on the beat, then with notes on the half beats
to introduce swing. I proposed a separate on-the-beat "Rhythm" lesson first; the student pointed out that **Chord tones already has the
learner play one note per beat against the metronome** (its assets: every note on a whole beat, 80 BPM, clicks on), so it would repeat
that. There is no Rhythm lesson. The student chose that **the system plays the chords** (scripted harmony, as in Chord tones), so the
only new thing is the "and".

### What was built
- **`Lessons/Lesson_L3_Swing.asset`** (id `L3`, right hand, Watch and Listen then Try Yourself, metronome on), listed after Chords in the
  catalog's Session 1. It plays ii-V-I four times, **one bar (4 beats) per chord, 12 beats a pass, 57 notes**. The fingers are the
  finger-bend scheme's (thumb = root … pinky = 9th), so the same fingers play new notes as the chord changes:

  | Pass | Notes in each bar (beat: finger) | New idea |
  |---|---|---|
  | 1 | 0 thumb · 1 index · 2 middle · 3 ring | warm-up, on the beat (swing never moves a whole beat) |
  | 2 | 0 · 1 · 2, then **2.5** ring | one "and", after beat 3 |
  | 3 | 0 thumb, **0.5** index · 2 middle, **2.5** ring | two "and"s, each followed by a rest |
  | 4 | 0 · **0.5** · 1 · **1.5** · 2 · **2.5** · 3 = thumb, index, ring, pinky, middle, ring, index | a running line |

  The lesson's `_swingRatio` is 0.66, so the 0.5 notes are pushed to 0.66 by `SwingQuantizer` everywhere that already applied it: the
  lanes, the ghost's presses, system playback. No Core code changed. Pass 4's two-note gaps are 566 ms then 291 ms at 70 BPM, and **the
  291 ms step is always between fingers that are not neighbours** (index→ring, pinky→middle, ring→index); in passes 2 and 3 the
  note after an "and" is a beat or more away. That is deliberate: ADR-0068's drag veto makes fast runs on neighbouring fingers the
  weak spot of the finger-bend input, and a swung "and" followed straight by a beat is the shortest gap this lesson plays. It is a
  checked property of this authored pattern, not something the code enforces.
- **A pass-by-pass cue track** (a warm-up line, then one line at the start of each new pass, and a closing line on completion) and
  a four-slide opening discussion in plain language: "between the beats is an 'and'", "in jazz the 'and' is played a little late", then
  the one word, **swing**, defined by that sentence (§1.5). No "triplet", "syncopation" or "ratio" appears on screen.
- **`LessonDefinition._metronomeSubdivision`** (default `None`), passed to `Metronome` by `LessonRunner` along with the lesson's swing.
  `SwungEighths` adds a quieter click on the swung "and" (`MetronomeSettings ▸ Off Beat Volume Scale`). **The asset leaves it off**:
  whether that click teaches the "and" or just adds noise is something to hear on the headset, and the change is one field.
  Existing lessons are unaffected (default `None`, which is what the runner used before).

### `[TUNABLE]` values — not findings
70 BPM (chosen below Chord tones' 80 so the short swung gap is nearer 300 ms; it is inside the ~70 BPM range CLAUDE.md §3.6 says the
literature validated; the learner can still step the tempo), the 0.66 ratio, the four note patterns, the cue timings and the slide
seconds are all engineering judgement. They need a pianist's ear and pilot use before they reach Chapter 6 (`Docs/CALIBRATION.md`).
Published work reports that jazz swing ratios vary with tempo (Friberg & Sundström, *Music Perception* 19(3), 2002, on drummers' ride
patterns at much faster tempos than this lesson's; read from search summaries, **not checked against the paper**), so 0.66 at 70 BPM is
a starting point, not a measured preference.

### What is not verified
Nothing has run on a headset: whether the pass 2 → 4 progression is the right size, whether the 291 ms step is playable with
finger-bends, whether the swung lane spacing reads as "late" rather than "mistimed", and whether the metronome subdivision helps. There
is no Test Yourself in this lesson (and no timing lesson in the project at present), so the learner's timing is **not scored**; Try
Yourself on a right-hand lesson sounds every note the learner plays.

### Thesis and paper
- §3.6 says "Lessons 1–3 are straight; Lesson 4 introduces swing", and §3.9's table has Timing as L2 (left hand) and Swing as L4. With
  this lesson at position 3 and ADR-0069's order, that text is out of date; the student has said thesis conflicts are acceptable while
  lesson content is being validated. **`CLAUDE.md` is deliberately not edited** and Chapters 4 and 6 need matching wording if it stays.
- The lesson is right-hand-only with system-played changes, which is what §3.9 already says for L4 ("Right … C Ionian").
- **No open source for the content was found.** Searched for open lesson data (see the reply that accompanied this ADR): the Weimar Jazz
  Database has solo transcriptions with open access, and PDMX is a public-domain MusicXML corpus, but neither provides ii-V-I teaching
  patterns, and no openly licensed set of lick exercises turned up. The patterns above are the author's own, deliberately simple.

---

## ADR-0070 — The learner's hands are the Interaction SDK hand visuals; dimming and chord colour now drive those

**Date:** 2026-10-21 · **Status:** Accepted (student's call, from a failure they reported); the Unity assemblies compile in a scratch
project — **not run in the Editor, not seen on a headset, and not live until the setup menu item is run again and the scene saved** ·
**Milestone:** M5 · **Corrects:** ADR-0064 (which hands), ADR-0065 (the left hand's colour) · **Changes the thesis:** nothing in
`[THESIS]`; it puts the chord colour on the left hand's *outline*, which is where §3.10 says it belongs.

### What went wrong
ADR-0064 and ADR-0065 tinted "the hand", which I took to be the two `[BuildingBlock] Hand Tracking` objects my scene scan found.
**They are switched off** (`OVRMeshRenderer`, `OVRSkeletonRenderer` and the `SkinnedMeshRenderer` are disabled in the scene, in every
commit) and nothing enables them, so nothing was ever drawn from them. The student ran it on the headset and reported, twice, that
neither the dimming nor the colour showed. I had scanned `main.unity` for renderers and counted two; the real hands live inside a
**nested prefab** (`OVRHands` → `OVRHandVisualLeft` / `OVRHandVisualRight`), whose components never appear as blocks in the scene
file. The device log showed the component deciding correctly (chord held, hand in use, the right hue, the right hand dimmed at lesson
start) while nothing changed on screen, which located the fault at the last step; an old screenshot of the app (dark hands, light
outline) and a recursive scan of the prefabs found the real hands.

### What the real hands are
Each visual has two `SkinnedMeshRenderer`s (the OVR mesh path, and an OpenXR path that is inactive), both on `OculusHand.mat`, shader
**`Interaction/OculusHand`**: `_ColorTop` / `_ColorBottom` (dark grey), `_Opacity`, `_OutlineColor`, `_OutlineOpacity`. Each renderer has
a **`MaterialPropertyBlockEditor`** that **rewrites the renderer's block every frame** from its own lists — so anything written to the
renderer directly, even on the right one, would have been overwritten within a frame.

### What changed
- **`HandUseTint` writes into those editors' lists** (`ColorProperties` / `FloatProperties`, then `UpdateMaterialPropertyBlock()`), the
  way the SDK expects a hand to be coloured, for both editors of each hand. It is still the only writer of these properties.
- **Dimming** (the hand the lesson does not use): `_ColorTop`, `_ColorBottom` and `_OutlineColor` scaled to 0.4, `_Opacity` and
  `_OutlineOpacity` to 0.35 of their values, with a floor of 0.12 so it is never invisible. Amounts as in ADR-0064; still `[TUNABLE]`.
- **Chord colour** (the left hand, while it holds a pose and the lesson uses it): the **outline** takes the chord's colour in full and
  its opacity goes to 1; the **body** (dark grey) is tinted toward it at 0.45 of the colour's brightness, keeping its own top-to-bottom
  ratio. `Body Tint Brightness` is the new knob. The colours are ADR-0065's (`ChordHue`).
- **`Jazztures ▸ Hands ▸ Set Up Hand Dimming + Chord Colours (ADR-0064, ADR-0065, ADR-0070)`** now finds the property-block editors
  under a hand visual and assigns them. **It must be run again, and the scene saved**: the component's old renderer fields are gone.
  *Its first version searched for the prefab's own name (`OVRHandVisualLeft`) and found nothing: inside `OVRHands` the nested
  prefabs are renamed (`OVRLeftHandVisual`, `OVRRightHandVisual`), so the Console reported "found no MaterialPropertyBlockEditor". It
  now takes any editor whose nearest ancestor name contains "HandVisual", the side being "Left" or "Right" in that name, and its
  error message lists the parent chains of the editors it did find, so a third naming surprise is readable.*
- `HandUseTint`'s log (`adb logcat -d | grep HandUseTint`) now says at start how many editors it found per hand and the shader.

### `TODO(OPEN)` and not verified
- **Everything on a headset** — including whether 0.4 / 0.35 reads as "this hand is off", and whether a body tinted to 0.45 of a
  chord colour is told apart from the grey it starts as.
- **Dim and colour on the same hand** are worked out together (dim last), so a left hand in a right-hand lesson shows neither a colour
  nor a stale one; untested.
- **The Building Blocks hand objects are still in the scene, disabled.** They do nothing; removing them is the student's call.
- **`TensionColorDriver`'s own outline path** (`Left Hand Material Blocks`, which writes the same `_OutlineColor`) is left unassigned on
  purpose: two writers on one list would overwrite each other. `HandUseTint` is the one.

---

## ADR-0069 — EXPERIMENT: Chord tones is lesson 1 and Chords is lesson 2

**Date:** 2026-10-21 · **Status:** Accepted (student's call) **as an experiment** — data only, no code changed; **not run in the
Editor, not tried on a headset** · **Milestone:** M5 · **Changes the thesis: yes.** CLAUDE.md §3.9's lesson table numbers Chords
as L1, Timing as L2 and Chord tones as L3, and a `[THESIS]` change must be raised first (rule 3): it was, and the student chose to
try it. `CLAUDE.md` is deliberately **not** edited; **Chapters 4 and 6 will need matching wording** if this stays.

### The request
Feedback the student had received said chord tones should be discussed before chords (its source is not recorded in the repo —
`TODO(OPEN)`: note who said it and how it was worded, since "discuss first" and "teach first" are different asks). The student
had already moved Chord tones to id `L2` (and deleted the Timing asset), and then asked to try swapping the order.

### What changed — data only
- **`LessonCatalog`**: Session 1 now lists Chord tones, then Chords. The menu numbers lessons by catalog position (ADR-0061), so
  it reads `1. Chord tones`, `2. Chords`, and *Next lesson* after Chord tones leads to Chords.
- **Ids and files follow the student's convention (id = position):** Chord tones `L1` (`Lesson_L1_ChordTones.asset`), Chords `L2`
  (`Lesson_L2_Chords.asset`). Renamed with their `.meta` files, so the guids — what the catalog and the scene use — are unchanged.
- It works without code because **neither lesson needs the other**: Chord tones selects its own chords (ADR-0063), so a learner who
  has never made a chord shape can play it; Chords needs nothing from the notes.
- **The wording was left alone at first** (the student has been editing it, and it is theirs). The copy below was then drafted,
  approved and **applied the same day** — see "Copy for the new order".

### Why it may be better, and what it sets up
- The learner plays the notes first and meets the names (Dm7, G7, Cmaj7) second, which fits the zero-vocabulary rule (§1.5).
- The chord colours (ADR-0065) are seen first on the *notes* — the letters coming down the lanes — and then on the *left hand* when it
  holds the same chord: the colour of what you played is the colour of what you now hold.
- The ii-V-I story can be told in notes already under the fingers (D and F shared by ii and V, G and B by V and I; F falls to E, B
  rises to C) instead of as three names to memorise.

### Copy for the new order (applied)
The old wording used "chord" before it had been explained (Chord tones) and re-taught what Chords' learner now already knew. Changed:
- **Chord tones, now first:** a new first slide defines both words ("A chord is several notes played together. The notes that
  belong to a chord are called chord tones.", 8 s); slide 1 now says "…plays chord tones"; the old fifth slide ("The chords play on
  their own…") became "A chord plays underneath on its own. Time your finger curls to hit the notes."; the closing line now confirms
  the term instead of introducing it ("Every note you played was a chord tone: it belongs to the chord underneath."); and the menu
  line no longer uses "chord" (the title carries the term). The discussion is 38 s, up from 30.
- **Chords, now second:** three slides (18 s, down from 23) that build on the first lesson instead of introducing chords ("You've
  already played the notes of chords. Now you'll play whole chords with your left hand." / how to play one / the get-ready-tension-home
  story); "root note" and "building blocks of harmony" dropped. Its cues for the second and third chords had **no text** (beats 4
  and 8 were blank); all three now name the chord and its job — "Dm7: the getting-ready chord. Its notes are D, F, A, C." — so the
  notes just played are named again as the chord's. The closing line uses the same words ("Getting ready, tension, home. Musicians
  call this a ii-V-I."). If the blank cues were deliberate, they are the three lines to blank again.

### `TODO(OPEN)` and not verified
- **The first thing a novice now does is the right-hand finger-bend input** — the less reliable signal (DESIGN-SPACE D4; ADR-0068
  fixed one false-trigger cascade, unverified on a headset). The three left-hand shapes are the sturdier first impression.
- **The Timing lesson is gone** (the student deleted `Lesson_L2_Timing.asset`). It was the only lesson offering Test Yourself and
  the only one with a scored pulse, so **§3.7's onset scoring has no lesson to run in**; it was already absent from the menu
  (ADR-0061), but the content now no longer exists. §3.9 lists it as L2 `[THESIS]`.
- **Fix the order before real participants** (M8): changing it mid-study breaks cross-participant comparison. If order is to be a
  studied variable, the build must record which order each participant saw; ids no longer say, because they follow position.
- **`Jazztures ▸ Lessons ▸ Create Session 1 Starter Lessons` is stale** and would recreate the old names and ids (L1 Chords, L2
  Timing, L3 Chord tones) as new, duplicate assets, since nothing exists under those names any more; do not run it.

---

## ADR-0068 — A finger that has just pressed cannot hand the press on to the fingers it is dragging; the thumb must hold a press for two frames

**Date:** 2026-10-21 · **Status:** Accepted (student's call); the detector is headless-tested (832 tests, 16 new) and the Unity
assemblies compile in a scratch project, including with `JAZZTURES_DIAG` on — **not run in the Editor, not tried on a headset;
the evidence is a simulated hand, not a measured one** · **Milestone:** M5 · **Extends:** ADR-0050 (the finger-bend input) ·
**Changes the thesis:** nothing in `[THESIS]`. As ADR-0050 says, finger bends are an exploration on `explore/finger-bend-keys`
that the co-design record may have rejected; this makes that experiment behave better, it does not argue for it.

### The report
"The middle, ring and pinky fingers seem to accidentally activate one another, and the thumb occasionally gets accidentally
activated."

### What was wrong — the neighbours (a hole in ADR-0050's isolation rule, reproduced by test)
`FingerBendDetector` presses a finger only if it rose `IsolationMargin` further than every other finger — but **leaves out
any finger already held down**, so that legato works (the next finger pressing while the last is still down). The held finger
is exactly the one dragging its neighbours along, a few frames behind and a little less (tendon coupling). So: the middle
presses; from the next frame it is ignored; the ring is still rising and now only has to beat the pinky, which rises less; the
ring presses; then the ring is ignored and the pinky only has to beat everything left. **One bend, three notes** — middle,
then ring, then pinky, which is the chain described.

Reproduced first against the unmodified detector (six tests failed), then with a stress test of 300 simulated strokes: the
bent finger drags the others by amounts that fall off with distance (adjacent 0.6–0.8 of its rise, 1–2 frames late), with
tracking noise on every finger:

| | meant finger sounded | neighbours that sounded |
|---|---|---|
| old rule, default thresholds (press 0.14, margin 0.06) | 300 / 300 | **545** |
| old rule, the scene asset's (press 0.20, margin 0.10) | 300 / 300 | **142** |
| with this change, either | 300 / 300 | **0** |

These are numbers from a model I wrote, not a hand. They show the mechanism is real and severe enough to match the report;
they do not say how often it happens on yours.

### What changed
- **The drag veto.** A held finger that is *still moving* — its peak advanced by 0.04 or more within `CouplingSeconds` (0.10 s) —
  is consulted after all. A finger that has not out-risen it by the isolation margin is **dragged, not played**. The held
  finger stops being consulted once it has been still for that long, so a real second note after the first still sounds.
- **A dragged finger's new level becomes its rest.** I first latched it until it relaxed; a test I wrote for legato showed that was
  wrong: a ring dragged by a middle that is still held *cannot* relax, so it would be locked out for the whole note. Re-basing
  means it cannot fire late once the driver stops, and a deliberate press afterwards needs a fresh rise from where it was dragged to.
- **The thumb must qualify on 2 frames running** (`ThumbConfirmFrames`; ≈ 11–16 ms, thumb only). A single-frame spike — the
  thumb flickers in confidence and folds out of sight — is not a press a thumb can make. The veto also covers a thumb dragged
  along by another finger's stroke, as it covers any finger.
- **Both are `MelodyConfig` fields** (`Bend Coupling Seconds`, `Bend Thumb Confirm Frames`; `0` and `1` switch them off, to
  compare). The diagnostic log now says *which finger dragged which* ("Ring rose 0.25 but Middle was still moving - dragged
  along, ignored"). `Docs/CALIBRATION.md` has the new rows, and its recorded scene values for the press level and isolation margin
  (0.15, 0.05) are corrected to what the asset actually holds (0.20, 0.10).

### Behaviour that changed on purpose
A second finger that begins to bend while the first is still moving, or within 0.1 s of it stopping, will not sound unless it
out-rises the first. That drops very fast different-finger runs (roughly under 0.15–0.25 s between notes). Every lesson is
slower than that (the quickest is one note a beat, 750 ms at 80 BPM) but free play can be quicker. The existing legato test
modelled a second finger starting the instant the first stopped; it now leaves a 0.2 s pause, which is the same rule stated
honestly, not a loosened one.

### `TODO(OPEN)` and not verified
- **Everything on a headset.** The coupling ratios, the lag, the 0.04 "moving" level and the 0.10 s window are mine. Real
  coupling differs by person and by finger; if neighbours still sound, raise `Bend Coupling Seconds`, then the isolation margin; if
  quick notes vanish, lower it.
- **The thumb is only partly addressed.** Isolated spikes and being dragged are covered. A thumb reading that is *wrong for
  several frames* — a long occlusion, a jump that holds — still presses. I did not change the thumb's press level or its spans
  (ADR-0050, revisions 2–3, which were tuned by feel), because there is no data to say which way. With `JAZZTURES_DIAG` the thumb's
  press line prints its palm-side offset in millimetres; a few of those from the occasional false presses would say whether it is
  curl or the offset.
- **The new fields are not in `MelodyConfig.asset`** yet, so the defaults apply; the Editor writes them in when the asset is next
  saved, and they show in the Inspector.
- **Detection, not recovery:** a stroke so coupled that the neighbour rose almost as far as the driver is still "say nothing"
  (ADR-0050's isolation rule), not guessed.

---

## ADR-0067 — The strike plate is hidden on the menu, and appears with a lesson's interface

**Date:** 2026-10-21 · **Status:** Accepted (student's call); the Unity assemblies compile in a scratch project — **not run in
the Editor, not seen on a headset** · **Milestone:** M5 · **Extends:** ADR-0039/0040/0048 (the strike plate), ADR-0052 (the
ghost strikes it), ADR-0066 (the interface waits for the discussion) · **Changes the thesis:** nothing in `[THESIS]`. §3.10 asks
for visual restraint; a plate floating in the menu, where nothing is struck on it, spends attention on nothing.

### The request
"I want the strike plane to disappear on the menu anyway." The plate (`ChordStrikeTarget`) had been a permanent scene object,
drawn on the menu, in the tutorial and in every mode — ADR-0066 left it out for that reason and asked.

### What is built
- **The plate is shown only while the performance root is enabled** — a lesson or free play — and **not through a lesson's opening
  discussion.** So it is gone on the menu, the post-run screen and the tutorial, and it appears with the ghost hand and the rest of
  the interface (ADR-0066), or at once in free play. The root decides, each frame, from `LessonRunner.InDiscussion` (new); its
  `OnDisable` hides the plate when the flow controller switches the root off, and its `Awake` starts it hidden.
- **It fades through its own colour** (0.25 s, `ChordStrikeTarget ▸ Fade Seconds`), the plate material being alpha-blended. The
  renderer stays enabled, so `TopCenterWorld` — which the ghost hand reads from the renderer's live bounds to know where to land —
  is never stale.
- **A hidden plate senses nothing**, like an untracked hand: no touch and no speed, so a hand passing through the empty space where
  it was cannot sound a chord. That includes the discussion, when a learner who has not been shown the plate yet has no reason to
  strike it.

### `TODO(OPEN)` and not verified
- **Everything on a headset:** whether a plate that fades in as the lesson starts is noticed, or whether a learner reaches for the
  place it will be before it appears. If the discussion should show it (so its place is known), it is one condition in
  `PerformanceCompositionRoot.Update`.
- **Free play** shows the plate as before; so does every lesson mode once the discussion is over, including Test Yourself, where
  the ghost is hidden but the learner still strikes it.
- **Chapter 4 / Chapter 6:** if the plate is described as always present, it now differs. Not touched (rule 7).

---

## ADR-0066 — A lesson's cues and interface start when its opening discussion is over, not under it

**Date:** 2026-10-21 · **Status:** Accepted (student's call); the pacing is headless-tested (816 tests, 9 new) and the Unity
assemblies compile in a scratch project — **not run in the Editor, not tried on a headset** · **Milestone:** M5 ·
**Extends:** ADR-0056 (the lead-in), ADR-0061/ADR-0065 (the concept slides, and the lanes' explanation) · **Changes the thesis:**
nothing in `[THESIS]`. §3.9's "plain-language theory alongside exercises" is the discussion; this stops the exercise talking over it.

### The report
"The cue track for lesson 1 - chords seems broken. It introduces the Dm7 chord immediately. Also, can we only make the
interface pop up after the short discussion on chords?"

### What was wrong (found in `LessonRunner`, not seen on a headset)
- **The cue clock was held at beat 0 for the whole discussion.** The runner advances cues with
  `AdvanceTo(beat < 0 ? 0 : beat)`, and the phrase beat is negative through the discussion, the pre-roll and the lead-in. The cue
  at beat 0 — "The first chord is D minor seventh (Dmin7)" — therefore fired on the very first frame, in the same frame as slide
  one, and replaced it. The same would have been true of any cue triggered by "the phrase started".
- **"The discussion is still going" was false a slide too early.** The flag meant "a slide is left to show", which went false the
  frame after the *last* slide appeared, not when it had finished. Anything that waited on it started under the final slide.
- **The ghost frame was published from the first frame**, so the ghost left hand stood in the lesson's opening pose while the
  chords were being explained, and in lesson 3 the note lanes and their letters were up during the slide that explains them.

### What changed
- **`Core/Lessons/ConceptSlidePlayer`** (tested): paces the slides and says the discussion is over only once the **last slide has
  held for its time**. It replaces the runner's three loose fields. A 0-second slide (the single plain explanation) is shown and
  delays nothing, as before.
- **`LessonRunner` holds three things back until then:** the cues (so beat-0 cues fire as the lead-in opens, which is where they
  were always meant to be), the "phrase started" signal (now raised from `Update`, once, at that moment), and the ghost frame — a
  *hidden* frame is published meanwhile, so the ghost hands, the lit targets and the lanes are not drawn. The words are the only
  thing on screen during the discussion; the interface appears with the lead-in.

### `TODO(OPEN)` and not verified
- ~~**The strike plate is not covered.** It is a permanent scene object, drawn on the menu and in every mode.~~ **Covered by
  ADR-0067:** the plate is now hidden on the menu, and through the discussion, and appears with the rest of the interface.
- **The learner's own hand labels are not covered:** they appear when a pose is held, which only the learner decides.
- **The interface appears as the lead-in opens** (the pre-roll plus four beats before the first chord, ~3 s at 80 BPM), not at beat 0:
  the ghost "waits in the opening pose" (ADR-0056) and the first cue is read as it arrives. If it should appear later, that is the
  condition in `LessonRunner.Update`.
- **Everything on a headset**, as with all of M5.
- **The slides still replay at the start of every mode run** (ADR-0061), so Try Yourself repeats the discussion; this fix makes it
  clean each time, it does not shorten it.

---

## ADR-0065 — The left hand wears the colour of the chord it holds, and the coming notes wear the colour of theirs

> **Corrected by ADR-0070 (hand only).** The left-hand half targeted the wrong renderers, so it never showed; it now drives the real
> hand's outline and body. The coming letters on the note lanes were never affected.

**Date:** 2026-10-21 · **Status:** Accepted (student's call); the Unity assemblies compile in a scratch project —
**not run in the Editor, not seen on a headset, and not live until the setup menu item is run (see ADR-0064, "Wiring")** ·
**Milestone:** M5 · **Extends:** ADR-0017 (the tension palette), ADR-0044 (the right hand stopped following the chord's
colour), ADR-0060 (the note lanes), ADR-0064 (the hand component) · **Changes the thesis:** nothing in `[THESIS]`. §3.10 says
the tension arc is "cool/neutral for ii, warm/saturated for V, resolved/settled for I", carried by the left hand; this is that,
reaching the hand for the first time. It adds no element — it colours two that are already there — but §3.10's justify-against-
Chapter-4 applies to the lanes' letters, which now carry colour.

### The request
"When a left hand pose is activated, give it a color like we used to do with the right hand. Make the incoming chord tones
match that as well." The right-hand targets used to take the chord's colour (ADR-0017) until ADR-0044 fixed them to one
neutral colour and wrote the note on them instead. The lanes' letters are the incoming chord tones.

### What is built
- **The left hand takes the held chord's colour** (`HandUseTint`, extended; the one component that writes the hand's colour
  properties, so the dimming of ADR-0064 and this are worked out together and cannot overwrite each other). From the moment a
  pose is selected until it is let go it eases toward the chord's colour over 0.35 s and back. Body (`_ColorTop`/`_ColorBottom`)
  and rim glow (`_ColorPrimary`), by the shader's own knobs — the bottom keeping the material's ratio to the top. The colour comes
  from `TensionColorDriver.Current` (it already eases on every chord change), so a chord change slides from one colour to the next
  and the hand does not flash through grey. After a release it fades out of the last hue, not through the palette's neutral.
- **Only while the learner's own left hand is in use and something is being performed.** A right-hand lesson selects its chords
  itself (ADR-0063) and its left hand is dimmed (ADR-0064): colouring it would say it had a part. On the menu the engine still
  remembers the last chord but nothing reads the hand, so it is not coloured there.
- **The coming letters on the lanes wear the colour of the chord they belong to** — the same hue the hand takes for that
  pose. Per letter, not the current chord's: the lanes look several beats ahead, so the next chord's notes can be on their way
  while this chord's are still arriving, and a V's letters are already burnt orange before the V sounds (the tension arc, in
  advance). The gold flash on arrival is unchanged and the same in every chord. `NoteLanes` takes the `TensionPalette` as an
  optional field; `MelodyConfig ▸ Note lanes ▸ Lane Letter Chord Tint` (1) says how far.
- **The colour is the palette's hue at full brightness** (`ChordHue`): sage → a light yellow-green (0.88, 1, 0.70), burnt
  sienna → orange (1, 0.45, 0.24), warm purple → violet (0.87, 0.60, 1). The palette's values are mid-tones made for a translucent
  outline and its alpha is a strength for that outline; on a hand body and on small text over a dark sky they would be dull and
  hard to read. Hue and saturation are kept. This is a departure from the palette's literal values, in one place, and the
  palette is not edited.
- **Not touched:** the right hand (it plays notes, not chords), the letters over the learner's own fingertips (free play, where
  there are no lanes), the lane lines, the ghost hands, the chord-name label.

### `TODO(OPEN)` and not verified
- **Everything on a headset:** whether the three colours are told apart on the hand and at letter size; whether sage reads as
  "preparation" once brightened (ADR-0017 already compromised on it); whether 0.85 of the way on the hand is too much or too little;
  whether coloured letters are as legible as the plain ones against the sunrise sky.
- **Colour is a redundant channel here, and must stay one** (§3.10). A colour-blind learner has the letter's name, the chord's name
  over the hand and the sound. ii (yellow-green) and V (orange) are the pair most likely to be confused; if testers mix them, that
  is a palette question for M8, not a bug.
- **A coloured hand and the tracking-loss cue** (ADR-0064's open item): when the §3.5 desaturation is built, it must not be
  mistakable for a hand that has simply let go of its pose.
- **Needs the setup menu item** (it wires the driver into the hand component and the palette into the lanes), then a saved scene.
  The palette must be assigned on the `TensionColorDriver` (it is, in this scene).
- **Reported on a headset: the left hand's colour does not change when it forms a chord (2026-10-21).** Checked, and *not* the
  cause: the saved scene has `HandUseTint` with both hand renderers, the performance root and the `TensionColorDriver` all
  assigned; the driver, the bridge and the labels share one chord-changed channel asset; the installed build (22:18) is newer
  than the colour code; the poses (`ii`, `V`, `I`) are recognised on the device; no other hand mesh is drawn (`OVRHands` holds
  only data sources); and no `HandUseTint` exception reached the log. Not found by reading the update path either. **Cause not
  yet known.** `HandUseTint` now logs, once at start and whenever a hand's tint or dimming flips, the three things the colour
  depends on (chord held, hand in use, performing) — read with `adb logcat -d | grep HandUseTint`. Until a run with that
  build, which of "the chord never arrives", "the lesson says the hand is not in use" and "the colour is applied but does not
  show" is true is unknown. (`HandPoseFrameLogger` was writing several lines per frame to logcat — its "on change" test never
  held, because the frame carries live speeds — and overwrote anything older than a few seconds. The student switched it off, the
  poses being reliably detected: it is now opt-in, `Log Poses` on the component, off by default, so a bring-up session can tick it.)
- **Chapter 4 / Chapter 6:** if either describes the right hand as carrying the chord colour, or the lanes' letters as one
  colour, it now differs. Not touched (rule 7).

---

## ADR-0064 — The hand a lesson does not use is drawn darker and more transparent

> **Corrected by ADR-0070.** This targeted the two Building Blocks hand meshes in the scene, which are switched off and were never
> drawn; the learner's real hands are the Interaction SDK's `OVRHandVisual`s, with a different shader. The *idea* and the amounts
> stand; the renderers, the shader properties and the wiring below are superseded.

**Date:** 2026-10-21 · **Status:** Accepted (student's call); the Unity assemblies compile in a scratch project —
**not run in the Editor, not seen on a headset, and not yet wired into the scene (see "Wiring")** · **Milestone:** M5 ·
**Extends:** ADR-0063 (a hand the lesson does not use is silent and bare) · **Changes the thesis:** nothing in `[THESIS]`.
§3.10 asks every visual change to be justified against Chapter 4; this adds no element, it takes emphasis *off* the hand the
lesson does not use, so the hand that is playing is the one that stands out.

### The request
"Let's make disabled hands darker and more transparent." After ADR-0063 the unused hand makes no sound and carries no
label; this is the same idea for how it is drawn.

### What is built
- **`Presentation/HandUseTint`** asks `IHandsInUse` (ADR-0063; the performance root answers) which hands the running lesson
  uses and eases the other one dim over 0.35 s. No lesson running, free play, the menu and the tutorial are unchanged, and a
  hand that has never been dimmed is never written to.
- **Two of the hand shader's own knobs, not a new material.** The scene's hands are the Building Blocks hand
  (`Oculus Sample/Alpha Hand Outline (SBB)`): its colour is `lerp(_ColorTop, _ColorBottom, fresnel)` times a lighting term, so
  scaling `_ColorTop` / `_ColorBottom` is a plain darkening; and it blends on `_HandAlpha`. Both go in a
  `MaterialPropertyBlock` on the hand's skinned-mesh renderer, **merged with whatever block is already there** rather than
  replacing it. A material instance was rejected: `OVRMeshRenderer` swaps the renderer's whole material for a system gesture
  and would swap the instance away. A block survives that, and leaves the shared material asset untouched.
- **Never invisible.** The shader draws *nothing* when `_HandAlpha` is below `_MinVisibleAlpha` (0.164 on this material), so the
  dimmed opacity is held a little above it. A learner who cannot see their hand cannot tell whether it is tracked (§3.5).
- **`[TUNABLE]`, unmeasured:** brightness 0.4, opacity 0.35, fade 0.35 s — Inspector fields on the component.

### Wiring — one step for you
**`Jazztures ▸ Hands ▸ Set Up Hand Dimming + Chord Colours (ADR-0064, ADR-0065)`** (named for the dimming alone when this was
written; ADR-0065 extends it), then save the scene
(`App/Editor/HandTintSetup`: idempotent, undoable, adds the component beside the performance root and points it at the two OVR
hand renderers). It is not wired already because the open Editor cannot be driven from here: the Unity CLI reports no
connected Editor, since the project does not have the `com.unity.pipeline` package, and a new package is a dependency to ask
about (rule 5), not to install. Until the menu item is run, the hands look as they did.

### `TODO(OPEN)` and not verified
- **Everything on a headset:** whether 0.4 / 0.35 reads as "this hand is off" and not as "I have lost this hand"; whether a
  see-through hand is comfortable; whether the depth pre-pass the shader draws (it writes depth so the top layer wins) hides a
  transparent label or lane that happens to sit behind the dimmed hand.
- **It must stay distinguishable from the tracking-loss cue** (§3.5: a non-modal *desaturation* after 200 ms of loss). That cue
  is configured (`GestureThresholdsConfig`) and documented on `TrackingQualityChannel`, but nothing draws it yet. When it is
  built, darker-and-transparent here and desaturated there must not be mistakable for each other.
- **Observed on the way:** `TensionColorDriver`'s left-hand outline writes `_OutlineColor` / `_OutlineGlowColor`
  through `MaterialPropertyBlockEditor`s, but its list is empty in the scene and the current hand shader has no outline
  properties, so the tension colour on the hand did not reach it. **ADR-0065 gives the hand that colour through this
  component instead** (the body and rim, which this shader does have), which is why this component is the only one that
  writes those properties. The driver's own outline path is still there and still does nothing; if it is wired up through
  `MaterialPropertyBlockEditor`, it and this both write the renderer's one property block and whichever writes last would
  replace the other's values until its next update.
- **The ghost hands are not dimmed** — they have their own material, and they are the system's, not the learner's.
- **Chapter 4 / Chapter 6:** if the hand rendering is described there it now differs for one-hand lessons. Not touched (rule 7).

---

## ADR-0063 — A hand the lesson does not use is silent and bare, and a right-hand lesson selects its own chords in every mode

**Date:** 2026-10-21 · **Status:** Accepted (student's call); the Core part is headless-tested (807 tests, 15 new) and the
Unity assemblies compile in a scratch project — **not run in the Editor, not tried on a headset** · **Milestone:** M5 ·
**Extends:** ADR-0043 decision 3 (scripted harmony), ADR-0054 (the chord-name label) · **Changes the thesis:** nothing in
`[THESIS]`. §3.8 ("the mode gates the note sink, not the gesture recogniser; silent-but-logged is a first-class state") is
kept, and is the reason the silence is made in the sink (decision 1).

### The report
In the chord-tones lesson the chords should come on by themselves and the left hand should not be able to sound them; and
where a lesson uses one hand, no label should hang over the hand that is switched off.

### What was wrong (found by reading the code, not by watching it happen)
- **The lesson's chords were selected for the learner only in Try Yourself and Test Yourself.** In Watch and Listen the system
  played the chords and the melody but selected nothing, so the letters on the learner's fingers and the melody's tones
  followed whatever the idle left hand happened to be doing.
- **The left hand still drove the harmony in a right-hand lesson.** A pose it held replaced the lesson's chord until the next
  scripted change (the melody's tones going to the wrong chord mid-phrase), and a strike sounded the chord: nothing
  anywhere asked which hands the lesson used, and in Try Yourself the audio gate is open for any lesson that is not
  left-hand-only (`UpdateGestureCorrectness`).
- **The chord-name label (ADR-0054) followed the whole performance root**, so it hung over the left hand in every lesson.
  The right hand's letters and lanes were already right: they follow the finger-bend input, which the flow controller
  switches on only for a lesson that uses the right hand.

### Decisions
1. **Silence is made in the sink.** `ModeGatedNoteSink.SetActiveHands` (Core, tested): the learner's notes from a hand the
   lesson does not use — the chord voicing for the left, the melody for the right — never reach the audible sink, in any
   mode, including a chord already ringing when the hand is switched off, and they sound again if it is switched back on.
   They still reach the unconditional sink, so the strike is **seen, recognised and logged, only not heard** (§3.8). The
   system's own playback (Accompaniment) is never the learner's and is never gated. The runner sets it per phase from the
   plan and puts it back to both hands in `StopLesson`, as it does the mode.
2. **A right-hand lesson selects its chords in every mode.** `LessonRunner` starts the scripted harmony whenever the plan has
   no left hand (`ActiveHands.SystemSelectsChords()`, now the one place that rule is written; `ModePolicy` uses it too), not
   only where it also plays them. It selects at the instant the chords sound (the playback lead), so what is heard, labelled
   and playable agree. **Starting a script now releases whatever was selected before** (`ScriptedHarmony.Start`), so a pose held
   on the way in, or the last lesson's last chord, is not left sitting through the lead-in.
3. **While a script is selecting, the left hand's pose is not taken as the selection** (`PerformanceCompositionRoot`). It is
   still recognised and published. When the script ends, the interpreter's confirmed pose becomes the selection again — a
   pose held through the lesson raised no change to say so, so it is read, not waited for. This also applies to the timing
   drill's fixed chord (ADR-0043), which is not reachable from the menu (ADR-0051).
4. **The chord-name label asks whether its hand is in use** (`IHandsInUse`, Presentation; implemented by the performance
   root, which the label already follows — no scene change). No lesson running (free play) means both hands.
5. **Free play, the menu and every both-hands lesson are unchanged.** Both-hands is the default at every layer.

### `TODO(OPEN)` and not verified
- **The ghost left hand is untouched.** In a right-hand lesson it still appears and strikes the chords the system plays
  (ADR-0052), with the chord's name over it (ADR-0058). That is the system's demonstration, not the learner's disabled hand,
  so I read the ask as not covering it — but it is the likeliest thing to look like a leftover. If it should go, it is a
  one-line change in how the runner builds the strike schedule.
- **The left hand's strikes in a right-hand lesson are still logged as chord notes** (Harmony channel, source Left, on the
  scripted chord). That is what "silent-but-logged" means, but an analysis script counting left-hand strikes must filter by
  the lesson's active hands or it will count incidental ones (M7).
- **Everything on a headset:** whether an incidental left-hand motion still disturbs the right hand's tracking or reads as the
  learner doing something; whether Watch and Listen on the chord-tones lesson now shows the right letters (it should — the
  chord is now selected there).
- **Chapter 6:** if it says the left hand sounds in a right-hand lesson, or describes Try Yourself as audible for both hands,
  it now differs. Not touched (rule 7).

---

## ADR-0062 — The menu keeps a standing reminder of its controls: right hand, flick to browse, strike to choose

**Date:** 2026-10-21 · **Status:** Accepted (student's call); the Unity assemblies compile in a scratch project —
**not run in the Editor, not seen on a headset** · **Milestone:** M5 · **Extends:** ADR-0051 (the menu), ADR-0053 (the
tutorial) · **Changes the thesis:** nothing in `[THESIS]`. **It adds a visual element, so §3.10 applies: the student must be
able to justify it against Chapter 4**, and if Chapter 4 argues the menu carries no standing text this contradicts it.

### The report
Testers still struggled to navigate and to use the gestures, after the tutorial. The student asked for permanent
instructions on the menu — flick the right hand left or right to switch cards, strike to select — and left the form
open: "figure out better wording or maybe use icons".

### What is built
- **One dim row under the cards, on every menu screen:** `Right hand  ◀ ▶ Flick to browse  ▼ Strike to choose`
  (`Presentation/MenuControlsHint`, owned by `MenuView`; nothing to wire in the scene). It is static: nothing in it
  animates when a gesture is recognised — the cards' own pulse and bump already answer that (ADR-0051).
- **Arrows carry direction, words carry the verb.** The motion's direction (sideways, down) is what a learner cannot
  guess from a word, and what an icon says at a glance, so the words are three or four: "Flick to browse", "Strike to
  choose". Neither is the sole carrier (§3.10).
- **The arrows are drawn as geometry (a triangle mesh), not font glyphs.** Nothing in the project draws an arrow in a
  `TextMesh`, so there is no evidence the built-in font has the glyphs on the headset; a missing one is a blank box that
  cannot be seen before a build. This is a decision, not a preference — if the font is later shown to carry them, the
  words-only wording could use them.
- **Wording follows the tutorial's** ("flick", "strike", ADR-0053) — a learner was just taught those two words, and a
  third ("swipe") would make the reminder a new thing to learn. **"Right hand" is named once** because only the right hand
  is listened to (ADR-0051): a learner with both hands up who flicks the left would otherwise conclude the menu is broken.
  The flick's *direction* is not spelled out as "left = next": it is the swipe convention, the row follows the hand, and the
  arrows show both ways.
- **Hidden while the tutorial runs** (it teaches the same two motions, and puts its own line where the hint sits), **on
  when it completes**, and gone with the menu while a lesson runs. A learner with the tutorial switched off in `MenuConfig`
  has it from the first frame. It fades in with the menu and holds steady through a screen change.
- **The fist is not on it.** The Back cards cover going back; the fist's other job, leaving a lesson, happens where no
  menu is drawn. Offering it would be a third item on the one line meant to be glanced at.
- **Look and place** are `MenuView ▸ Controls hint` fields, `[TUNABLE]` and unmeasured: type at 0.55 of the menu's
  (about 1.8° at 0.8 m), 0.215 m below the cards, 80 % opacity, arrows in the focus colour, the words in the idle colour, the
  hand in the title's dim. The row is about 0.8 m wide (≈ 53°), a little wider than a full footer.

### `TODO(OPEN)` and not verified
- **Everything on a headset:** whether the words read at 1.8°; whether the row is clear of a three-line footer (the
  post-run screen) and of the fist's hold bar; whether the arrows read as directions and not as a media control.
- **Spacing is estimated, not measured** — each piece is placed from its character count (0.55 em a character), with gaps
  well wider than the error, so a long edit to the words cannot overlap the next piece but the row may sit off-centre.
- **Whether "Flick" and "Strike" mean anything to someone who skipped or forgot the tutorial** is untested. The student
  said "swipe"; the tutorial says "flick". If testers still stumble, the words are the first thing to change (they are
  Inspector fields, no code).
- **Chapter 4 / Chapter 6:** if the menu's chrome is described there it now differs. Not touched (rule 7).

---

## ADR-0061 — The menu says what each lesson and mode is, numbers them, and L1 and L3 play four times through; L3 explains the note lanes

**Date:** 2026-10-21 · **Status:** Accepted (student's call); the Unity assemblies compile in a scratch project, the two
lesson assets' beats and cue markers were checked by script, the 792 headless tests pass — **not run in the Editor, not
tried on a headset** · **Milestone:** M5 ·
**Extends:** ADR-0051 (the menu), ADR-0060 (the lanes, on `explore/finger-bend-keys`) · **Changes the thesis:** nothing in
`[THESIS]`. The note-lane wording describes an exploration that is itself not a thesis decision (ADR-0060): if the lanes
are dropped, L3's slides and captions go with them.

### The report
Testers still struggled to find their way round the system and to use the gestures. Three asks: say what lessons and modes
are in the menu, number them, play the chord and chord-tone lessons four times through instead of twice, and explain the
note lanes, which nothing did.

### What changed
- **Description under the row.** The highlighted card's description shows in the menu's footer (the line the post-run screen
  already used), so it changes as the learner flicks. A lesson brings its own, `LessonDefinition ▸ Menu Description` (data,
  not code, §3.9); the five modes, Free play and Tempo have fixed wording in `LessonFlowController`. Try Yourself is
  worded for the hand the learner plays (ADR-0043 decision 3). Kept to about 80 characters: two lines, which stays clear of
  the tutorial's line below the menu (ADR-0053). The post-run screen is unchanged. No text on Back.
- **Numbers.** `1. Chords`, `2. Chord tones`; `1. Watch and listen`, `2. Try yourself`. A lesson is numbered by its place in
  the catalog and a mode by its place in the lesson's mode list — the order they are meant to be worked through — not by the
  lesson id. **L2 (Timing) is not in the catalog**, so Chord tones reads as lesson 2 although the thesis calls it L3; if L2
  returns it takes number 2 and Chord tones becomes 3. Free play, Tempo, Back and the post-run cards are not numbered.
  *(Overtaken by ADR-0069: the Timing asset has been deleted, Chord tones is now lesson 1 and Chords lesson 2, and the ids match.)*
- **Four passes.** L1 (ii-V-I) and L3 (chord tones) each play the pass four times: the first, then three more. L1 was
  twice; **L3 was once** — it had no repeat at all — so this is the biggest change to L3. Each repeat has a caption ("Once
  more." / "Again." / "One last time.") at a marker, hidden two beats in, as L1's was. At 80 BPM L1 is ~33 s of phrase
  and L3 ~69 s, plus the concept slides.
- **L3 explains the lanes.** Five concept slides (what a lane is, that letters travel down it, that a letter reaching the
  fingertip is the cue to curl that finger, that the letters change with the chord), a caption on the first notes ("Thumb
  first, then each finger across."), one on the first chord change ("The chord changed, so the letters changed. Same
  fingers, new notes."), and a closing line that names chord tones. The placeholder `_conceptExplanation` is cleared; it
  was ignored once slides exist.

### `[TUNABLE]` and not verified
- **Four passes is a user-feedback default, not a finding.** It is the same kind of number as every `[TUNABLE]` here: it
  needs a pilot (M8) before it appears in Chapter 6. 69 s of Watch and Listen on L3 may be long.
- **Concept slides replay at the start of every mode run** (the menu starts a lesson one mode at a time, so every run is
  phase 0): ~23 s for L1, ~33 s for L3, again before Try Yourself. Pre-existing; this makes L3's longer.
- **Everything on a headset:** whether two lines of footer read at that size; the fist's hold bar is drawn through the
  footer's zone while a fist closes (it was already, on the post-run screen).
- **Chapter 4 / Chapter 6:** if the menu or the lesson length is described there, it now differs. Not touched (rule 7).

---

## ADR-0060 — EXPLORATION (branch `explore/finger-bend-keys`): the right-hand cue as lanes out of the fingertips

**Date:** 2026-10-20 · **Status:** **Proposed — exploration only.** Lives on `explore/finger-bend-keys`; it is not a thesis
decision and must not be merged as one. The schedule is headless-tested (787 tests), every Unity assembly compiles in a
scratch project — **not run in the Editor, not seen on a headset** · **Milestone:** M5 · **Replaces, for the experiment:**
ADR-0055 (ghost finger press), ADR-0058 (ghost letters), ADR-0059 (next-finger preview) as the lesson's right-hand cue ·
**Changes the thesis:** it *would* — see below. `CLAUDE.md` is deliberately **not** edited here.

### The report and the tension
After ADR-0059 the student found Try Yourself on the right hand "still very noisy" and proposed "emulating piano roll
through lanes that shoot out from the user's fingers with the letters coming in".

CLAUDE.md §1.3 `[THESIS]` records the earlier three-lane design as **cut** after co-design feedback that it "added novice
complexity and made the system feel like a rhythm game", and says not to reintroduce lanes. Those lanes were the three keys
(C, B♭, A♭), not note lanes, so this is not the same feature - but a lane of arriving notes is the idiom the feedback was
about, and §3.10 asks every added element to be justified against Chapter 4. The student chose to build it as an exploration
(asked, rule 3), on the premise that it is only a net reduction in noise if it *replaces* the ghost right hand. **Before it
could ship**, someone must cite what in the co-design findings distinguishes a lane of arriving letters from the rhythm-game
look that was rejected.

### What is built
- **`Presentation/NoteLanes`** - five faint lanes run out in front of the learner's own fingertips, along the heading (§3.4);
  each note of the phrase is a letter that travels in along its finger's lane and **reaches the fingertip on the beat** -
  the cue to bend that finger. Lane length is a number of *beats* at a fixed distance per beat, so the spacing of the letters
  is the spacing of the notes in the score whatever the tempo. The note due next is at full strength, the ones behind it quiet
  (ADR-0059's fade); an arrived letter glows gold at the fingertip, lingers, and fades. A lane brightens while a note is on it.
  Lanes are not drawn while the hand is untracked (§3.5). Pooled lines and labels, nothing allocated per frame.
- **The ghost right hand is switched off** by the setup (below), not deleted, so the two can be compared on the headset.
  The letters over the learner's own fingertips (`FingerNoteLabels`) are **hidden while the lanes are up** - the arriving
  letter already names the note at that fingertip - and come back when no lesson is running (`Suppressed`). That hide is a
  default of mine, not the student's ask: it is one assignment in `NoteLanes.LateUpdate` if the student wants them kept.
- **Core:** `GhostPressSchedule` now exposes every press (`Count`, `OnsetAt`, `FingerAt`, `ChordAt`, `FirstAtOrAfter`) and
  `GhostFrame` carries it with `MelodyBeat` (the beat the notes are shown on - the ghost's less the playback lead), so the
  view measures the notes against the beat they sound on. The letter for a note names the chord *that note* belongs to.
- **`MelodyConfig ▸ Note lanes`** - look-ahead 3 beats, 0.2 m per beat (a 0.6 m lane), linger 0.4 beat, width 4 mm, idle /
  active opacity 0.10 / 0.45, letter hover 2 cm. All `[TUNABLE]`, unmeasured. The quiet letters use ADR-0059's
  `Ghost letters` values.
- **`Jazztures ▸ Experiments ▸ Use Note Lanes Instead of the Ghost Right Hand` / `Use the Ghost Right Hand Again`**
  (`Experiments/Editor/NoteLanesSetup`) - adds or removes the `Note Lanes` child under the finger-bend input and flips the
  ghost right hand. Needs `Use Finger-Bend Input` run first; re-running that switches the ghost back on. Undoable; save the scene.

### `TODO(OPEN)` and not verified
- **Everything on a headset:** whether the lanes read at arm's length; whether 3 beats at 0.2 m per beat is the right pace;
  whether the quiet letters behind the next one are still legible once perspective has shrunk them.
- **Lane anchoring** is each fingertip's tracked position, unsmoothed, with the lane running along the head's heading, not the
  finger's. Tracking jitter will move the lane's origin and the arrival point; smooth it if it shows.
- **Parallel lanes** a finger's width apart. Letters on different lanes are at different distances (different times), so they
  should not collide, but two notes close together on adjacent fingers may crowd. A slight fan is the fix.
- **Test Yourself** still hides the ghost (§3.8), so it shows no lanes either - the learner plays from memory, as before.
- **Watch and Listen** shows the lanes too; the letters arrive as the notes sound.
- **Chapter 4 / Chapter 6:** if the ghost right hand was described there, the paper now describes something the experiment
  does not show. Not touched (rule 7).

---

## ADR-0059 — The ghost right hand announces the next finger a beat ahead and fades the letters that aren't the cue

**Date:** 2026-10-20 · **Status:** Accepted (student's call); the schedule is headless-tested, the Unity assemblies compile
in a scratch project — **not run in the Editor, not tried on a headset** · **Milestone:** M5 · **Extends:** ADR-0055 (the
ghost's finger press), ADR-0058 (the ghost's letters) · **Changes the thesis:** nothing in `[THESIS]`. §3.10 asks that every
added visual element be justified against Chapter 4; this adds no element, only emphasis on one already there (below).

### The report
Try Yourself on the right hand is hard to follow: with a labelled hand and a labelled ghost for each of the learner's two
hands (ADR-0054, ADR-0058) there is a lot of text on screen, and the ghost finger only starts to move 0.25 beats (≈ 190 ms at
80 BPM) before its note lands. In Try Yourself that press *is* the cue, so the learner finds out which finger is next about when
it is already due. The student asked whether to show "an incoming list of the notes to hit".

### Decision: not a list
A scrolling list of upcoming notes is the rhythm-game idiom the three-lane design was cut for (§1.3), and it moves the
learner's eyes off the hands onto a new HUD element (§3.10). Both problems are fixed where the cue already is:

1. **Next-finger preview.** `GhostPressSchedule.TryGetNext(beat, lookAheadBeats, …)` (Core, tested) returns the nearest press
   still ahead and within the look-ahead; `LessonRunner` publishes it as `GhostFrame.NextFinger` and, because the beat before a
   chord change has the *next* note already in the new chord, `GhostFrame.NextMelodyPose` — the chord the next note belongs
   to. `RightGhostHandView` names the next finger's letter for that chord, so the letter shown ahead is the one that will sound.
   Look-ahead is `LessonRunner ▸ Ghost Next Look Ahead Beats`, default **1.0** `[TUNABLE]`: with notes a beat apart the next
   finger is marked from the instant the last one lands. 0 turns it off. While a press is under way its finger is still "next",
   so the mark carries straight into the press.
2. **Quiet letters.** On the ghost right hand, only the finger pressing and the finger next are at full strength; the other
   letters fade and shrink a little (`MelodyConfig ▸ Ghost letters`: opacity 0.3, size 0.8, ease 0.15 s — all cosmetic). Two
   channels, fade and size, so the cue does not rest on colour (§3.10). A letter that lights as its finger lands still goes
   gold and swells, whatever its emphasis. Every letter starts quiet at the start of a phrase.

### Not changed
The learner's own fingertip letters and the left hand's chord name are as they were; the left ghost's name label is untouched
(`GhostLabel` is at full strength unless a view cues it). Watch and Listen gets the same preview and fade — it is the same
ghost.

### Notes
- If the look-ahead is more than the gap between notes, only the nearest is marked, never two.
- Between phrases of notes — a rest in the lesson, as L3 has before each chord change — every ghost letter is quiet.
- **Chapter 4:** if it argues the ghost carries no text at all, this and ADR-0058 contradict it. The learner-facing claim here is
  attention management (one letter at a time), not extra information.
- Not run in the Editor or on a headset. The fade values and the 1-beat look-ahead are guesses; the thing to look at first is
  whether the faded letters are still legible at arm's length.

---

## ADR-0058 — The ghost hands carry the same labels as the learner's: note letters on the right, the chord's name on the left

**Date:** 2026-10-20 · **Status:** Accepted (student's request); the right ghost's letters exist only on
`explore/finger-bend-keys`, like the hand itself (ADR-0055) — compiled in a scratch project against the Editor's own
assemblies; **not run in the Editor, not tried on a headset** · **Milestone:** M5 · **Extends:** ADR-0044 / ADR-0050 (finger
letters), ADR-0054 (chord name, glow), ADR-0055 / ADR-0052 (the ghosts) · **Changes the thesis:** nothing in `[THESIS]`. As with
ADR-0054, **if Chapter 4 argues the ghost carries no text, this contradicts it.**

### The request
"Can you have the labels appear over the ghost hands as well?"

### What was built
- **Right ghost: a letter over each fingertip** - the note that finger plays (thumb = root … pinky = 9th, ADR-0050), re-pitched
  with the chord the demonstrated notes belong to, in the same size, colour and glow as the letters over the learner's own
  fingers. It **lights as the finger lands on its key** - the moment the note sounds in Watch and Listen, and the cue to play
  in Try Yourself, where nothing sounds and the learner's own letters never flash.
- **Left ghost: the chord's name** ("Dm7", "G7", "Cmaj7") floating above the hand, as the learner's own hand carries it
  (`ChordNameLabel`). It shows from the moment the ghost takes the pose - through the lead-in too - and **lights as the ghost's
  strike lands**, which is the moment the chord sounds in Watch and Listen.
- **`GhostLabel`** (Presentation, internal) is the one floating label both use: a `TextMesh` with `LabelGlow` and the
  learner's label settings (`MelodyConfig`), turned to face the head. Both views own their labels; they are pure subscribers to
  the ghost channel (§2.3).
- **`GhostFrame.MelodyPose`** (Core, tested) - the chord the demonstrated *notes* belong to. It is the ghost's pose except where
  the system's playback trails the ghost (Watch and Listen on a lesson that strikes chords, ADR-0052 revision 2): there the ghost
  has already posed the next chord while a note of the last is still to be shown, and a letter taken from the pose would name the
  wrong note for the lead's length before every chord change. `LessonRunner` fills it from the playback's beat.
- **Scene wiring:** the labels work with no scene change, in the built-in look. **`Jazztures ▸ Labels ▸ Set Up Ghost Hand Labels`**
  copies the scene's existing label settings into both ghosts so they match the learner's exactly (idempotent, undoable - save
  the scene); `Use Finger-Bend Input` does it for a right ghost it creates.

### Justification (visual restraint, §3.10)
The ghost is the demonstration, and a demonstration that names nothing leaves "which note?" to be worked out from where a
finger is - for a novice with no note names. The labels are redundant, never the only carrier (the finger moves; the sound
plays), and they are the same notation as the learner's own (ADR-0044: letters, no octave numbers; ADR-0054: chord names, no
theory words, §1.5). The cost is a second set of letters on screen while the learner's own are up; whether that is too much is for
a pilot, and the ghost's labels are one component away from off.

### `TODO(OPEN)` and not verified
- The fingertip is estimated from the last two joints of each finger (the rig has no tip joint): the last bone's length beyond
  the distal joint is taken as 0.75 of the one before it, and the letter hangs 3.5 cm above, as over the real fingers. Look at it
  on the thumb first.
- The chord name hangs 11 cm above the middle knuckle, as on the real hand; the ghost hovers above the pad, so check it against the
  lesson caption.
- Nothing here has run in the Editor or on a headset.

---

## ADR-0057 — The right ghost hand's joint rotations are mirrored from the left-hand captures, not copied

**Date:** 2026-10-20 · **Status:** Accepted (bug fix, student's report) — compiled in a scratch project against the Editor's
own assemblies; **not run in the Editor, not tried on a headset** · **Milestone:** M5 · **Corrects:** ADR-0053 decision 5, and
ADR-0055 which repeated it · **Changes the thesis:** nothing.

### The report
"The right hand looks distorted, most notably the thumb."

### Cause
Both right ghost hands (`RightGhostHandView`, ADR-0055, and `TutorialGhostView`, ADR-0053) were driven by the left-hand ghost
captures (`Input/Poses/Ghost/*.asset`), and both copied the **joint rotations unchanged**, mirroring only the wrist, on the
belief (ADR-0053) that "a `HandPose`'s joint rotations carry across hands unchanged". They do not. The SDK is compiled against
the OpenXR hand skeleton (`ISDK_OPENXR_HAND` is set by the Interaction assembly's own `versionDefines`), where the two hands share
their distal and dorsal axes and differ in the way the thumb side points (left +X, right −X — read from the compiled
`Constants`). A rotation therefore has to be reflected to change hands, and the SDK does exactly that wherever it changes a
pose's hand: `HandGrabUtils.MirrorHandGrabPose` runs every joint through `HandMirroring.Mirror`, and `ShadowHand.FromJoints` does
the same with `flipHandedness`. Neither ghost prefab carries a compensating rotation offset (every `rotationOffset` is zero in
both). `HandPose.CopyFrom(…, mirrorHandedness)`, which the ADR leaned on, only carries the *label* and the finger freedom.

### Why it is the thumb
Flexion, which is nearly all a finger does between open and fist, is a rotation about the axis the mirror leaves alone, so the
fingers were close to right. The thumb's metacarpal turns through a large sideways angle. Measured on the captured *I* pose, the
error from not mirroring is **133°** on that joint (**169°** in *V*), 24-26° on the next two thumb joints, 58° on the pinky's
metacarpal, and under 8° on most finger joints.

### The fix
`GhostPoseAsset` gained `CreateRightHandPose()` (every joint through `HandMirroring.Mirror`) and `RightWristRotationHeadLocal`
(`HandMirroring.Reflect` across the head-local left-right plane — the same result as the hand-rolled negation it replaces, now
from the SDK, so it stays right if the skeleton define changes). Both right-hand ghosts use them; the tutorial's private
`MirrorAcrossLeftRight` is gone. Left ghost: untouched.

### Not verified
That the mirror is the right one is read from the SDK source and the compiled constants, not seen: `HandMirroring` calls
`Quaternion.LookRotation`, a native Unity call, so it cannot be run outside the Editor. Look at both right hands, the thumb
first. If the thumb is *still* wrong, the next suspect is the blend, not the mirror: the press moves each pressed finger
60 % of the way from the *I* pose to the *V* fist, and the fist's thumb is tucked across the palm rather than pressed down
(`Press Curl` and `Rest Curl` on `RightGhostHandView`).

---

## ADR-0056 — Every phrase opens with four beats of silence; floating panels never trail the head by more than 20 cm

**Date:** 2026-10-20 · **Status:** Accepted (student's request) — compiled in a scratch project against the Editor's own
assemblies; **not run in the Editor, not tried on a headset** · **Milestone:** M5 · **Extends:** ADR-0043 (the shared
phrase start), ADR-0021 (text panels) · **Changes the thesis:** nothing in `[THESIS]`. A session's lessons each run four
beats longer (≈ 3 s at 80 BPM), which the ≤ 2 h session budget absorbs.

### The request
"Can all the lessons have four beats of silence before starting? Also, the interface takes a bit to adjust to the
player's viewport."

### 1 — The lead-in
- **What it is:** `LessonRunner` moves the phrase's one shared start time (`_phaseStartDsp`, ADR-0043) later by
  `_leadInBeats` (default **4**, at the lesson's own tempo). The system's playback, the scripted harmony, the metronome and
  the onset capture are all anchored to that time, so **nothing sounds before it** - no chord, no melody, **no metronome
  click** (silence, not a count-in; a count-in would be a one-line change to start the click earlier). Applied to every
  phase the runner starts, which with the menu's one-mode runs (ADR-0051) is once per lesson attempt.
- **The phrase position is now signed.** It used to be clamped at 0 through the 0.15 s pre-roll, which was harmless; held at 0
  for three seconds it would have kept a beat-0 note lit, and a beat-0 strike's hand on the pad, for the whole silence. It
  now starts negative and counts up to 0. The ghost's *pose* is read at max(beat, 0), so it waits in the pose the phrase
  opens with - ready, not already striking or lit - and its strikes, lit targets and finger presses (ADR-0052/0055) follow the
  signed beat, so they begin on time. Tests pin that a beat-0 press or strike has not begun in the lead-in.
- **Captions** still fire when the phase starts, so the lesson's opening line is on screen through the silence. In Try Yourself
  on a left-hand lesson the learner's own strikes are silent until the first expected pose arrives (the correctness gate has no
  expected pose before beat 0) - which is the silence asked for; the right-hand lessons are silent for want of a chord.
- **Test Yourself:** a note played inside the lead-in is kept by `OnsetCapture` at a negative time, as before, so anticipating
  the start can still be scored as an extra. Unchanged by this ADR; worth a look at the first pilot.
- **Not changed:** the timing drills (`TimingDrillRunner`) - they measure latency, not a lesson - and free play.
- **Config:** one field on the runner, beside the ghost-strike ones; set it to 0 to start at once.

### 2 — The interface and the viewport
**Found, not observed:** I could not reproduce this without a headset, so these are the causes the code shows, fixed together.
1. **`LessonHud` kept a stale position while it was switched off.** The flow turns it off between lessons; on the next one its
   caption appeared where it had last been and eased (0.5 s time constant, about a second and a half to look arrived) to the
   learner's present view. The menu and tutorial already restarted at the view when shown; the HUD did not. It now does.
2. **An ease has no bound on how far behind it can be.** All three panels (`LessonHud`, `MenuView`, `TutorialView`) eased with a
   0.4-0.5 s time constant, so a panel placed before the headset's pose settled, or a head that turned quickly, left the panel out
   of view for a second or more. They now share `Presentation/PanelFollow`: the same ease, **leashed** so the panel never trails the
   point it belongs at by more than `_maxTrailMetres` (default 0.2 m, about 14-17° at the panels' distances). Within the leash a small
   head movement still moves the panel exactly as softly as before (checked: 1.37 mm in a frame either way); beyond it the panel is
   carried along at the head's pace. The three copies of the placement code became one.
- **Measured** with `PanelFollow` run outside Unity at 90 fps: a panel placed 1.6 m off is within 0.2 m at once and within 5 cm in
  0.57 s (was 1.39 s); across a 90° turn in half a second the worst trail is 0.20 m (was 0.64 m) and it settles 0.56 s after the
  head stops (was 1.02 s).
- **If it still feels slow,** the two knobs are each panel's `Max Trail Metres` (smaller = tighter) and `Follow Ease Seconds`. The
  strike pad and the melody arc are not these panels: they are body-anchored on purpose and recentre only after a 35° turn held
  for 0.6 s (ADR-0015/0040), so they will still lag a turn - say if that is the thing you meant.

### Not verified
`dotnet test` passes (762). `Presentation`, `Lessons` and the editor command compile in a scratch project. The lead-in and the
panel follow have not run in the Editor or on a headset; whether 4 beats feels right at each lesson's tempo, and whether 0.2 m
is the right leash, are for the first session on device.

---

## ADR-0055 — EXPLORATION (branch `explore/finger-bend-keys`): the lesson's ghost right hand presses the cued finger

**Date:** 2026-10-20 · **Status:** **Proposed — exploration only**, same footing as ADR-0050 (it exists only because
that input does) · **Milestone:** M5 · **Extends:** ADR-0012 (ghost hands), ADR-0050 (finger-bend input), ADR-0052
(the detached ghost) · **Changes the thesis:** nothing in `[THESIS]`. **Supersedes, for this branch only,** ADR-0012's
"right hand: no ghost fingertip" row — `CLAUDE.md` §7 still says it and is deliberately not edited here (as with
ADR-0050).

### The request
"The fingers on the right ghost hand should move to press the keys when cued by the lesson."

### What was there
Only the left ghost followed the lesson (`GhostHandView`). The right hand's cue was `GhostFrame.LitTargetIndex`, which
lights a touch-target sphere — but with finger bends there are no spheres (ADR-0050 hides them), so in Try Yourself the
learner had no cue at all, and in Watch and Listen only the letter over their own fingertip flashed. The tutorial has a
right ghost (`TutorialGhostView`), but it does not read the lesson.

### What was built
- **`Core/Lessons/GhostPressSchedule`** (tested) — each melody note becomes a press of the finger that plays it
  (target index mod 5: thumb = root … pinky = 9th, the same map as `FingerBendBinder`). It is one
  `GhostStrikeSchedule` per finger, so it has the strike's envelope: the finger accelerates down and is **fully down
  exactly on the note's swung onset**, then lifts on an ease. The fingertip meets the key as the note sounds; in Try
  Yourself, where nothing sounds, the press is the cue to do it now.
- **`GhostFrame`** gained `PressFinger` / `PressProgress`. `LessonRunner` builds the schedule per phase (only where the
  lesson includes the right hand and the mode shows ghosts), publishes it shifted by the playback lead like the lit
  target, and does not end a phrase while the last finger is still lifting. Timing: `_ghostPressApproachBeats` 0.25,
  `_ghostPressReturnBeats` 0.35, on the runner beside the strike's two — the same place ADR-0052 put its equivalents.
- **`Presentation/RightGhostHandView`** — a pure subscriber. A palm-down open hand hovers right of centre in the
  learner's heading frame (not on their wrist, for ADR-0052's reasons); the pressing finger's joints blend from the open
  pose toward the fist, the rest stay put. **No poses were recorded:** the open hand is the left ghost's *I* capture, the
  bend its *V* capture, as the tutorial ghost reuses *ii* and *V* — recapture either and this follows. (These were first
  copied onto the right hand unchanged; that was wrong and is corrected by ADR-0057.) A finger that stops
  being the cue before it has lifted eases up over 0.2 s rather than snapping when another takes over (quick passages).
  Shown only while the finger-bend input is enabled, i.e. a lesson that uses the right hand (the flow already toggles it).
- **Scene wiring:** `Jazztures ▸ Experiments ▸ Use Finger-Bend Input` now also adds the view (idempotent, undoable) as a
  child of `Finger Bend Input`, with its own copy of the SDK's right ghost hand, so `Use Touch Targets Again` takes it
  away too. **Re-run that command once and save the scene** — nothing in `main.unity` was edited by hand.

### Justification (visual restraint, §3.10)
It replaces a cue that was missing, not an extra one: with the touch targets gone the right hand had no demonstration at
all. It carries no letters; the note names stay on the learner's own fingertips (ADR-0050/0054), so the ghost is movement
only. Whether the ghost *and* the flashing fingertip letter in Watch and Listen is one cue too many is for a pilot.

### `TODO(OPEN)`
- **The upper octave.** Targets 5..9 press the same finger as 0..4 (the same degree). ADR-0050 gives the finger input one
  octave, so the learner cannot reach those pitches; no shipped lesson uses them (L3 uses 0–4). Pick a convention if a
  lesson ever does.
- **Station and look.** The hover position (0.25, −0.30, 0.45 m from the head), the 0.12 resting curl and the 0.6 press
  curl are guesses; a fist's thumb tuck is not a thumb press. All Inspector fields.

### Not verified
`dotnet test` passes (759), including 23 new tests for the schedule and the frame (a mutation that ignores swing fails
the swing test). `Presentation`, `Lessons` and the editor command compiled in a scratch project against the Editor's own
assemblies; **none of it has run in the Editor or on a headset, and the setup command has not been run.**

---

## ADR-0054 — The left hand carries its chord's name, and both hands' labels light up when they sound

**Date:** 2026-10-19 · **Status:** Accepted (student's request) — every Unity assembly compiles in a scratch
project; **not run in the Editor, not tried on a headset** · **Milestone:** M5 · **Extends:** ADR-0044 (the note
letters), ADR-0050 (the finger letters) · **Changes the thesis:** nothing in `[THESIS]`. **Chapter 6 / Chapter 4
should mention the chord-name label** (see "Justification" below), and §3.10 of `CLAUDE.md` lists the left hand's
visuals as the outline only.

### The request
"Add a chord name indicator to the left hand, similar to the notes on the right hand's fingers, and make the
labels on both hands light up when activated."

### What was built
- **`ChordNameLabel`** (Presentation) — the name of the chord the left hand is holding ("Dm7", "G7", "Cmaj7", from
  `Chord.ToString`, which is already tested) floating 11 cm above the palm, turned to face the learner. It appears
  when the pose is confirmed and goes when the hand lets go. It reads the same channels as the finger letters
  (`ChordChangedChannel`, `NoteTriggeredChannel`) and writes to nothing in the domain (§2.3).
- **It lights on the strike, not on the pose.** Selecting a chord does not sound it (§3.2, ADR-0025), so the name
  appears and rests while a pose is merely held, and lights when the chord sounds — the learner sees *selected* and
  *sounded* apart, just as they hear them apart. It lights for the learner's own strike (`MidiChannel.Harmony`) and
  for the system's demonstration (`Accompaniment`, left-hand source), and, like the finger letters, for notes the
  mode gate keeps silent (the unconditional stream, §3.8).
- **`LabelGlow`** (Presentation) — one shared "lit" look for both hands, so a note and a chord answer in the same
  visual voice. A label *rests* at 72 % of the label colour, and the instant it is activated jumps to a warm gold,
  swells 35 % and holds, then eases back over 0.45 s. The swell is a second channel beside the colour (§3.10).
  `FingerNoteLabels` now uses it too: **the old flash went from near-white to pale yellow over 0.2 s, which was close
  to invisible** — the resting colour was already almost white.
- **Config:** four cosmetic fields under "Label glow" in `Config/MelodyConfig.asset` (lit colour, resting brightness,
  lit seconds, pop). They are a look, not a behaviour; the existing asset picks up the defaults.
- **Scene wiring:** `Jazztures ▸ Labels ▸ Set Up Left-Hand Chord Name` (idempotent, undoable) adds a
  `Chord Name Label` object under the performance harness, copying the left hand from the hand-pose source and the
  channels, config and head from the finger letters. Run it once and save the scene. It hides on the menu screens, as
  the finger letters do.

### Justification (visual restraint, §3.10)
The left hand's outline carries the chord's *function* (cool / warm / settled) but not which chord it is, and with the
selection/strike split a learner holding a pose has nothing on screen that confirms the pose was *read* before they
strike. The name is the left-hand counterpart of ADR-0044's note letters, and the same dual-channel argument applies:
the chord is also audible and the outline also colours it, so the label is redundant, never the only carrier. It is
notation, like the finger letters — not the theory words §1.5 rules out (no "dominant", no "ii-V-I"). **If Chapter 4
argues that the left hand carries colour only, this contradicts it** and the text or the label needs to change.

### Consequences and open points
- **Right-hand-only lessons (L3–L5):** the name still shows above the idle left hand, because it follows the
  performance harness, not the lesson's active hands. It reads as "the chord playing now", which suits "over
  system-played changes", but it is a second thing to look at on a lesson that does not use the left hand. If that
  is too much, gate it on the lesson's active hands as the melody input is (`LessonFlowController`).
- **Touch-target mode:** the glow applies to the finger letters only. A touch target already flashes as a whole when
  struck (ADR-0046), so its letter was left alone.
- **The label hides while the left hand is untracked.** The chord itself sustains (§3.5); only the label, which has
  nowhere to hang, goes.
- **Untested on a headset:** the 11 cm hover, the 1.25× size against a letter, and whether the resting dimming
  reads against the sunrise sky (ADR-0053). Tune by eye; all four glow numbers are Inspector fields.

---

## ADR-0053 — The tutorial: a first lesson gated by what the learner does, guided by a ghost hand, in the dark until the menu opens

**Date:** 2026-10-19 · **Status:** Accepted (student's script and design calls); the state machine is headless-tested
(32 new tests) and every Unity assembly compiles in a scratch project — **not run in the Editor, not tried on a
headset** · **Milestone:** M5 (lesson flow) · **Resolves:** the `[OPEN]` "system tutorial" left by ADR-0051 ·
**Extends:** ADR-0051 (the gesture menu), ADR-0052 (the detached ghost hand) · **Changes the thesis:** nothing in
`[THESIS]`. **Chapter 6 should mention the tutorial**, and the session-length budget (§3.9, ≤ 2 h) now includes it.

### The request

"A special lesson that runs on startup that introduces someone who is completely unfamiliar with VR to the system. It
follows this script and is gated by the user actually doing the action." The script: *Place your hands in front of you.* →
*Move your hands to your side, out of view.* → *Bring your hands back in front of you.* → *When you put your hands in
front of you, the VR headset can detect it.* (five seconds) → *To open the menu, make a fist with your right hand.*
(ghost hand; the lesson menu appears when they do it) → *Now open your right hand with your thumb facing up.* (ghost
hand) → *To navigate the menu, flick your hand left or right.* (ghost hand; originally "swipe", see Revision 3) → *Strike your hand down to select a
lesson.* (ghost hand; the modes menu appears when they do it). Then, added mid-way: **complete darkness for the start,
until they open the menu, where we have a warm sunrise palette.**

### Decisions

1. **The script is data.** `TutorialDefinition` (a ScriptableObject beside the lesson assets) holds the eight steps —
   text, what the learner must do, which ghost demo plays, whether the menu is on screen, which menu gestures the step
   lets through — and bakes them into the pure `TutorialPlan`. A new asset starts with the student's script verbatim.
   Changing a line or its order needs no C# (§3.9's spirit).
2. **Gated by action, not time** (`Core/Tutorial/TutorialRunner`, pure, tested). *Posture* gates — both hands in view, both
   out of view, an open right hand with the thumb up — must hold for a moment, so a flicker of tracking is not "doing it";
   thumb-up has a Schmitt pair of angles (35° / 50°, §3.4). *Gesture* gates — fist, flick, strike — fire on the **menu
   recogniser's own events**, so the tutorial teaches exactly the gestures the menu then expects, at the same thresholds.
   The only timed step is the explanation ("the headset can detect it", 5 s). Every line is shown at least 1.5 s even if
   the learner is already in position, and a gesture arriving in the first 0.5 s of a step is taken to be the tail of the
   last motion and ignored.
3. **It runs on the real menu.** The lesson menu appears when the fist is made; a swipe really moves the highlight; the
   last strike really opens the chosen lesson's modes, and the tutorial ends there, handing over mid-flow. Each step lets
   through only the gestures taught so far (`Forward`), so a stray strike while still learning to swipe cannot pick a
   lesson. A strike that would land on **Free play** (a flick can reach it) is ignored in the tutorial rather than
   quietly starting free play.
4. **"Open your right hand with your thumb facing up"** is judged with the SDK's own `TransformFeature.WristUp` —
   `TransformFeatureValueProvider.GetValue`, the value the ii recogniser uses, with the same gravity-up reference as the
   left hand (ADR-0047) — plus all four long fingers under 0.35 closure. Not hand-rolled joint maths (§3.4).
   **The menu itself never checks hand shape** (flicks and strikes are palm motions): this step teaches a *ready posture*
   the recogniser does not enforce. If the student means it to be required, that is a change to ADR-0051.
5. **The ghost right hand reuses the poses already recorded.** The SDK ships `OpenXRGhost-HandRight`, so the *ii* capture
   (open palm, thumb up) is the open hand and the *V* capture the fist. ~~A `HandPose`'s joint rotations carry across hands
   unchanged~~ — **wrong, corrected by ADR-0057:** only the wrist was mirrored here and the joints were copied as they were,
   which distorts the thumb badly; both are now mirrored. No recording was needed — and re-capturing those two
   poses updates the tutorial. The ghost hovers to the right and below the menu, in the learner's heading frame, **not**
   tied to their hand (ADR-0052's reasoning: it must not jump or vanish when the real hand does, which is what the first
   steps are about). Four looping demos: close into a fist and open; hold open with the thumb up (a slow bob so it reads as
   alive); swipe right–left–right; drop fast and lift slowly.
6. **The left hand has to be visible to the tutorial.** `MenuHandFrame` gained `LeftTracking`, `MaxFingerClosure`
   (an open hand needs *no* finger curled, where a fist needs *every* finger curled) and `ThumbUpAngleDegrees`;
   `MetaXRMenuHandSource` takes an optional left hand. With none assigned it borrows the right's tracking and warns,
   rather than leave the tutorial waiting forever at step 1.
7. **The sky** (`Presentation/SkyDome`). Darkness is the cameras clearing to black (which also stops the scene's own
   skybox drawing). When the lesson menu opens, a **sunrise** comes up over four seconds, with the sun on the horizon
   *straight ahead of where the learner is facing at that moment*, and stays for the rest of the session: nadir rose-brown →
   gold horizon → coral → rose → violet → deep-blue zenith. It is a vertex-coloured dome that follows the head, drawn in the
   Background queue with no depth write, using `Sprites/Default` (already in every build for the menu's plates) — **not a
   skybox shader**, which would be code that cannot be compiled without the Editor open, and whose palette could not be
   tuned live. With the tutorial off, the sunrise is simply there from the start.
8. **When it runs.** On every startup, per the student ("runs on startup"). `MenuConfig ▸ Show Tutorial` switches it off;
   with no script, no view, or an invalid script it is skipped and the app opens on the menu — a broken script never
   traps a learner.
9. **At a desk:** hold **Tab** for "both hands out of view", hold **T** for "right hand open, thumb up", plus the menu keys
   (arrows, Down/Enter, hold Backspace) — so the whole sequence can be walked without a headset.

### Consequences and risks

- **Everything is unseen on a headset.** Likeliest to need tuning: the thumb-up thresholds, whether the SDK's `WristUp`
  reads the way the ii recogniser's does for the *right* hand, the ghost's position against the menu and text, and
  whether 1 s of "hands out of view" is detected reliably at the edge of the tracking cone.
- **The OVR rig may re-apply its own camera clear settings**; if the sky is not black at the start, that is where to look.
  Passthrough, if ever enabled, is hidden by the black clear (which is what "complete darkness" asks for).
- **A warm sky behind a warm tension colour.** §3.10 colours the left hand's outline cool → warm → settled across ii → V →
  I; a sunrise-coloured background reduces the contrast of "warm = V". Colour is a redundant channel, never the only one, so
  it is not a rule break, but worth looking at in a lesson.
- **Session time.** The tutorial adds a few minutes to every session's start; the protocol should say whether returning
  participants (sessions 2–5) see it.
- The flow changes: `LessonFlowController` runs the tutorial before the first menu; `MenuSetup` (now **Jazztures ▸ Menu ▸
  Set Up Gesture Menu + Tutorial**) creates the script asset, the text view, the sky dome and the ghost right hand
  (instantiated from the SDK prefab, given the project's ghost material, wired to the *ii* and *V* poses), and gives the
  menu source the left hand.

### Revision 1 (same day) — "out of view" is about the gaze, not tracking loss

**The report:** "It doesn't advance beyond moving hands out of view."

**The cause, from the headset's own log.** The hand-pose logger prints every change in either hand's tracking quality.
Over a whole tutorial run (≈ 30 s) it printed 350 lines, **349 of them `L:High R:High`**; the one other was the first frame
at startup, before the hands appeared. So with the learner's hands hanging at their sides the Quest still reported both
as tracked at High confidence (its downward-facing cameras see them). The gate as built — "neither hand is tracked High" —
could therefore never be met on the hardware, however correctly the learner followed the line.

**The fix: judge where the hands are, not whether they are tracked.** `MenuHandFrame` carries `ViewAngleDegrees` and
`LeftViewAngleDegrees`, the angle between the learner's gaze and the direction from the head to each wrist
(`MetaXRMenuHandSource`). A hand is **in front of you** when it is tracked High and within 50° of the gaze (a hand held at
chest height is ≈ 35°); **out of view** when it is not tracked High *or* at least 60° off (a hand at the side is ≥ 80°).
The 10° gap means a hand in between is neither, so the two instructions cannot both be satisfied by the same posture. Both
hands must meet the condition, held for a moment, as before. "Place your hands in front of you" now also means *in front*,
not merely tracked.

**A safety valve.** A hands-in-view, hands-out-of-view or thumb-up step still unmet after 20 s is passed
(`TutorialRunner.TimedOut`, logged as a warning) rather than strand the learner behind something the hardware cannot see.
Gesture steps (fist, swipe, strike) never time out — the menu needs them. `MenuConfig` can set it to 0, which restores
strict gating; **a study build should decide that deliberately**, since a timeout means a participant can reach the menu
without having done the posture.

**Keyboard stand-in tightened.** The desk keyboard no longer papers over a lost hand: it is used in the Editor only, and
only until real hands have first appeared (or while a key is held). Before this, on a machine with a keyboard attached a
hand that went `NotTracked` was replaced by a pretend, tracked one — which would have defeated this very step.

**Diagnostics kept in release builds:** each step change, and once a second while the tutorial waits, the tracking quality
and gaze angle of both hands, fist/openness and thumb angle — so the next stuck step can be read off `adb logcat`.

**Text fades.** The line now cross-fades visibly: the old one fades and drifts up over 0.4 s, the new one fades in rising
into place over 0.6 s (it was 0.15 s / 0.35 s, too quick to read as a fade), eased, with a 2.5 cm drift like the menu's
transitions. All three are fields on `TutorialView`.

### Revision 2 (same day) — a slower, darker sunrise

The student asked for a longer transition from black to the sunrise, and for it to be darker.

- **12 s, not 4.** And bent: a plain linear fade from black looks as if it jumps at the start (the screen brightens faster than the
  alpha rises), so the rise is smootherstep raised to a power (`Rise Curve` 2): the first seconds stay dim and the light gathers.
  It rises while the learner is on the thumb-up and swipe lines, which is the point.
- **Darker.** A `Brightness` multiplier on the dome's tint, 0.55 by default (1 = the palette as written), darkens everything —
  gradient and sun glow — evenly without changing a hue. The palette itself is untouched, so brightness is the one knob. I read
  "that" as the sunrise; if the *black* start is not black enough on the headset, that is a different problem (the rig re-applying
  its own camera clear), and worth telling me.

### Revision 3 (same day) — a flick, not a swipe

The student asked for the menu gesture to be "more of a flick" than a swipe. A swipe is a sweep of the arm, slow and long; a
flick is a quick snap of the wrist, short and sharp. Three things followed:

- **The words.** The step now reads "To navigate the menu, **flick** your hand left or right." (the existing `Tutorial.asset` was
  edited as well as the default script, since the asset was already created). `TutorialDemo.Swipe` became `TutorialDemo.Flick`.
- **The ghost.** It no longer sweeps ±22 cm over half a second. It rests, **snaps** 9 cm to one side in 0.12 s with the wrist
  turning 22° toward it, holds a beat, relaxes back slowly, then does the other side: quick out, slow back, which is what a
  flick looks like. Width, turn and timing are fields on `TutorialGhostView`.
- **The sensing — the part that matters.** A flick is a rotation of the wrist, and the palm is only a few centimetres from the
  wrist, so a snap barely moves it (the same reason ADR-0028 gave for the left-hand strike). With the palm as the sensing
  point and 0.7 m/s to beat, a true flick could fail to register while a swipe sailed through. Sideways motion is now sensed
  at **a point 16 cm past the wrist along the line from the wrist to the middle knuckle** (`MetaXRMenuHandSource ▸ Flick
  Reach`), where the same wrist snap is two to three times faster. It is built from those two joints alone, so **curling the
  fingers cannot move it** (a fist closing must not flick — the reason the palm and not a fingertip was chosen in ADR-0051), and
  from joint positions rather than the SDK's `Constants.*Distal` (the OpenXR and OVR hand data disagree about which way "along
  the hand" is). Strikes still use the palm. The thresholds (0.7 / 0.25 m/s) are unchanged.
- **The risk, honestly:** the new point amplifies *every* wrist movement, so a hand that rotates sharply at the wrist for another
  reason may now read as a flick. The recogniser's axis-dominance, settle and return-stroke rules still apply and the fist
  suppression still holds, but it has not been tried on a hand. If false flicks appear, lower `Flick Reach` (0 restores the palm)
  or raise the flick enter speed in `MenuConfig`.

### Revision 4 (same day) — ease-out on every ghost movement

The student asked for an ease-out curve on the ghost hand's movements, to look more natural. Movements used a mix before (smoothstep
for the fist and the returns, ease-out only on the flick's snap, ease-in on the strike's drop), so some started and stopped like a
machine. Now **every movement runs on one editable `AnimationCurve`** (`Move Curve`, default the cubic ease-out — leaves at three
times the average speed, arrives at rest): the fist closing and opening, the flick's snap and its return, the strike's lift. The
strike's drop has its own (`Strike Drop Curve`, also ease-out by default).

- **Trade-off:** an ease-out drop leaves at speed and lands softly, where a real strike *accelerates* into contact (the ease-in used
  before). The drop is only 0.25 s, so it reads as a snap down and settle, but if it feels too gentle, give `Strike Drop Curve` an
  ease-in shape (slow start, fast finish).
- **Between demos:** when the learner finishes a step and the next demo begins, the hand now **glides** from where it is to the new
  motion over 0.4 s on the same curve, instead of jumping (`Handover Seconds`; 0 restores the jump). A demo starting from nothing just
  appears.
- Because the curves are Inspector fields, the feel can be tuned live on the headset without a rebuild — an overshoot is a bump past 1.

### Revision 5 (same day) — the tutorial line sat too low

The line was parked 34 cm below eye line (about 23° down at 0.8 m) so it would clear the menu's footer — but for the first five
steps there is no menu, so it just sat low in an empty view. It now has **two heights**: `Height Without Menu` (−0.05 m, near eye
line, where the menu will appear) until the menu is revealed, then it eases down to the existing −0.34 m under the footer
(`LessonFlowController.RevealMenu` → `TutorialView.SetMenuVisible`). The −0.34 m below the menu is unchanged: the footer sits about
0.24 m below eye line, and raising the line further would overlap it. If it still reads low *with* the menu up, the fix is a
shorter footer, not a higher line.

### Revision 6 (same day) — a fist skips the tutorial

The student asked that closing the fist during the tutorial skip it. The fist already means "go home" everywhere in the menu
(ADR-0051), so the tutorial follows suit: **on any step except the one that teaches the fist, a fist ends the tutorial** and the
lessons menu comes up (with the sunrise) on whatever screen it was on. On the "make a fist to open the menu" step the fist is the
answer, as before. The rule lives in `TutorialRunner.Notify` (pure Core, tested), so it uses the recogniser's own fist hold
(0.35 s) and the runner's usual `EventGrace` — the tail of the fist that opened the menu cannot skip the step after it.

- **No on-screen hint.** Telling a first-timer "make a fist to skip" in the first line would spend the attention the tutorial exists
  to direct (§1.5), and the fist is taught two steps in anyway. A returning participant or an operator who knows the gesture can use it.
- **Risk:** a novice who clenches a hand in steps 1–4 could skip by accident and arrive at the menu untaught. The 0.35 s hold makes
  that unlikely, but it is untried; if it happens in the pilot, gate the skip behind the fist step having been reached once.
- **This partly answers the `[OPEN]` below about returning participants** (they can skip), but not the protocol question: whether a
  skip is allowed on a given participant's day is still a study decision, and a skip is not yet distinguishable from finishing in the log (both print `[Tutorial] complete.`; neither reaches telemetry).

### `TODO(OPEN)`

- Whether returning participants skip the tutorial (a protocol decision; today `Show Tutorial` is global).
- Whether the sunrise should persist *inside* lessons or only on the menus.
- Whether the thumb-up posture should become a requirement of the menu (it is taught, not enforced).

### Not verified

`TutorialRunner` and the extended `MenuHandFrame` are covered by `TutorialRunnerTests` / `MenuHandFrameTests`. The Unity side
(`TutorialView`, `TutorialGhostView`, `SkyDome`, the flow changes, the setup command) compiled in a scratch project against the
Editor's own assemblies; **none of it has run**, and the setup command has not been run.

---

## ADR-0052 — The ghost hand demonstrates the strike: it dips onto the pad, and the pad lights when it lands

**Date:** 2026-10-19 · **Status:** Accepted (student design call); the schedule is headless-tested (18 tests), every
Unity assembly compiles in a scratch project — **not run in the Editor, not seen on a headset** · **Milestone:** M5
(lesson content) · **Extends:** ADR-0012 (ghost hands), ADR-0024 (Lesson 1), ADR-0025 (selection vs strike), ADR-0039/0047
(the pad) · **Changes the thesis:** nothing in `[THESIS]`; Chapter 6's description of the Lesson 1 demonstration (poses
only) is now incomplete.

### The report

"The tutorial for the chord gestures is outdated. It doesn't do the strike." Since ADR-0025 a pose only *selects* a
chord and a strike *sounds* it, but Lesson 1 (Gesture Learning) still taught poses alone: a learner copied the ghost,
held the shape, and heard nothing. The student asked whether the Ghost Pose Recorder could capture a strike, then
decided: **"the ghost hand dips towards [the pad], and I would also like the pad to light up when it is hit, even by
the ghost hand."**

### Why the recorder cannot do this

`GhostPoseAsset` is one still frame — the finger joint rotations plus the wrist orientation — and there are three
slots (ii, V, I). A strike does not change the hand's shape, it moves it; there is nothing to snapshot. And
`GhostFrame` named only a pose and when it began, while `GhostHandView` always placed the ghost on the learner's own
wrist, so even a recorded movement had no way to play. The recorder remains the right tool for the *poses* (below).

### Decision

1. **A strike is data, not a recording.** `GhostStrikeSchedule` (Core, tested) holds the beats the hand meets the pad
   and two shared durations. `ProgressAt(beat)` is 0..1 "how far toward the pad": the hand *accelerates* onto the pad
   (progress ∝ time², so it arrives fast, as a strike does), reaches exactly 1 on the strike beat, then lifts away on
   a smooth ease. `ContactsUpTo(beat)` counts strikes landed. `GhostFrame` carries both (`StrikeProgress`,
   `StrikeCount`); a running count rather than a one-frame flag, so a skipped frame cannot lose a hit.
2. **Where the beats come from** (`LessonTimeline.StrikeBeats`): a lesson that scores a pulse (L2) strikes **on the
   pulse** — the dip *is* the rhythm being asked for; any other lesson strikes each chord **2 beats after its pose
   appears** (the 1.5-beat morph plus half a beat of grace), so the shape is formed first. Swing is applied.
3. **The ghost dips onto the pad.** `GhostHandView` lerps the ghost's root from the learner's wrist to the pad's top
   face (`ChordStrikeTarget.TopCenterWorld`, 5 cm above for the wrist) by `StrikeProgress` and back. At rest it is
   exactly where it always was, so ADR-0012's superimposition is unchanged between strikes. Without a pad assigned it
   dips straight down 12 cm. The rotation (the demonstrated wrist orientation) is untouched, so the hand strikes in
   the pose it is teaching.
4. **The pad flashes on every ghost contact**, with the same `Flash()` a real hit gives (the composition root calls it
   when the learner's touch lands), so a ghost strike and a real one read alike.
5. **Where it applies.** Only where the learner plays the left hand (`ActiveHands` includes Left) and the ghost is
   visible: Gesture Learning and Try Yourself. **Originally not Watch and Listen (superseded by Revision 2):** there the system sounds each chord on its
   beat, when the ghost is only beginning to form the shape, so a dip would land on the wrong pose or two beats off
   the sound. Fixing that means the ghost must lead the sound (switch pose early) — a change to W&L timing across all
   lessons, left for the student to decide. Right-hand-only lessons (L3) get no dip.
6. **The phrase waits for the last strike** (`LessonRunner.PhraseIsFinished`): L1's last chord is at beat 20 and its
   last strike at 22, so it now ends after the strike has lifted away (+ the usual tail).
7. **Lesson 1's words.** The concept text now says "Make a shape, then tap the pad to play the chord"; a first caption
   "Copy the ghost hand, then tap the pad to play the chord." (beat 0) precedes the meanings caption (moved to beat 6);
   and the closing line, which was an unfinished sentence ("…this is "), now reads "Prepare, tension, release. That is
   a ii-V-I." The captions say *tap the pad* because the scene's commit gesture is the pad (`_commitGesture: Touch`).
8. **The closing line is shown under the post-run menu.** `LessonFlowController` keeps a caption raised in the same
   frame a run ends and shows it as the post-run footer, above a Test Yourself score. Without this the closing caption
   was switched off with the HUD before it was ever drawn (the HUD now clears its caption when disabled, ADR-0051).

### Not decided / assumed

- **Whether Gesture Learning should wait for the strike** was left open; it only *prompts* (no gate), because §3.8
  defines Gesture Learning as pose fluency with no gating — waiting would turn it into Try Yourself. A reactive
  "now tap the pad" hint after the first confirmed pose would need a new cue trigger ("chord struck"); not built.
- **The geometry is a guess:** a straight line from the wrist to 5 cm over the pad's centre, whatever the pose. Hands
  of different size and poses whose lowest edge sits differently (ii's pinky side, V's knuckles, I's palm) will
  overlap the pad by a few centimetres either way. `Contact Height` is the lever.
- **The ghost poses themselves are old.** The three captures date from 10 Sep, before ADR-0025/0026/0047 (19 Sep,
  12 Oct); ADR-0026 recorded re-capturing ii as an in-editor action, and the pose it teaches is now "palm sideways"
  (ADR-0047). **Re-capture with Jazztures ▸ Ghost Pose Recorder** — this is what the recorder is for. The ghost will
  strike in whatever pose is on file.
- `Jazztures ▸ Lessons ▸ Create Session 1 Starter Lessons` still describes the *old* Lesson 1 (W&L → Try Yourself,
  three chords) and overwrites an existing asset's fields: do not run it over the shipped assets.

### Revision 3 (2026-10-20) — the ghost strikes wherever the system sounds a chord, including right-hand lessons

The student, on Lesson 3: "the chord tone lesson doesn't show the left hand ghost striking the plane. In any instance
where the system actuates a chord, the ghost hand should strike the plane." Decision 5 had scoped the strike to lessons
where the learner plays the left hand, and said right-hand-only lessons "get no dip" — but L3's Watch and Listen
(full playback) and Try Yourself (harmony only, §3.8 footnote / ADR-0043 decision 3) both have the *system* sound the
chords, so the pad lit and the chord rang with no hand striking it.

- `LessonRunner` now strikes when the ghost is visible and **either** the learner plays the left hand **or** the system
  sounds the chords (`SystemPlayback.Full` or `HarmonyOnly`). The pad flash and chord-name glow follow, as before.
- Where the system sounds the chords the ghost must land as the chord sounds, so the Revision 2 rule applies: the
  strike is 2 whole beats after the pose, and the system's playback trails the ghost by that lead. `HarmonyOnly` used to
  start at beat 0; its chord playback and `ScriptedHarmony` now start `lead` beats late, and the ghost right hand's lit
  targets and finger presses trail by the same amount, so each note still sits over its own chord (ADR-0058).
- **Cost:** a right-hand lesson's Watch and Listen and Try Yourself now open with 2 beats (1.5 s at 80 BPM) before the
  first chord, as left-hand Watch and Listen already does.
- **Not covered:** Test Yourself hides the ghost (§3.8), so a right-hand lesson's Test Yourself still plays its harmony
  with no hand striking. That follows the mode table; the student can say if it should differ.
- Compiled in a scratch project; not run in the Editor, not seen on a headset.

### Revision 2 (same day) — Watch and Listen is back in Lesson 1, and the ghost strikes in it

The student asked "why is there no sound in Watch and Listen?" about the chord lesson. The headset log showed the
build had only run Lesson 1, whose single mode was Learn the shapes, where §3.8 has the system play nothing: the ghost
struck the pad and lit it in silence, which reads as broken. The student's call: **restore Watch and Listen to Lesson 1;
Try Yourself is a separate mode.**

- **Lesson 1's modes** are now *Learn the shapes → Watch and listen → Try yourself*, each its own card on the mode screen
  (the menu runs one mode at a time, ADR-0051) and **Next** steps through them. This returns Lesson 1 toward §3.9's table
  (L1: Watch and Listen, Try Yourself), with §3.8's Gesture Learning first because it "precedes Watch and Listen". **Learn the shapes was then
  removed from Lesson 1 at the student's request** (same day): its modes are Watch and listen → Try yourself,
  exactly §3.9's table. Gesture Learning remains a mode in Core (§3.8, the ghost still strikes in it) but no
  lesson lists it now.
- **The ghost strikes in Watch and Listen, and the chord sounds as the hand lands.** The ghost keeps its usual timing
  (the pose appears on the chord's beat, the strike comes a couple of beats later once the shape is formed), and the
  *system's playback is simply started that many beats later* — `ceil(Strike After Pose)` = 2 beats, so a metronome
  that keeps its own start stays on the grid. The right-hand ghost lights trail by the same amount. A test pins the
  invariant (the chord's note-on time equals the ghost's contact time; mutation-checked — a half-beat drift fails it).
- **What Watch and Listen strikes** is the chords, never a scored pulse (`StrikeBeats(…, useScoredPulse: false)`): the
  system sounds the chords, not the beat. So Lesson 2's Watch and Listen shows two strikes, not the beat-by-beat rhythm
  its Try Yourself asks for — that would need the system to play the pulse.
- **Cost:** the first sound in a left-hand Watch and Listen comes 2 beats (1.5 s at 80 BPM) after the lesson starts —
  the ghost's lead-in — and the phrase is that much longer. Right-hand-only lessons (Lesson 3) are unchanged.
- **Try Yourself** is unchanged: it waits for the pose, not the strike, and the audio reward still needs the learner's
  own strike. Whether it should also wait for the strike is still open.
- **Lesson 1's first caption** now reads "Shape first, then tap the pad. That plays the chord." — descriptive rather
  than a command, because the learner's input is silent in Watch and Listen (§3.8) and an instruction to tap would invite
  the same "why is there no sound" there. Cues cannot be mode-specific.
- Not run in the Editor or on the headset.

### Revision 1 (same day) — the ghost hovers over the pad instead of riding the wrist

On first sight the strike "goes forward instead of downwards". The cause is the design above: the dip ran from the
learner's *own wrist* to the pad, and the pad is anchored to the learner's left at strike height (ADR-0040), not
wherever the hand happens to be held — so unless the hand was directly above the pad, the path was a diagonal, mostly
forward. Any ghost tied to the learner's hand has this problem.

The student asked whether un-tying the ghost from the player's hand would feel limiting. Assessment, and what was built:

- **What is given up** is ADR-0012's superimposition: with the ghost on the learner's own hand, the difference in
  finger shape between the two is visible in place. Detached, the learner copies a shape they see *over there*.
  That is the ordinary "ghost in front of you" idiom, and the hand is placed where the learner's own hand should end
  up, so it is not a perspective flip; but it is a real loss for fine shape matching (Try Yourself). It also changes
  what Chapter 6 says about the ghost (see Thesis below).
- **What is gained:** the strike is a true straight drop, as the pad requires (it reads *downward* hand speed); the
  ghost teaches where to hold the hand as well as what shape to make; and it no longer vanishes or jumps when the
  learner's own hand is lost (§3.5), nor sits glued to an idle left hand in a right-hand lesson.
- **Built as an option, not a replacement:** `GhostHandView ▸ Anchor` = **StrikePad** (new default) or **Wrist** (the
  ADR-0012 behaviour, kept so the two can be compared on the headset). StrikePad needs the pad and falls back to Wrist
  without one. In StrikePad the ghost's wrist hovers 18 cm above the pad's top face (`Station Height`) and drops
  straight onto it; `Station Offset` shifts the hovering ghost and its contact point together if the fingers overshoot
  the pad. The orientation is still the demonstrated one, re-expressed through head yaw (§3.4).
- It applies to every ghost mode, not only the ones with strikes: a ghost that sat on the wrist in Watch and Listen but
  over the pad in Gesture Learning would jump between the two.
- **Thesis:** CLAUDE.md §7 and Chapter 6 describe the ghost as "superimposed on the learner's hands" (ADR-0012). With
  StrikePad that is no longer what the left ghost does. §7 is annotated; the paper is not touched (rule 7).

### `[TUNABLE]` — none measured

`LessonRunner`: strike after pose 2.0 beats, approach 0.5, return 0.5 (≈ 0.375 s each at 80 BPM). `GhostHandView`:
anchor StrikePad, station height 18 cm, station offset 0, contact height 5 cm, no-pad dip 12 cm. Mirrored in
`Docs/CALIBRATION.md`.

### Wiring

Re-run **Jazztures ▸ Menu ▸ Set Up Gesture Menu (ADR-0051)**: it is idempotent and now also gives `GhostHandView` its
strike pad and `LessonFlowController` the cue channel. Until then the ghost dips straight down and no closing line
appears under the menu.

---

## ADR-0051 — The menu is driven by the right hand: flick to move, strike to choose, fist to go home

**Date:** 2026-10-19 · **Status:** Accepted (student design call); the Core part is headless-tested (66 tests)
and every Unity assembly compiles in a scratch project — **not run in the Editor, not tried on a headset** ·
**Milestone:** M5 (lesson flow) · **Supersedes:** the point-and-pinch lesson selector (`LessonSelector`, with its
sessions → lessons list and +/- tempo stepper) and the overhead `ExitButton`; the menu *surface* of ADR-0043 (its
Timing test and free-play tempo stepper are no longer reachable — the code is kept) · **Changes the thesis:**
nothing in `[THESIS]`. **Chapter 6 may need matching wording** if it describes a session-grouped menu, a
point-and-pinch selector, or an Exit button.

### The request

"We're changing the menu system entirely. We want a tutorial at the start for how to use the interface, and that
leads you into the main menu, which you navigate by flicking your right hand horizontally, striking down to
select an option, and closing your fist to go back to the main menu. Back should also be an option in sub-menus."
The tree: lessons → (pick one) its modes → (pick one) the run starts; when a mode finishes, a menu with *back to
modes*, *restart*, *next* (the next mode, else the next lesson). The student asked to do the menu first and the
system tutorial after.

### Decisions

1. **One vocabulary, three gestures, right hand only.** *Flick* (a quick sideways stroke either way) moves the
   highlight — **left = next card, right = previous**, the swipe convention, so the row follows the hand (swapped from
   the first version on the student's request; the recogniser still reports the physical direction and only
   `MenuNavigator.Handle` maps it, so the desk arrow keys follow suit); *strike* (a quick downward stroke) chooses it; *fist* (held) goes to the main menu from anywhere.
   `Core/Menu/MenuGestureRecognizer` turns right-hand motion into these. It follows the left-hand strike's rules
   (§3.4/§3.5): Schmitt enter/exit speeds, a settle of several quiet frames before the next gesture can arm
   (ADR-0030), an axis-dominance test so a diagonal swipe counts as nothing ("never guess"), nothing fires while
   tracking is Low and not until 3 High frames have returned, and a minimum gap between gestures.
2. **Layout.** A single row of cards that scrolls under a fixed highlight (`Presentation/MenuView`), so the
   card you would choose is always centre. It is a dumb view like the HUD (ADR-0021); it reads no input.
3. **Structure is a pure state machine** (`Core/Menu/MenuNavigator`, tested): *Main* = lessons then **Free play**;
   *Modes* = the lesson's own modes, then a **tempo card**, then **Back**; *PostRun* = **Back to modes**,
   **Restart**, **Next**. The highlight **wraps around** at the ends (the student's request, same day: it first clamped), and
   returning to Main puts it back on the card you came from. Modes come from each `LessonDefinition`'s own mode list, so L1 offers only "Learn the shapes" and
   L2 also offers "Test yourself". Adding a lesson is still data only (§3.9).
4. **A lesson now runs one mode at a time.** `LessonRunner.LoadLesson(lesson, mode, tempo)` builds a one-mode
   plan; `Completed` fires when that mode ends. The multi-phase path (a lesson walking its modes in order) is
   untouched and still tested.
5. **The fist is also the way out of a running lesson or free play** — the student chose to delete the overhead
   Exit button now rather than keep it as a fallback. While something runs the right hand is busy playing, so
   there the recogniser listens for the fist **only**, and it needs a longer hold (1.0 s, against 0.35 s in a
   menu); the view draws a thin bar that fills during the hold, because a hold with no feedback just reads as
   latency. A fist is latched until the hand has been seen open, so leaving a lesson with a fist does not
   immediately fire again.
6. **Free play** is the last card of the main menu. **The Timing test is dropped from the flow** (student's call):
   `TimingDrillRunner`, the drill assets and the catalog's drill list stay on disk, but nothing reaches them.
   `FreePlayMetronome.StepTempo` has no caller now either.
7. **Tempo card** (student's call: "a card next to the mode selection", and it sets *this lesson's* tempo). Striking
   it steps through presets (60-100 BPM, `MenuConfig`), wrapping from the fastest to the slowest; the tempo is
   remembered per lesson and applies to every mode started from that screen — timeline, metronome and onset
   windows all follow it.
8. **After a run.** *Next* starts the next mode of the same lesson at once; after a lesson's last mode it opens the
   **next lesson's mode screen** instead (a new lesson deserves its concept text and its tempo card) and does not
   start anything. After the last mode of the last lesson there is no *Next*. A Test Yourself score is shown as the
   post-run screen's footer — after the attempt, never during it (§3.7) — which the old flow lost: it disabled the
   HUD the instant the lesson completed.
9. **The post-run highlight starts on *Back to modes***, not *Next*: the screen appears the moment a phrase ends,
   with the right hand perhaps still mid-strike on a target, and the safe card is the one that cannot start
   anything. The recogniser also ignores flicks and strikes for 0.6 s after any change of the hand's job
   (`Rearm`) and until the hand has settled.
10. **The catalog keeps its sessions; the menu lists lessons flat**, in session order. CLAUDE.md §3.9's session
    distribution (S1 = L1+L2+L3 …) is a study protocol the menu no longer displays — confirm that is intended.

### Why it is built this way

- **The palm is tracked, not a fingertip** (`Input/MetaXRMenuHandSource`). Closing a fist swings the fingertips
  tens of centimetres in a fraction of a second; read at a fingertip that is a strike, at the instant the learner
  means to go back. The palm barely moves when the fingers curl. Strikes also stay suppressed while a fist is
  closing. (The left-hand strike reads a fingertip for the opposite reason — ADR-0028; if the palm proves too
  insensitive for a menu strike, that one joint is the line to change.)
- **"Left"/"right" are the learner's, by projecting velocity, not position.** The hand's world displacement each
  frame is projected onto the head's current right vector and accumulated. Measuring the hand's position *in the
  head's frame* instead would read a head turn as a flick, because the frame itself swings round a stationary hand.
- **A return stroke is not a second flick.** After a flick, one the other way inside 0.5 s is swallowed (and
  disarms), or every flick would be followed by its own return undoing it. The cost: a deliberate flick back needs
  half a second.
- **Fist = the SDK's own finger curl** (`FingerShapes`, curl blended with knuckle flexion, the least-closed of the
  four long fingers), not hand-rolled joint maths (§3.4) — the same read `FingerBendBinder` uses. The Schmitt edges
  are §3.4's 0.75 "curled" and a lower exit.
- **The recogniser, navigator and their tests are pure `Core`** (§2.1): `MenuHandFrame` and `IMenuHandSource` are a
  new port beside `IHandPoseSource`, with a headset adapter (`MetaXRMenuHandSource`) and a desk one
  (`KeyboardMenuHandSource`: arrows = flick, Down/Enter = strike, hold Backspace = fist). `LessonFlowController`
  only translates: it builds the `MenuModel` from the catalog, words the cards, draws them, and carries out the
  `MenuIntent`s the navigator raises.

### Revision 1 (same day) — feedback on the menu

The student asked for more feedback, "like maybe have cards fade up/down when switching submenus". With no haptics
(§3.10) the menu has to say what the hand just did, so `MenuView` now answers each gesture and nothing else:

- **Transition on a screen change.** Going deeper (into a lesson, into a result, on to the next lesson) the old cards
  drift up and fade out (0.10 s), then the new ones rise in from below (0.18 s); coming back out it runs the other
  way. A menu appearing from nothing fades in — from above when it is Main (arriving home), from below otherwise.
  `MenuTransition` carries the direction; `LessonFlowController` works it out from a depth per screen
  (Main 0, Modes 1, PostRun 2). A change that arrives while the old screen is still fading just replaces the
  destination. Alpha is linear in the phase so a fade reversed mid-way does not pop.
- **Pulse on a strike.** The highlighted card swells ~12 % and flashes white, settling over 0.22 s. On the tempo card
  (which stays on screen and only changes its number) it is the whole answer; on a card that leads elsewhere it is
  seen for the instant before the transition.
- **Bump at the end of the row** (since the row wraps, now only on a one-card row). `MenuNavigator.Bumped` fires when a flick is recognised but the highlight is already
  at that end; the row is nudged 0.2 of a card and springs back. This is not decoration: without it "recognised,
  nowhere to go" and "flick not recognised" look identical, which would make the first headset session much harder
  to read.

Not added, deliberately: sound (needs assets and a mixing decision) and any per-flick effect beyond the glide the row
already had. Durations and sizes are fields on `MenuView`, `[TUNABLE]` and unseen on a headset.

### `[TUNABLE]` — none measured

All in `Config/MenuConfig.asset` (the transition, pulse and bump fields are on `MenuView`) and mirrored in `Docs/CALIBRATION.md` ("Menu"). The speeds (flick 0.7 / 0.25 m/s,
strike 0.45 / 0.18 m/s) are guesses sized from the left-hand strike's tuned 0.25 / 0.10, set higher because a menu
has no musical need for sensitivity and a wrong choice is worse than a missed one. **Expect to retune them on the
first headset session.**

### Consequences and risks the student should know about

- **A learner-chosen tempo confounds timing scores across participants** (onset windows are absolute milliseconds,
  so a slower tempo is an easier test). `MenuConfig` has **Show Tempo Card** and **Show Free Play** toggles: untick
  both for the study build (§3.10: visual restraint; M8: no interaction parameters change once participants start).
- **A right-hand fist now exits a lesson**, so a clenched right hand mid-phrase is a new way to be thrown out. The
  1 s hold and the open-hand latch are the mitigation; only the headset can say whether it is enough.
- **Strike vs touch targets.** In a right-hand lesson the learner strikes targets downward with the same hand; the
  menu is not listening then (only the fist is), and on return the entry grace and the safe default card cover the
  hand's follow-through. Not yet seen.
- **Scene wiring is a one-click editor command**, as with the other setup tools: **Jazztures ▸ Menu ▸ Set Up
  Gesture Menu (ADR-0051)** creates the config asset, adds the view and the input components (pointing at the
  existing right hand and head), assigns them to `LessonFlowController`, and deletes the now-scriptless
  `LessonSelector` / `ExitButton` objects. Until it is run and the scene saved, the scene has those two as
  "Missing (Mono Script)" and the controller has no menu.

### `TODO(OPEN)`

- **The system tutorial** (student: "we'll need to do the system tutorial after"). It plays before the first menu,
  teaches flick / strike / fist, then hands over to the navigator. The hook is `LessonFlowController.Start`, marked
  `TODO(OPEN)`. Until it exists a learner meets the menu cold.
- *Next* after a lesson's last mode opens the next lesson's mode screen rather than starting it — a one-line change
  in `MenuNavigator.NextAfter` if the student wants it to start straight away.
- Whether the session grouping (decision 10) should return to the display.

### Not verified

The recogniser and navigator are covered by `MenuGestureRecognizerTests` and `MenuNavigatorTests`. The Unity side
(`MenuView`, `MetaXRMenuHandSource`, `MenuInputDriver`, `LessonFlowController`, `MenuSetup`) compiled in a scratch
project against the Editor's own assemblies while the Editor was open; **none of it has run in the Editor**, the
card layout (sizes, wrap width) is a guess to be adjusted in the Inspector, and **nothing has been tried with real
hands**.

---

## ADR-0050 — EXPLORATION (branch `explore/finger-bend-keys`): right hand plays by bending one finger per chord tone

**Date:** 2026-10-12 · **Status:** **Proposed — exploration only.** Lives on `explore/finger-bend-keys`; it is
not a thesis decision and must not be merged as one · **Milestone:** M4 (alternative to ADR-0016/0045)
**Reverses, for the experiment only:** ADR-0005 and ADR-0018's rejection of finger bends · **Changes the
thesis:** it *would* — CLAUDE.md §1.3 (right hand = mid-air touch targets) and §3.1 (ten targets, two
octaves) are not met on this branch. `CLAUDE.md` is deliberately **not** edited here.

### The request and the tension

The student asked to try "one finger bend = one chord tone" as the right-hand input, in a separate branch,
arguing it does not contradict the earlier co-design evidence because those bends were presented as more
extreme and these are "more like pressing keys".

The repo has recorded this idea as rejected twice: ADR-0005 (finger curl is "the piano-mimicking /
individual-finger-bend interaction that the co-design sessions explicitly rejected") and ADR-0018 (raised
again as a fix for hard-to-hit targets, rejected again, "recording the re-test here because a panel may ask
whether the constraint was ever pressure-tested"). CLAUDE.md §1.2 adds that keyboard realism bought with
gestural fluidity is a regression, and `DESIGN-SPACE.md` D4 records finger shape as the least reliable
classification signal this hardware gives. The reading that "pressing keys" is the part co-design rejected is at
least as plausible as the reading that only extreme bends were. The co-design notes are not in the repo, so
neither reading is checked here.

It is therefore built as an experiment, and **before it could ship** someone must cite what in the co-design
findings distinguishes a key-sized bend from the bends that were rejected.

### What is built

- **`Core/Melody/FingerBendDetector`** (pure, 21 tests) — turns five normalised finger bends into presses. Each
  finger is a **peak/trough machine** (revised, see below): free, it remembers its lowest recent point and
  presses when it has risen `PressDelta` above it; held, it remembers its peak and is re-armed when it has
  fallen `ReleaseDelta` below that. Everything is measured from the finger's own last turning point, never from
  a fixed resting level. Plus an **isolation margin** (a finger presses only if it rose `IsolationMargin`
  further than every other free finger, so a fist, a hand relaxing from open, or a neighbour dragged along by
  tendon coupling sounds nothing — and `Blocked(finger)` reports when that happened). `FingerBendThresholds`
  holds the four levels.
- **`Input/FingerBendBinder`** — reads the SDK's `FingerShapes` features (no hand-rolled joint maths, §3.4):
  **curl blended with knuckle flexion**, `(2·curl + flexion)/3`, as the SDK's own grab detection does (the thumb
  uses curl alone), normalised with `PalmGrabAPI.CURL_RANGE`. It calls the **existing** `MelodyEngine`.
  Chord-tone locking, retrigger cooldown and the velocity curve are untouched. Loudness is the finger's tip
  speed through the bend; a slow deliberate press is lifted to the engine's entry gate rather than dropped. It
  starts over after a tracking gap (§3.5) and ignores frames whose joint read failed.
- **Mapping:** thumb = root, index = 3rd, middle = 5th, ring = 7th, pinky = 9th — slots 0..4 of
  `ChordToneSet`, so "finger 3 is always the fifth" and the tones re-pitch with the left hand's chord.
- **`Presentation/FingerNoteLabels`** — a small letter above each fingertip saying what that finger plays now;
  flashes when its note sounds, including the system's demonstration notes in Watch-and-Listen (standing in for
  the target spheres the ghost hand lights in touch mode). Hides the touch-target pads while this input is on.
- **`Jazztures ▸ Experiments ▸ Use Finger-Bend Input` / `Use Touch Targets Again`**
  (`Experiments/Editor/FingerBendSetup`) — wires or unwires all of the above in the open scene; Ctrl+Z undoes
  it. The harness gained an optional `Finger Bend` slot; the lesson flow already took its melody input as a
  generic component, so it needed no change.
- Tuning fields: `MelodyConfig` ► *Finger bend (exploration)*. All `[TUNABLE]`, unmeasured.

### `TODO(OPEN)`

- **Octaves.** Five fingers give five tones; §3.1 specifies two octaves. Chosen by the student for the trial:
  **one octave only** (slots 0..4, lowest ≥ MIDI 72). Hand height as an octave switch was the alternative.
  Nothing about the upper octave (slots 5..9) is wired.
- **Loudness source.** Fingertip speed was reused because the engine's contract is metres per second; curl
  speed may be a better signal.
- The thumb uses the SDK's thumb curl range; whether the thumb "presses" naturally is untested.

### Revision 1 (same day) — "I bend a finger and sometimes nothing plays"

The first version was inconsistent on device. Reading it against the SDK source found five causes, any of which
produces exactly that; **which one dominated is not yet known**, and the diagnostics below exist to say.

1. **The signal ignored the knuckle.** `FingerShapes.GetCurlValue` averages the angles at the finger's *middle*
   and *tip* joints (Finger1–2–3 and 2–3–Tip); it does not include the knuckle. A key press is mostly a knuckle
   movement with the finger staying fairly straight, so curl barely changed for it, while curling the fingertips
   registered at once — "sometimes it works". Fixed by blending in `GetFlexionValue` (the knuckle), as the SDK's
   own grab detection does. This is the most likely main cause.
2. **One low-confidence finger silenced all five.** Any finger reported low-confidence made the whole frame be
   skipped, and a gap over 250 ms reset the detector — so a bend that straddled the reset was lost outright. The
   thumb and pinky flicker often, and a finger folding toward the palm hides itself. The gate is now **off by
   default** (`Require High Confidence` on the binder turns the old behaviour back on for comparison).
3. **The "resting level" lagged a moderately slow bend away.** It followed the finger upward with a 0.4 s time
   constant, so a 0.2 bend over half a second reached only ~0.11 above it and never pressed. The drift memory is
   now 2 s, so only bends slower than roughly press-level ÷ 2 s are treated as drift.
4. **A press could fail to re-arm.** The finger was "released" only when it came back within the release level
   *of its original rest*. A hand that settled back slightly more bent than it began stayed "down" and the next
   bend was silent for seconds. Re-arming is now relative to the finger's own peak (hence the peak/trough
   detector), and a test reproduces the case.
5. **The isolation margin can swallow a press silently** when a neighbour moves nearly as much. That is by
   design, but it is now visible: the log says "bent … but another finger moved nearly as much — ignored".

**Revision 2 — the thumb.** The student found the thumb unintuitive: it is pressed *down* instinctively, but the signal needed it to curl *in*. The SDK's curl feature averages the angles at the thumb's middle joints, and a downward press swings the whole thumb toward the palm side while those joints stay nearly straight; the SDK itself leaves flexion out of the thumb in its grab detection. The thumb's bend is now its curl **plus** the offset of its tip toward the palm side, measured in the wrist's own frame with the SDK's palmar axis (`Constants.RightPalmar` / `LeftPalmar`), so it works however the hand is turned. Either motion raises it and the detector, which only looks at change, treats it like any other finger. The scale is `Thumb Press Span` (0.04 m per unit of bend, `[TUNABLE]`, guessed). **Unverified on device**: the sign and size of the offset are assumptions from the SDK's axis definitions, so a negative span flips it and the thumb's press log prints the offset in millimetres.

**Revision 3 — the thumb was then too easy.** Two reasons, both mine. The SDK's thumb curl range is 30° (its grab test needs the thumb to move little), against ~70° for the other fingers, so the same 0.15–0.2 of bend meant about half the movement; and the palm-side offset added in Revision 2, at 0.04 m per unit, let roughly 6 mm of ordinary thumb movement press. Because the thumb's summed bend also rose faster than the others' whenever the hand moved, it won the isolation test that is meant to stop a closing hand sounding a note. Both scales are now explicit and halved: `Thumb Curl Span` 60° (was the SDK's 30°, a deliberate departure from "verbatim") and `Thumb Press Span` 0.08 m (was 0.04). Both are guesses to tune on the headset.

Not touched, but worth knowing: `MelodyEngine.TriggerTarget` plays nothing unless the left hand is holding a
chord, and drops a repeat of the same finger within 80 ms. The per-press log line says "gated" in those cases.

### What to measure on the headset

False presses at rest and while moving the hand; missed presses; neighbour fingers sounding (ring/pinky above
all); and the same timing drills (ADR-0043) with this input against the arc — the drills already log which
mechanic was used. With `JAZZTURES_DIAG` set the log shows each press (rise, tip speed, whether it sounded), each
press that the isolation margin suppressed, and every few seconds how often each finger was low-confidence.

### Not verified

Compiles (Config, Input, Presentation, App, and the Editor assembly checked in throwaway projects) and the
detector is headless-tested. **Revision 1 has not been tried on a headset**: the causes above are read from the
code and the SDK source, not observed.

---

## ADR-0049 — Right-hand targets lie flat, face up, struck from above; the octaves are near and far; more spacing

**Date:** 2026-10-12 · **Status:** Accepted (student request); compiles — **not seen or tried on a
headset** · **Milestone:** M4
**Supersedes:** the radially-outward, vertically-stacked layout of ADR-0019 (its shoulder pivot, column
sweep and lazy recenter are unchanged) · **Changes the thesis:** nothing in CLAUDE.md `[THESIS]`
(targets are "touch targets in mid-air", orientation unspecified). **Chapter 6 may need matching wording**
if it describes the targets as facing the learner, or the octaves as an upper and a lower row.

### The request

"Make the right-hand note targets face up instead and give them a bit more spacing between."

### What the code showed

- Everything about a target is defined along its **local Z**: the trigger volume (`TargetVolume`), the push
  of the paddle (ADR-0046) and the contact rule (ADR-0045) all read the fingertip in the target's local
  frame. Orientation therefore lives in one place — `TouchTargetRig.LocalPlacement` — and "face up" is a
  placement change, not a rewrite of the hit test.
- **The two octave rows could not stay stacked.** They were 10 cm apart *vertically* and each volume is
  12 cm deep. With the targets struck from above, a fingertip coming down onto the lower pad would pass
  through the upper pad's volume and sound the wrong octave.
- **The saved asset's column angle was 8°, not the 12° the calibration doc recorded.** At a 0.45 m reach
  that is a 6.3 cm chord between neighbours: a 3 mm gap between 3 cm-radius pads, well inside tracking
  jitter. This is the real reason the targets felt crowded.

### Decision

1. **Each target lies flat, face up.** Its local +Z points straight down — the way the fingertip travels —
   so the pad rests at the top of the volume and is pushed down. The pad, rail, label and volume code are
   unchanged; only the placement rotation is.
2. **The octave rows are near and far**, not stacked: the near row (lower octave) at `reach − row/2`, the far
   row (higher octave) at `reach + row/2`, both at the pivot's height. Columns still sweep about the pivot,
   so within a row a glissando is one shoulder rotation and the outer degrees are no harder to reach.
   Higher octave = farther away, as on a drum-pad grid, in place of "higher up".
3. **More spacing.** Column angle 8° → **12°** (the value the doc already recorded): 8.3 cm between
   neighbours on the near row, 10.6 cm on the far row, i.e. gaps of 2.3 / 4.6 cm between 3 cm-radius pads.
   Row spacing 10 cm vertical → **11 cm near/far**.
4. **Labels lie flat on the pad**, upright for the learner (their "up" points away from the viewer, and
   they look back up the travel axis). An upright billboard would stand half-sunk into a flat pad.
5. The Scene-view gizmo draws the two rows' rings in place of one sphere.

### Consequences

- **Reach.** The far row is `reach + 5.5 cm` = 0.505 m from the shoulder pivot, which at the default
  pivot height (−0.25 m from the head) is a forward-extended arm. The near row (0.395 m) is easier than
  anything before. Lower `Arc Reach` a little, or raise the pivot height offset, if the far row tires.
- **Height.** Both rows now sit at the pivot's height rather than ±5 cm around it. `Shoulder Height Offset`
  moves the whole set up or down, as the left plate's height offset does for the plate.
- **A move toward a pad-pressing metaphor.** CLAUDE.md §1.2 warns against drifting toward a virtual
  keyboard. This stays a 2 × 5 grid of degree-targets, not sorted by pitch and re-pitched in place, so it
  is a drum-pad grid rather than a keyboard — but the student should judge whether it reads that way.
- `MelodyConfig`'s code defaults now match the asset (12° / 11 cm); the old defaults were 16° / 14 cm.
- No unit test: the placement is Unity-side geometry (`Quaternion`/`Vector3`) with no `Core` logic to
  assert; the hit test it feeds is unchanged and already covered. Checked by compiling the Config and
  Presentation assemblies and by working the rotation through by hand (`Euler(90°, yaw, 0)` maps +Z to
  straight down).

### Open

- Whether the far row is too far, and whether higher-octave-farther reads naturally, are for the headset.
- Spacing is by feel, not a pilot measurement. Record the final column angle and row spacing in
  `Docs/CALIBRATION.md` after the pilot.

---

## ADR-0048 — The strike plate's hitbox is built from its visual, not typed in beside it

**Date:** 2026-10-12 · **Status:** Accepted (on-device report); the box is headless-tested and the
component compiles — **not tried on a headset** · **Milestone:** M3
**Supersedes:** the cylinder volume ADR-0039 gave the plate (radius 0.06 m, half-depth 0.08 m) ·
**Changes the thesis:** nothing — an implementation fix; no Chapter 6 wording depends on the plate's shape.

### The report

"The strike plane's hitbox feels like it's not aligned with the visual. Hitbox feels too high."

### What the scene showed

The visual is a child Cube scaled 0.2 × 0.2 × 0.01 — a flat **20 × 20 cm slab, 1 cm thick**, top face
5 mm above the plate's origin. The hitbox was a separate pair of numbers on the component, a
`TargetVolume` cylinder (radius 0.06, half-depth 0.08) centred on the *same origin*:

- It reached **8 cm above the origin — 7.5 cm above the top of the slab** — so a chord fired with the hand
  a hand's-width in the air, which is exactly "too high".
- It was a **12 cm-wide disc against a 20 cm-wide square**: the outer 4 cm on every side, and all four
  corners, were visible but dead.
- The two had no link. Resizing the visual (it had evidently been changed since the hitbox was written)
  left the hitbox behind — "not aligned" would have kept happening.

### Decision

1. **The volume is derived from the visual.** `ChordStrikeTarget` reads its `Renderer`'s local bounds,
   expresses the eight corners in the plate's own frame (so it is independent of where the plate is in the
   world), and builds a `StrikePlateVolume` around the **top face**: the footprint plus a margin, a short
   hover above the surface, a deep reach below it. Resize or move the visual and the hitbox follows.
2. **`StrikePlateVolume` is a box, in `Core/Gesture`** (pure scalars, tested headlessly), replacing the
   cylinder for this object only. The right hand's `TargetVolume` is untouched.
3. **Asymmetric on purpose.** Above the surface the reach is short (the hand should touch what it sees);
   below it, long (depth is the axis a person cannot judge in mid-air, ADR-0018 — overshooting must still
   count; stopping short is the only way to miss).
4. **Tunables** (on the component, `[TUNABLE]`; not pilot-calibrated): footprint margin **0.02 m**, hover
   above the surface **0.02 m**, depth below the surface **0.10 m**. The 2 cm hover is an engineering
   estimate that a tracked joint's centre sits about a centimetre inside the skin, so the *skin* meets the
   slab as the joint reaches ~1–2 cm above it.
5. **Selected-object gizmo** draws the computed box, so alignment can be checked in the Scene view.

### Consequences

- The old `_radiusMetres` / `_halfDepthMetres` values are still serialised in `main.unity`; Unity ignores
  serialised fields the class no longer declares, so nothing needs re-saving. The three new fields take
  their defaults.
- The volume is built once, in `Awake`. A visual resized or moved at runtime is not re-read.
- Tests: `StrikePlateVolumeTests` — hand on the surface counts, a hand 5–8 cm in the air does not (the
  reported bug), the whole square including corners is hittable, the margin reaches but not far, an
  off-centre visual is followed, overshoot below counts but not without limit.

### Open

- If it still feels high or low on device, `Hover Above Surface` is the lever (0 = skin-on-slab strictly;
  raise to make it easier). Record the chosen value in `Docs/CALIBRATION.md` after the pilot.

---

## ADR-0047 — Left hand: ii is "palm sideways" (either thumb direction), "up" is gravity, and the plate takes any part of the hand and a tap inside

**Date:** 2026-10-12 · **Status:** Accepted (student design call + on-device report); the Core part is
headless-tested and the rest compiles — **none of it tried on a headset** · **Milestone:** M3
**Supersedes:** ADR-0026's `WristUp` rule for ii (not its pose) and ADR-0014's `Head` up-reference;
extends ADR-0039/0042 · **Changes the thesis:** Chapter 6's note on how ii is recognised (ADR-0026's
"wrist roll — the thumb points up"); §1.3's wording ("open palm, facing the user's right") is unchanged
and is, if anything, matched more closely.

### The report

On device, ii and I stayed hard to actuate. The student's guesses: a downward "chop" for ii may not
register on the plate, and the thumb's position makes the recogniser "weirdly strict". Later: some people
hold the sideways hand with the thumb up to the ceiling, others with the thumb pointing forward.

### What the code showed

1. **ii accepted exactly one way of holding the hand.** It was open palm AND `WristUp` — the wrist's
   thumb-side axis within 25° of "up" (the saved thresholds are 40° ± 15° → enter 25°, exit 55°; the
   ADR-0041 widening was never applied to this row). Fingers-forward/thumb-up passed; fingers-up/thumb-
   forward did not — though both are "an open palm facing the user's right".
2. **"Up" was the headset's up.** Both ii (`WristUp`) and I (`PalmDown`) compared against
   `UpVectorType.Head`, which is `centerEyePose.up` — it tilts with head pitch. The strike plate sits
   (−0.20, −0.25, +0.20) m from the head, ~41° below eye level, so a learner who looks at it pitches the
   headset ~40° and every vertical check is measured against a tilted reference. I's `PalmDown` enters
   at 35°: a perfectly flat palm-down hand is already borderline. This is the likely reason I was hard,
   and part of why the thumb felt strict.
3. **A chop moves the very axis ii was measured on.** ADR-0025/0027 noted that a strike rotates the
   thumb-side axis away from vertical. A palm-sideways rule reads the *back-of-hand* axis instead, which
   a chop (a rotation about the palm normal) does not move.
4. **The plate tested one joint, and the wrong kind.** The scene saved `_fingertip: 10`, which is
   `HandMiddle2` — the middle finger's intermediate phalange, not a tip. A sideways chop's leading edge is
   the pinky side; a fingers-up chop's is the heel of the hand. Neither is a middle-finger joint.
5. **The plate fired only on the frame of entry,** the same flaw ADR-0045 fixed on the right hand. The
   volume is 16 cm deep, so the hand is often inside already — or arrived too gently to count.

### Decision

1. **ii = open palm AND palm sideways.** "Sideways" is "not palm-down, not palm-up, not palm-toward-face,
   not palm-away-from-face" — four `IsNot` conditions on the SDK's existing features, all required. It
   accepts thumb up, thumb forward, and anything between, and rejects palm-down (so ii and I are now
   mutually exclusive by construction, not only by the ambiguity rule). The open-palm shape asset already
   leaves the thumb unconstrained.
2. **ii and I measure "up" against gravity** (`UpVectorType` Head → Tracking). Gravity-relative is still
   independent of which way the learner faces (CLAUDE.md §3.4), but no longer tilts with the head.
3. **The plate counts any part of the hand.** `ChordStrikeTarget` tests a set of joints (fingertips,
   knuckles, the pinky-side palm, the wrist); any inside is a touch. *(As first written, the strike speed
   was "that of the fastest joint inside" — that was wrong; see the addendum.)*
4. **The plate takes a tap inside the volume,** by the same pure `TouchContact` rule as the right hand
   (ADR-0045): a deliberate entry, or a tap at ≥ 0.25 m/s while already inside, with a settle (≤ 0.10 m/s
   for 3 frames) before the next. ADR-0042's wait-for-confirmation grace is unchanged.

(1) and (2) are applied to the open scene by the menu **Jazztures ▸ Gestures ▸ Apply Sideways-Palm ii +
Gravity-Up (ADR-0047)** — through `SerializedObject`, undoable, idempotent; the scene must then be saved.
It reads the state ids from the project's thresholds asset rather than assuming them.

### Costs and risks (watch for these)

- **A relaxed, open left hand hanging at the side reads as ii** (its palm faces the thigh, i.e. sideways).
  Mitigations: the open-palm shape needs all four fingers extended, and a pose needs a 150 ms hold; and
  selecting ii is silent — nothing sounds without a plate touch. But it does re-pitch the right-hand
  targets. If it happens, tighten the shape or add a "hand raised" condition.
- **Palm-facing-left also reads as ii.** Unnatural for a left hand; harmless.
- **Any part of the hand can arm the plate,** so raising a hand *through* it while a pose is confirmed
  will sound a chord, as an entry would have before.
- **The ghost hand's ii still shows thumb-up.** The pose is now broader than the demonstration; fine for
  teaching one clear hold, but re-capture it if a thumb-forward ghost is wanted.
- **Tap values are borrowed,** not measured for the plate (see `CALIBRATION.md`).
- **The hysteresis gap is unchanged** for I (flat palm 35°–50° from vertical keeps its previous state);
  ii has none of that — it is "not any of the four".

### Addendum, same day — "now it's prone to unintended inputs"

**Reported on device** after the changes above. **Cause, in my own change:** decision 3 took the strike
speed from the fastest *joint inside* the volume, fingertips included. Opening or closing the hand moves
the fingertips at 0.5 m/s and more while the hand itself is still, and with decision 4's tap-inside rule
(0.25 m/s) any pose change near the plate could read as a strike. Entry was also still any-direction
at 0.08 m/s — a fingertip gate — with eleven joints now able to cross the volume's edge as a learner
adjusts.

**Fix.** The two jobs are separated:
- **Where:** unchanged — any `Contact Joint` (fingertips, knuckles, pinky-side palm, wrist) inside the
  volume is a touch.
- **How hard, and whether it is a strike:** the **peak downward speed of the palm and wrist** (`Speed
  Joints`: wrist, the four knuckles, the pinky base) via the pure `DownwardSpeedWindow` — the same
  "current unbroken downward run" rule as the strike (ADR-0028/0035). Fingertips are not in it, so
  finger movement never counts; sideways and upward motion read as 0, so raising a hand through the
  plate no longer sounds it; and the plate is, as designed, struck from above.
- **Entry gate 0.08 → 0.15 m/s** for the plate (`TouchCommitThresholds`). 0.08 was inherited from the
  right hand's aimed fingertip; a chop is 0.5 m/s and up.

**Cost.** A deliberately slow, level approach no longer fires on entry; a downward press at ≥ 0.25 m/s
does (the tap). Both gates are `[TUNABLE]` and unmeasured.

**Levers if it is still wrong** (no code needed): remove a joint from `Contact Joints` on the
`ChordStrikeTarget`; raise the tap speed or entry gate in `TouchCommitThresholds`.

**Other suspects, not yet touched** — which of these it is decides the next fix:
(a) the relaxed hanging hand selecting ii (selection is silent, but re-pitches the right-hand targets);
(b) the wrist/forearm brushing the volume as the arm moves; (c) the right hand's tap-inside
(ADR-0045), which has no direction rule because a finger tap has no fixed direction.

### Thesis impact
Replace ADR-0026's note ("ii is recognised by wrist roll — the thumb points up") with: *ii is an open hand
whose palm is held sideways — neither facing up, down, toward nor away from the learner — and both I and ii
measure orientation against gravity rather than the headset.* No Chapter 6 impact beyond that sentence.

---

## ADR-0046 — Right-hand targets are paddles: a pad you push along a rail, drawn at the trigger volume's size

**Date:** 2026-10-12 · **Status:** Accepted (student design call); compiles, **not yet seen on a
headset** · **Milestone:** M4 · **Narrows:** ADR-0018 (the true-extent tube) · **Corrects:** ADR-0044's
note-label size · **Changes the thesis:** design chapter's description of the target's look only.

### What the scene was actually doing

The rig's `Target Prefab` slot in `main.unity` points at a plain **Sphere scaled 0.05** (a 5 cm ball),
so the targets were never the generated tube/rim/disc ADR-0018 describes — they were spheres. Three
consequences:

1. **The note labels were shrunk twentyfold.** A label is a child of its target and inherited the 0.05
   scale. (This, not the character size, is why they looked "way too small"; doubling the size in
   ADR-0044 was the wrong fix and is reverted.)
2. **The visible object did not match the thing that triggers.** The invisible trigger volume is a
   6 cm × 12 cm cylinder; the ball is 5 cm across. A fingertip touching the ball's skin is already
   inside the volume, so a note fired ~3.5 cm *before* contact, and a tap on the ball happened inside
   the volume, where there was no entry to detect (the failure ADR-0045 fixes).
3. **No depth cue at all.** The ADR-0018 depth disc existed only on the generated tube.

### Decision

1. **The default look is a paddle** (`MelodyConfig.Target Look = Paddle`). A flat pad, the volume's own
   3 cm radius and 1.2 cm thick, rests at the **near, learner-facing end** of the 12 cm trigger volume,
   on a faint rail the length of the volume. It reads as "push me" and shows where the push goes.
2. **The pad is pushed.** While a fingertip is inside the volume the pad is carried along the rail in
   front of it (its learner-facing face meets the fingertip), bottoms out against the far end, and
   springs back (~60 ms) when the finger leaves. This makes depth — the one axis a person cannot judge in
   mid-air (D2) — *visible*, which is the point. It is a visual response only; **the trigger is unchanged**
   (ADR-0045: a deliberate entry, or a tap inside), and the pinch mode never moves the pad.
3. **The note label rides the pad**, just in front of its face, and is now **scale-independent**: it undoes
   whatever scale its parents carry, so a scaled primitive can no longer shrink it. Default character size
   is back to 0.005.
4. **The scene's sphere is ignored by default.** With `Target Look = Paddle` the rig's `Target Prefab` slot
   is not used; set `Target Look = Prefab` to get the sphere (or any styled prefab) back. The rig also
   instantiates that prefab *as a child of the rig*, so the template sphere in the scene sits at the
   shoulder pivot — delete it once the paddle is settled.
5. **Removed:** the tube, the "entry rim" and the separate depth disc. The rim was drawn at local +Z,
   which points *away* from the learner (the arc is built outward from the shoulder) — the far end, not the
   near one its comment claimed. (Reading the geometry, not seen on a headset.)

### Not decided: should pushing the pad *be* the trigger?

Today the pad moves but a note still fires on entry or on a tap. A true virtual key would fire when the pad
reaches a set point on its travel. That is a different trigger, with the cost already on record: a lateral
sweep has no motion along the push axis (D7), which is why the plane-crossing redesign was set aside, and
it competes with the tap logic just added. Worth trying only if the paddle plus the tap fix still feels
indistinct. **Open for the student.**

### Unverified on a headset
That the pad reads as pushable at arm's length; that 6 cm pads 6.3 cm apart do not look like a wall (widen
`Column Angle Degrees` if they do); that the pad tracking the fingertip does not feel laggy or jittery with
hand-tracking noise; and the label's drawing order against a pad that moves under it.

### Thesis impact
The design chapter's description of the target (ADR-0017/0018: a translucent tube with a depth disc)
becomes: a pad on a rail that the learner pushes, one neutral colour, labelled with the note it plays.
No Chapter 6 impact.

---

## ADR-0045 — Right-hand touch: a tap inside a target's volume plays it, not only an entry

**Date:** 2026-10-12 · **Status:** Accepted (fixes a reported on-device failure); headless-tested,
compiles, **not yet re-tried on a headset** · **Milestone:** M4 · **Builds on:** ADR-0016/0018/0019
**Changes the thesis:** none — §3.3's "note-on fires when a right-hand fingertip enters a target
volume" is extended (a tap inside also fires), not contradicted; the gate speeds are `[TUNABLE]`.

### The finding

Reported on device: tapping a target with a finger does not play it, but swinging the whole hand
does — and the wide hand then catches a neighbouring target.

Cause, from the code: `TouchTargetBinder` fired only on the **frame the fingertip crossed from
outside to inside** a target's volume, and only if the fingertip was moving at ≥ 0.08 m/s then. The
volume is 12 cm deep (ADR-0018) because depth is unaimable in mid-air (D2), so a person puts the
finger "on" the target — already inside — and taps. No crossing, no note. A gentle arrival was
rejected silently *and* locked the target until the finger left the volume. A fast whole-hand swing
leaves and re-enters at speed, so it was the one thing that reliably worked.

### Decision

A new pure `TouchContact` (`Core/Melody`), one per (fingertip, target), decides a touch two ways:

1. **Enter it deliberately** — the original rule: the frame of crossing in, at ≥ the entry speed
   (0.08 m/s, the melody engine's gate). A fast reach or a sweep still plays on arrival.
2. **Tap it while inside** — a fingertip already inside plays on a sudden movement of ≥ the **tap
   speed** (0.25 m/s). A gentle arrival now stays armed instead of locking the target.

After a note the fingertip must **settle** (≤ 0.10 m/s for 3 consecutive frames) before another tap
can play — a Schmitt trigger on speed, the same shape as the left-hand strike (§3.4, ADR-0030) —
so a resting hand cannot machine-gun a target and repeated tapping still plays every time. The tap
speed is far above the entry speed on purpose: with no entry edge to vouch for intent, the margin
over tracking jitter is what separates a tap from a still hand. The values start from the strike's
(tuned on this sensor, ADR-0028/0030); they are unmeasured for the right hand and live on
`MelodyConfig` (`Tap Speed`, `Tap Settle Speed`, `Tap Settle Frames`).

### Not changed, and a correction

- **The pinch spike is untouched.** `main.unity` has `Pinch Spike` ticked on the binder, so
  lessons and free play already run in pinch mode; this fix is for the touch mechanic, which the
  Timing test's "Mechanic: touch" row selects (and which a scene with `Pinch Spike` off uses
  everywhere). An earlier note called touch "the default" — true of the code, not of this scene.
- **The wide-hand / neighbour problem is a separate cause.** Both the index and middle fingertips
  are active (`Finger Tips` on the binder), and adjacent faces are only ~6.3 cm apart. If finger
  taps now work, hand-swinging should stop; if neighbours are still hit, remove the middle
  fingertip (ADR-0019 anticipated this) or widen `Column Angle Degrees`.
- **Open:** the tap speed. If a gentle tap still does nothing, lower `Tap Speed`; if a resting hand
  fires notes, raise it. Measure with the timing drills' extra and missed counts.

---

## ADR-0044 — Right-hand targets do not change colour with the chord; each shows the note it plays

**Date:** 2026-10-12 · **Status:** Accepted (student design call); compiles, **not yet seen on
a headset** · **Milestone:** M4/M5 (presentation) · **Narrows:** ADR-0017 §1 and §3 (targets)
**Changes the thesis:** yes — the design chapter's colour-coding description, and the
"Touch targets that light on chord change" line in CLAUDE.md §3.10 (updated).
**Source of the advice:** the student reports being told the right-hand targets should not
change colour. Who said it is not recorded here — add it.

### Decision

1. **The ten targets stay one colour.** Every target takes `TensionPalette.Neutral` once, at
   start, and keeps it. `TensionColorDriver` no longer pushes the eased chord tint to them, and
   the 0.6 s brighten on every chord change is gone (`TouchTarget.SetHighlighted`,
   `TouchTargetBinder._highlightSeconds` and the timer that ended it are deleted).
2. **Each target carries a note label** — the letter name of the pitch it plays under the
   current chord ("C", "E", "G", "B", "D" over Cmaj7). It is rewritten on every chord change
   (`TouchTarget.RePitch`), blank when no chord is held (a blank target cannot sound), a
   fixed near-white, turned to face the learner each frame and drawn above the tube. Names come
   from `PitchNames` (sharps only; in C major over these three chords none appear). **Letters
   only — no octave numbers**, which would be unexplained notation for a novice (§1.5), the same
   rule ADR-0043 decision 1 set for the ladder. Size and the on/off switch are on `MelodyConfig`
   (`[TUNABLE]`).
3. **The left hand's outline keeps the tension colour** (ADR-0017 §3, unchanged) — the request
   was about the right-hand targets.
4. **Touch feedback still changes the target's brightness**: the hover glow, the strike flash,
   the pinch "armed" glow and the ghost hand's "play this one" glow. These answer the learner's
   own action or the lesson's instruction, not the chord, and a label cannot replace them (the
   ghost hand's cue in Try Yourself *is* "this target lights up", ADR-0012). **Open: if "do not
   change colour" was meant to cover these too, they need a non-colour cue (e.g. a small scale
   pulse) — say so and it is a small change.**

### Consequences and things to check

- **Labels name pitches; the map is still by degree.** CLAUDE.md §3.1 `[THESIS]` keeps targets
  "re-pitched, never re-arranged" so the learner builds a map of *scale degree*. That is
  unchanged — slot 2 is still the fifth — but the label now shows the absolute pitch name,
  which changes with the chord, on top of that map. It does not break the rule as written; the
  student should decide whether it blunts the "degree, not pitch" rationale in the paper.
- **L3's concept text should say what the letters are** ("the letters are the notes' names",
  §1.5). It is lesson content, so it is not edited here.
- **Active vs inactive.** The tint used to say "a chord is held" (grey, then colour). Now the
  presence of a label does.
- **Label size — corrected by ADR-0046.** The first labels looked "way too small" and the default
  was doubled to 0.010. That diagnosis was wrong: the scene's targets are clones of a sphere scaled
  0.05, and a label parented to a target inherits its scale, so the letters were shrunk twentyfold.
  Labels are now scale-independent and the default is back at 0.005 (a ~3 cm capital by estimate).
- **Unverified on a headset:** that the label size reads at arm's length without crowding; and
  that the legacy font material (no depth test of its own, as the selector and HUD use it) does
  not draw a label over the hand when the hand passes in front of it.

### Thesis impact

The design chapter's description of colour-coding (ADR-0017's "targets take the chord's colour
… always rendered in low-alpha grey when no chord is held") becomes: the targets are a single
neutral colour; each carries a note-name label; the left (harmony) hand's outline carries the
ii → V → I tension colour as a redundant channel. §3.10's feedback model (auditory primary,
visual secondary and redundant, restraint) is unchanged. No Chapter 6 impact.

---

## ADR-0043 — Right hand: a fixed pitch ladder with a swappable trigger; an audible, configurable metronome and timing drill to measure it

**Date:** 2026-10-08 · **Status:** Proposed — order-of-work steps 1–4 built (see
*Implementation notes — steps 1–2* and *— steps 3–4* below; compiles, headless tests green,
**not yet run on a headset**); nothing else built. Part 2 has no thesis conflict and
can be accepted and built now. Part 1 is cleared for prototyping behind the mechanic switch
(decision 4); adopting it still depends on the bake-off.
**Decided by the student, 2026-10-08** (referred to below as decisions 1–4):
(1) ladder rungs show **note names**; (2) **one column of 10 rungs (C5–E6)** is accepted in
place of §3.1's two octaves; (3) right-hand lessons keep the **chords audible** in Try
Yourself and Test Yourself; (4) **§3.1's degree-map rule may be broken** — the ladder maps
pitch, not degree, and may be prototyped, and adopted if it wins the bake-off.
**Milestone:** Part 1 — M4 (right hand). Part 2 — M5/M6 (L2's metronome) and M7/M8 prep
(timing instrumentation).
**Changes the thesis:** Part 1 — yes: §1.3's right-hand interaction, §3.1's degree map and
two-octave span, §3.3's note-on trigger. Part 2 — no: it builds the metronome §3.6 and L2
already assume.
**Builds on:** `Docs/DESIGN-SPACE.md` (§2 findings D1–D7, §5 bake-off harness);
ADR-0028/0030/0031/0035 (the strike detector's fixes, reused); ADR-0019/0020 (the arc this
would replace).

### Context

- **The right hand is still unsettled.** Six redesigns in three weeks (`DESIGN-SPACE.md`,
  "Why now"). The 19 Sep consultation (Doc Raffy) demo showed touching targets is finicky
  (spare fingers bump neighbouring targets), the pinch plays one note at a time, and finger
  curls would cap the hand at one octave and may not be recognisable. In the music-teacher
  co-design session the objection was to **the pinch itself: too complicated a motion in
  practice, especially for repeated notes in sequence.** The teacher's larger concern was
  what makes the system sound jazz-specific rather than generic ii-V-I.
- **The team proposed two directions:** (1) solfege-style hand signs, one sign per note;
  (2) "melody shift" — a wave up moves the melody up, a wave down moves it down.
- **There is no way to test either hand against a beat in-app.** `Core/Timing/Metronome`
  exists and is tested, but nothing makes it audible (`LessonRunner.DrainMetronome`
  dequeues clicks at `now` and discards them). ADR-0025 measured its finding against an
  external metronome and deferred `MetronomeVoice` as a fast-follow; ADR-0024 assumed it
  exists by L2.

### Considered and not built

**Solfege hand signs (Curwen/Kodály), one sign per note.**

- Selection by static hand shape is D4's weakest signal: 5–7 shapes, several differing only
  by tilt (re, mi, la) or by one finger (fa is thumb-down, ti is a pointed index), on the
  hand that has to keep time — while the left hand's three shapes are still not reliable
  (ii runs on wrist roll, ADR-0041).
- Every sign pays the confirmation cost (150 ms hold plus confirming frames, §3.4) before
  it can sound — already outside the 80 ms on-time window (§3.7) — and a repeated note needs
  the sign dropped and re-formed. In Kodály teaching the voice keeps time and the sign only
  reinforces pitch; the signs were never a trigger.
- Collides with the left hand: Curwen "do" (home, at rest) is a fist, which is this
  system's V (peak tension); "mi" is a flat palm-down hand, which is I.
- Changes L3's concept from place-in-the-chord (R/3/5/7/9) to place-in-the-key (do–ti), and
  several signs are the single-finger shapes co-design rejected (Set 1).
- Kept as: a video option in the right-hand design session. Rungs are labelled with note
  names, not solfege (decision 1).

**Melody shift as a wave gesture.**

- A bidirectional speed-threshold gesture: every wave up has to come back down, and that
  return must not read as a deliberate wave down. The left hand's strike went in one
  direction only and still needed ADR-0025 through ADR-0037 before it was set aside.
- One wave is one step (low root to high 9th is nine waves); a repeated note needs a third
  gesture.
- If the system decides where "up" lands, it can read as the algorithmic melody co-design
  rejected. Only defensible if strictly deterministic.
- Its idea is kept: Part 1 makes "up = higher" a matter of hand position, not motion.

---

### Part 1 — Right hand: a fixed pitch ladder, selection separate from the trigger

**Selection: a pitch ladder.**

- One rung per C-major note, stacked vertically in front of the right shoulder, lowest at
  the bottom: **one column of 10 rungs, C5–E6** (MIDI 72–88; decision 2), which keeps
  §3.1's ≥ MIDI 72 floor. Every chord's root to ninth is reachable at least once in that
  window; G7 has the fewest lit rungs (6 of 10). **Rungs never move** — a chord change only
  changes which ones are lit.
- **Labels: note names** (decision 1) — letters only (C D E F G A B C D E), no octave
  numbers, which would be unexplained notation for a novice (§1.5). Because rungs never
  move, each letter always lives in the same place; a dim rung keeps its label, dimmed.
- **Legal rungs** come from a melody *vocabulary*: `ChordTones` (the active chord's
  R/3/5/7/9 pitch classes — L3) or `Scale` (every C Ionian rung — L4 on). A dim rung
  **rounds to the nearest lit rung** by semitone distance (ties go lower; `[TUNABLE]`).
  This is `DESIGN-SPACE.md` §2's "position picks, system quantises": there is no miss
  state, and the system never chooses a pitch, it only rounds the learner's choice to the
  nearest legal one. The current 5×2 arc (`ChordToneSet`) cannot hold L4's seven scale
  notes; the ladder needs no new mechanism for them.
- **Wrist height** picks the rung — the wrist, not a fingertip, for ADR-0038's reason (a
  fingertip's height moves with the finger). Nearest rung with a Schmitt band (§3.4:
  hysteresis is mandatory). The picked rung glows before anything sounds. Dropping the hand
  below the ladder disarms it (rest position).
- **Guide tones fall out of the geometry.** With the hand still, a chord change re-resolves
  to the nearest tone of the new chord. Held on the F rung, ii–V–I plays F–F–E; held on the
  B rung, it plays C–B–B. Those are the two standard guide-tone lines (the 7th of one chord
  moves a half step to the 3rd of the next; the 3rd stays as the next 7th). This is the
  jazz-specific behaviour to point to, and it becomes a unit test.
- **Why vertical:** people associate high pitch with "up" whether or not they have musical
  training; the left-to-right association (what the arc encodes within a row) is weaker and
  shows up mainly in trained musicians (Rusconi et al., 2006). The up-and-down shape of a
  melody is what untrained listeners retain from it (Dowling, 1978).
- Aiming becomes one-dimensional: only height matters, so each rung is a full-width band,
  and the elbow can rest at the side (the 19 Sep consultation's fatigue point).

**Trigger: swappable, independent of selection.** Each trigger emits a commit (time,
velocity); selection never knows which one is active. Bake-off candidates:

1. **Wrist tap (primary).** The hand drops from the wrist and springs back, like tapping a
   table.
   - Signal: the middle fingertip's downward speed **relative to the wrist**, in the
     head/gravity frame. Moving the whole arm to change rungs moves wrist and fingertip
     together, so it never fires; only the flick does. A flick barely moves the wrist
     (ADR-0028's finding, used the other way round), so selection holds still through the
     tap — and the rung is **latched at stroke start** regardless.
   - Reuses `ChordStrikeDetector`'s logic: Schmitt enter/exit, settle frames before re-arm
     (ADR-0030), freeze through tracking loss (ADR-0031), no stale peaks (ADR-0035). The
     right hand has no held pose and no release, so the strike/release races (ADR-0034,
     ADR-0037) have no counterpart here.
   - **Which instant fires the note is measured, not guessed.** Dahl (2014) found that in
     air drumming the end of the stroke (direction reversal) lands after the intended beat
     and drifts with tempo, while sharp acceleration peaks land just before it and don't.
     The detector records four landmarks per stroke — threshold crossing, speed peak,
     braking peak, reversal — and fires on a configurable one (default: speed peak). The
     timing drill (Part 2) logs all four against the click, so the default is chosen from
     data.
   - Loudness from peak relative speed via `VelocityCurve.FromSpeed`.
2. **Pinch (baseline).** The current `_pinchSpike` rising edge, promoted into `Core`
   (shaped like `ChordPinchDetector`), kept so the bake-off measures against what the
   teacher objected to. With the ladder, selection reads the wrist, not the thumb–index
   midpoint the spike uses (which moves every time you pinch — part of why repeats felt
   bad).
3. **Thumb microgesture tap (spike only, ≈ ½ day).** The Meta XR Core SDK (v74 on) detects
   a thumb tap and thumb swipes on the side of the curled index finger — already installed,
   no new dependency. The hand must be held sideways with the fingers curled; Meta built it
   for discrete commands (teleport, snap turn) and publishes no latency or reliability
   figures. Wire the event and run the drill; drop it if latency or misses aren't
   competitive. If it is competitive, its forward/back swipes are the melody shift in
   miniature (step up/down) and earn one extra drill.

**What stays.** The arc (`TouchTargetRig`/`TouchTargetBinder`, ADR-0019/0020) and
`MelodyEngine.TriggerTarget` are untouched and remain the default until the bake-off
decides (`DESIGN-SPACE.md` §8: the current mechanic stays runnable throughout). Whatever
loses is deleted afterwards (CLAUDE.md rule 9).

**If the ladder is adopted, lessons change with it.** Melody notes are authored as pitches
(e.g. `F5`) instead of degree slots (`targetIndex`, 0–9); L3's seven notes are re-authored;
`TimelinePlayback` and the ghost target highlight resolve through the ladder. Until the
bake-off decides, lessons stay on the arc and the ladder runs in the timing drills.

**Not yet verified.** All of it — in particular: rung spacing against hand-tracking jitter;
whether relative fingertip speed separates a flick from arm motion cleanly on Quest;
whether a novice can hold a height steady while tapping; whether the latch is early enough.

---

### Part 2 — An audible, configurable metronome and a timing drill

**Found while drafting** (each checked against the code):

- **No audible click anywhere.** `LessonRunner.DrainMetronome` discards clicks; free play
  has no pulse at all.
- **L2's Test Yourself has nothing to score.** `LessonRunner.OnNoteTriggered` keeps only
  right-hand melody notes, and `ScoreAttempt` scores against
  `LessonTimeline.ExpectedOnsetSeconds()` — the authored melody notes. L2 is a left-hand
  lesson with no notes, so every attempt is empty. Also, one chord strike emits one note-on
  per voicing pitch (`HarmonyEngine.Strike`), so left-hand onsets need de-duplicating
  before they can be scored.
- **Right-hand-only exercises have no harmony.** The only caller of
  `HarmonyEngine.SetHeldFunction` is the left-hand interpreter (`PerformanceCompositionRoot`).
  In L3's Try Yourself (no system playback, §3.8) the melody only sounds if the learner's
  left hand happens to hold a chord — but L3 is "chord tones over system-played changes"
  (§3.9). A right-hand timing drill has the same problem.
- **Per-frame `[DIAG]` logging** in `PerformanceCompositionRoot.Update` (every frame with
  |vy| > 0.05) and `SamplerNoteSink.Send`/`StartNote` (every note): string interpolation
  plus `Debug.Log` on the gesture/audio path (§2.4). That is GC and log-I/O jitter in
  exactly the timings a drill measures.

**2a. `MetronomeSettings` asset** (`Config/`; every value `[TUNABLE]`, mirrored into
`CALIBRATION.md` when built):

| Setting | Default | Notes |
|---|---|---|
| Tempo | 80 BPM | 40–100; above 100 needs an explicit `AllowUnvalidatedTempo` flag (§3.6) |
| Beats per bar | 4 | `Metronome.DefaultBeatsPerBar` |
| Accent downbeat | on | separate click sound |
| Subdivision | none | none / eighths / swung eighths. The quarter pulse is never swung (§3.6, and `Metronome`'s own contract) |
| Swing ratio (subdivision only) | 0.66 | `SwingRatio.Default` |
| Count-in | 1 bar | drills only; lessons unchanged |
| Click volume | 0.6 | `AudioSource` volume; balance against the piano by ear |
| Click sound | ~2 kHz tick, ~20 ms | generated at start-up (no asset, no licence); sits above the ladder's register (C5–E6, fundamentals ≈ 523–1319 Hz) so it doesn't mask the learner's notes |
| Look-ahead | 100 ms | scheduling horizon only |

**2b. Core.**

- `Metronome`: optional subdivision — off-beat clicks at
  `SwingQuantizer.Swing(beat + 0.5, ratio)`, quarter grid unchanged. `MetronomeClick` gains
  a kind (downbeat / beat / off-beat) and its fractional beat position. Still
  allocation-free.
- `TimingDrill` (pure plan): tempo, bars, count-in, expected-onset pattern (every beat /
  every 2nd beat / every eighth / an authored list of beat positions), swing, scored hand,
  harmony (a fixed chord or a chord timeline), and optionally an expected pitch per onset
  (the target rung, highlighted on the ladder) so wrong notes are counted separately from
  timing → `ExpectedOnsetSeconds()` relative to the shared start. Scoring is
  `OnsetScorer.Evaluate`, unchanged.
- `OnsetCapture`: collects the **learner's** onsets from the note stream, filtered by hand
  and by channel — `Melody` or `Harmony`, never `Accompaniment` (system playback). Left-hand
  note-ons sharing a timestamp (one strike's voicing) count as one onset.
- `ScriptedHarmony`: drives `HarmonyEngine.SetHeldFunction` from a timeline or a fixed
  function — silent by construction (ADR-0025) — so a right-hand-only drill or lesson has a
  chord without the left hand. Wired by the composition root, so the §2.3 direction rule
  holds.
- **Chords audible in right-hand lessons (decision 3).** `ModePolicy.For(mode, hands)`: for
  a right-hand lesson, Try Yourself and Test Yourself play the harmony only (a new
  `SystemPlayback.HarmonyOnly`); left-hand lessons are unchanged, since there the learner
  plays the chords. `TimelinePlayback` gains a harmony-only option. The audible chords come
  from `TimelinePlayback` on the `Accompaniment` channel; the harmonic state comes from
  `ScriptedHarmony`, driven by the same timeline and start time, so the chord the learner
  hears is the chord the ladder (or the arc) follows.

**2c. Unity.**

- `MetronomeVoice` (`Audio/`): a pool of ≥ 4 `AudioSource`s, round-robin, pumping
  `Metronome.TryDequeueClick(now + lookAhead, …)` and calling `PlayScheduled(click.DspTime)`
  — the design §3.6 and `Metronome`'s doc comment already describe. `Stop()` also stops
  every pool source, which cancels clicks already scheduled. Clicks bypass `INoteSink`
  (they are not part of the performance); one log line per run records the grid (start DSP
  time, tempo, bar length, subdivision, swing) so every click time can be reconstructed.
- **One shared start time.** Lessons anchor the phrase clock, `TimelinePlayback` and the
  metronome at `now`, so the first click cannot be scheduled ahead. Use
  `t0 = now + preRoll` (≥ look-ahead) for all three (`TimelinePlayback.Start(double atDspTime)`
  overload).
- `LessonRunner`: hand the metronome to the voice instead of `DrainMetronome`;
  `LessonDefinition._useMetronome`'s "silent" tooltip goes. L2 scores left-hand strikes via
  `OnsetCapture` against a pulse pattern authored on the lesson. For right-hand lessons,
  play the chords (decision 3) and record the learner through `OnsetCapture`'s channel
  filter. Today `OnNoteTriggered` ignores every note while `_playback` is running (it
  assumes playback means a demonstration), so turning the chords on without this change
  would silently stop Test Yourself from recording the learner at all.
- **Timing test menu.** The session list gains a "Timing test" row after "Free play",
  opening a list of `TimingDrillDefinition` presets (ScriptableObjects; a new preset needs
  no C# change — the same rule as lessons, §3.9). A preset = metronome settings + pattern +
  bars + scored hand + harmony (fixed chord or chord timeline; audible by default, matching
  decision 3, with a per-preset switch) + optional target rungs + scoring on/off
  ("Metronome only, 80 BPM" is a preset with scoring off, for informal free play). The list's first row cycles the right-hand
  mechanic at runtime (`DESIGN-SPACE.md` §5's switch), so one headset session covers every
  candidate.
- **Results.** One deferred line on the HUD after the run (§3.7 — never mid-phrase), e.g.
  "16 beats: 12 on time · 3 close · 1 off · 0 missed · 1 extra · avg 34 ms late". A CSV per
  run under `Application.persistentDataPath/timing/` (`adb pull`): a header (drill,
  mechanic, tempo, subdivision, swing, build, tester code), one row per expected onset
  (expected, actual, deviation, verdict), the extra onsets, and the wrist-tap stroke
  landmarks when that trigger is active. Derived timings only, no joint positions (§4.1
  data-handling note). Anonymous tester codes; confirm informal testers are covered by the
  ethics approval.
- **Desk keys** (Editor / Link): `M` metronome on/off, `-`/`=` tempo ∓5 BPM, `B` cycle the
  subdivision.
- **Quiet build.** Route every `[DIAG]` log through one helper marked
  `[System.Diagnostics.Conditional("JAZZTURES_DIAG")]`, so the calls — and their string
  interpolation — compile out unless the define is set. Off for every timing run.

**Reading the numbers.**

- Timestamps are quantised by the hand-tracking frame (≈ 11–17 ms at 60–90 Hz), and
  `AudioSettings.dspTime` can advance in audio-buffer-sized steps — log its step size on
  device once. Treat differences between mechanics under ~20 ms as noise.
- People tap ahead of a metronome (negative mean asynchrony, typically tens of ms; Repp,
  2005), so a negative mean error is human, not a system fault.
- A measured deviation folds together tracking latency, detection latency and anticipation.
  The shared parts cancel when mechanics are compared on the same build — compare
  mechanics, not absolute zeros.

---

### Implementation notes — steps 1–2

Built on `feat/quiet-build-metronome`. Where the build departs from, or fills a gap in, the
text above:

- **Quiet build covers more than `[DIAG]`.** `DiagLog.Log` (`Diagnostics/`) is the one door;
  the four `[DIAG]` call sites use it. `[POSTURE]` (`MetaXRHandPostureSource`, one line *per
  frame*) and the per-note `[PinchSpike]` line (`TouchTargetBinder`) go through it too: the
  done-when is "free play logs nothing per frame", and `main.unity` had both flags switched
  on. One-off lines (errors, the spike's session-end summary, the metronome's per-run grid
  line) stay on plain `Debug.Log`. Left alone: `HandPoseFrameLogger` (opt-in component, logs
  on change), `EventChannel._logToConsole` (editor-only flag), `LessonRunner._logCues` (cue
  events, not frames). To bring the logs back, add `JAZZTURES_DIAG` to Scripting Define
  Symbols.
- **A new port, `IMetronomeOutput` (`Core/Ports`).** `LessonRunner` gets the metronome sound
  through it (`Bind`'s optional fourth argument), so `Jazztures.Lessons` does not take a
  dependency on `Jazztures.Audio` (§2.2). `MetronomeVoice` implements it; the composition
  root passes it in.
- **Pre-roll only for lessons that use the metronome.** `t0 = now + PreRollSeconds` anchors
  the phrase clock, `TimelinePlayback.Start(double)` and the metronome. Lessons with no
  metronome (L1, L3) start immediately, as before. The phrase beat is clamped at 0 during the
  pre-roll so the cue cursor does not advance before the first click.
- **Settings the ADR's table did not list:** off-beat volume scale (0.5), downbeat click pitch
  (2800 Hz; beat click is the table's ~2 kHz), pre-roll (150 ms, never below look-ahead), voice
  pool size (4), desk-key tempo step (5 BPM). All `[TUNABLE]`, set by judgement; logged in
  `CALIBRATION.md` as unmeasured.
- **Count-in is not in `MetronomeSettings` yet.** Only drills use it, so it arrives with
  `TimingDrill` (step 3/4) rather than as an unread field.
- **`ClickInFreePlay` defaults to on**, so "clicks audible in free play" holds on a headset
  before the timing-test menu (step 4) exists. Revisit when that menu can start a pulse.
- **Tempo bounds live in `Core/Timing/TempoRange`:** 40–100 BPM, lifted only by
  `AllowUnvalidatedTempo`, plus a 240 BPM absolute ceiling as an Inspector typo guard (not a
  validated figure).
- **A click already in the past is dropped, not played late** (a frame hitch longer than the
  look-ahead); the first drop per run logs a warning.
- **In-headset tempo control (added at the student's request after the first device build;
  not in the ADR's text).** The session menu gets "+" / "-" buttons with a "Free-play click /
  N BPM" readout, to the right of the rows: `LessonSelector.SetSideControl` / `SideStepped`
  (a generic stepper — the selector stays a dumb view), wired by `LessonFlowController` to
  `FreePlayMetronome.StepTempo`. Pinch-to-step uses the selector's existing ray and pinch.
  Choices: it sets **free play's** tempo only (a lesson carries its own, §3.9), so it shows on
  the top-level menu and not on a lesson list; the tempo persists across entering and leaving
  free play; a press on the menu plays `TempoPreviewBars` of click at the new tempo, since
  free play isn't running there to hear it; the step (5 BPM) and the preview length are
  `MetronomeSettings` fields. **Cost:** one more element on the learner-facing menu, against
  §3.10's visual restraint — it is a testing affordance, so `LessonFlowController`'s
  `_showTempoControl` removes it without a code change (do that for a study build). Still
  missing from the headset: an on/off switch and subdivision (desk keys only), and the
  ADR's step 4 timing-test menu.
- **Not verified:** how the click sounds against the piano on a Quest; that the clicks land
  on their DSP samples (`AudioSettings.dspTime` step size is still to be logged on device,
  per *Reading the numbers*); the `LessonRunner` pre-roll arithmetic, which sits in a
  MonoBehaviour and has no automated test; and the menu stepper — its placement, whether the
  buttons are easy to hit with the pinch ray, and that its text fits, are all unseen on a headset.

### Implementation notes — steps 3–4

Built on `feat/quiet-build-metronome`. Where the build departs from, or fills a gap in, the
text above:

- **`OnsetPattern` (`Core/Timing`) is the one description of "where the learner should play".**
  Every beat / every 2nd beat / every eighth / authored positions that repeat each bar.
  `TimingDrill` and a lesson's scored pulse both use it; positions are straight beats and
  swing is applied afterwards, so every-eighth off-beats swing with the phrase. An authored
  list is **per bar, repeated** (the ADR said only "a list of beat positions"; a per-bar rule
  is what "comp on 1 and 3" and a repeated rhythm need without writing out every bar).
- **L2's pulse is data on the lesson.** `LessonDefinition._scoredPulse` (an `OnsetPatternSpec`)
  becomes `LessonTimeline.ScoredPulse`, spanning beat 0 to the end of the timeline, and
  `ExpectedOnsetSeconds()` merges it with the melody notes. L2's asset is set to *every beat*,
  so its Test Yourself now has four expected onsets (chords at beats 0 and 4 → beats 0–3).
  The pulse starts with the phase — a lesson has no count-in (§3.6 table: "drills only").
- **`OnsetCapture` implements `INoteSink`** and replaces `LessonRunner`'s hand-rolled list. It
  takes note-ons from the scored hand's own channel only (left = `Harmony`, right =
  `Melody`); `Accompaniment` is never captured. Left-hand notes within 5 ms count once (one
  strike's voicing); right-hand notes never merge. `LessonRunner` no longer drops every note
  while a playback exists — only during Watch and Listen's *melody demonstration*, so harmony-
  only playback does not hide the learner.
- **`ScriptedHarmony` lives in `Core/Lessons`**, not `Core/Harmony`, because it consumes a
  `LessonTimeline` and `Lessons` already depends on `Harmony`. `Stop()` releases the selection
  only if it started one.
- **Right-hand lessons now sound their chords in Try / Test Yourself.** `ModePolicy.For(mode,
  hands)` returns `HarmonyOnly` for those two modes when the lesson has a right hand and no left
  (L3–L5); `LessonPhase` carries its lesson's hands so `phase.Policy` is the hands-aware one.
  CLAUDE.md §3.8 has the footnote. `LessonRunner` plays `TimelinePlayback(includeMelody: false)`
  and starts `ScriptedHarmony` at the same shared start. The phase still ends on the phrase
  clock (duration + tail), not when the chords stop.
- **Not built, on purpose: "expected pitch per onset" and the wrong-note count.** They only
  mean something once there are pitch targets (the ladder, steps 6–7); on the arc the target is
  a *degree*. `TimingDrill` and the CSV have no pitch column yet.
- **A fixed drill chord is struck on every bar line**, count-in included, because a piano chord
  decays (one strike would be gone by bar 3). The timeline is given a minimum duration
  (`LessonTimelineBuilder.WithMinimumDuration`) so the last chord rings out instead of dying
  after the default tail.
- **Drills, as built.** `TimingDrillDefinition` is a ScriptableObject in the `LessonCatalog`'s
  new `Timing Drills` list; a new preset is an asset. `TimingDrillRunner` anchors the click, the
  chords and the capture at one `t0` a pre-roll after "now"; the count-in is beat 0 of `t0`, and
  every time in the CSV is seconds from there. Scoring is `OnsetScorer.Evaluate`, unchanged.
  A scored left-hand drill may not also script harmony (the learner plays it).
- **Results appear under the menu, not as a HUD caption.** The ADR said "one deferred line on
  the HUD". The HUD's caption fades in a few seconds and its width wraps at ~34 characters; a
  tester needs to read six counts and an average. After the run the flow returns to the Timing
  test menu and `LessonSelector.SetFooter` shows the line until the next run or leaving the
  menu. Same rule (§3.7): nothing is shown mid-phrase. The line says "N notes" (the ADR's
  example said "N beats", wrong for an every-eighth drill).
- **CSV** — `Application.persistentDataPath/timing/<UTC stamp>_<drill>_<tester>.csv`, UTF-8,
  invariant-culture numbers. `#`-prefixed `key,value` header lines (drill, tester, build,
  mechanic, left-hand commit gesture, tempo, subdivision, swing, scored hand, pattern, count-in,
  windows), then `index,kind,expected_s,actual_s,deviation_ms,verdict` with `kind` ∈ `onset` /
  `missed` / `extra`. Derived timings only (§4.1). The wrist-tap stroke landmarks the ADR lists
  wait for that trigger (step 7). A failed write is logged and never shown to the learner (§4.4).
- **The mechanic row cycles what exists today:** arc + touch and arc + pinch
  (`TouchTargetBinder.PinchSpike`, now switchable at runtime). The ladder's triggers join it at
  step 7. Leaving the Timing test restores whatever the scene was authored with, so lessons
  and free play are not left on the pinch spike.
- **Tester code is an Inspector string on `TimingDrillRunner`** (default `T00`), not something
  entered in the headset. **Open for the student: confirm informal testers are covered by the
  ethics approval before collecting anyone's runs.**
- **Starter presets** are made by the menu `Jazztures ▸ Timing ▸ Create Starter Drills`: right
  hand over an audible I chord at 60 and 80 BPM, one onset per beat and per eighth (4 bars after
  a 1-bar count-in); a left-hand comping drill on beats 1 and 3; a click-only run. The
  pitch-specific bake-off drills (alternating rungs, a repeated shape) wait for the ladder.
- **Not verified:** all of it on a headset — in particular that the harmony-only chords sit well
  under the learner's notes, that the menu fits and reads at 8 rows, and that the CSV pulls
  cleanly with `adb`. The runners' frame-by-frame behaviour is MonoBehaviour code with no
  automated test; the pure parts (`OnsetPattern`, `OnsetCapture`, `ScriptedHarmony`,
  `TimingDrill`, `DrillReport`) are covered headlessly.

### Order of work

Part 2 first: it is the measuring instrument, it unblocks L2, and it can baseline today's
arc + touch and arc + pinch before Part 1 exists. Sizes are rough (S ≈ ½ day, M ≈ 1–2 days).

1. **Quiet build** (S). *Done when* a free-play session logs nothing per frame.
2. **Metronome subdivision, `MetronomeVoice`, `MetronomeSettings`, shared start time** (M).
   *Done when* clicks are audible in free play and in L2, the new `MetronomeTests` pass, and
   CLAUDE.md §7's `MetronomeVoice` item can be closed.
3. **`OnsetCapture`, `TimingDrill`, `ScriptedHarmony`, harmony-only playback,
   `ModePolicy.For(mode, hands)`, with tests** (M). *Done when*
   `dotnet test DotNet/Jazztures.sln` is green, including "one strike's voicing is one
   onset", "scripted harmony sends no note events", "system chords are never captured as
   the learner's onsets", and "a right-hand Test Yourself attempt still records the learner
   while the chords play". CLAUDE.md §3.8 gets decision 3's footnote in the same change.
4. **Timing test menu, presets, HUD line, CSV** (M). *Done when* a drill runs end to end on
   device and the CSV pulls cleanly.
5. **Baseline** (S): run the drills on arc + touch and arc + pinch.
6. **`PitchLadder`, ladder selector, `MelodyEngine.TriggerPitch`, vocabulary, with tests**
   (M) — cleared by decision 4. First add a scoped exception under CLAUDE.md §3.1's
   "do not sort targets by pitch", pointing here (the rule still governs the arc; the
   ladder sorts by pitch by design), so an agent reading §3.1 doesn't stop on the conflict.
   Tests include the held-hand guide-tone lines (F rung → F–F–E; B rung → C–B–B), ties
   resolving lower, and hysteresis at a rung boundary.
7. **Right-hand adapter, wrist-tap and pinch triggers with tests, ladder rig, mechanic
   switch** (M–L).
8. **Microgesture spike** (S, optional).
9. **Bake-off, then decide and record** (S): update CLAUDE.md §1.3/§3.1/§3.3/§7,
   `ARCHITECTURE.md`, `CALIBRATION.md`, and the pending thesis-prose list.

### Bake-off

- **Drills** at 60 and 80 BPM, right hand alone over an audible I chord (decision 3),
  1-bar count-in, 4 bars each: one rung on every beat; one rung on every eighth; two
  neighbouring rungs alternating on eighths; a four-note up-and-down shape repeated. Then
  the same with the left hand comping on beats 1 and 3.
- **Guide-tone check** (ladder only): one note per beat over audible ii–V–I changes, a bar
  each, with the hand held still on the F rung, then on the B rung. Expect F–F–E and C–B–B;
  anything else is a ladder bug, not a learner error.
- **Per mechanic:** missed, extra (double) and wrong-rung counts; mean and spread of signed
  timing error; share on time (≤ 80 ms); NASA-TLX afterwards; one preference question.
- **Testers:** at least two people besides the developer (19 Sep consultation: "the system
  might be only for you").
- **Gate** (proposed; the student decides): adopt the ladder with the trigger that has the
  fewest missed + extra notes on the repeated-note drills and is at least as on time as the
  pinch baseline, with NASA-TLX no worse. If nothing beats the baseline, keep the arc and
  record why.
- Results and the options list feed the right-hand co-design session recommended on 19 Sep
  (list options → participants choose → build → try in VR).

### Open questions

The questions this ADR was drafted with — rung labels, two octaves vs. one column, whether
chords sound in right-hand lessons, and sign-off on breaking §3.1's degree map — were
answered by the student on 2026-10-08 (decisions 1–4, header). Still open:

- If the left hand moves to Design A (height picks the chord, ADR-0038), both hands would be
  working in height — check they don't pull each other along.

### Thesis impact

- **Part 1, if adopted:** §1.3's right hand ("touch targets in mid-air") becomes "hand
  height selects from a fixed pitch ladder; a tap — or whichever trigger wins — plays".
  **Two `[THESIS]` changes to §3.1** (CLAUDE.md rule 3: the student decides, an agent does
  not):
  - "Spanning two octaves" becomes one column of ten notes, C5–E6, labelled by note name —
    **decided** (decisions 1 and 2).
  - "Stable spatial map of scale degree … do not sort targets by pitch" becomes a stable
    map of pitch — **signed off** (decision 4). This goes beyond the 2026-09-15 exploration
    mandate, which released §1.3's gesture rules but deliberately kept §3.1's degree map
    (Design B was "re-pitched, never re-arranged"); decision 4 is what clears step 6.

  §3.3's note-on ("fingertip enters a target volume", entry-velocity gate) becomes the
  chosen trigger's rule. L3's concept text changes with the labels: the lit rungs are the
  chord's notes, and the letters are the notes' names (§1.5). Chapter 4 gains citable
  grounding (Rusconi et al., 2006; Dowling, 1978; Dahl, 2014), and the guide-tone property
  belongs in the design chapter as the jazz-specific rationale.
- **Decision 3, independent of the bake-off** (applies with the arc too): CLAUDE.md §3.8's
  mode table says "System plays: no" for Try Yourself and Test Yourself. For a right-hand
  lesson that now means no melody demonstration while the harmony still plays — which is
  what §3.9 ("over system-played changes") and the proposal's L3 description ("the system
  plays chords") already say. The §3.8 table needs a footnote and Chapter 6 matching
  wording. Added to the pending thesis-prose list below.
- **Part 2:** none to Chapter 6 — L2's metronome is already in the lesson plan. Drill
  results are technical-evaluation data for Chapter 7, alongside the §4.3 latency
  percentiles.
- No thesis prose edited (rule 7). Part 1's entries go on the pending thesis-prose list if
  it is adopted.

**References.** Dahl, L. (2014). Triggering sounds from discrete air gestures: What movement
feature has the best timing? *Proceedings of NIME 2014*, 201–206. · Dowling, W. J. (1978).
Scale and contour: Two components of a theory of memory for melodies. *Psychological
Review, 85*(4), 341–354. · Repp, B. H. (2005). Sensorimotor synchronization: A review of the
tapping literature. *Psychonomic Bulletin & Review, 12*(6), 969–992. · Rusconi, E., Kwan,
B., Giordano, B. L., Umiltà, C., & Butterworth, B. (2006). Spatial representation of pitch
height: The SMARC effect. *Cognition, 99*(2), 113–129.

---

## ADR-0042 — A touch that arrives before selection confirms was discarded outright; give it a bounded grace window instead

**Date:** 2026-09-23 · **Status:** Proposed — headless-tested; unverified on device.
**Milestone:** M3 (revises `Core/Gesture/ChordTouchDetector`, `TouchCommitThresholds`)
**Changes the thesis:** none — a reliability fix within ADR-0039's existing mechanism, not
a gesture or chord change.

**The finding.** Reported on-device: touching the strike plate (ADR-0039) while a chord is
selected sometimes produces nothing at all — not a wrong chord, no sound. Reproducible by
reading, not just by report: `ChordTouchDetector.Feed` (pre-fix) required a *rising edge*
(fingertip outside → inside on the same frame) to even consider articulating, and treated
any rising edge that arrived before `GestureInterpreter.ConfirmedFunction` had a value as
simply spent — `_wasInside` was still latched `true` on that frame, so once selection
*did* confirm a moment later, there was no fresh rising edge left to key off. The fingertip
would have to leave the volume and re-enter to fire at all.

**Root cause.** ADR-0039's own reasoning — "with nothing selected, an entry here is simply
spent, no re-arm budget needed... a deliberate approach into a fixed volume is
unambiguous" — implicitly assumed selection would already be settled *before* the hand
reached the plate. That doesn't hold for a single continuous reach-and-touch motion: the
arm travel that carries the fingertip into the target is exactly the motion most likely to
still be inside `GestureThresholds.PoseHoldSeconds`'s confirmation window (150 ms, plus its
own miss-tolerance retries) when it arrives — more so for ii specifically, whose `WristUp`
selection is already the least reliable to confirm quickly (ADR-0041). `ChordTouchDetectorTests`
had a test for "touch with nothing selected → no articulation" in isolation
(`ATouchWithNothingSelected_DoesNotArticulate`) but nothing exercising what happens if
selection *then* confirms while the fingertip is still resting inside — the exact sequence
a real reach produces, and structurally impossible to pass before this fix.

**Decision.** A rising edge with no confirmed function no longer discards its entry speed —
it goes *pending* (`ChordTouchDetector._awaitingConfirmation` / `_awaitingEntrySpeed`,
`_awaitingSince`) and is retried every frame the fingertip stays inside the target, from
both `Feed` overloads (once at the top of `Feed(bool, float)`, for the plain call-then-feed
style the tests use and to catch a confirmation that landed on a prior frame; once more
after `_interpreter.Feed(frame)` inside `Feed(HandPoseFrame)`, to catch a confirmation that
lands on the *same* frame at zero added latency — the same reasoning ADR-0037 already
established for the strike/pinch/touch ordering). A pending entry resolves one of three
ways: confirmation lands → fires immediately using the *originally captured* entry speed
(the fingertip is stationary by then, so re-reading its live speed would fail the velocity
gate); the fingertip leaves the volume → cancelled, the next approach starts fresh; neither
happens within `TouchCommitThresholds.MaxAwaitingConfirmationSeconds` (350 ms, new,
ADR-0042's own guess, sized to comfortably cover the 150 ms confirmation window plus
margin) → given up on, matching the original "simply spent" behaviour as the fallback.

**Why bounded, not indefinite.** Without the grace window, a touch attempted while nothing
is selected would remain pending forever, and could fire on some unrelated, much-later
confirmation if the fingertip happened to still be resting on the plate when the learner
next held a pose — using stale entry-speed data from a completely different attempt. Every
other tolerance window in this codebase (confirmation miss tolerance, release miss
tolerance, strike settle frames) is similarly bounded rather than open-ended; this follows
the same pattern.

**What stays unchanged.** The already-confirmed path (rising edge, `ConfirmedFunction`
already set) is untouched and still fires within the same call, preserving ADR-0037's
same-frame race guarantee (`ATouch_CancelsAReleaseThatIsPendingAtThatMoment`,
`FeedingAWholeFrame_LetsATouchClaimTheSameFrameAReleaseWouldOtherwiseConfirmOn`,
`FeedingSeparately_InterpreterFirst_LetsTheReleaseSwallowTheTouch` all still pass
unmodified). The velocity gate and retrigger cooldown are unaffected and still spend an
entry outright on failure — only the "nothing selected yet" case now waits.

**Test.** `ChordTouchDetectorTests` gains
`AnEntryBeforeConfirmation_ArticulatesOnceConfirmationLandsWhileStillInside` (the bug's
exact repro: touch while unconfirmed, confirm afterward, still inside → fires, using the
captured entry speed even though the live speed has since dropped to zero),
`AnEntryBeforeConfirmation_ThatLeavesBeforeConfirming_DoesNotArticulate` (cancels
correctly on exit), and
`AnEntryBeforeConfirmation_PastTheGraceWindow_DoesNotArticulate` (the bound actually
bounds). All pre-existing tests pass unmodified.

**Not yet verified.** Whether 350 ms is the right grace window, and whether this actually
resolves the on-device symptom rather than merely one plausible mechanism for it — the
plate-touch failure could still have a second, unrelated cause. Read `[DIAG]`-style
candidate/confirmed logging on the `Touch` path (still absent — ADR-0041's own open item)
before concluding this alone fixes it.

**Thesis impact:** none.

---

## ADR-0041 — Widen the `WristUp` cone: ii is the one pose built on a roll signal, and roll is noisier

**Date:** 2026-09-22 · **Status:** Proposed — unverified on device.
**Milestone:** M3 (edits `Config/GesturePalmConeThresholds.asset` only — no code change)
**Changes the thesis:** none — a recognition-tolerance tweak, not a gesture or chord change.

**The finding.** On-device testing found ii's selection and (downstream) its Touch
articulation both noticeably less reliable than V or I's — reported as "the ii chord is the
most inconsistent," with V and I "relatively more consistent" by comparison, and both a
selection-side failure (the pose won't confirm) and an articulation-side failure (touching
the plate sounds nothing) observed for ii specifically.

**Root cause (by elimination, not yet log-confirmed).** ii, V, and I each key off a
different SDK signal, and the three are not equally noisy. V is pure finger curl (`Fist`).
I is `PalmDown`, an orientation check against palm-forward direction. ii, per ADR-0014/0026,
has no clean palm-orientation signal available and is instead built on `WristUp` — a
wrist-*roll* check (rotation about the forearm's own long axis). Roll is generally the
weakest-constrained axis for camera-based hand tracking (there is much less visual signal
for twist-about-the-bone than for the bend/pitch the other two poses use), and it is also
harder for a person to hold at a precise, steady angle than a curl or a forward-facing
direction. A roll reading that wanders outside the `WristUp` cone for more than the ordinary
2-frame confirmation miss tolerance (§3.4) restarts the whole 150 ms hold — explaining the
selection-side symptom directly. The articulation-side symptom follows from the same noise:
if `ConfirmedFunction` drops to null right as the plate is touched, `ChordTouchDetector`
correctly declines to fire (nothing selected) — which reads, from the outside, as "I touched
it and nothing happened," even though the detector did the right thing with no function
selected.

**Decision.** Widen the `WristUp` feature's Schmitt cone in
`Config/GesturePalmConeThresholds.asset` (feature index 0): `_thresholdWidth` `20° → 30°`,
midpoint unchanged at 40° — enter `30° → 25°`, exit `50° → 55°`. A data-only asset edit, no
code change. Low risk: ii/I ambiguity (both `OpenPalm`) already falls back to "hold whatever
was last confirmed" when both cones match (§3.4's ambiguity rule) — so a wider WristUp cone
can only produce more frequent ambiguity holds, never a wrong chord.

**Not yet verified.** No log-based confirmation that roll noise is actually the mechanism —
this is a diagnosis by elimination (the one pose on a different, harder-to-hold signal is
the one pose that's flaky) rather than one read off `[DIAG]` output, unlike this session's
earlier strike-path bugs. If widening the cone doesn't resolve it, the next step is adding
live candidate/confirmed logging to the discrete-pose path (currently there is none active
while `_commitGesture == Touch`) to read the actual sequence around a failed attempt.

**Thesis impact:** none.

---

## ADR-0040 — Body-anchor the strike plate, reusing the melody arc's own follow logic verbatim

**Date:** 2026-09-22 · **Status:** Proposed — code lands complete; no automated coverage
possible for a MonoBehaviour anchor, and it is unverified on device.
**Milestone:** M4 (adds `Presentation/ChordStrikeTargetAnchor`; extends
`PerformanceCompositionRoot`)
**Changes the thesis:** none — refines ADR-0039's placement, not the taught gesture.

**The finding.** `ChordStrikeTarget` (ADR-0039) shipped as a plain, world-fixed `Transform`
positioned by hand in the Editor — asked directly, the student described wanting a flat
plane you strike down onto that, like the right hand's melody arc (ADR-0015), follows the
learner rather than sitting at one fixed point in the room.

**Decision.** Reuse `Core.Melody.LazyRecenter`/`LazyRecenterSettings` — the pure, headlessly
tested yaw-follow logic the melody arc already relies on — unchanged, including its exact
default thresholds (`LazyRecenterSettings.Default`: 35°/0.6s/0.5s). This is the same body,
answering the same "how much yaw drift before the rig should follow" question the melody
arc already answered; nothing about a left-hand plate justifies re-deriving it. Only the
anchor *offsets* are new — mirrored to the left (`_lateralOffsetMetres = −0.20`). Height was
revised during the same session, before any device test: shipped first at `−0.45` (a fresh
guess, reasoned as "struck downward near waist height, not reached out to"), then moved to
`−0.25` — the same value as `MelodyConfig.ShoulderHeightOffsetMetres` (ADR-0015) — on direct
instruction, so both hands' target objects sit at the same level rather than the left plate
inventing its own. The lateral offset remains a new, undemonstrated guess.

**A flat disc doesn't need to face the learner.** `TargetVolume`'s local Z is always the
approach axis (ADR-0016/0018) and the containment test is a circular cylinder — rotationally
symmetric about that axis. `ChordStrikeTargetAnchor.ApplyAnchor` fixes rotation to
`Quaternion.Euler(-90, 0, 0)` (local Z pointing straight up, so the disc lies horizontal,
struck from above) and never touches it again. Unlike the melody arc, which must rotate its
whole ring to keep each target's approach axis pointed at the learner as they turn, the
plate's *position* needs to follow head yaw (so it stays in front of the body) but its
*orientation* never does. This is a direct, checkable consequence of one already-tested
piece of code (`TargetVolume.Contains`), not a new claim.

**Explicit call, not `LateUpdate` ordering — following ADR-0039's own precedent.**
`TouchTargetRig` anchors itself in `LateUpdate()` at `DefaultExecutionOrder(-10)`, relying on
Unity's phase ordering to land before whatever reads the target transforms. ADR-0039 already
rejected that pattern for `ChordStrikeTarget.Sense()` in favour of an explicit call from
`PerformanceCompositionRoot.Update()`; `ChordStrikeTargetAnchor.Reanchor()` follows the same
rule for the same reason — the composition root calls it immediately before `Sense()`, so
there is no frame where sensing runs against a stale anchor.

**Optional, not required.** `PerformanceCompositionRoot._strikeTargetAnchor` may be left
unassigned — Touch mode then behaves exactly as ADR-0039 shipped it, a fixed plate. Assigning
it (on the same GameObject as `_strikeTarget`, enforced by `[RequireComponent]`) switches on
the follow.

**Not yet verified.** Both offset guesses are unmeasured — the "waist height, close in"
description is a description of intent, not a measurement. First device session should
check whether the plate physically ends up in a strikeable spot at all before judging
anything about the touch mechanism itself.

**Thesis impact:** none beyond ADR-0039's existing note.

---

## ADR-0039 — A third articulation commit: touching a virtual object, reusing the melody targets' own proven volume test

**Date:** 2026-09-22 · **Status:** Proposed — code lands complete and fully headless-tested;
the Unity components and scene wiring are unverified on device.
**Milestone:** M3/M4 (adds `Core/Gesture/TouchCommitThresholds`, `ChordTouchDetector`;
adds `Presentation/ChordStrikeTarget`; extends `HandPoseFrame`/`HandPoseRecording`; revises
`PerformanceCompositionRoot`)
**Changes the thesis:** none beyond what ADR-0038 already flags — this changes which
articulation mechanism is behind the same switch, not the taught gestures.

**The finding.** After ADR-0038 shipped, on-device testing reported no change — traced to
the scene never actually being switched to it (`PerformanceCompositionRoot._commitGesture`
was still `Strike`, `_handPoseSource` still pointed at the discrete
`MetaXRHandPoseSource`, and neither `_harmonicField` nor the new
`MetaXRHandPostureSource._config` were assigned): the new components existed in the scene
but were never wired live. Separately, and independently of that wiring gap, the student
proposed a third articulation mechanism: a virtual object the left hand must touch to sound
the chord, using the same hand that performs selection.

**Decision.** Reuse `Core/Melody/TargetVolume.cs` — the exact cylinder-containment struct
the right hand's ten melody targets already use (ADR-0016/0018) — for the left hand's
single strike target, rather than a velocity threshold (ADR-0025) or the SDK's pinch flag
(ADR-0038). This is the first articulation mechanism in this project's history built from a
piece that has already worked reliably on this hardware — melody's touch targets have not
had a single reliability report in this entire session, unlike every left-hand mechanism
tried. `ChordTouchDetector` mirrors `ChordPinchDetector`'s shape exactly (the ADR-0037
`Feed(HandPoseFrame)` ordering built in from the start again), differing in two ways: it
adds an entry-velocity gate (`TouchCommitThresholds.EntryVelocityGateMetresPerSecond`,
rejecting a resting hand that drifts in rather than deliberately approaches), and its
velocity comes directly from `VelocityCurve.FromSpeed` against a real m/s entry speed
rather than a normalised 0..1 value, since containment naturally carries a physical speed
the way pinch strength does not.

**Both new thresholds are inherited, not guessed.** `MinInterTouchSeconds` (120 ms) is the
same value as `MinInterStrikeSeconds`. `EntryVelocityGateMetresPerSecond` (0.08 m/s) is
`MelodyConfig`'s own entry gate, verbatim — the one commit-signal default in this project's
history that has already been on-device pressure-tested (ADR-0018 fixed the exact "careful
aiming... dropped silently" failure this same gate answers), just for the other hand. This
is a meaningfully higher-confidence starting point than either ADR-0038's ten invented
landmark/radius values or its two invented pinch-rate bounds.

**Self-contained sensing, called explicitly, not left to `Update()` ordering.**
`ChordStrikeTarget` senses its own containment and peak entry speed (mirroring
`TouchTargetBinder`'s exact fingertip-speed algorithm — unsigned `Vector3.Distance`/dt, peak
over a window, no direction-reversal concern the way ADR-0035's signed derivative had,
since containment is a boolean "inside right now"), but `PerformanceCompositionRoot.Update()`
calls `Sense()` explicitly before reading its results, rather than relying on the target's
own `Update()` having already run this frame — Unity does not guarantee component update
order, and `MetaXRHandPoseSource`/`MetaXRHandPostureSource` already solve the identical
problem for their own `CurrentFrame` via per-`Time.frameCount` memoization.

**Orthogonal to selection.** `ChordTouchDetector` and `ChordStrikeTarget` do not care
whether the discrete pose recognisers or `HarmonicField` produced the confirmed function —
`HarmonyCommitGesture.Touch` composes with either hand-pose source. `PerformanceCompositionRoot.Update()`
merges the target's sensed state into the frame it already has before feeding it to
whichever detector is active.

**What stays, unchanged, behind the switch.** All of ADR-0025's and ADR-0038's code is
untouched; `HarmonyCommitGesture` gains a third value (`Touch`) alongside `Strike`/`Pinch`.

**Not yet verified.** `ChordStrikeTarget` and the `PerformanceCompositionRoot` wiring are
outside the `dotnet test` mirror; 14 new EditMode tests cover every line of
`ChordTouchDetector`'s logic headlessly (389 total, all green). Unverified on device: where
in space the target should sit relative to wherever the hand naturally holds a pose (no
existing anchor concept — the target is a plain placed `Transform`, positioned by hand in
the Editor); whether the fixed target position works across ii/V/I or needs to move with
the selected function; and whether one shared target for all three functions is confusing
compared to one per function (deferred — start with one, per the student's request).

**Manual in-editor setup required.** Create a GameObject with `ChordStrikeTarget`
(optionally a child sphere mesh for a visual, wired to its `_renderer` field), position it
somewhere reachable while holding a left-hand pose, assign its `_leftHand` reference (the
same `IHand` component the other adapters use); on `PerformanceCompositionRoot`, set
`Commit Gesture` to `Touch` and assign `_strikeTarget`. Works with the existing
`_handPoseSource` unchanged (either `MetaXRHandPoseSource` or `MetaXRHandPostureSource`) —
this mechanism does not require switching selection mechanisms at all.

**Also fixed while here: the ADR-0038 wiring gap.** Documented above as "the finding" — no
code change was needed, only correcting the scene: pointing `_handPoseSource` at
`MetaXRHandPostureSource`, assigning `_harmonicField` in two places, and setting
`_commitGesture` to `Pinch`, if Design A's pinch path is still worth testing on its own
terms later.

**Thesis impact:** none beyond ADR-0038's existing note.

---

## ADR-0038 — Design A: continuous (height, openness) selection, pinch articulation

**Date:** 2026-09-22 · **Status:** Proposed — code lands complete and fully headless-tested;
the Unity adapter and every numeric default are unverified on device.
**Milestone:** M3/M4 (adds `Core/Gesture/HarmonicField*`, `PinchCommitThresholds`,
`ChordPinchDetector`; adds `Input/MetaXRHandPostureSource`,
`Config/HarmonicFieldConfig`; revises `PerformanceCompositionRoot`)
**Changes the thesis:** §1.3's left-hand recognition mechanism, pending which design wins
— see "Thesis impact" below. The taught gestures (ii/V/I) and their meaning are unchanged.

**The finding.** ADR-0031 through ADR-0037 — five rounds, each a real, verified,
individually-correct fix — did not resolve the left hand's on-device reliability. Every one
of those bugs was in the *articulation* path (`ChordStrikeDetector`, a velocity-threshold
downward motion); *selection* (which of ii/V/I is held, reflected in the tension-colour UI)
was reliable throughout every round. The pattern across all five: a continuous signal's
history (a stale velocity peak, a release quietly accumulating in the background, an
`AudioSource`'s prior state) kept leaking into a frame where the physical situation had
already moved on. The right hand's melody targets already solved the equivalent problem by
moving to pinch — self-contact — as the commit signal (`Docs/DESIGN-SPACE.md` finding D1:
"the only reliable proprioceptive click... is thumb-to-finger self-contact"). The left
hand's strike was the last velocity-threshold commit signal left in the system.

**Decision.** Pursue Design A from `Docs/DESIGN-SPACE.md` §3 for *selection* — one
continuous 2D posture space (hand height × openness) with ii/V/I as landmarks, nearest-
neighbour with hysteresis, replacing the three discrete pose classifiers — paired with
**pinch** for *articulation*, which that document does not itself resolve (it predates
ADR-0025's selection/strike split by four days). Pairing them keeps the interaction model
consistent across both hands: a continuous signal selects, self-contact commits.

**A latch, not "nothing near a landmark ⇒ release."** `HarmonicField` keeps the currently
latched landmark until either the hand drops below a release floor of its own (a second
Schmitt pair, `FloorHeight`/`FloorReleaseHeight`) or a different landmark comes within
`LockRadius` while the current one has drifted past `UnlockRadius`. Nothing in mid-air
between landmarks ever reads `HandPoseCandidate.None`. This is a *structural* version of
what ADR-0034/ADR-0037 had to fix reactively: there, a release could confirm from ordinary
pose noise while the hand had never actually released, because the old model treated
"doesn't currently match a pose" as evidence toward releasing. Here, travelling between two
landmarks is never itself evidence of anything — a release can only be produced by a
deliberate hand-drop. The same class of bug this session spent five rounds chasing cannot
occur in this design at all, not merely tolerated further.

**No new port.** `Docs/DESIGN-SPACE.md` proposed a second port, `IHandPostureSource`, run
alongside `IHandPoseSource`. This turned out to be unnecessary: `HarmonicField` produces the
same `HandPoseCandidate` the discrete recognisers do, so `MetaXRHandPostureSource` simply
implements the existing `IHandPoseSource` — which is a one-property interface — and
`GestureInterpreter`'s confirmation hold-time, miss tolerance, tracking-loss sustain and
release asymmetry are all reused completely unchanged. `ChordPinchDetector` mirrors
`ChordStrikeDetector`'s shape exactly, including a `Feed(HandPoseFrame)` overload built with
ADR-0037's commit-before-interpreter ordering from the start this time, not discovered the
hard way again. `GestureInterpreter.NotifyStruck` is reused verbatim — a pinch is at least
as strong evidence of continued engagement as a strike, and arguably stronger, since
(unlike a ballistic strike) a pinch with the index excluded from the openness axis does not
itself disturb the posture reading. Cost of skipping the second port: a recorded fixture
now stores only the resolved candidate, not the raw (height, openness), so `HarmonicField`'s
ten thresholds cannot be re-tuned offline against a recording the way gesture thresholds
can — only on-device, or by re-deriving from a `_logPosture` console capture.

**Openness deliberately excludes the index finger.** Pinching curls the index by roughly a
quarter of its range; if openness averaged it in, the act of committing a chord would
itself drag the selection point toward V. Openness averages middle/ring/pinky only.

**Velocity comes from pinch-*closing rate*, not pinch strength.** Strength at the rising
edge sits right at the SDK's own pinch threshold by construction and carries almost no
dynamic range; the closing rate — `d(strength)/dt`, sampled with the same peak-over-window
backward walk `ReadVerticalSpeed` already uses (ADR-0035, sign flipped: peak positive
instead of peak negative) — is where the expressive signal actually is. This matches
`DESIGN-SPACE.md`'s own §3 Design B: "dynamics come from pinch-closing speed."

**Height reads the wrist, never a fingertip.** `HandJointId.HandWristRoot`, not
`HandMiddleTip` (which the strike path reads for the opposite reason — ADR-0028). A
fingertip's height co-varies with finger curl; reading it for the height axis here would
contaminate height against openness. There is no `HandPalm` joint on this SDK.

**§3.4 compliance.** "Do not hand-roll joint-angle math" is satisfied: openness is read via
`Oculus.Interaction.PoseDetection.FingerShapes.GetCurlValue`, the SDK's own feature
extractor — the same one `ShapeRecognizerActiveState` uses internally
(`DefaultFingerShapes = new FingerShapes()`). The per-finger curl normalisation bounds
(middle/ring 180°–250°, pinky 180°–245°) are copied verbatim from the SDK's own
`PalmGrabAPI.CURL_RANGE`, not invented.

**Per-finger confidence loss holds the last pinch value.** `MetaXRHandPostureSource` does
not force the reported pinch to `false` when the pinch finger's or thumb's own confidence
drops, even while the whole hand otherwise reads `TrackingQuality.High` — it holds whatever
was last reported. This is ADR-0031's "sustain, do not release" principle one level down,
at finger granularity: without it, a confidence blip mid-pinch could read as
release-then-re-pinch and manufacture a phantom rising edge in `ChordPinchDetector`.

**What stays, unchanged, behind the switch.** `MetaXRHandPoseSource`, the ii/V/I
`ShapeRecognizer`/`TransformRecognizer` assets, and `ChordStrikeDetector` are untouched.
`PerformanceCompositionRoot.HarmonyCommitGesture` (`Strike`/`Pinch`) picks which
selection+articulation pair is wired; reverting is one Inspector field, matching
`Docs/DESIGN-SPACE.md`'s own "kept behind the bake-off switch... deleted from whichever
loses" principle. Neither system's code is deleted by this ADR.

**Fully new `[TUNABLE]` values — see `Docs/CALIBRATION.md`'s Harmonic Field section for
the complete table.** Ten values (six landmark coordinates, the floor pair, the two radii)
are pure invention, not derived from any measurement — the least-confidence numbers in this
codebase's history. Two pinch-rate bounds are also invented. Everything else new
(`MinInterPinchSeconds`, the shoulder height offset, the pinch-rate sample window) is
inherited from an existing, already-guessed value rather than re-guessed, and the curl
normalisation bounds are not tunables at all — they are the SDK's own documented ranges.

**Not yet verified.** `MetaXRHandPostureSource`, `HarmonicFieldConfig`, and the
`PerformanceCompositionRoot` wiring are outside the `dotnet test` mirror (same status
ADR-0033/0035/0036 already carry) — 24 new EditMode tests cover every line of
`HarmonicField`/`ChordPinchDetector` logic headlessly (375 total, all green), but nothing
can verify on paper whether index-pinch registers reliably from a closed fist at V, whether
the SDK's pinch boolean is itself hysteretic or chatters, or whether excluding the index
leaves enough openness range to separate the three landmarks on a real hand.
`MetaXRHandPostureSource._pinchFinger` is Inspector-selectable (default `Index`) for a
same-day fallback to `Middle` if the first proves unreliable, with no code change.

**Manual in-editor setup required**, matching ADR-0026's precedent: create
`HarmonicFieldConfig.asset`; add `MetaXRHandPostureSource` alongside the existing
`MetaXRHandPoseSource`, wiring the same `_head`/left-hand/right-hand references
`TouchTargetRig`/`MetaXRHandPoseSource` already use; on `PerformanceCompositionRoot`, point
`_handPoseSource` at the new component, set `Commit Gesture` to `Pinch`, assign
`_harmonicField`. Enable `MetaXRHandPostureSource._logPosture` for the first session and
replace every landmark/radius default with a real measured value *before* judging whether
the mechanic works — judging the invented defaults would be measuring the guesses, not the
design.

**Thesis impact.** §1.3's left-hand description ("hold a pose... a static shape") would
need to describe a continuous posture space instead of three discrete classifiers if Design
A wins the bake-off this ADR starts — the *taught* gestures and their meaning (ii/V/I,
preparation→tension→resolution) are unchanged, which is the same defense ADR-0014 already
established for a prior recognition-mechanism swap ("a co-designed pose... turned out to be
physically unobservable... and had to be re-keyed on a reliable correlate while the taught
gesture stayed identical" — `Docs/DESIGN-SPACE.md` §7 makes the same argument at length).
No thesis prose is edited here (rule 7); record the outcome once decided.

---

## ADR-0037 — A strike and a same-frame release confirmation raced; the release could win and silently swallow the strike

**Date:** 2026-09-21 · **Status:** Accepted (on-device finding, confirmed against the
`[DIAG]` log from the reporting session) · **Milestone:** M3 (revises
`ChordStrikeDetector`, `PerformanceCompositionRoot`; landed in ADR-0025, revised in
ADR-0034)
**Changes the thesis:** none — closes a gap ADR-0034 left open, not a policy change.

**The finding.** After ADR-0034/-0035/-0036, double-hits stopped reproducing, but a new,
consistent symptom appeared: a pose reads correctly (confirmed, reflected in the tension
colour), but striking it produces no sound — the chord only sounds later, on an unrelated
event. The per-frame `[DIAG]` log (velocity, candidate, `ConfirmedFunction`, `Phase`) made
this exact and reproducible: at the frame where the log showed a clean, strong,
strike-shaped downward velocity (`-1.541` decelerating toward zero) with `confirmed=Five`
and `phase=Detecting`, no `STRUCK` line was ever emitted, and the very next entries showed
`confirmed=` (released).

**Root cause.** `PerformanceCompositionRoot.Update()` called `_interpreter.Feed(frame)`
*before* `_strikeDetector.Feed(velocity)`. ADR-0034's `NotifyStruck()` cancels a pending
release, but only *after* a strike has actually fired — it has no effect on a release that
finishes confirming *before* the strike ever gets to run. A release that had been pending
in the background (exactly ADR-0034's own scenario) can cross
`GestureThresholds.ReleaseHoldSeconds` on the identical frame a genuine strike occurs, not
only on a frame after it. With the interpreter fed first, that frame's own candidate
processing could confirm the release and clear `ConfirmedFunction` to `null` *before*
`ChordStrikeDetector.Feed` ever checked it — so the strike found nothing selected to sound,
silently. This is the same underlying defect ADR-0034 fixed (a pending release unaware a
strike is happening), surfacing through a different door: same-frame ordering rather than
next-frame timing.

**Decision.** `ChordStrikeDetector` gains `Feed(HandPoseFrame frame)`: it strikes first
(using `Feed(float)`, unchanged), *then* hands the frame to
`GestureInterpreter.Feed(frame)`. This guarantees a strike is always evaluated against
whatever `ConfirmedFunction` the interpreter was holding at the end of the *previous*
frame — before this frame's own candidate can invalidate it — so `NotifyStruck` always gets
its chance to cancel a same-frame pending release. `PerformanceCompositionRoot.Update()`
now calls this single overload instead of feeding the interpreter and detector separately.
The original `Feed(float)` stays, unchanged, for existing tests and for any caller that
manages the interpreter itself.

**Why encapsulate the order here, not just reorder the two calls in `Update()`.** The
original bug was two independently-reasonable-looking lines in the wrong order — exactly
the kind of thing a future edit could silently reorder back, since nothing about
`_interpreter.Feed(frame); _strikeDetector.Feed(velocity);` looks wrong on its own. Making
`ChordStrikeDetector.Feed(HandPoseFrame)` own the ordering internally means the composition
root cannot get this wrong by construction — there is only one call to make.

**Test.** `ChordStrikeDetectorTests` gains
`FeedingAWholeFrame_LetsAStrikeClaimTheSameFrameAReleaseWouldOtherwiseConfirmOn` (builds a
release-pending attempt to the same near-threshold state as ADR-0034's test, then feeds one
frame carrying both the release-confirming candidate and a strike-speed velocity — the
strike must fire and the function must stay held) and, made explicit as a named test rather
than left implicit,
`FeedingSeparately_InterpreterFirst_LetsTheReleaseSwallowTheStrike` (the identical
setup, fed the old way — interpreter, then detector — reproduces the bug exactly: release
confirms, strike finds nothing to sound). Both pass against the current code.

**`[DIAG]` logging left in place again.** Given this is the fourth fix in this chain
(ADR-0034/-0035/-0036/-0037), the per-frame velocity/candidate/confirmed/phase log and the
`Send`/`StartNote` logs are staying in `PerformanceCompositionRoot` and `SamplerNoteSink`
for one more on-device round rather than being removed pre-emptively.

**Thesis impact:** none.

---

## ADR-0036 — `CutVoiceIfSounding` (ADR-0033) freed a slot before acquiring, so the fresh attack usually reused the source it had just stopped

**Date:** 2026-09-21 · **Status:** Accepted — confirmed by the `[DIAG] StartNote` log in
the ADR-0037 reporting session: every acquired slot now shows `wasPlaying=False
wasActive=False`, i.e. genuinely idle before reuse, in every sample of that session.
**Milestone:** M2/M3 (revises `SamplerNoteSink.StartNote`, landed in ADR-0025, revised in
ADR-0033)
**Changes the thesis:** none — an audio-implementation correction, not a policy change.

**The finding.** After ADR-0034 and ADR-0035, the repeated/silent-strike symptoms
persisted. The student's own diagnostic observation was the key one: the right hand's
tension colour (driven by `ChordChangedChannel`, i.e. *selection*) tracked chord changes
reliably, while the *sound* did not — pointing away from gesture detection (already
verified clean twice, ADR-0034/-0035) and squarely at the audio path, and noting this
never happened before the strike system (ADR-0025) existed at all.

**Root cause.** `StartNote` called `CutVoiceIfSounding` — which sets the old voice's slot
`_voices[i] = default` — *before* calling `AcquireVoice()`. `AcquireVoice`'s scan returns
the first `!Active` slot, low index first. Freeing the old slot before acquiring meant it
was very often the lowest-indexed free slot available, so the fresh attack's
`AcquireVoice()` call picked that exact same slot straight back — meaning
`_sources[i].Stop()` was followed, within the same call frame, by reconfiguring that same
`AudioSource`'s clip/pitch/volume and calling `PlayScheduled` on it again. Before ADR-0033,
this same-frame stop-then-immediately-reuse pattern only happened in the rare
voice-pool-exhaustion path (`AcquireVoice`'s own stealing branch); ADR-0033's fix made it
the *common* case for every re-comp and every shared-tone chord change, which is exactly
the class of change the student correctly identified as the difference from before the
strike system existed. Re-using one `AudioSource` this way — stop, then immediately
reconfigure and schedule again in the same frame — is a well-known category of Unity audio
reliability risk (the source's internal/scheduling state is not guaranteed to settle
synchronously within one frame), which would plausibly explain both directions of the
reported symptom: an unreliable restart could silently fail to produce audible output
(reads as "didn't fire"), or interact with the still-draining old schedule in a way that
produces more than one audible attack (reads as "repeated").

**Decision.** Reorder: call `AcquireVoice()` first, while the old same-pitch voice is
still `Active` (so it can never be the slot the scan selects), then call
`CutVoiceIfSounding` on the old slot afterward. The fresh attack now always lands on a
genuinely idle `AudioSource` that has not been touched this frame; the old voice is simply
stopped on its own slot, with no new `PlayScheduled` call touching that same object.

**Known residual edge case, not fixed.** If the voice pool is exhausted and the stolen
"oldest" voice happens to already be the same pitch/channel being started, `AcquireVoice`'s
own stealing branch (`_sources[oldest].Stop()`, same slot returned) still produces the
same same-frame stop-then-reuse pattern this ADR otherwise removes. Left as is: it is the
pre-existing, already-logged (`SamplerNoteSink` voice-pool-exhaustion warning) exceptional
path, not the common case the student is hitting, and 32 voices against this session's
polyphony makes it unlikely to be reached at all.

**Not yet verified.** This is a confirmed *code-level* pattern (the call order and its
consequence for which slot gets selected are provable by reading `AcquireVoice`), not a
confirmed *audio-engine-level* one — whether Unity's `AudioSource` actually misbehaves
when reused this way cannot be verified from source alone. Diagnostic logging
(`SamplerNoteSink.Send`, and a new one-line log at `StartNote`'s acquire point recording
the acquired slot and its prior `isPlaying`/`Active` state) has been left **in place**
this time, rather than removed, specifically so that if this does not fully resolve the
symptom, the next on-device session already has the data needed rather than requiring
another blind round.

**Thesis impact:** none.

---

## ADR-0035 — A stale downward peak could outlive the hand's own direction reversal

**Date:** 2026-09-21 · **Status:** Accepted (on-device finding, reported by the student) ·
**Milestone:** M3 (revises `MetaXRHandPoseSource.ReadVerticalSpeed`, landed in ADR-0025,
revised in ADR-0028)
**Changes the thesis:** none — an instrumentation correction, not a policy change.

**The finding.** After ADR-0034, striking felt improved but the student reported a chord
sometimes triggering while the hand was moving *up* — the recovery/rebound after a strike,
not the strike itself. Expected: only a downward motion should ever articulate a chord.

**Root cause.** `ReadVerticalSpeed`'s peak-over-window search (ADR-0028) scanned all
`_speedSampleFrames` (default 3) ring-buffer slots unconditionally and took the minimum
(most downward) value found *anywhere* in that window, with no regard for whether the
window's samples were still part of one continuous motion. A strong downward sample from
an already-finished strike could keep winning that comparison for up to two more frames
after the hand's own newest sample had turned positive (moving up) — so
`ChordStrikeDetector` could see a reported "downward speed" that crossed
`StrikeEnterSpeedMetresPerSecond` on a frame where the hand was demonstrably already
reversing. The same staleness also worked against `StrikeSettleFrames` (ADR-0030): a
lingering stale peak could keep the reported speed above `StrikeExitSpeedMetresPerSecond`
for a frame or two after the hand had genuinely already begun to settle, delaying re-arm
past when the physical motion had actually stopped. Both are the same root defect —
"peak anywhere in the window" carries no notion of *when* the peak happened relative to
now — surfacing as two different symptoms of the reported double-hit.

**Decision.** Walk the window backward from the newest sample and stop at the first
non-downward (`>= 0`) one, tracking the minimum only across that unbroken run. This still
finds the true peak of a decelerating strike within one continuous downward motion —
ADR-0028's original concern, since the newest sample is included in the walk and older
samples are only consulted while every sample since them has stayed downward — but a frame
whose own instantaneous sample has already turned non-negative reports 0 immediately,
never reaching back past its own direction reversal to a finished motion's peak. No
existing threshold or `[TUNABLE]` value changes; this corrects the search algorithm, not a
calibration number.

**Not yet verified in Unity.** `MetaXRHandPoseSource` is a `MonoBehaviour`
(`Oculus.Interaction` dependency) outside the `dotnet test` mirror, so — like ADR-0031's
`ChordStrikeDetector` fix and ADR-0033's `SamplerNoteSink` fix before Unity confirmed
them — this has no automated coverage. Hand-traced against a constructed
down-then-up sample sequence (`-1.5, -0.5, +0.8`) to confirm the ring-buffer indexing:
frame 3 (the direction reversal) now reports `0`, where it previously reported the stale
`-1.5` from frame 1. Verify on-device: a strike followed by an ordinary upward hand
recovery should never itself sound a second hit, and settling after a strike should feel
at least as responsive as before, not slower.

**Thesis impact:** none.

---

## ADR-0034 — A strike must cancel a release that happens to be pending

**Date:** 2026-09-21 · **Status:** Accepted (on-device finding, confirmed via `[DIAG]`
console logging against a live session, since this path is real-time and could not be
reproduced from a recorded hand-pose fixture) · **Milestone:** M3 (revises
`GestureInterpreter`, `ChordStrikeDetector`, landed in ADR-0025, revised in ADR-0027/-0031)
**Changes the thesis:** none — closes a gap the ADR-0025 split left open, not a policy change.

**The finding.** ADR-0033's fix did not resolve the reported repeated-hit / silent-strike
symptoms. Temporary logging of every `Struck` event and every note `Send` (kind, pitch,
channel, timestamp) showed the domain layer calling `HarmonyEngine.Strike()` exactly once
per physical gesture, with clean, correctly-ordered Off/On pairs — ruling out a
double-invocation bug. But the same logging surfaced a different, recurring pattern: an
unstruck `Send Off` (a release, not a strike-triggered cut) landing 43–150 ms after a
`Struck` line, over and over through the session — far too fast to be the learner
deliberately relaxing their hand.

**Root cause.** `GestureThresholds.ReleaseHoldSeconds` (400 ms) is measured from
`GestureInterpreter._pendingSince` — set once, when a release attempt first starts pending
— not from a continuous run of matching frames. `RegisterMiss`'s tolerance (ADR-0027/-0029)
forgives an individual miss without resetting `_pendingSince`, so a release can accumulate
wall-clock progress in the background from ordinary pose noise (a None blip here, a
tolerated bounce-back there) for a long stretch before ever reaching its
`ConfirmingFrames`/`heldLongEnough` conditions. Nothing about a successful strike tells the
interpreter "the hand is still here" — `ChordStrikeDetector` only reads
`ConfirmedFunction`/`TrackingUsable` from it, and a strike's own disruption of the pose
reading is exactly the kind of weak evidence ADR-0027 built the release tolerance to
absorb, not to clear. So a release that had been silently ticking since well before a
strike could cross its threshold moments after that strike sounded a chord, cutting it —
audible either as a stutter (if the learner re-struck to compensate, producing two real,
individually-correct strikes in quick succession) or as a near-silent misfire (if the cut
landed within tens of milliseconds).

**Decision.** `GestureInterpreter.NotifyStruck()` (new, public): if a release is currently
pending, cancel it outright (`ResetPending`) rather than let it merely tolerate the strike
as one more miss. `ChordStrikeDetector` calls it on every successful strike, right where
`Struck` fires — it already holds the `_interpreter` reference this needs. Scoped
narrowly, matching ADR-0027's own precedent: only a *pending release* is cancelled: a
pending switch to a different concrete pose is untouched, since a strike happening mid
pose-to-pose switch is a different, unevidenced scenario.

**Why ADR-0025/-0027/-0029 did not already cover this.** Those fixed the confirmation and
release *thresholds and tolerances* — how noisy a reading can be before it stops counting
as the same attempt. This gap is different in kind: it is about one gesture-detection
subsystem (`ChordStrikeDetector`) having no way to inform another
(`GestureInterpreter`'s release-pending state) that a strong, independent confirming event
just happened. No threshold tuning closes it; the two subsystems needed a line of
communication that did not exist before.

**Verification.** `GestureInterpreterTests.NotifyStruck_CancelsAPendingRelease_...` and
`ChordStrikeDetectorTests.AStrike_CancelsAReleaseThatIsPendingAtThatMoment` reproduce the
exact log pattern headlessly: build a release-pending attempt via alternating None/bounce
frames without yet reaching `ReleaseHoldSeconds`, strike, then continue the identical
pattern for long enough that, without the fix, the *original* pending-release's elapsed
wall-clock time would already have crossed the threshold. Confirmed both tests fail with
the fix temporarily disabled (`ConfirmedFunction` came back `null` where `Two` was
expected) before re-enabling it — the same negative-control check used to validate
ADR-0031's tests.

**Thesis impact:** none. Worth citing alongside ADR-0025 if Chapter 7 discusses the
left-hand articulation work as a single body of findings — this is the same class of
"strike disrupts pose reading" issue ADR-0027/-0028 already document, discovered one layer
deeper.

---

## ADR-0033 — A shared chord tone between the outgoing and incoming voicing double-attacks

**Date:** 2026-09-21 · **Status:** Accepted (on-device finding, reported by the student) ·
**Milestone:** M2/M3 (revises `SamplerNoteSink`, predates ADR-0025 but only became audible
through repeated re-articulation)
**Changes the thesis:** none — an audio-implementation correction, not a policy change.

**The finding.** Switching the left hand between V and I produced an audible double-hit;
switching between ii and V, or ii and I, did not. Re-striking the *same* held function
(comping) sounded fine.

**Root cause.** `Voicing.Close()` for the three chords (verified by direct computation,
`DefaultRootFloorMidi`/`CeilingMidi` = 48/60):

| Function | Voicing |
|---|---|
| ii — Dm7 | D3, F3, A3, C4 |
| V — G7 | **G3, B3**, D4, F4 |
| I — Cmaj7 | C3, E3, **G3, B3** |

V and I share two exact MIDI pitches (G3, B3) — a direct consequence of Dm7/G7/Cmaj7's
real voice-leading (G7 and Cmaj7 share the pitch classes G and B); ii shares no pitch with
either. `HarmonyEngine.Strike()` (ADR-0025) sends Off for every outgoing pitch, then On for
every incoming one. For a shared pitch, that is an Off immediately followed by an On for
the *identical* MIDI note. `SamplerNoteSink.ReleaseNote` answers the Off by starting an
`_releaseSeconds` (80 ms default) fade-out on the existing voice — correct for a note
ending on its own, so its tail does not click — but `StartNote` then acquires a **different**
free voice slot and begins a brand-new, full-volume attack for the same pitch on top of
it. For ~80 ms, two voices sound the same note: one fading out, one freshly attacking. Two
overlapping attack transients on an identical pitch is audible as the note hitting twice.
This is why full re-comping (all 4 notes shared) sounded fine — everything refreshes
coherently — while V↔I's *partial* overlap (2 notes change cleanly, 2 get a redundant
fade-plus-reattack) stands out against the two notes around it that changed cleanly.

**Decision.** `SamplerNoteSink.StartNote` now calls `CutVoiceIfSounding(midi, channel)`
before acquiring a voice: if a voice is already active for that exact (pitch, channel), it
is hard-stopped immediately rather than left to fade. The `_releaseSeconds` fade remains
exactly as before for the case it exists for — a note ending on its own decays without a
click — but a fresh re-attack on the same pitch now always gets a clean cut first, whether
that pitch is being deliberately re-struck (comping) or happens to be a shared tone across
a chord change. This is a strict improvement for every re-strike, not a V/I-specific patch:
the fix is keyed on (pitch, channel) equality, not on which chord functions are involved.

**Why this predates ADR-0025 but was only just found.** The double-attack condition exists
whenever two Send calls for the same pitch land close together, which was structurally
impossible for harmony before ADR-0025 (a chord only ever sounded once, on
`ProgressionState.Changed`) — ADR-0025 is what made re-articulating harmony ordinary, and
therefore what made this reachable at all.

**Not yet verified in Unity.** `SamplerNoteSink` is a `MonoBehaviour` (`Jazztures.Audio`,
references `UnityEngine.AudioSource`) and outside the `dotnet test` mirror, so this fix has
no automated coverage — the same status as `LatencyProbe` and other thin Unity audio/adapter
code in this codebase. Verify on-device: V↔I switches, and comping the same chord, should
both now sound like a single clean attack per strike with no overlap.

**Update — "a few gestures did not fire at all" resolved separately.** This fix alone did
not resolve the reported symptoms; see ADR-0034. The likely explanation for silent
misfires: a chord cut by a stale pending release (ADR-0034) within tens of milliseconds of
being struck would barely register as having sounded at all.

**Thesis impact:** none.

---

## ADR-0032 — `LatencyProbe` must not record a release's hold time as a confirmation

**Date:** 2026-09-21 · **Status:** Accepted (code-review finding) · **Milestone:** M3/M7
(revises `LatencyProbe`, `LatencyStage`)
**Changes the thesis:** none — an instrumentation correction, not a policy change.

**The finding.** `LatencyProbe.OnConfirmed` recorded the hold time of *every*
`GestureInterpreter.ConfirmedFunctionChanged` event — releases included — into the single
`LatencyStage.PoseToConfirm` bucket.

**Root cause.** ADR-0027 deliberately gave releasing its own, larger hold requirement
(`ReleaseHoldSeconds`, default 400 ms) than confirming a selection (`PoseHoldSeconds`,
default 150 ms) — a false release is audible, a late one is not, so releasing is held to a
more conservative bar on purpose. `GestureInterpreter.LastConfirmationHoldSeconds`
faithfully reports whichever hold time actually applied, but `LatencyProbe` never
distinguished the two cases downstream, so every release folds a ~400 ms sample into a
metric CLAUDE.md §4.3 defines as "pose confirmation (hold + frames)" and specifically asks
to be reported in the thesis as the segment "you currently control." Once releases happen
at any real frequency, the recorded `PoseToConfirm` percentiles (p90/p95 especially) stop
describing selection responsiveness and instead describe a mixture of two populations with
deliberately different floors.

**Decision.** Split the stage by what the interpreter just reported. `LatencyStage` gains
`PoseToRelease` alongside the existing `PoseToConfirm`. `LatencyProbe.OnConfirmed` now
routes on whether the newly confirmed function has a value: a value means a fresh
selection or a pose-to-pose switch (`PoseToConfirm`, the §4.3 number); `null` means a
release (`PoseToRelease`, tracked separately). `LatencyRecorder` needed no change — it is
already generic over `LatencyStage` and sizes its ring buffers from `Enum.GetValues`, so
the new stage is tracked and reported (`LatencyProbe.Report`'s `Enum.GetValues` loop) with
no other wiring.

**Thesis impact:** none directly, but this is the fix that makes the §4.3 numbers
trustworthy once they're gathered on-device — if `PoseToConfirm` was ever sampled before
this change, those samples are the pre-fix, conflated kind and should be discarded.

---

## ADR-0031 — Tracking loss during a strike must not force a re-arm

**Date:** 2026-09-21 · **Status:** Accepted (code-review finding, not yet reproduced
on-device) · **Milestone:** M3 (revises `ChordStrikeDetector`, landed in ADR-0025, revised
in ADR-0030)
**Changes the thesis:** none — an instrumentation correction, not a policy change.

**The finding.** `ChordStrikeDetector.Feed` forced `_armed = true` (and reset
`_settleFrames`) whenever `GestureInterpreter.TrackingUsable` was false — the same branch
used for "nothing is selected." This reopens the exact failure ADR-0030 closed, through a
different trigger.

**Root cause.** `GestureInterpreter.TrackingUsable` drops to `false` on a single
Low/NotTracked frame — no hysteresis on the way down; only the climb back up needs
`HighFramesToResumeAfterLoss` consecutive High frames (§3.5). A strike is fast, ballistic
motion (ADR-0025/-0028) — exactly the kind of motion most likely to degrade optical hand
tracking for a frame or two. If a tracking blip lands mid-strike, every frame of the blip
*and* the following recovery climb forced `_armed = true`; by the time tracking is usable
again, the detector is unconditionally re-armed regardless of whether the hand is still
moving. If it is still fast on the first good frame back — the common case, since the
physical strike hasn't actually finished — `Struck` fires a second time for what the
learner experiences as one motion. This is ADR-0030's double-fire (a rebound re-arming
early) reproduced via a tracking gap instead of a rebound; the existing test suite had a
test (`AfterTrackingLoss_TheFirstGoodFrameCanStillStrike_ButOnlyOnceReArmed`) that
constructed this exact scenario and asserted the resulting double-fire as *correct* — the
same "bug encoded as a passing test" pattern ADR-0025's own postmortem names for the
pre-ADR-0025 confirmation-reset bug.

**Not yet observed on-device.** Unlike ADR-0026 through -0030, this was found by
re-reading the ADR-0030 fix against the tracking-loss path, not by a fresh on-device
symptom, and reproduced deterministically in a unit test (`VirtualClock`, no headset). It
should still be treated as live risk, not a theoretical one — §1.4 names fast motion and
occlusion as the system's acknowledged tracking-reliability limitation, and a strike is
the fastest motion the left hand makes.

**Decision.** Split the two conditions in `ChordStrikeDetector.Feed`. "Nothing selected"
(`!ConfirmedFunction.HasValue`) still forces an immediate re-arm — there is nothing to
strike, so nothing is lost, and a fresh selection should be able to strike right away.
"Tracking unusable" (`!TrackingUsable`) now leaves `_armed`/`_settleFrames` untouched and
simply skips the frame — the same "sustain, do not release" policy §3.5 already applies to
the confirmed chord itself, now applied to this detector's own internal state. A strike in
progress when tracking drops stays in progress when tracking resumes, and still needs a
genuine `StrikeSettleFrames`-frame settle before it can fire again. A detector that was
already armed (idle, no strike in flight) before a blip is unaffected — it was already
`_armed = true` and stays that way, so ordinary responsiveness right after a blip is
unchanged.

**Alternatives considered and rejected:**
- *Leave tracking-loss handling as-is and rely on `MinInterStrikeSeconds` to absorb a
  double-fire* — the cooldown (120 ms default) is shorter than a plausible
  blip-plus-recovery window (a Low frame plus `HighFramesToResumeAfterLoss` High frames),
  so it cannot be relied on to suppress this case, and conflating "debounce" with "re-arm
  correctness" was exactly ADR-0030's objection to the pre-fix behaviour.
- *Give the detector its own tracking-quality hysteresis, separate from the interpreter's*
  — would work, but duplicates a policy §3.5 already owns at the interpreter level; freezing
  on `!TrackingUsable` reuses that policy instead of re-deriving it.

**Test.** `ChordStrikeDetectorTests.cs` replaces the old test with
`TrackingLossMidStrike_DoesNotReArm_UntilTheHandActuallySettles` (no second strike on the
first good frame back while still fast; a genuine settle afterward still re-arms normally)
and `TrackingLossWhileAlreadyArmed_DoesNotBlockTheNextStrike` (a blip while idle costs the
next strike nothing).

**Calibration note.** No new parameters. Recommend adding a rhythmic on-device fixture
that includes a tracking dropout mid-strike once one exists (§7's still-open item), so this
path is exercised by replay, not only by a hand-constructed unit test.

**Thesis impact:** none.

---

## ADR-0030 — A strike's own rebound could re-arm and fire a second, unintended strike

**Date:** 2026-09-19 · **Status:** Accepted (on-device finding, same session as ADR-0028/-0029)
· **Milestone:** M3 (revises `ChordStrikeDetector`, landed in ADR-0025)
**Changes the thesis:** none — an instrumentation correction, not a policy change.

**The finding.** After ADR-0028 (fingertip velocity, peak-over-window), striking became
reliably detectable — and a single strike would sometimes fire **twice**.

**Root cause.** The Schmitt trigger re-armed on a single frame at or below
`StrikeExitSpeedMetresPerSecond`. A real strike's deceleration commonly includes a small
rebound or settle wobble near the bottom of its arc — entirely ordinary human motion, not
tracking noise. One frame of that wobble dipping below the exit speed was enough to re-arm,
and the wobble's own small secondary downward motion then crossed the enter threshold
again, registering as a second, unintended strike for what the learner experienced as one
motion.

**Decision.** Re-arming now needs `GestureThresholds.StrikeSettleFrames` (default 3)
*consecutive* frames at or below the exit speed, not one — mirroring the "consecutive
frames," not "one sample," pattern already used everywhere else in `GestureInterpreter`
(`ConfirmingFrames`, `HighFramesToResumeAfterLoss`). A single frame back above the exit
speed resets the count, so a genuine rebound — which by definition doesn't stay settled —
can no longer re-arm the detector; only an actually-stopped hand can.

**Thesis impact:** none.

---

## ADR-0029 — A pose-to-pose switch must not inherit the release attempt's larger budget

**Date:** 2026-09-19 · **Status:** Accepted (on-device finding, same session as ADR-0027/-0028)
· **Milestone:** M3 (revises `GestureInterpreter`, landed in ADR-0025/-0027)
**Changes the thesis:** none — corrects an unintended side effect of ADR-0027's own fix.

**The finding.** After ADR-0027 (releasing needs more sustained evidence than confirming),
switching between two concrete left-hand poses started intermittently reading as ii no
matter which pose was actually being formed.

**Root cause.** ADR-0027 classifies a pending attempt as a "release" from the *first*
reading after leaving the confirmed pose, and grants that whole attempt
`ReleaseMissTolerance` (6) / `ReleaseHoldSeconds` (400 ms) for its entire lifetime. But an
ordinary pose-to-pose switch (ii → V, say) very often passes through one
`HandPoseCandidate.None` frame first — any change of orientation can briefly read as
unrecognised — and that single frame was enough to lock the whole transition into "this is
a release attempt." Every subsequent, perfectly clear `V` reading was then absorbed as a
merely-tolerated "miss" against the stale release-pending, rather than recognised as the
strong, unambiguous signal it actually is. ii stayed confirmed for up to the full 400 ms
release window while the learner visibly held a completely different pose — which reads
exactly as "the wrong pose confirms."

**Decision.** `GestureInterpreter.RegisterMiss` now grants the elevated release budget only
to genuinely *weak* evidence against a release: `Ambiguous`, `None` itself, or a reading
that matches what's already confirmed (the hand bouncing back to the pose it never really
left — the exact pattern a strike disruption produces, per ADR-0027). A miss that is a
**different, concrete** pose is never weak evidence, regardless of what the pending attempt
started as — it always uses the ordinary `ConfirmationMissTolerance` (2), so a genuine
switch redirects within a couple of frames even if it happened to pass through `None`
first. This does not reopen the ADR-0027 hole: a strike's disruption reads as `None` /
`Ambiguous` / a bounce back to the already-held pose, never as a different concrete pose, so
it still gets the full, patient release budget.

**Thesis impact:** none.

---

## ADR-0028 — Strike velocity read at the fingertip, not the wrist; peak-over-window

**Date:** 2026-09-19 · **Status:** Accepted (on-device finding, immediately after ADR-0027)
· **Milestone:** M3 (revises `MetaXRHandPoseSource`, landed in ADR-0025)
**Changes the thesis:** none — an instrumentation correction, not a policy change.

**The finding.** With ADR-0027 landed (chords no longer cut mid-strike), striking became
reliably *hard to trigger at all* — confirmed on device: the physical motion is a wrist
flick, not a whole-arm drop.

**Root cause.** `ReadVerticalSpeed` measured `IHand.GetRootPose` — the **wrist** joint's
world-space height — and differentiated it frame to frame. A wrist flick is a rotation
*about* the wrist joint: the joint itself barely translates, no matter how fast the hand
swings, because it's the pivot. The signal being measured was, by construction, close to
blind to exactly the motion learners make from the ADR-0026 horizontal ii pose. (This is
the same "the hand is now naturally a wrist-flick, not a translation" geometry ADR-0027
already used to explain why releases needed more tolerance — it turns out the strike side
of the same motion needed a different fix, not more tolerance.)

**Decision — read the fingertip, and take the peak over a short window.**

1. `ReadVerticalSpeed` now differentiates `HandJointId.HandMiddleTip`
   (`Assets/Jazztures/Input/MetaXRHandPoseSource.cs`), not the wrist. A point farther from
   a rotation's pivot moves proportionally faster for the same rotation, so the fingertip
   picks up a wrist-flick that the wrist joint itself cannot show. This also generalises
   correctly to a whole-arm-drop strike, where the fingertip moves at least as fast as the
   wrist did — nothing is lost for that motion, only gained for the flick.
2. The reported value is the **peak** downward speed over the last `_speedSampleFrames`
   frames (default 3), not the instantaneous one — mirroring `TouchTargetBinder`'s
   fingertip-speed sampling for the right hand (ADR-0018) exactly, down to the same default
   window size and the same underlying reason: a real strike decelerates near the bottom of
   its arc, and the single frame that happens to cross the enter threshold is often already
   past its peak speed. `ChordStrikeDetector` and `GestureThresholds` are unchanged — the
   smoothing is entirely a sensor-side concern in the adapter, exactly where ADR-0018 put
   the equivalent smoothing for the right hand.

**Why `_speedSampleFrames` is a plain `[SerializeField]` on the component, not threaded
through `GestureThresholdsConfig`.** Every other ADR-0025/-0027 value there is consumed by
`Core` (via `GestureThresholds`/`ToThresholds()`) or is a human-facing mirror of a real SDK
asset (the finger-curl/palm-cone group). This one is neither: it's a smoothing-window size
internal to one Unity adapter, with no Core consumer, closer in kind to a filter constant
than a gesture-ergonomics parameter. Wiring a new cross-reference from
`MetaXRHandPoseSource` to a shared config asset for one int was judged more machinery than
the value warrants; `MelodyConfig.SpeedSampleFrames` was only cheap for `TouchTargetBinder`
to adopt because that component already held a config reference for unrelated reasons.

**Thesis impact:** none. This corrects where a signal is sampled, not what any policy
requires or measures.

---

## ADR-0027 — Releasing a confirmed function needs its own, more conservative bar

**Date:** 2026-09-19 · **Status:** Accepted (on-device finding, immediately after ADR-0026)
· **Milestone:** M3 (revises `GestureInterpreter`, landed in ADR-0025)
**Changes the thesis:** none — this is a direct application of §1.4's existing constraint
("a dropped frame must never produce a spurious chord change") to a case ADR-0025 missed.

**The finding.** With ii re-oriented per ADR-0026, striking (the fast downward motion that
sounds a chord, ADR-0025) while holding ii would sometimes cut the sound instead of
re-articulating it. Tracking quality stayed High throughout — this is not the §3.5
tracking-loss path, which already sustains correctly.

**Root cause.** ADR-0025's confirmation-miss tolerance
(`GestureThresholds.ConfirmationMissTolerance`) only protects an **already in-progress**
confirmation attempt from brief blips. It does nothing for an **already-confirmed, steady**
pose: the very first frame that reads `None` while ii is held starts a *fresh* pending
attempt toward release (bypassing the tolerance check entirely, since nothing was pending
yet), and if that reading holds for the ordinary confirmation window
(`PoseHoldSeconds`/`ConfirmingFrames` — 150 ms / 3 frames) it genuinely confirms the
release, which cuts the sounding chord immediately per ADR-0025 ("an explicit release is a
deliberate stop"). It also stops `ChordStrikeDetector` from firing at all, since it
requires a confirmed function — so the new strike goes silent on top of the old one cutting.

150 ms turns out not to be a safe bar for "the hand is definitely not making this pose
anymore," because a strike is itself a fast, deliberate hand motion, and — per ADR-0026's
own geometry — the natural downward-strike rotation from the new horizontal ii pose rotates
the hand's thumb-side axis away from vertical, which is exactly what `WristUp` measures.
The old vertical/`FingersUp` ii was comparatively immune to this because a downward strike
from that starting orientation is more naturally a translation (an arm drop), which
doesn't move any orientation feature at all. ADR-0026 didn't introduce this bug, but it
made the pre-existing gap far more likely to be hit.

**Decision.** Releasing **from** an already-confirmed function now needs its own,
separately-tunable, more conservative bar: `GestureThresholds.ReleaseHoldSeconds` (default
400 ms, must be ≥ `PoseHoldSeconds`) and `ReleaseMissTolerance` (default 6, must be ≥
`ConfirmationMissTolerance`), applied in `GestureInterpreter.RegisterMatch` /
`RegisterMiss` whenever the pending target is `null` and a function is currently held.
Confirming a *fresh* pose (nothing was held) and switching *between* two concrete poses are
both untouched — exactly as fast and tolerant as ADR-0025 left them. This is a direct
instance of CLAUDE.md §1.4 / §3.5's own principle, just extended to cover a release racing
against a strike: **a false release is audible (it cuts the sounding chord) where a merely
late one is not** — the same "silence is a recoverable error; a wrong chord is not"
asymmetry the thesis already states, now also applied to *losing* a chord, not just
picking the wrong one.

**Alternatives considered and rejected:**
- *Raise `ConfirmationMissTolerance` / `PoseHoldSeconds` globally instead of adding a
  release-specific pair* — would equally slow down every ordinary pose-to-pose transition
  and fresh confirmation to buy safety only the release path needs, trading away exactly
  the responsiveness ADR-0025 was written to protect.
- *Have `ChordStrikeDetector` suppress `GestureInterpreter` during a strike* — would work,
  but makes the interpreter's correctness depend on the strike detector's timing, a
  dependency in the wrong direction (the detector already reads the interpreter, not the
  reverse) for a problem the interpreter can solve on its own terms.

**Calibration note.** 400 ms / 6 frames are engineering guesses, not measured against a
real strike's disruption duration — no rhythmic hand-pose fixture exists yet to measure
that (`CLAUDE.md` §7, still open). Pilot-calibrate alongside the other ADR-0025 values.

---

## ADR-0026 — ii re-oriented horizontal (`WristUp`), superseding ADR-0014's `FingersUp`

**Date:** 2026-09-19 · **Status:** Accepted (student design call, ergonomics) · **Milestone:**
M3 (revises the ii recogniser landed there)
**Changes the thesis:** §1.3's description of how ii is recognised (not the gesture's
meaning); Chapter 6 per ADR-0014's own note, now superseded
**Supersedes:** ADR-0014's "ii keys on `FingersUp`" decision. ADR-0014's other finding —
the SDK has no lateral ("faces right") axis at all, so I's `PalmDown` was the reliable
correlate — still stands and is unaffected.

**The request.** Reorient ii from vertical (fingers pointing at the ceiling) to
horizontal — still an open palm facing right, but with the fingers pointing away from the
learner instead of up.

**The SDK still has no feature for this, literally.** `TransformFeature` (Meta XR
Interaction SDK 205.0.0) is nine fixed (hand-axis, target-axis) pairs:
`WristUp/WristDown/PalmUp/PalmDown/FingersUp/FingersDown` all measure a hand axis against
*vertical* (head/tracking/world up); `PalmTowardsFace/PalmAwayFromFace/PinchClear` measure
a hand axis against *depth* (`CenterEyePose.forward`). There is no pairing that measures
the **fingers** axis against forward — "fingers pointing away from the face" is simply not
an expressible feature, the same class of gap ADR-0014 hit for "palm right" itself.

**The geometry resolves it anyway.** The request over-specifies the pose: fixing both
"fingers point away from the face" (forward) *and* "palm faces right" pins the hand's full
orientation — for a hand held with fingers forward and palm right, the thumb **must** point
up (the alternative, palm facing left, needs active forearm pronation and is not the
neutral, comfortable hold implied by "still facing right"). "Thumb points up" is exactly
`TransformFeature.WristUp` (`Constants.LeftThumbSide` vs. vertical-up,
`TransformFeatureValueProvider.cs`). So the taught pose the request describes has a single,
correct, already-existing SDK feature — it just isn't named anything resembling "fingers".

**Never-guess is preserved, by the same argument as ADR-0014.** `WristUp` reads the
thumb-side axis; `PalmDown` (I's discriminator) reads the dorsal axis. These are two
orthogonal axes of the same rigid wrist frame, so they cannot both register a small angle
against the same vertical target at once — a hand can't have its thumb-side *and* the back
of its hand both pointing up. ii and I stay mutually exclusive except genuinely
mid-rotation, exactly as before; `Ambiguous` still resolves to "hold the previous state,
emit nothing" (§3.4).

**Change.** `Assets/main.unity`, `iiPose`'s `TransformRecognizerActiveState`:
`_feature: 6` (`FingersUp`) → `_feature: 0` (`WristUp`); `_state: 18` → `_state: 0`
(`WristUp`'s own `_firstState`, i.e. "angle below the enter threshold" — mirrors exactly
how I's block already points `_state` at `PalmDown`'s `_firstState`).
`GesturePalmConeThresholds.asset` needed no change: `WristUp` (feature 0) already carries
the SDK's default midpoint 40° / width 20° (→ enter 30° / exit 50°) threshold row, unused
until now. `UpVectorType` stays `Head`, unchanged — `WristUp` reads against the same
head-relative vertical the old `FingersUp` check used.

**What this does *not* fix by itself.** `Assets/Jazztures/Input/Poses/Ghost/Ii.asset`
(`GhostPoseAsset`) is a captured snapshot of a real held hand — per-finger joint quaternions
plus `_wristRotationHeadLocal` — authored by physically holding the *old* vertical pose in
front of the `GhostPoseRecorderWindow` tool. It still encodes that old orientation. The
finger shape (open, relaxed) does not need to change; the wrist orientation does, and doing
that correctly requires a real hand in front of the capture tool, not a hand-edited
quaternion — a wrong edit here would teach the learner a subtly incorrect pose and could go
unnoticed until a pilot session. **Action required in-editor:** Play Mode with hand
tracking (Quest Link is fine) → `Jazztures/Ghost Pose Recorder` → hold the new horizontal
ii → `Capture → ii`. The ghost hand will keep showing the old pose until this is done.

**Alternatives considered and rejected:**
- *`TransformConfig.RotationOffset` on the existing `FingersUp` feature*, rotating the
  target vector 90° instead of switching features — mathematically equivalent to picking a
  different named feature, but "the fingers-up detector, rotated, means fingers-forward" is
  exactly the kind of indefensible-under-questioning reframing ADR-0014 already rejected
  for `PalmDown`. Using the feature whose plain-English name matches what it measures
  (`WristUp`) is more defensible and no harder to implement.
- *`JointRotationActiveState` on the wrist* (ADR-0014's noted "true palm-orientation"
  path) — still deferred; still not needed, since `WristUp` cleanly resolves this pose.

**Thesis impact.** ADR-0014's Chapter-6 note ("ii is recognised by hand verticality
(`FingersUp`), not palm azimuth") is superseded — replace with: ii is now recognised by
wrist roll (`WristUp` — the thumb points up), not palm azimuth. The taught concept is
unchanged: open palm, facing the user's right, preparation.

---

## ADR-0025 — Left-hand articulation: selection and sound are separate events

**Date:** 2026-09-19 · **Status:** Accepted (student design call, driven by an on-device
finding) · **Milestone:** M3/M5 (revises the gesture-interpreter and harmony-engine
behaviour landed there)
**Changes the thesis:** §1.3's left-hand interaction description, §3.2, §3.4
(confirmation), §4.3 (an added latency segment). See "Thesis impact" below.

**The finding.** Testing against an external metronome at 80 BPM, playing a left-hand
chord on every 2nd or 4th beat was not reproducible — gestures were missed or misfired,
not merely late.

**Root cause, in order of contribution:**

1. **`ProgressionState.Hold` no-ops on re-selecting the same function**
   (`Core/Harmony/ProgressionState.cs`). Because a chord only sounded on a *change* of
   held function (`HarmonyEngine.OnProgressionChanged`, pre-ADR-0025), re-articulating the
   same chord — exactly what "play a chord every N beats" asks for, and exactly what L2's
   own stated objective ("steady pulse... frozen harmony", CLAUDE.md §3.9) requires —
   was structurally impossible without releasing to no-pose and re-forming it. Each round
   trip paid a full confirmation window twice (release + re-select), roughly 340 ms of
   state-machine overhead against a 750 ms beat at 80 BPM, before any hand travel.
2. **`GestureInterpreter.ProcessCandidate` reset all confirmation progress on any single
   non-matching frame** — including a one-frame `None`/`Ambiguous` reading, which ordinary
   hand-tracking noise produces routinely while the hand is physically mid-transition. This
   made confirmation time *variable* rather than a fixed 150 ms: a learner could not build
   a stable expectation of when a gesture would land, which reads as "missed" more than
   "slow." (`AFlickerToADifferentCandidate_RestartsConfirmation` in the test suite asserted
   this as correct behaviour — it was the bug, encoded as a passing test.)

Neither the one existing hand-pose fixture (`Fixtures/HandPoseFixture_M3.txt` — a clean,
deliberately-held cyclical test with zero `Ambiguous` frames) nor the wired latency
instrumentation (only `PoseToConfirm` is populated; `ConfirmToNoteEvent`/`EndToEnd` are
declared but never fed) could have surfaced either problem: the fixture never exercises
rapid re-articulation, and nothing measured confirmation *variance*, only its lower bound.

**Decision — split selection from articulation.**

The left hand's three static poses (§1.3) now do two different jobs:

- **Pose confirmation selects** which chord is active (`GestureInterpreter`, unchanged
  gestures, unchanged mapping). `HarmonyEngine.SetHeldFunction` is now silent — it updates
  `ActiveChord` (so the melody engine's chord-tone set and presentation still react
  immediately) but sends no note events.
- **A downward hand motion articulates** — `ChordStrikeDetector` (new,
  `Core/Gesture/`), a Schmitt-triggered speed threshold exactly like every other gesture
  threshold in §3.4, reading a new signed vertical-speed field on `HandPoseFrame`.
  `HarmonyEngine.Strike(velocity)` sounds the currently-selected voicing: cuts whatever is
  still ringing, then sounds the new one, struck-piano style with a fixed sustain
  (`HarmonyEngine.DefaultSustainSeconds`) — the same model `MelodyEngine` already used for
  the right hand (§3.3), now applied symmetrically. Re-striking the *same* function is now
  ordinary — it is no longer a distinguished case at all.

  Velocity reuses `Melody.VelocityCurve.FromSpeed` rather than a second bespoke mapping.

A downward strike (comping) was chosen over "re-entering the same pose re-triggers it"
because pose entry is inherently fuzzy (a static shape has no clean onset instant), while
a ballistic motion gives a crisp, low-variance onset, free dynamics, and is literally how a
pianist comps — and because decoupling lets pose confirmation become *more* tolerant
(next point) without that tolerance ever blurring musical timing.

**Confirmation is now tolerant of brief noise.** `GestureInterpreter` no longer resets
progress on a single non-matching frame; it tolerates up to
`GestureThresholds.ConfirmationMissTolerance` (default 2) consecutive misses —
`None`, `Ambiguous`, or a third pose — before concluding the hand has genuinely moved on.
Only a *sustained* mismatch redirects confirmation. Because pose selection no longer gates
when a chord sounds, `PoseHoldSeconds` also moved 120 ms → 150 ms: selection can afford to
buy stability now that it is off the critical timing path.

**What was deliberately left out of this change**, to keep it scoped to the measured
left-hand problem:

- **`MetronomeVoice` was not built.** `LessonRunner.DrainMetronome` still discards clicks
  (`Assets/Jazztures/Lessons/LessonRunner.cs`); L2's in-app metronome remains silent, which
  is why the finding above was tested against an external one. ADR-0024 already assumed
  this would exist by L2 ("Watch and Listen, with the metronome") — it is a real,
  independent gap, not new. Recommend: fast-follow, before any L2 pilot session.
- **A real rhythmic hand-pose fixture was not recorded** — this needs an actual headset
  session (`HandPoseRecorderComponent`) attempting fast re-articulation, which cannot be
  produced from a desk. `Fixtures/HandPoseFixture_M3.txt` remains a happy-path fixture
  only. Recommend: record one and add it as a regression fixture before M8.
- **`LatencyStage.ConfirmToNoteEvent` / `EndToEnd` remain unwired.** Under this design a
  "confirm → note" stage no longer has a fixed meaning for harmony (confirmation no longer
  causes a note at all); the meaningful new stage is strike → note event, which is a
  synchronous same-call-stack operation with no frame boundary to measure. Re-scoping
  `LatencyStage` was judged out of scope for this change rather than half-done.
- Right-hand melody/touch-target behaviour is untouched.

**Thesis impact.** §1.3's left-hand description ("hold a pose, the chord sounds") is now
incomplete: the left hand has a static component (pose → selection) *and* a dynamic
component (a downward strike → sound), the latter closer to how §1.3 already describes the
right hand's mid-air touch targets than to a sustained organ-style hold. This is additive,
not a reversal of the co-design gesture set — ii/V/I are unchanged — but Chapter 6's
description of the interaction model, and any figure showing "pose held → chord audible",
needs the strike step added. §4.3's latency budget gains a segment (pose → confirm remains
as specified; confirm → sound is no longer applicable to harmony the way it is to melody).
Pilot-measure `ChordStrikeDetector`'s thresholds and `HarmonyEngine.DefaultSustainSeconds`
at M8 alongside the existing gesture thresholds; record them in `Docs/CALIBRATION.md`.

---

## ADR-0024 — Lesson 1 is a single Gesture Learning phase

> **Superseded in part (2026-10-19, ADR-0052 Revision 2):** Lesson 1 again offers Watch and Listen and Try Yourself,
> as separate modes, and no longer lists Gesture Learning. The reason below for cutting Watch and Listen ("six bare voicings, nothing
> to watch") no longer holds now the ghost forms and strikes each chord, and the menu makes every mode a choice.

**Date:** 2026-09-10 · **Status:** Accepted (student design call) · **Milestone:** M5 ·
**Changes the thesis:** §3.9's mode table for L1 (again — see ADR-0021), and Chapter 6 lesson pacing
**Supersedes:** the "L1 gains a `GestureLearning` phase → GestureLearning → WatchAndListen → TryYourself" decision in ADR-0021 §3

ADR-0021 made L1 a three-phase follow-along: find the shapes (Gesture Learning), hear
the progression demonstrated (Watch and Listen), then a gated attempt (Try Yourself).
On device that sequence did not hold up.

**Why it was cut.**

- **Watch and Listen was jarring.** L1 authors no melody (`_notes: []`), so the demo is
  six bare close voicings over ~16 s with no pulse (`_useMetronome: 0`, and no
  `MetronomeVoice` exists yet), no groove and nothing for the learner to do — landing
  straight after an interactive phase. It read as the system stalling, not demonstrating.
- **Try Yourself was redundant.** Once Gesture Learning was restored to its §3.8 policy
  (unconditional audio, ungated — see the same-day fix to `ModePolicy`), the only thing
  Try Yourself adds for a *pose-only* lesson is that the ghost waits and the audio is
  reward-gated. For three static shapes with no melodic dimension that is a near-duplicate
  of the Gesture Learning phase the learner just did.

**Decision.** L1's mode list is `[GestureLearning]` only. The lesson is a ~30 s primer:
the ghost demonstrates ii → V → I twice on the phrase clock, the learner copies at their
own pace and hears each chord as they form it (§3.8 — "pose fluency only, no musical
target"). The timeline, markers and cue track are unchanged; the closing cue now fires at
lesson end rather than after phase 1.

**What L1 no longer does:** play the learner a fluent, in-time ii-V-I. That first fluent
demonstration now arrives in **L2** (Watch and Listen, with the metronome, over frozen
harmony) and **L3**, both in Session 1. L1's stated objective — gesture→chord mapping and
the prepare/tension/release relationship — is carried by the mapping itself (shape →
chord), the ii-V-I demonstration order, and the concept text.

**Thesis text:** §3.9's table should show L1 as Gesture Learning only. Chapter 6's
description of L1 as a three-phase follow-along (already flagged by ADR-0021) needs the
same update. The Gesture Learning mode description in §3.8 is unchanged and now matches
the implementation exactly.

---

## ADR-0021 — Lesson 1 as a three-phase follow-along; cue actions get a channel; a placeholder HUD

**Date:** 2026-09-09 · **Status:** Accepted (student design call) · **Milestone:** M5 ·
**Changes the thesis:** §3.9's mode table for L1, and lesson pacing

M5's code was all written but never assembled: `LessonRunner`, the state machine, the
timeline, the cue track and the mode gate all existed, but nothing was in the scene and
cues had nowhere to go but the Console. Making L1 an actual follow-along lesson exposed
three gaps.

### 1. Cue actions had no route to presentation

`LessonRunner.OnCueAction` consumed the control-flow cues (wait-for-input, scoring,
advance-phase) and `Debug.Log`ged everything else, so an authored `ShowText` could never
reach a display.

**Decision.** New `CueActionChannel` (`EventChannel<CueAction>`). The runner still consumes
control flow itself and publishes only *presentational* cues — captions, highlights,
tension colour. The cue track can therefore drive the HUD without the HUD ever being able
to steer the lesson, which is the §2.3 direction rule holding.

### 2. No instruction surface

**Decision.** `Presentation/LessonHud.cs`, an explicit M5 placeholder. Three lines of
world-space text, soft-following the head (eased, not head-locked — rigid geometry at
reading distance is a comfort problem), sitting slightly *below* eye line so reading it
does not tilt the head and pull the hands out of the tracking cone (ADR-0020):

- **banner** — the mode, in plain words ("Watch and listen", "Your turn");
- **prompt** — the pose the lesson is asking for *right now*, from `GhostFrameChannel`,
  named the way the learner was taught it: "Fist", "Open palm, facing down". Never a
  chord symbol (§1.5 — no jargon without explanation);
- **caption** — authored text from the cue track, plus the deferred end-of-attempt score
  (§3.7 — never mid-phrase).

It also cross-references `ChordChangedChannel` (the learner's *confirmed* pose) against the
prompt and marks a match, so the learner gets a visual confirmation alongside the audio
reward `ModeGatedNoteSink` already provides in Try-Yourself.

Built from Unity's builtin font via runtime `TextMesh`, deliberately: TextMeshPro would
need its Essentials package imported into `Assets/` and an extra assembly reference —
two more things to be wrong on a first device test, for a surface M6 replaces anyway.

### 3. L1's phrase was a demonstration, not a lesson

As authored, L1 put its three chords at beats 0, 2 and 4. `LessonTimeline.DurationBeats`
is the last event's beat, so the phrase was **4 beats — 3 seconds at 80 BPM**. Ample to
*hear* a ii-V-I; nowhere near enough for a novice to find three unfamiliar hand shapes,
each of which needs a 120 ms hold plus three confirming frames before it even registers
(§3.4).

**Decision.** One chord per bar, played twice: beats 0/4/8/12/16/20 → a 20-beat phrase,
**15 s**, 3 s per chord, with a `second-time` marker at beat 12 for the cue track. Roughly
48 s for the whole lesson.

**Also: L1 gains a `GestureLearning` phase**, so the mode sequence is
**GestureLearning → WatchAndListen → TryYourself**. §3.9's table lists L1 as "W&L, TY", so
this is a deviation — but §3.8 describes Gesture Learning as the mode that "precedes
Watch-and-Listen ... builds a movement lexicon before musical demand is added", which is
exactly what a first lesson teaching three hand shapes needs. Its policy (no system
playback, ghost visible, user audio *always* on) gives the learner a no-pressure phase to
find each shape and hear it, before the demonstration and then the gated attempt.

**Known limitation, deliberately not fixed:** cues are not phase-scoped —
`LessonCuePlayer` resets per phase, so a beat-0 caption fires in all three. L1's captions
are therefore written phase-neutrally. If lessons later need "say this only in Try
Yourself", `CueTrigger` needs a phase filter; not worth the complexity until a lesson
actually calls for it.

**Thesis impact:** none to Chapter 6. §3.9's L1 row should read "GL, W&L, TY". The design
chapter should note that lesson phrase length is a pedagogical parameter, not just a
musical one — the same progression needs a longer phrase for practice than for
demonstration, and the M8 pilot should measure it rather than inherit 80 BPM's arithmetic.

---

## ADR-0020 — Melody arc pulled toward centre: the two hands compete for the Quest 2 hand-tracking cone

**Date:** 2026-09-09 · **Status:** Accepted (student design call) · **Milestone:** M4 ·
**Amends:** ADR-0017 (the +0.20 m lateral offset), ADR-0019 · **Relates to:** the open
"target Quest model" item (§7) · **Changes the thesis:** §1.4 wording

On device (Quest 2), the learner instinctively turns their head right to look at the melody
arc — and doing so carries the **left** hand toward the edge of the hand-tracking camera
cone, dropping harmony tracking. §3.5's loss policy catches it (the chord sustains, no
spurious change), but the layout should not force the choice.

**The geometry.** With `_shoulderLateralOffsetMetres = 0.20` and the ADR-0019 ±32° arc
span, the arc's right edge sits **+49° from body-forward**. Seeing it needs a ~30° head
turn, which puts a centrally-held left hand at ~−40° from *head*-forward — at the ragged
edge of the Quest 2's usable hand-tracking zone (roughly ±45–50° before reliability falls
off; the ~140° in §1.4 is closer to head/controller tracking and to the Quest 3, not Quest
2 hands).

**Decision.** `_shoulderLateralOffsetMetres` 0.20 → **0.08** `[TUNABLE]`. The arc centre
moves to ~10° right (still matching the learner's instinct to "look a bit right"), the
right edge to ~+40°, the left edge to ~−20°. A modest head turn now reaches the outer
melody targets while the left hand stays inside the tracking cone. The ADR-0019 arc *shape*
— equal reach, glissando as one shoulder sweep — is unchanged; only where the arc sits
laterally. The 16° columns / 4.5 cm neighbour gap are kept.

This unwinds part of ADR-0017's reasoning ("shift the grid toward where the right hand
rests"): correct for the arm in isolation, wrong once the left hand's FOV budget is
counted. On a narrow-FOV headset, "directly in front of the learner" (§1.4) has to win.

**Thesis impact:** §1.4's "inside the Quest's ~140° tracking FOV" should be qualified — for
**hand** tracking on Quest 2 the usable cone is nearer ~100–120°, and both hands' resting
and working positions must fit inside it *simultaneously*, which is a tighter constraint
than either hand alone. This strengthens the case for running the study on Quest 3 / 3S if
the lab has them (open item, §7); on Quest 2 the melody arc cannot be pushed as far to the
side as the arm would prefer. No Chapter 6 (methodology) impact.

---

## ADR-0019 — Right-hand targets on a shoulder-centred arc; a plane-crossing redesign built and set aside

**Date:** 2026-09-09 · **Status:** Accepted (student design call) · **Milestone:** M4 ·
**Amends:** ADR-0016 (geometry), ADR-0018 (finger set) · **Changes the thesis:** §3.3
`[TUNABLE]` wording + a design-chapter paragraph (not Chapter 6)

### The brief

After playing the ADR-0018 build (spheres, 3.5 cm radius, 8 cm spacing → **1 cm gap**, 15 cm
deep), the student's own read was sharper than either bug report:

> "it can actually be quite fun to slide my finger between the touch targets, but I find
> that touching the targets individually can be finnicky."

One number explains both halves. Ten 15 cm-deep volumes on a 1 cm gap form a near-continuous
slab: sliding a finger through it fires note after note — a glissando that feels good — while
reaching in for **one** target clips its neighbours. The depth is what makes the slide work;
the gap is the lever.

### Decision — open the spacing onto an arc

A flat grid cannot just be spread wider: on a flat row the outer columns sit further from
the shoulder than the centre, so they get harder to reach exactly as spacing grows. Instead
the five degree-columns are swept around a **pivot at the learner's right shoulder**, every
target at the same reach.

- Pivot = head + (−0.25 m up, +0.20 m right) — the real shoulder. `_reachDistanceMetres`
  (0.45 m) stops being a forward offset and becomes the **arc radius**.
- Column `d` is at angle `(d − 2)·_columnAngleDegrees` about the pivot's up axis; the two
  octave rows are stacked `_rowSpacingMetres` apart vertically.
- Each target is rotated to face **radially outward**, so its local +Z — the axis
  `TargetVolume` tests depth along, and the axis the fingertip approaches on — points back
  at the learner. The hit test already runs in each target's own frame, so this is nearly
  free.
- The centre column does not move: at angle 0 it lands exactly where the old grid centre
  was. Only the outer columns wrap toward the learner.

| `[TUNABLE]` | Was | Now | Effect |
|---|---|---|---|
| Column spacing | 8 cm flat | **16°** (≈ 12.5 cm chord) | neighbour gap **1 cm → 4.5 cm** |
| Row spacing | 8 cm | **14 cm** | 6 cm vertical gap; no reach cost, so generous is free |
| Target radius | 3.5 cm | **4 cm** | easier to hit, still leaves the 4.5 cm gap |
| Depth | 15 cm | **15 cm** (unchanged) | this is what lets a lateral slide play a glissando |
| Trigger fingers | index/middle/ring/pinky | **index + middle** | spare fingers clip neighbours when isolating one target |

Half-span is ±32°, putting the outer targets ~24 cm either side of the pivot and ~38 cm
out — a comfortable shoulder sweep, inside the ~140° tracking FOV (§1.4). If isolating one
target is still fiddly, `_columnAngleDegrees` 18–20° widens the gap to 6–7.6 cm; the cost is
a gappier glissando as the off-zones approach target width. That is the M8 pilot-calibration
loop, run early.

Renamed the two anchor params to `_shoulderHeightOffsetMetres` / `_shoulderLateralOffsetMetres`
— they now locate a pivot, not bias a grid, and the old names would mislead at calibration.

### The plane-crossing redesign, and why it was set aside

Between the ADR-0018 build and this one, a full redesign was drafted (and is in the plan
history): replace the ten volumes with a single flat panel tiled into ten cells, firing a
note the instant a fingertip **crosses** the plane pushing away — `TriggerPlane` in `Core`,
unit-tested, `TargetVolume` deleted. It solved the isolation and depth problems cleanly:
crossing is a sign flip on one coordinate, so depth error and frame-rate tunnelling both
become structurally impossible.

It was **not built** because it would have destroyed the gesture the student values. The
panel took MIDI velocity from the **normal** component of fingertip speed — how hard you
push through. A lateral slide has almost no normal component, so every glissando note would
have been gated to silence. The slide is worth more than the theoretical cleanliness.

This is a real finding, not just a dead end: **for a musical mid-air instrument, the
mechanic that best preserves expressive lateral gesture is a shallow-front, deep-back
containment volume — not a crossing plane.** The design chapter should say so, and note that
three right-hand mechanics were prototyped (shallow sphere, deep cylinder, crossing plane)
before the arc-of-cylinders settled.

### Thesis impact

None to Chapter 6 — the §3.3 interaction model (fingertip enters a target volume,
entry-velocity gate, speed→velocity) is unchanged. §3.3's `[TUNABLE]` line "Inter-target
spacing: 8 cm centre-to-centre" becomes an angular column spacing on a shoulder-centred arc.
§3.1's ten targets, chord-tone restriction and stable degree→slot map are all intact — slot
order is untouched, only the mapping from slot to position changed. The design chapter gains
the arc rationale, the glissando as an intended gesture, and the three-mechanic prototype
history.

---

## ADR-0018 — Touch targets: depth-extended volume, any fingertip, gate decoupled from the loudness curve

**Date:** 2026-09-08 · **Status:** Accepted; the depth-extended volume stands (it enables
the glissando), the four-finger set is narrowed to two by ADR-0019 · **Milestone:** M4 ·
**Amends:** ADR-0016 · **Changes the thesis:** §3.3 `[TUNABLE]` wording only

First on-device test of the right-hand melody targets: hard to hit. Two reports —
**only the index finger fires**, and **aiming carefully at a target often does nothing**.

Finger-curl-per-tone was raised again as the fix and **rejected again**: §1.3 `[THESIS]`
fixes the right hand as mid-air touch targets, ADR-0005 already discarded exactly that
mapping (co-design: individual finger bends are "unergonomic and semantically empty"), and
§1.2 warns that keyboard realism bought with gestural fluidity is a regression. Recording
the re-test here because a panel may ask whether the constraint was ever pressure-tested —
it was, under real usability failure, and held. The interaction stays; the implementation
was the problem, and all of it is `[TUNABLE]` (§3.3 lists radius, spacing and the entry
gate as tunables).

**Three causes, three fixes:**

1. **Depth was as unforgiving as lateral aim.** `TouchTarget.Contains` was a point-in-sphere
   test, so a 3.5 cm radius punished Z error exactly as hard as XY error — and Z is the one
   axis a person cannot judge in mid-air VR (no contact cue; §3.10 rules out haptics).
   → The volume is now a **cylinder along the target's local Z** (the approach axis):
   `Assets/Jazztures/Core/Melody/TargetVolume.cs`, a pure scalar `readonly struct`, unit-
   tested. XY face **unchanged** — still 3.5 cm radius, still selects degree + octave, still
   under half the 8 cm spacing (§3.3 "spacing must exceed 2×radius + jitter"). Depth default
   **±7.5 cm** (`MelodyConfig._targetDepthMetres`, `[TUNABLE]`).

2. **One finger.** `TouchTargetBinder` read only `HandJointId.HandIndexTip`.
   → `_fingerTips` is now a serialized array, defaulting to index / middle / ring / pinky
   tips (thumb excluded — it does not point the way the others do). Entry-edge state is
   per-`(finger, target)`; `MelodyEngine`'s existing 80 ms per-target cooldown collapses
   two fingers landing together into one note.

3. **The entry gate punished careful aiming.** `MelodyEngine.EntryVelocityGateMetresPerSecond`
   was aliased to `VelocityCurve.MinSpeed` (0.15 m/s), and the binder measured the *last
   single frame's* delta. Deliberate aiming is slow and decelerates on arrival, so a
   well-aimed approach landed under the gate and was dropped silently.
   → The gate is its own `const` at **0.08 m/s** — it answers "was this a deliberate
   strike?", a different question from the curve's "how loud?". `VelocityCurve` is
   untouched (`MinSpeed` stays 0.15); `FromSpeed` already clamps below its minimum, so an
   0.08–0.15 m/s entry sounds at `MinVelocity` (40) — well above §3.3's absolute floor of
   30. The binder also now takes the **peak speed over a short window**
   (`MelodyConfig._speedSampleFrames`, default 3) instead of one frame.

**Also added:** a hover glow. Targets a fingertip is *near* (within
`MelodyConfig._hoverScale`× the trigger volume, default 1.6) light up before the note
fires, so the learner gets a signal while approaching and can learn the depth — the CTML
secondary channel §3.10 already sanctions, no new HUD.

**Not done:** a swept / continuous hit test. Extending the volume along the approach axis
raises the in-volume dwell from ~3.4 frames to ~11 at 1.5 m/s (72 Hz), so tunnelling stops
being the binding constraint, and fast strikes were not among the reported failures.
Revisit only if lateral strumming across targets becomes a real gesture.

**Thesis impact:** none to Chapter 6. §3.3's `[TUNABLE]` line "Target radius: 3.5 cm
sphere" should read "3.5 cm radius, extended along the approach axis" in the design chapter.
Entry-velocity gate there (0.15 m/s) should read 0.08 m/s and note that it is distinct from
the velocity-curve minimum.

---

## ADR-0017 — Tension colour: always-visible neutral targets, right-biased anchor, hand+target share one eased colour

**Date:** 2026-09-08 · **Status:** Accepted (student design call); the +0.20 m rightward
anchor is walked back to +0.08 m by ADR-0020 (Quest 2 FOV) · **Milestone:** M4 ·
**Changes the thesis:** design chapter wording (not Chapter 6)

Three linked presentation decisions, driven by a request to make the touch targets legible
between chords and the colour scheme less arbitrary.

### 1. Targets are always rendered

Previously `TouchTarget.Clear()` disabled the renderer whenever no chord was held, so the
grid vanished between chords and on every ii/I ambiguity hold. §3.1 `[THESIS]` has the
learner building "a stable spatial map of scale degree" — a map that disappears is a map
that is hard to build. Against that, §3.10 demands visual restraint ("every added HUD
element spends limited-capacity budget").

**Decision.** The ten targets are always visible. With no chord held they render in a cool
grey at low alpha (`0.35`) — present enough to anchor the spatial map, muted enough to read
as inactive. Visibility and playability are **decoupled**: `IsSounding` (set only by a held
chord) still gates triggering in `TouchTargetBinder`, so a visible neutral target cannot
fire a note. This spends a modest, constant slice of visual budget to buy spatial-map
stability — a CLT trade in §3.1's favour, and cheaper than the alternative of the learner
re-locating ten targets every chord change.

### 2. The target grid is biased to the right

ADR-0015 body-anchors the grid to head position + flattened yaw, but centres it on the
body midline. The right hand plays it; a centred grid asks the learner to reach across.
`MelodyConfig._anchorLateralOffsetMetres` (default **+0.20 m**, `[TUNABLE]`) shifts the
whole grid right along the anchor's local X. At 8 cm spacing the 5-wide grid spans ~0.32 m,
so it sits roughly +0.04 → +0.36 m off the midline — within the right hand's natural reach
and inside the ~140° tracking FOV (§1.4). Extends ADR-0015; no new reference frame.

### 3. Hand and targets share one eased colour

New component `Jazztures.Presentation.TensionColorDriver` (the name §2.5's folder list
already reserves) owns all tension colour. It subscribes to `ChordChangedChannel`, eases a
single colour from the old chord's tint to the new one over
`TensionPalette.TransitionSeconds` (default 0.25 s, SmoothStep), and pushes that colour to
every touch target (base tint) and to the **left hand's outline** (`_OutlineColor` /
`_OutlineGlowColor` on `OculusHand.mat`, via the two `MaterialPropertyBlockEditor`s
`OVRHandVisual` ships). Tinting must route through `MaterialPropertyBlockEditor`, not a
direct `SetPropertyBlock` — `HandVisual` re-pushes the block from that editor's own lists
every frame and would clobber a direct write.

The left hand carries the harmonic colour because it *is* the harmony hand (§1.3). This
makes colour a **redundant** reinforcement of the tension–release arc, never the sole
carrier (§3.10): the chord is always also audible, and the pose and the lit targets show it
too. A learner who cannot resolve sage from olive loses nothing.

### Palette — `[TUNABLE]`, on `Config/TensionPalette.asset`, pilot-calibrated at M8

| Function | Colour | RGBA |
|---|---|---|
| none | cool grey | `0.58, 0.58, 0.62, 0.35` |
| ii — preparation | sage / olive | `0.53, 0.60, 0.42, 0.80` |
| V — peak tension | burnt sienna | `0.74, 0.33, 0.18, 0.85` |
| I — resolution | warm purple | `0.52, 0.36, 0.60, 0.85` |

**Deviation from §3.10.** §3.10 specifies "cool/neutral for ii, warm/saturated for V,
resolved/settled for I". The student's palette direction was **mustard** for ii — a warm
yellow, not cool. Sage/olive is the compromise: a desaturated yellow-green that still reads
as "preparation / not yet resolved" while sitting closer to §3.10's intent than mustard
would. V (burnt sienna) and I (warm purple) match §3.10 directly. Saturation and depth still
rise across ii → V → I, so the arc reads as designed. All values are `[TUNABLE]` — an
engineer's pick, not a co-design finding — and go in `Docs/CALIBRATION.md`, not the paper.

**Thesis impact:** the design chapter's colour-coding description must say the ii → V → I
arc runs **sage/olive → burnt sienna → warm purple** (not "cool → warm → resolved" in the
abstract), that the neutral state is a low-alpha grey with the targets always visible, and
that the left (harmony) hand's outline carries the same colour as a redundant channel. No
Chapter 6 (methodology) impact — the §3.10 feedback model (auditory primary, visual
secondary and redundant, restraint) is unchanged.

---

## ADR-0016 — Touch targets are volumetric spheres over the tracked fingertip, not ISDK `PokeInteractor`

**Date:** 2026-09-07 · **Status:** Accepted · **Milestone:** M4 ·
**Changes the thesis:** §4.1 wording (not Chapter 6)

`CLAUDE.md` §4.1's stack table says "Interaction SDK pose detection + `PokeInteractor`
for touch targets". §3.3 says the note-on "fires when a right-hand fingertip enters a
target **volume**", gated by a minimum **entry velocity** (0.15 m/s `[TUNABLE]`), with the
MIDI velocity **mapped from fingertip speed**.

**The problem.** ISDK's Poke is a *surface* interaction — `PokeInteractable` wraps a
planar (or curved) surface with a front face, a normal, and hover/press distances; it is
built for buttons and panels. A mid-air sphere the learner may enter from any direction
is not a poke surface. And `PokeInteractor`'s select event carries neither the entry
speed the §3.3 gate needs nor the speed the velocity curve maps from — both would have to
be reconstructed alongside it.

**Decision.** Each `TouchTarget` is a sphere of `MelodyConfig.TargetRadiusMetres`. Each
frame (`LateUpdate`, after the rig has moved) `TouchTargetBinder` reads the right index
fingertip from the SDK — `IHand.GetJointPose(HandJointId.HandIndexTip, …)` — measures its
speed from the previous frame, and on the frame the fingertip crosses **into** a target
calls `MelodyEngine.TriggerTarget(index, speed)`. The engine — pure `Core`, already
tested — owns the entry-velocity gate, the 80 ms retrigger cooldown and the speed→velocity
curve. The binder only detects the volume-entry edge and supplies the speed.

This is **not** a hand-rolled classifier (ADR-0010): the SDK still does all the hand
tracking and provides the joint pose; this is a distance test against it.

**Alternatives rejected:**
- *Full Poke stack* (`PokeInteractable` + surface patch per target + one `PokeInteractor`
  on the fingertip) — the sphere-as-button mismatch, and a velocity gate bolted onto
  Select events.
- *Trigger colliders + `OnTriggerEnter`* — works, but couples note onset to the physics
  tick and needs a `Rigidbody` on the hand; a per-frame distance test after the rig moves
  is simpler and frame-deterministic.

**Thesis impact:** change §4.1's "`PokeInteractor` for touch targets" to
"tracked-fingertip volumetric targets". No Chapter 6 (methodology) impact — the §3.3
interaction model (volume entry, entry-velocity gate, speed→velocity) is exactly what is
implemented.

---

## ADR-0015 — Right-hand touch targets are body-anchored with a lazy recenter, not world- or head-locked

**Date:** 2026-09-07 · **Status:** Accepted (student design call) ·
**Milestone:** M4 · Resolves a gap `CLAUDE.md` §3 leaves unstated

§3.4 `[THESIS]` fixes the *gesture axes* (left-hand palm orientation) as relative to the
head/body-forward vector — "the user turns; the gestures must not break". It says nothing
about the reference frame of the **right-hand touch targets**. §1.4 has the learner
**standing**, with interaction "directly in front of them, inside the Quest's ~140°
tracking FOV". §3.1 has the learner building "a stable spatial map of scale degree" —
targets are re-pitched, never re-arranged.

**The problem.** Three candidate frames, and two of them break a thesis constraint:

| Frame | Failure |
|---|---|
| **World-locked** (targets fixed in the room) | The learner shifts weight, steps, or turns and the targets are off to one side or behind them. Breaks §1.4 ("directly in front"). |
| **Head-locked** (targets parented to the camera) | Targets swing with every glance — look at the left hand and the melody map lurches. The learner can never build the stable spatial map §3.1 is designed around. Rigidly head-locked geometry at arm's length is also a well-documented nausea source. |
| **Body-anchored** | — |

**Decision — body-anchored with a lazy recenter.** The target rig's anchor is the head
*position* plus the head yaw *flattened to horizontal*, dropped to roughly chest height
and pushed forward to a comfortable reach. It ignores head pitch and roll entirely. It
does **not** track yaw instantly: the rig recenters toward the current facing only after
the head has diverged past an angle threshold and held there for a short dwell, then eases
over. Net effect: glance around, lean, check your left hand — the targets stay put; turn
your body to face a new direction — the targets follow you there.

This also unifies the interaction. The left-hand gesture axes are already
head/body-forward relative (§3.4); the ghost hands superimpose on the learner's own
tracked hands (ADR-0012). Body-anchoring the right-hand targets puts every part of the
interface in one reference frame.

**Parameters** — all `[TUNABLE]`, on `Assets/Jazztures/Config/MelodyConfig.asset`,
pilot-calibrated at M8: reach distance, chest-height offset, recenter divergence angle,
recenter dwell time, recenter ease speed — plus the target geometry (radius, inter-target
spacing, the 2×5 degree/octave grid). Mirrored in `Docs/CALIBRATION.md`.

**Thesis impact:** none to Chapter 6 (methodology). The design chapter should describe the
targets as a body-anchored rig with a lazy recenter, and note that world-locked and
head-locked were considered and rejected (spatial-map stability and comfort).

---

## ADR-0014 — ii / I orientation: the SDK has no lateral axis; ii keys on `FingersUp`, I on `PalmDown`

**Date:** 2026-09-04 · **Status:** Accepted (supersedes the first cut, tested on device) ·
**Milestone:** M3

§1.3 `[THESIS]` fixes the three left-hand poses: **ii** = open palm facing the user's
right (preparation), **V** = fist (done, ADR-0006 / M3), **I** = open palm facing down
(release). ii and I share one hand shape (`Assets/Jazztures/Input/Poses/OpenPalm.asset`,
all four fingers `Curl = Open`) and differ only in wrist orientation, detected by the
Interaction SDK `TransformRecognizerActiveState` per §3.4 ("`TransformRecognizer` for palm
orientation … do not hand-roll joint-angle math").

**The problem — and why the first attempt failed on device.** Every SDK `TransformFeature`
is `Vector3.Angle(handVector, targetVector)` where `targetVector` is either the vertical
(head/gravity up) or `CenterEyePose.forward`. There is **no "user's right" axis.** For a
left hand held in front of the body with the palm facing the user's right, the back of the
hand points to the user's *left* — ~90° from *both* "up" and "face-forward", so
`PalmDown`, `PalmUp`, `PalmTowardsFace` and `PalmAwayFromFace` all read ~90° and none fire.
The first cut (ii = `PalmTowardsFace` + `FingersUp`) only triggered when the arm was
extended far to the left so the dorsal vector swung toward face-forward — which is why ii
was "inconsistent and very hard to trigger" in the first on-device test. **"Palm faces the
user's right" is not observable with this SDK's feature set.**

**Decision — key on the reliable correlate instead:**

| Pose | Shape | Orientation (`TransformRecognizerActiveState`, AND with the shape) |
|---|---|---|
| **I**  | `OpenPalm` | `PalmDown = True` |
| **ii** | `OpenPalm` | `FingersUp = True` |

ii as performed is a **vertical** open hand (fingers up); I is a **flat** open hand
(fingers forward). `FingersUp` separates them cleanly and widely — ii sits near 0–30°, I
near 90°, with no overlap — and it degrades gracefully: a slightly rolled palm still reads
as ii. The gesture the learner is *taught* and the ghost hand *shows* is unchanged
("open palm, facing right"); only the machine's discriminator changes, because verticality
is the part of that pose the tracker can see reliably.

**Alternatives considered and rejected:**
- *`PalmTowardsFace` (+ `FingersUp`)* — the first cut. Geometrically only valid at extreme
  arm extension; failed on device.
- *Rotating the reference frame (`TransformConfig.RotationOffset`) so `PalmDown` maths
  measure a rightward palm* — works, but "the palm-down detector fires on a sideways palm"
  is indefensible under questioning.
- *`JointRotationActiveState` on the wrist, checking the palm normal against
  tracking-space right* — this **is** an SDK recogniser (not a hand-rolled classifier, so
  not barred by §3.4 / ADR-0010) and would restore a true palm-orientation check. Deferred:
  its reference direction is world/hand-local, not head-relative, so keeping ii stable as
  the user turns (§3.4) needs extra rig work. Revisit at M8 if pilot data shows learners
  confusing ii with a generic "hand up".

**Threshold asset.** §3.4 specifies the palm cone at **enter 35° / exit 50°** `[TUNABLE]`.
`Assets/Jazztures/Config/GesturePalmConeThresholds.asset` (a full copy of
`DefaultTransformFeatureStateThresholds`) sets **`PalmDown` to midpoint 42.5° / width 15°**
(→ False→True at 35°, True→False at 50°). `FingersUp` is left at the SDK default (midpoint
40° / width 20° → 30°/50°); widen it here if ii proves finicky when the hand is held with
fingers angled forward. Both ii / I `TransformRecognizerActiveState`s point
`TransformConfig.FeatureThresholds` at this asset; `UpVectorType = Head`. Mirrored in
`Docs/CALIBRATION.md`; pilot-calibrated at M8. The `_palmConeEnterDegrees` /
`_palmConeExitDegrees` fields on `GestureThresholdsConfig` stay the human-readable record
of intent; the SDK reads the asset, so the two are kept in sync by hand.

**Ambiguity (§3.4 "never guess").** `FingersUp = True` and `PalmDown = True` are
physically exclusive (a hand cannot be both vertical and flat), so ii and I rarely race.
When they do — mid-rotation — `MetaXRHandPoseSource.ReadCandidate()` returns `Ambiguous`
and `GestureInterpreter` holds the previous function and emits nothing.

**Thesis impact:** the design chapter must state plainly that the ii gesture ("open palm
facing the user's right") is **recognised by hand verticality (`FingersUp`), not by palm
azimuth**, because the Interaction SDK exposes no lateral orientation feature and a
head/gravity-relative cone cannot capture "palm right" for a hand held in front of the
body. Note `JointRotationActiveState` as the path to a true palm-orientation check if a
reviewer presses on fidelity. No Chapter 6 (methodology) impact.

---

## ADR-0013 — Hand-tracking scene topology: one data path, one renderer

**Date:** 2026-09-04 · **Status:** Accepted · **Milestone:** M3

`Assets/main.unity` ends up with two overlapping hand systems, because the Meta XR
Building Blocks hand-tracking block and the Interaction SDK rig both ship a full hand
stack. Left as installed they z-fight, and it is not obvious which one the pose
recognisers actually read. Fixed topology:

```
[BuildingBlock] Camera Rig
└── TrackingSpace
    ├── LeftHandAnchor  → [BuildingBlock] Hand Tracking left    ← OVRHand: DATA ONLY
    └── RightHandAnchor → [BuildingBlock] Hand Tracking right   ← OVRHand: DATA ONLY
└── OVRInteraction            (OVRCameraRigRef + OVRTrackingToWorldTransformer)
    └── OVRHands
        ├── OVRLeftHand  → OVRHandDataSource, Hand, OVRLeftHandVisual   ← THE renderer
        └── OVRRightHand → OVRHandDataSource, Hand, OVRRightHandVisual
```

**Decision — the Building Block hand objects are a data source, not a visual.** Their
`OVRMeshRenderer` **and** `SkinnedMeshRenderer` are disabled (both: `OVRMeshRenderer`
carries `_confidenceBehavior = ToggleRenderer`, so disabling only the renderer lets it
switch itself back on). `OVRSkeletonRenderer` ships disabled already.

**They must stay active.** `OVRCameraRigRef.LeftHand` resolves via
`handAnchor.GetComponentInChildren<OVRHand>(true)`, and `FromOVRHandDataSource` reads that
`OVRHand` every frame. Deactivating the GameObjects — the tempting way to kill the second
mesh — silently returns the whole pipeline to `TrackingQuality.NotTracked`.

**The Interaction SDK `HandVisual` is the single hand renderer.** It reads the same
`IHand` that `ShapeRecognizerActiveState` reads, so what the learner sees is what the
recogniser sees. That equivalence matters because tracking dropout is a reported result,
not just a rendering concern (§1.4, §3.5.4) — a visual that could diverge from the
recognised pose would make the telemetry harder to defend.

**Wiring that is easy to lose.** Both `FromOVRHandDataSource` components need
`_cameraRigRef` and `_trackingToWorldTransformer` pointing at `OVRInteraction`, and
`OVRCameraRigRef._ovrCameraRig` must point at the Camera Rig. All three are null in the
shipped prefabs and are asserted in `Start()`; unset, hand data never arrives and the only
symptom is a permanently untracked hand. Separately, `FingerFeatureStateProvider` needs
all five `_fingerStateThresholds` entries populated from
`Packages/Meta XR Interaction SDK/Runtime/DefaultSettings/PoseDetection/`
(`DefaultThumbFeatureStateThresholds` for the thumb, `DefaultFingerFeatureStateThresholds`
for the other four) — an empty or null-valued list throws inside the SDK on every frame.

**Related code change:** `MetaXRHandPoseSource.CurrentFrame` now reads tracking quality
first and skips the recognisers entirely when the left hand is `NotTracked`. The SDK's
`IsStateActive` omits the `IsDataValid()` guard its sibling `GetCurrentState` has, so
querying a pose on an untracked hand throws instead of reporting no match. Nothing is
lost: `GestureInterpreter` discards the candidate whenever tracking is unusable (§3.5).

**Thesis impact:** none. Scene configuration only.

**Follows on:** ADR-0012 adds a *deliberate* second hand mesh (the translucent ghost).
That one is additive and must not be confused with this duplicate — it is tinted,
translucent, and driven by `GhostFrameChannel`, not by `IHand`.

---

## ADR-0012 — Ghost hand visualisation

**Date:** 2026-09-02 · **Status:** Accepted (student design call) ·
**Milestone:** M5 (ghost-hand data stream) / M6 (full renderer) · Resolves the `CLAUDE.md` §7 `[OPEN]` item

The thesis says co-design informed the ghost-hand visualisation but did not fix the
specifics. Decision, made by the student:

| Aspect | Choice |
|---|---|
| **Representation** | Translucent hand mesh, full articulated fingers. |
| **Anchoring** | Superimposed on the learner's own tracked hands — the ghost shows the delta between where the hand is and where it should be. **Not** flying toward the user: a rhythm-game reading was considered and rejected as too high a mental load (consistent with §1.3, where the three-lane design was cut for feeling like a rhythm game). |
| **Left-hand pose** | The full articulated hand forms the pose (open-palm-right / fist / open-palm-down). |
| **Right-hand melody** | **No ghost fingertip.** The target spheres light in sequence; the learner chooses the reach. Keeps the melodic choice with the learner (§1.3) and spends less visual budget (§3.10). |
| **Motion model** | Continuous animation through the phrase — the ghost morphs between poses rather than snapping. Conveys the *movement*, which is the point of Gesture-Learning mode given the absence of haptics (§3.8). |
| **Ghost vs. real hand** | Distinguished by translucency and a colour tint. |

**Architecture consequence:** the ghost visual is a pure subscriber. `LessonRunner`
publishes a ghost-hand *data stream* from the `LessonTimeline` — the demonstrated left-hand
`ChordFunction` (with the beat it changed, for morph timing) and the demonstrated melody
target lights (`targetIndex`, dsp time) — during ghost-hand modes only (§3.8 table). The
renderer consumes that and owns all mesh, translucency and tint values. M5 ships the data
stream and a placeholder renderer; the articulated translucent mesh with pose morphing is
M6 polish.

**Thesis impact:** none for Chapter 6 (methodology). The design chapter should describe
the ghost hands as above; note the rhythm-game option was considered and rejected on
cognitive-load grounds.

---

## ADR-0011 — Lesson content: SMF musical timeline + separate authored LessonScript

**Date:** 2026-09-01 · **Status:** Accepted (student signed off; changes Chapter 6) ·
**Milestone:** shapes the M5 `LessonDefinition` schema

The proposal states lesson material loads from Sibelius `.sib` files. `.sib` is a
proprietary binary format with no Unity reader — not implementable as written (`CLAUDE.md`
§3.9, §7).

**Decision:**

1. **Musical timeline from a Standard MIDI File.** Lesson phrases are engraved in a
   notation tool (Sibelius, MuseScore) and exported as `.mid`. An edit-time Unity
   importer reads it and produces a `LessonTimeline`:
   - left-hand staff / channel → `(beat, ChordFunction)` events (chord detected from the
     sounding notes, or a one-note-per-chord encoding on a dedicated channel);
   - melody staff / channel → `(beat, pitch, velocity)`, mapped at import to
     `(beat, targetIndex, velocity)` using the chord active at that beat;
   - tempo / time-signature meta events → the beat grid.
   This is what the ghost hands replay. SMF is sufficient — Jazztures needs a fingertip
   target and a hand pose per beat, not engraved-notation fidelity or per-finger detail.
   MusicXML was considered and rejected: a much larger parser for information this
   system does not use.

2. **Presentation cues from a separate `LessonScript`**, authored by hand on the
   `LessonDefinition` ScriptableObject — a list of `trigger → action`:
   - **trigger:** a beat, a named timeline event, or a learner action (e.g. "after the
     learner plays the tonic");
   - **action:** show/hide text, highlight a target, set the tension colour, wait for
     input, advance the lesson phase, gate scoring, …
   The music format carries none of this. Decoupling means a caption can be retimed
   without re-engraving the score, and cues can react to the learner, not just the clock.
   This is the ImproVisAR / Synthesia / Melodics pattern (note chart + cue track).

3. **Runtime loads baked assets only.** No `.mid` or notation parsing on-device.

**Thesis impact (Chapter 6):** replace the Sibelius/`.sib` runtime-pipeline description
with: "phrases engraved offline and exported as SMF; an edit-time importer bakes the
musical timeline into a lesson `ScriptableObject`; text and visual cues are authored
separately as a beat/event-keyed script on the same asset; runtime consumes only the
baked assets." Cheap to fix now, expensive in April.

---

## ADR-0010 — Gesture recognition: SDK recognisers + Core temporal state machine

**Date:** 2026-09-01 · **Status:** Accepted · **Milestone:** M3

`CLAUDE.md` §3.4 says to build on the Meta XR Interaction SDK's pose detection
(`ShapeRecognizer` + `TransformRecognizer` + `ActiveStateGroup`) and warns that "a custom
classifier ... cannot be defended in a viva". §2.6 is equally firm that gesture logic
must be testable without repeatedly donning the headset.

**Decision (confirmed by the student):**

- **Per-frame pose match** stays in the SDK. Three composed recognisers (one per pose:
  palm-right / fist / palm-down) each expose an `IActiveState`. `MetaXRHandPoseSource`
  reads them and the left/right `IHand`, and reports a `HandPoseCandidate` +
  `TrackingQuality` per frame. More than one match → `Ambiguous`; it never guesses.
- **All temporal logic** — pose-hold time, consecutive confirming frames, inter-chord
  debounce, the ii/I ambiguity rule, and the §3.5 tracking-loss policy — lives in a pure
  `Jazztures.Core.Gesture.GestureInterpreter`, unit-tested headless and replayable against
  recorded `IHandPoseSource` fixtures.
- The SDK curl/cone values and the interpreter's temporal values both live on one
  `GestureThresholdsConfig` asset (§3.4). The interpreter reads the temporal group; the
  recogniser assets must be kept configured to match the SDK group.

This is not a custom classifier — the SDK still classifies the pose. Only the timing
and safety rules are ours, and those are exactly what needs headless tests.

**Thesis impact:** none to the methodology. If §3.4's prose is quoted verbatim in the
paper, note that classification is SDK-side and only the confirmation state machine is
bespoke.

---

## ADR-0009 — Jazztures `.asset` files kept out of Git LFS

**Date:** 2026-09-01 · **Status:** Accepted · **Milestone:** M2

`.gitattributes` (inherited from the Unity Gitignore template) routes `*.asset`
through Git LFS. The project serialises assets as **Force Text**
(`ProjectSettings/EditorSettings.asset`, `m_SerializationMode: 2`), so `.asset` files
are YAML, and Jazztures leans hard on reviewable ScriptableObjects — event channels
(§2.3), tuning config (§0, `[TUNABLE]`), lesson data (§3.9, "data, not code"). LFS
pointers would make those undiffable and unmergeable.

**Decision:** add an override so `Assets/Jazztures/**/*.asset` and
`Assets/Tests/**/*.asset` are plain text, out of LFS. Third-party/boilerplate `.asset`
files elsewhere keep the LFS rule. Verified with `git check-attr`. No history rewrite —
the three pre-existing LFS `.asset` blobs (Unity/Meta boilerplate) are left as-is.

---

## ADR-0008 — Piano samples: Salamander Grand V3 (CC-BY 3.0)

**Date:** 2026-09-01 · **Status:** Accepted · **Milestone:** M2

`SamplerNoteSink` (§4.2) needs a pitched acoustic-piano sample set. Chosen: **Salamander
Grand Piano V3** (Yamaha C5, recorded by Alexander Holm), **CC-BY 3.0**. Pre-cut for
samplers, multiple velocity layers, sample points roughly every third semitone.

Obligations: CC-BY needs attribution — a `NOTICE` / credits entry naming the work,
author and licence. To be added when the samples land.

Placement: `Assets/Jazztures/Audio/piano/` (WAV, tracked via Git LFS — `*.wav` already
in `.gitattributes`). The student downloads the set; the loader (`SampleMap` +
`SamplerNoteSink`) maps a MIDI note to the nearest recorded sample and pitch-shifts by
the residual cents.

**Thesis impact:** Chapter 6 should name the sample source and licence.

---

## ADR-0007 — Domain value types are `readonly struct : IEquatable<T>`, not `record struct`

**Date:** 2026-09-01 · **Status:** Accepted · **Milestone:** M1

`CLAUDE.md` §2.4 says "domain values are `readonly record struct` where possible".
Unity `6000.5.0f1` compiles C# at **`-langversion:9.0`** (verified in
`Library/Bee/artifacts/*/Assembly-CSharp.rsp`). `record struct` is a **C# 10** feature,
so it does not compile in this project. Overriding the language level per-assembly via
`csc.rsp` was rejected: it fights the pinned toolchain and is a defensibility risk at the
viva for zero functional gain.

**Decision:** the "where possible" clause resolves to **`readonly struct` implementing
`IEquatable<T>`** with explicit `==` / `!=` / `Equals` / `GetHashCode` / `ToString` for
the small immutable value types (`Pitch`, `Chord`, `ChordVoicing`, `ChordToneSet`,
`NoteEvent`, `Beat`). Same semantics the spec intends (immutable, value equality,
allocation-free); more boilerplate. Larger, non-hot domain objects may use `record`
(reference type, C# 9) where an allocation is acceptable.

**Thesis impact:** none. If Chapter 6 names `record struct` specifically, soften to
"immutable value types".

---

## ADR-0006 — Left-hand pose assets rebuilt, not salvaged

**Date:** 2026-09-01 · **Status:** Accepted · **Milestone:** M0 → M3

The prototype's `Assets/Resources/PoseDetection/LeftHand_{II,V,I}_Recognizer.asset`
encoded the wrong gesture set: ii = "pointing gun", V = "peace sign", I = "open palm
facing forward" (see the deleted `Assets/Scripts/Hands/SETUP_INSTRUCTIONS.md`). The
thesis (§1.3 `[THESIS]`) specifies:

| Function | Pose | Palm orientation |
|---|---|---|
| ii | Open palm | facing the user's **right** |
| V  | **Fist** | — |
| I  | Open palm | facing **down** |

The prototype assets were **deleted**. New `ShapeRecognizer` + `TransformRecognizer`
assets are authored fresh in M3 (Phase 4) against the poses above, with hysteresis and
the ii/I ambiguity rule (§3.4).

---

## ADR-0005 — Right-hand melody input is mid-air touch targets, not finger curl

**Date:** 2026-09-01 · **Status:** Accepted · **Milestone:** M4

The prototype (`Assets/Scripts/Hands/RightHandToneDetector.cs`) triggered the five
melody tones by **individual finger curl** (index → root, middle → 3rd, …). This is the
piano-mimicking / individual-finger-bend interaction that the co-design sessions
explicitly **rejected** (§1.2, §1.3 `[THESIS]`: "co-design rejected individual finger
bends as unergonomic and semantically empty").

**Decision:** the right hand uses mid-air touch targets entered by a fingertip
(§1.3, §3.3). **Ten** targets (5 chord tones × 2 octaves, §3.1), ordered by scale
degree and **re-pitched, never re-arranged** on chord change. The curl approach and the
prototype implementing it are discarded.

---

## ADR-0004 — Unity version pinned at 6000.5.0f1 (Tech stream)

**Date:** 2026-09-01 · **Status:** Accepted

`ProjectSettings/ProjectVersion.txt` is `6000.5.0f1`. This is a Unity 6 **Tech-stream**
release, not a `6000.0.x` LTS. The student confirmed this is intentional and the editor
will not be upgraded for the duration of the study. `CLAUDE.md` §2.4 / §4.1 updated to
name the exact version. The "do not upgrade" rule stands regardless of stream.

**Thesis impact:** if Chapter 6 says "Unity LTS", change it to name `6000.5.0f1`.

---

## ADR-0003 — Meta XR SDK pinned at 205.0.0 / audio 85.0.0

**Date:** 2026-09-01 · **Status:** Accepted

Installed and pinned in `Packages/manifest.json`:

| Package | Version |
|---|---|
| `com.meta.xr.sdk.all`, `.core`, `.interaction`, `.interaction.ovr`, `.platform`, `.haptics`, `.mrutilitykit` | `205.0.0` |
| `com.meta.xr.sdk.audio` | `85.0.0` |
| `com.meta.xr.sdk.voice` | `85.0.1` |

Resolves the `[VERIFY]` in `CLAUDE.md` §4.1. Do not bump without re-running the
recogniser fixture tests (§2.6).

---

## ADR-0002 — Domain logic tested headless via a parallel .NET SDK-style build

**Date:** 2026-09-01 · **Status:** Accepted · **Milestone:** M0

§2.1 requires `Jazztures.Core` to compile and unit-test without Unity, on the desktop
CLR. Implementation:

- `Assets/Jazztures/Core/` carries `Jazztures.Core.asmdef` (`noEngineReferences: true`)
  for Unity's compile/test path.
- `DotNet/Jazztures.Core/Jazztures.Core.csproj` (SDK-style, `netstandard2.1`) compiles
  **the same `.cs` files** via a linked glob.
- `DotNet/Jazztures.Core.Tests/` (`net8.0`, NUnit 3) compiles the same test `.cs` files
  that live in `Assets/Tests/EditMode/`, so one test source runs in both Unity's Test
  Runner and `dotnet test`.
- CI / local headless loop: `dotnet test DotNet/Jazztures.sln`.

**Prerequisite:** the .NET SDK (8.0+) must be installed on the dev machine. The machine
as of 2026-09-01 has the .NET **runtime** and VS 2026 MSBuild but **no SDK** — install
`Microsoft.DotNet.SDK.8` before Phase 0 can be marked done.

**Thesis impact:** none — this is a testing-infrastructure choice, invisible to the
methodology.

---

## ADR-0001 — Prototype (`Assets/Scripts/`) deleted

**Date:** 2026-09-01 · **Status:** Accepted · **Milestone:** M0

The initial prototype (`Assets/Scripts/Audio/*`, `Assets/Scripts/Hands/*`) predated the
architecture in `CLAUDE.md` and violated it structurally: no `Jazztures.Core` assembly,
all `MonoBehaviour`, `FindObjectOfType`, magic-number voicings, custom oscillator synth,
no `IMusicalClock`, no `INoteSink` fan-out, no telemetry. It also encoded two
thesis-rejected interactions (see ADR-0005, ADR-0006).

**Decision:** delete it wholesale and rebuild against the ports-and-adapters
architecture (§2.2). Salvaged as reference only, recorded here:

*Prototype left-hand chord voicings (MIDI), root/3/5/7/9 — NOT authoritative, the
`Core/Music/Voicing.cs` algorithm output must be pilot-calibrated (§7 open item
"Chord voicing register"):*

| Function | Chord | MIDI notes |
|---|---|---|
| ii | Dm7   | 50, 53, 57, 60, 64 (D3 F3 A3 C4 E4) |
| V  | G7    | 55, 59, 62, 65, 69 (G3 B3 D4 F4 A4) |
| I  | Cmaj7 | 48, 52, 55, 59, 62 (C3 E3 G3 B3 D4) |

Also deleted: `Assets/audioengine.unity` (the prototype demo scene — every component on
it lost its script). `Assets/Scenes/SampleScene.unity` is kept: it is the untouched URP
template scene and carries no prototype components. A purpose-built debug scene arrives
in M2 (Phase 3).

---

## Known thesis-prose changes still pending (tracked, not yet actioned)

Working rule #8 requires these to be surfaced. They are **not** to be edited by an agent
(rule #7) — the student edits the paper.

- **RtMidi (Chapter 6):** no ARM64 Unity binding. MIDI is the event/serialisation model,
  not a runtime library. See `CLAUDE.md` §4.2.
- **Sibelius `.sib` pipeline (Chapter 6, §3.9):** proprietary binary, no Unity reader.
  Pending `[OPEN]` resolution — likely MusicXML/SMF offline bake. Blocks M6.
- **Unity "LTS" wording:** see ADR-0004.
- **`PokeInteractor` for touch targets (§4.1):** targets are volumetric spheres tested
  against the tracked fingertip; Poke is a surface interaction and does not fit. See
  ADR-0016. §4.1 table wording only — no Chapter 6 impact.
- **Right-hand lessons keep the chords audible (§3.8 mode table, Chapter 6 lesson
  descriptions):** in Try Yourself and Test Yourself a right-hand lesson plays the harmony
  but no melody demonstration — student decision 2026-10-08, ADR-0043 (decision 3).
  Matches §3.9 and the proposal's L3 description ("the system plays chords"); the §3.8
  table's "System plays: no" needs a footnote.
