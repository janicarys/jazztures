# CLAUDE.md — Jazztures

Guidance for automated coding agents working in this repository.

**Project:** Jazztures — a mid-air gesture VR system for teaching jazz piano improvisation concepts to novices.
**Institution:** De La Salle University Manila, College of Computer Studies (undergraduate thesis).
**Platform:** Unity → Meta Quest (Android, ARM64).

---

## 0. How to read this file

Every specification below is tagged:

| Tag | Meaning | Agent behaviour |
|---|---|---|
| `[THESIS]` | Stated in the approved thesis proposal or derived from co-design findings. | **Do not change.** If code conflicts with a `[THESIS]` rule, the code is wrong. If you believe the thesis is wrong, stop and raise it — do not silently deviate. |
| `[TUNABLE]` | An engineering default chosen for implementation. Not a research finding. | Change freely with justification. Must live in a `ScriptableObject` or config asset, never as a magic number in a `MonoBehaviour`. |
| `[OPEN]` | Unresolved. The thesis does not specify this. | Do not invent a permanent answer. Implement the simplest thing that works, mark it `// TODO(OPEN):`, and surface it in your summary. |

**Critical rule for an academic repository:** `[TUNABLE]` values are *not* findings. Never write them into the paper, a commit message, or a docstring as though the co-design sessions produced them. If a number in this file has no `[THESIS]` tag, it came from an engineer's judgment and must be pilot-calibrated before it appears in Chapter 6.

---

## 1. Thesis objectives and system constraints

### 1.1 Research objectives `[THESIS]`

1. Co-design a set of mid-air gestures for teaching jazz piano improvisation concepts, through dialogue with experienced pianists. **(Complete — see §1.3.)**
2. Develop a VR training system that uses those gestures to teach jazz improvisation concepts to novices. **(This repository.)**
3. Evaluate the system's effectiveness with novices.

### 1.2 What the system claims — and does not claim `[THESIS]`

This determines what is worth building and what is scope creep.

**Claims to teach:** harmonic function (preparation → tension → resolution), timing and swing, chord-tone-constrained melodic choice, motif construction and variation, real-time improvisational decision-making.

**Explicitly does not claim:** transfer to physical piano, finger technique, note-reading, or any genre other than jazz.

**Implication for the agent:** any feature that improves *keyboard realism* at the cost of *gestural fluidity* is a regression. The co-design sessions rejected the piano-mimicking gesture set precisely because novices could not read the metaphor. Do not "improve" the interface toward a virtual keyboard.

### 1.3 Interaction model `[THESIS]`

Cognitive split across hands — this mirrors the harmony/melody division that experienced improvisers internalise, and it is the system's central design claim.

**Left hand — harmony.** Three static poses, one per chord function in a ii-V-I. Broad, fluid, ergonomic motions (co-design rejected individual finger bends as unergonomic and semantically empty).

| Function | Gesture | Metaphor |
|---|---|---|
| **ii** | Open palm, facing to the user's right | Preparation |
| **V** | Fist | Peak of tension |
| **I** | Open palm, facing down | Release / resolution |

**Right hand — melody.** Touch targets in mid-air, dynamically restricted to the chord tones of whatever chord the left hand is currently holding. No algorithmic note generation — the co-design sessions rejected it as diminishing the learner's role in constructing melodies. The learner chooses every note; the system only removes the wrong ones.

**Single key: C major.** `[THESIS]` The earlier three-lane / three-root-note design (C, B♭, A♭) was **cut** after co-design feedback that it added novice complexity and made the system feel like a rhythm game. Do not reintroduce multi-key support, lanes, or root-note switching. If you find scaffolding for it in the codebase, it is dead code from an earlier design — flag it, do not extend it.

### 1.4 Hardware and physical constraints `[THESIS]`

- User **stands**. Interaction happens directly in front of them, inside the Quest's ~140° tracking FOV.
- Camera-based optical tracking. Occlusion is the known failure mode: fingers overlapping, one hand crossing the other, hands leaving the FOV.
- Tracking reliability is an acknowledged limitation of the study. **The system must degrade gracefully, not glitch.** A dropped frame must never produce a spurious chord change or a stuck note — see §3.5.

### 1.5 Pedagogical constraints `[THESIS]`

- Target user: **novice** — no piano experience, or has not passed ABRSM Grade 1. Assume zero music theory vocabulary. Any on-screen text that says "dominant seventh" without explanation is a bug.
- Cognitive Load Theory governs the design. Every feature must justify itself against extraneous load. When in doubt, remove the affordance.
- Progressive disclosure is mandatory: harmonic vocabulary stays frozen while a new dimension (timing, melody, swing) is introduced.

---

## 2. Architecture rules

### 2.1 Non-negotiable structural rule

> **The musical domain is pure C#. It does not reference `UnityEngine`.**

`Jazztures.Core` compiles without Unity and is unit-tested on the desktop CLR. Chords, scales, voicings, progression state, lesson state machines, timing evaluation, and scoring all live there. If a domain type needs `Vector3`, define your own struct or restate the problem.

Rationale: this is a thesis on a deadline with a user study booked. Domain logic that can only be tested by putting on a headset will not get tested.

### 2.2 Ports and adapters

`Core` defines interfaces; Unity assemblies implement them.

```
IHandPoseSource      → MetaXRHandPoseSource      (Interaction SDK)
                     → ReplayHandPoseSource      (recorded session playback)
                     → FakeHandPoseSource        (edit-mode tests)
INoteSink            → SamplerNoteSink           (on-device audio)
                     → OscNoteSink               (DAW recording)
                     → TelemetryNoteSink         (JSONL log)
IMusicalClock        → DspMusicalClock           (AudioSettings.dspTime)
                     → VirtualClock              (deterministic tests)
```

`INoteSink` is fanned out through a composite. **Every note event goes to all three sinks.** This is what makes the study reproducible: audio, DAW capture, and analysis log are guaranteed to describe the same performance.

### 2.3 Event-driven composition

Use **ScriptableObject event channels** for cross-system communication. No `FindObjectOfType`. No singletons except one `CompositionRoot` per scene that wires dependencies in `Awake()`.

Inside `Core`, use plain C# events on domain services — not ScriptableObjects (see §2.1).

Canonical channels:

```
ChordChangedChannel        (ChordFunction, ChordVoicing, timestamp)
NoteTriggeredChannel       (NoteEvent)
GestureStateChannel        (Handedness, GesturePhase, confidence)
TrackingQualityChannel     (Handedness, TrackingQuality)
LessonPhaseChannel         (LessonId, LearningMode)
EvaluationResultChannel    (attempt scoring, Test-Yourself only)
```

**Direction rule:** input → domain → presentation. Presentation subscribes; it never writes to domain state. If a UI script calls `HarmonyEngine.SetChord()`, that is a bug.

### 2.4 C# conventions

- Target the Unity version pinned in `ProjectSettings/ProjectVersion.txt`: **`6000.5.0f1`** (Unity 6 Tech stream, not a `6000.0` LTS — confirmed by the student on 2026-09-01; see `Docs/DECISIONS.md`). Do not upgrade the editor.
- `#nullable enable` in `Jazztures.Core`. Elsewhere, encouraged.
- Domain values are `readonly record struct` where possible. `Chord`, `NoteEvent`, `Pitch`, `Beat` are immutable.
- **No allocations in the per-frame audio/gesture path.** No LINQ, no `foreach` over interfaces, no string interpolation in `Update`. Pool `NoteEvent` buffers. This is a real-time musical instrument; a GC spike is an audible artefact.
- Musical time is `double` seconds on the DSP clock, never `Time.deltaTime`. Anything rhythmic that reads `Time.time` is a bug.
- One public type per file, filename matches.
- Assembly definitions everywhere. Circular dependencies fail the build, by design.

### 2.5 Folder structure

```
Assets/
  Jazztures/
    Core/                        # asmdef, NO UnityEngine reference
      Music/                     # Pitch, Chord, Voicing, Scale, Interval
      Harmony/                   # HarmonyEngine, ProgressionState
      Melody/                    # MelodyEngine, ChordToneSet
      Timing/                    # MusicalClock, Metronome, SwingQuantizer
      Lessons/                   # LessonDefinition, LessonStateMachine, LearningMode
      Evaluation/                # OnsetScorer, AttemptResult
      Ports/                     # IHandPoseSource, INoteSink, IMusicalClock, ITelemetrySink
    Input/                       # Meta XR adapters, GestureRecognizer, ConfidenceGate
    Audio/                       # Sampler, voice pool, piano SoundFont/samples
    Networking/                  # OSC client, MIDI serialisation
    Presentation/                # GhostHands, TouchTargets, TensionColorDriver, HUD
    Lessons/                     # ScriptableObject lesson assets (1-8)
    Config/                      # ScriptableObject tuning assets ([TUNABLE] lives here)
    Diagnostics/                 # telemetry writer, latency probe, in-headset debug HUD
    Scenes/
  Tests/
    EditMode/                    # Core domain tests — must pass without a headset
    PlayMode/                    # adapter + integration tests
Docs/
  DECISIONS.md                   # ADR log; every [OPEN] resolution goes here
  CALIBRATION.md                 # pilot-measured values that replace [TUNABLE] defaults
```

### 2.6 Testing expectations

Edit-mode tests are mandatory for: chord/voicing generation, chord-tone set derivation, progression state transitions, onset scoring, swing quantisation, lesson state machine.

Play-mode tests for: gesture recogniser against recorded hand-pose fixtures, note-sink fan-out, OSC serialisation round-trip.

Record real hand-tracking sessions to fixture files early and test the recogniser against them. Do not iterate on gesture thresholds by repeatedly donning the headset — it is slow and it will exhaust you before the user study.

---

## 3. Domain logic specification

### 3.1 Pitch and chord model

MIDI note numbers. Middle C = C4 = 60.

**The three chords** `[THESIS]` — ii-V-I in C major:

| Function | Chord | Pitch classes (root, 3rd, 5th, 7th) |
|---|---|---|
| ii | Dm7 | D, F, A, C |
| V | G7 | G, B, D, F |
| I | Cmaj7 | C, E, G, B |

**Right-hand chord tones** `[THESIS]` — root, 3rd, 5th, 7th, 9th, spanning **two octaves**:

| Chord | Root | 3rd | 5th | 7th | 9th |
|---|---|---|---|---|---|
| Dm7 | D | F | A | C | E |
| G7 | G | B | D | F | A |
| Cmaj7 | C | E | G | B | D |

Ten touch targets total (5 tones × 2 octaves). Targets are **re-pitched, never re-arranged** on chord change — target #3 is always "the fifth", so the learner builds a stable spatial map of scale degree rather than of absolute pitch. This is the whole point; do not sort targets by pitch.

**Register assignment** `[TUNABLE]`:
- Left-hand chord voicing: close voicing, lowest note in MIDI 48–60.
- Right-hand lower octave: lowest target ≥ MIDI 72.
- Rationale: separates harmony and melody in the frequency spectrum so a novice can hear their own melodic line. Voicing algorithm lives in `Core/Music/Voicing.cs` and is unit-tested.

### 3.2 Harmony engine

State: `ChordFunction? Active` — nullable, because "no gesture held" is a legal state.

Transitions are gesture-driven and **unordered**. `[THESIS]` The system teaches the ii-V-I as a *functional* relationship, not as a fixed sequence to be executed. The learner may play I → V → ii. The lesson layer may *prompt* an order; the harmony engine must never *enforce* one.

On chord change: emit note-off for the outgoing voicing, note-on for the incoming, publish `ChordChangedChannel`. The melody engine recomputes its chord-tone set from that event and from nothing else.

### 3.3 Melody engine

`ActiveChordToneSet` is derived state, recomputed on every `ChordChanged`. Never cached across chord changes.

Note-on fires when a right-hand fingertip enters a target volume. Note-off after a fixed sustain — this is a plucked/struck piano model, not a sustained one, so a stuck note is not possible by construction.

`[TUNABLE]` defaults:
- Target radius: 3.5 cm sphere.
- Inter-target spacing: 8 cm centre-to-centre (must exceed 2× radius plus tracking jitter, or targets bleed).
- Entry velocity gate: 0.15 m/s minimum — prevents a resting hand from firing notes.
- Per-target retrigger cooldown: 80 ms.
- MIDI velocity from fingertip speed, mapped to 40–110 and clamped. Never emit velocity < 30; a novice's timid gesture producing near-silence reads as a system failure, not as expression.

### 3.4 Gesture recognition

Built on Meta XR Interaction SDK pose detection (`ShapeRecognizer` for finger curl, `TransformRecognizer` for palm orientation), composed through `ActiveStateGroup`. **Do not hand-roll joint-angle math.** The SDK's recognisers are already tuned against Meta's hand model; a custom classifier is a research project you do not have time for and cannot defend in a viva.

`[TUNABLE]` — all of the following belong in `Config/GestureThresholds.asset`:

| Parameter | Default | Note |
|---|---|---|
| Finger "extended" curl | < 0.25 | ShapeRecognizer normalised curl |
| Finger "curled" curl | > 0.75 | |
| Palm orientation cone (enter) | 35° | angle between palm normal and target axis |
| Palm orientation cone (exit) | 50° | **must be wider than enter** |
| Pose hold to confirm | 120 ms | |
| Minimum inter-chord interval | 100 ms | debounce |
| Consecutive confirming frames | 3 | at ~60 Hz hand update |

**Hysteresis is mandatory.** Enter and exit thresholds must differ (Schmitt trigger). Symmetric thresholds produce chord flicker at the boundary, which sounds like the system is broken and will contaminate the NASA-TLX frustration subscale.

**Ambiguity resolution:** ii (palm right) and I (palm down) share a hand shape and differ only in orientation. If both cones match, hold the previous state and emit nothing. Never guess. Silence is a recoverable error; a wrong chord is not.

Gesture axes are defined **relative to the head/body forward vector**, not world space. The user turns; the gestures must not break.

### 3.5 Tracking loss and occlusion `[THESIS]` (constraint) / `[TUNABLE]` (policy)

Policy on `TrackingQuality` dropping to Low or hand lost:

1. **Sustain, do not release.** Hold the current chord. Releasing on a dropped frame produces an audible stutter that the user will attribute to their own error.
2. After 200 ms of continuous loss, show a non-modal visual cue (desaturate the hand's UI region). No text, no popup, no modal — never interrupt a musical phrase with a dialog.
3. Suppress all gesture *transitions* while confidence is Low. Only accept a new chord after 3 consecutive High-confidence frames.
4. Log every tracking-loss interval with duration and timestamp to telemetry. **This data is a thesis result** — it quantifies the occlusion limitation acknowledged in Chapter 1 and gives Chapter 7 something empirical to say about it. Do not treat the log as debug output to be stripped.

### 3.6 Timing, metronome, swing

Everything rhythmic reads `IMusicalClock`, backed by `AudioSettings.dspTime`. Never `Update()` ordering, never coroutines for beat scheduling.

- Default tempo `[TUNABLE]`: 80 BPM. Literature in this space evaluates open-air tracking stability at tempos up to ~70 BPM; treat anything above ~100 BPM as unvalidated and gate it behind a config flag.
- Swing ratio `[TUNABLE]`: 0.66 (approx. 2:1 eighth-note ratio), configurable per lesson. Lesson 4 introduces swing; Lessons 1–3 are straight.
- Metronome audio is scheduled ahead on the DSP timeline via `PlayScheduled`, never triggered from a frame callback.

### 3.7 Onset scoring (Test Yourself mode) `[TUNABLE]`

Deviation of user onset from expected beat position:

| Window | Verdict |
|---|---|
| ≤ 80 ms | on time |
| ≤ 160 ms | close |
| > 160 ms | off |

Report as aggregate feedback **after** an attempt completes. Never interrupt a phrase in progress with a correctness judgment — Lesson 2 trains pulse, and real-time error popups destroy it.

### 3.8 Learning modes `[THESIS]`

Four modes from ImproVisAR, plus one Jazztures addition.

| Mode | System plays | Ghost hands | User input audible | Guidance |
|---|---|---|---|---|
| **Gesture Learning** *(Jazztures addition)* | no | yes | yes | Pose fluency only, no musical target. Precedes Watch-and-Listen. Rationale: builds a movement lexicon before musical demand is added, compensating for the absence of haptic feedback. |
| **Watch and Listen** | yes | yes | logged, not sounded | Full demonstration |
| **Try Yourself** | no | yes | **only on correct gesture** | Ghost shows target; audio is the reward for matching it |
| **Test Yourself** | no | no | yes | Feedback deferred to end of attempt |
| **Compose on the Fly** | backing only | no | yes | Free improvisation |

Implement as an explicit state machine in `Core/Lessons`. The mode gates the note sink, **not** the gesture recogniser — recognition always runs, and every attempt is always logged to telemetry even when it is not sounded. Silent-but-logged is a first-class state.

### 3.9 Lesson content `[THESIS]`

| # | Lesson | Hand | Objective | Modes |
|---|---|---|---|---|
| 1 | ii-V-I chords | Left | Gesture→chord mapping; harmonic function | W&L, TY |
| 2 | Timing | Left | Steady pulse with metronome; frozen harmony | W&L, TY, TestY |
| 3 | Chord tones | Right | Root/3/5/7/9 over system-played changes | W&L, TY |
| 4 | Swing | Right | Swung phrasing; C Ionian | W&L, TY |
| 5 | Sequences | Right | Note sequences as melodic units | W&L, TY |
| 6 | Motifs | Both | Repeat motifs; rhythmic patterns | W&L, TY |
| 7 | Variations | Both | Develop motifs via pitch/rhythm change | W&L, **CotF** |
| 8 | Question-and-answer | Both | System plays 2 bars, user answers 2 bars | W&L, **CotF** |

Session distribution `[THESIS]`: S1 = L1+L2+L3 · S2 = L4+L5 · S3 = L6 · S4 = L7 · S5 = L8. Five sessions, each ≤ 2 hours.

**Session 1 must not be left-hand-only.** Co-design flagged boredom and lack of creative expression as a real risk if day one is three gestures and nothing else. This is why chord tones land in Session 1 despite being Lesson 3.

Lessons are `ScriptableObject` assets — data, not code. Adding Lesson 9 must require no C# changes. Each asset holds: id, title, novice-facing concept explanation (co-design asked for plain-language theory alongside exercises), permitted modes, tempo, swing ratio, active hands, target phrase, success criteria.

`[OPEN]` **Content pipeline.** The proposal states that lesson material loads from Sibelius files. `.sib` is a proprietary binary format with no Unity reader — this is not implementable as written. Recommended resolution: export the ImproVisAR material to **MusicXML or Standard MIDI File** as an offline authoring step, and ship a small importer that bakes MusicXML/SMF into the lesson `ScriptableObject`s at edit time. Runtime should load baked assets only — never parse notation on-device. Record whichever path is chosen in `Docs/DECISIONS.md` and update the thesis text to match, since the current wording describes a pipeline the system will not have.

### 3.10 Feedback loops

**Auditory (primary).** Immediate, unconditional in free modes. This is the action-perception loop the whole design rests on.

**Visual (secondary, CTML dual-channel).** Ghost hands for gesture guidance. Touch targets that light on chord change. Tension colour-coding across the ii → V → I arc `[TUNABLE]`: cool/neutral for ii, warm/saturated for V, resolved/settled for I. Colour is a redundant channel reinforcing the tension-release metaphor — it must never be the sole carrier of information.

**Visual restraint is a design requirement, not a preference.** VR gives total control of the visual field; the thesis argues this is used to *eliminate* visual noise. Every added HUD element spends limited-capacity budget. Justify additions against Chapter 4 or drop them.

**No haptics.** Hand tracking is controller-free by design. Do not add controller fallback with vibration — the absence of haptic feedback is precisely what Gesture Learning mode exists to compensate for.

---

## 4. Technical dependencies

### 4.1 Stack

| Layer | Choice | Notes |
|---|---|---|
| Engine | Unity **`6000.5.0f1`**, pinned in `ProjectVersion.txt` | Unity 6 Tech stream. Do not upgrade. |
| XR | **Meta XR All-in-One SDK** — installed version **`205.0.0`** (`com.meta.xr.sdk.all` / `.core` / `.interaction` / `.interaction.ovr` / `.platform` / `.haptics` / `.mrutilitykit`); `com.meta.xr.sdk.audio` **`85.0.0`**, `com.meta.xr.sdk.voice` **`85.0.1`**. Pinned in `Packages/manifest.json`. Meta ships breaking changes between minor versions — do not bump without re-testing the recognisers. |
| Backend | OpenXR, Vulkan, IL2CPP, ARM64 | |
| Target | Meta Quest 3 / 3S `[OPEN]` — confirm lab hardware | |
| Hand input | Interaction SDK pose detection + `PokeInteractor` for touch targets | |
| Audio | On-device sampler (Unity `AudioSource` voice pool, or FMOD) | See §4.3 |
| Transport | OSC over Wi-Fi → DAW on a separate machine `[THESIS]` | |

**Project settings:** OVRManager hand-tracking support = **Hands Only**. Manifest permission `com.oculus.permission.HAND_TRACKING`. Hand-tracking frequency = High.

**Data-handling note:** Meta's terms permit hand-pose and hand-size data to be used only for enabling hand tracking in the app. Telemetry may log gesture *classifications*, timestamps, tracking-quality flags, and note events. Do **not** persist raw joint transforms off-device. This also keeps the repo aligned with the DLSU ethics approval and RA 10173.

### 4.2 Audio and MIDI — read this before writing any audio code

The proposal specifies RtMidi for MIDI message creation. **RtMidi is a native C/C++ library and there is no maintained Android/ARM64 Unity binding.** Do not spend days trying to build one.

Correct architecture:

1. `Core` emits `NoteEvent` — a pure C# record (pitch, velocity, dspTime, channel, source hand). This is the single source of truth.
2. `SamplerNoteSink` plays it locally through a pooled sampler. **On-device audio never touches MIDI.** MIDI is a wire format, not an audio path; routing local sound through a MIDI abstraction adds latency for zero benefit.
3. `OscNoteSink` serialises the same `NoteEvent` into MIDI-shaped OSC messages for the DAW.
4. `TelemetryNoteSink` appends to JSONL.

This preserves everything the thesis architecture requires — separation of low-latency local audio from recording — while dropping a dependency that will not build. Update the Chapter 6 wording to describe MIDI as the event/serialisation model rather than as a runtime library.

`[TUNABLE]` audio config: 48 kHz, DSP buffer "Best Latency", voice pool ≥ 32, samples preloaded and decompressed on load (never `DecompressOnPlay` in the trigger path).

### 4.3 Latency budget

Non-negotiable for a musical instrument. Measure end-to-end; do not estimate.

| Segment | Target |
|---|---|
| Hand tracking → pose available | platform-bound, ~30–50 ms |
| Pose confirmation (hold + frames) | ≤ 120 ms `[TUNABLE]` |
| Domain processing | < 2 ms |
| Note event → audible | < 20 ms |

The confirmation window is the **only** segment you control. If gesture→sound feels sluggish, reduce hold time and consecutive-frame count before touching anything else — but measure the false-positive rate at each setting, because that trade is the interesting one. Build `Diagnostics/LatencyProbe` in the first milestone: it timestamps at each stage and writes percentiles. **Report these numbers in the thesis.** Latency and jitter are standard technical benchmarks in this literature and you currently have none.

### 4.4 OSC and recording

- OSC is UDP: lossy, jitter-prone, wall-clock-unsynchronised with the headset.
- **The on-device JSONL telemetry log is the analytical ground truth**, not the DAW capture. The DAW gives you audio for qualitative review and participant playback; the log gives you timestamps you can defend.
- Send a session-start sync message with both DSP and wall-clock time so DAW audio can be aligned to the log post hoc.
- Wi-Fi failure must degrade silently. If OSC send fails, log it and continue — the user must never learn that the network dropped. Their session continues; your recording layer is your problem, not theirs.

---

## 5. Implementation milestones

Ordered so that each milestone produces something demonstrable to an adviser and de-risks the largest unknown remaining.

**M0 — Skeleton.**
Unity project, Meta XR SDK, asmdef graph, `Jazztures.Core` with zero Unity references, CI running edit-mode tests. Hands render, nothing sounds.
*Done when:* `dotnet test` on Core passes outside Unity.

**M1 — Domain (headless).**
Pitch/Chord/Voicing, the three chords, chord-tone derivation, `HarmonyEngine`, `MelodyEngine`, `IMusicalClock`. Full edit-mode coverage. No scene work at all.
*Done when:* a test drives a scripted gesture sequence and asserts the exact resulting note stream.

**M2 — Sound.**
`SamplerNoteSink`, voice pool, `DspMusicalClock`, `LatencyProbe`. Drive it from a keyboard-input debug adapter, not from hand tracking.
*Done when:* keypresses produce chords and melody notes with measured latency under budget.

**M3 — Gesture input.**
`MetaXRHandPoseSource`, the three recognisers, hysteresis, confidence gating, tracking-loss policy. Record fixture sessions and build `ReplayHandPoseSource`.
*Done when:* the recogniser is iterated against replayed fixtures, not by re-donning the headset.

**M4 — Right hand.**
Touch targets, poke interaction, dynamic re-pitching on chord change, velocity mapping.
*Done when:* both hands play together and the chord-tone constraint holds under every transition.

**M5 — Modes and lessons.**
Lesson state machine, five learning modes, mode-based sink gating, ghost hands, `ScriptableObject` lesson assets for L1–L3.
*Done when:* Session 1 runs start to finish, unattended.

**M6 — Full curriculum.**
L4–L8, swing, metronome, motif playback, question-and-answer generation, onset scoring.

**M7 — Instrumentation and study readiness.**
Telemetry schema frozen, OSC/DAW round-trip verified, session/participant ID handling, crash recovery mid-session, analysis scripts that ingest JSONL.
*Done when:* a full mock session with a non-participant runs end to end and produces a complete, parseable dataset.

**M8 — Pilot calibration.**
Run 1–2 pilot users. Replace `[TUNABLE]` defaults with measured values in `Docs/CALIBRATION.md`. Freeze the build. **Do not change interaction parameters once real participants start** — mid-study parameter changes invalidate cross-participant comparison, and a panel will ask.

---

## 6. Agent working rules

1. **Read before writing.** Check `Docs/DECISIONS.md` for prior resolutions. Do not re-litigate settled `[OPEN]` items.
2. **One milestone at a time.** Do not scaffold M5 while M2 is unfinished.
3. **`[THESIS]` conflicts stop work.** If a request contradicts a `[THESIS]` rule, say so and ask. Do not implement it and mention it afterward.
4. **`[TUNABLE]` values go in config assets.** A magic number in a `MonoBehaviour` is a review failure.
5. **No new dependencies without asking.** Every package is something the student must justify, install on lab hardware, and defend.
6. **Tests accompany domain code in the same change.** Not "later".
7. **Never write to `/Docs/*.tex` or thesis prose.** Code and paper are edited separately and deliberately.
8. **Surface anything that changes the paper.** If an implementation decision contradicts Chapter 6 (the RtMidi and Sibelius items are two known examples), flag it explicitly. A system that does not match its own methodology chapter is a defence problem, and the fix is cheap now and expensive in April.
9. **Prefer deleting to adding.** The thesis argument is that constraint reduces cognitive load. That applies to the codebase too.

---

## 7. Known open items

| Item | Status |
|---|---|
| Sibelius → Unity content pipeline | `[OPEN]` — see §3.9. Blocks M6. Resolve before M5. |
| RtMidi on ARM64 | Resolved in §4.2. Requires a Chapter 6 wording update. |
| Target Quest model | `[OPEN]` — confirm lab hardware; affects tracking-quality expectations. |
| Ghost hand visual design | Resolved in ADR-0012 — translucent articulated mesh, superimposed on the learner's hands, target spheres light in sequence (no ghost fingertip), continuous pose morphing. |
| Backing track for Compose-on-the-Fly | `[OPEN]` — L7/L8 need accompaniment; source and generation method undefined. |
| Q&A phrase generation (L8) | `[OPEN]` — pre-authored bank vs. generated. Prefer pre-authored; generation is a second thesis. |
| Chord voicing register | `[TUNABLE]` — needs a pianist's ear, not a developer's. |