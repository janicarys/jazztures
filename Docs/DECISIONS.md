# Architecture Decision Record — Jazztures

Every `[OPEN]` resolution and every deviation from the thesis prose is logged here.
Format: newest first. Each entry states the decision, the context, and — where relevant —
what must change in the thesis text (Chapter 6 in particular).

Status legend: **Accepted** · **Superseded** · **Proposed**

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
