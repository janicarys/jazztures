# Architecture Decision Record — Jazztures

Every `[OPEN]` resolution and every deviation from the thesis prose is logged here.
Format: newest first. Each entry states the decision, the context, and — where relevant —
what must change in the thesis text (Chapter 6 in particular).

Status legend: **Accepted** · **Superseded** · **Proposed**

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
