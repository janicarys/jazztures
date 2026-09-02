# Calibration Log — Jazztures

Every `[TUNABLE]` value in `CLAUDE.md` starts here as an **engineering default**.
During M8 (pilot calibration, 1–2 pilot users) each row gets a **measured value** and a
one-line note on how it was measured. After the build is frozen at the end of M8, this
file is the record of what the participants actually experienced.

**Rule (from `CLAUDE.md` §0):** a value here is *not* a co-design finding. It must not
appear in the paper as though the co-design sessions produced it. Cite this file, dated,
when a number reaches Chapter 6.

Status: `default` = engineering guess, untested · `pilot` = measured with pilot users ·
`frozen` = locked for the main study

All live values are stored in `ScriptableObject` assets under `Assets/Jazztures/Config/`,
never as literals in a `MonoBehaviour` (§2.4). This file mirrors them for the write-up.

---

## Gesture recognition — `GestureThresholdsConfig` asset (§3.4)

Asset: `Assets/Jazztures/Config/` (`Jazztures/Config/Gesture Thresholds`). Temporal values
feed `Core.Gesture.GestureInterpreter` via `ToThresholds()`; SDK values configure the
`ShapeRecognizer` / `TransformRecognizer` assets.

| Parameter | Default | Measured | Status | Consumed by |
|---|---|---|---|---|
| Finger "extended" curl | < 0.25 | — | default | SDK ShapeRecognizer |
| Finger "curled" curl | > 0.75 | — | default | SDK ShapeRecognizer |
| Palm orientation cone — enter | 35° | — | default | SDK TransformRecognizer |
| Palm orientation cone — exit | 50° | — | default | SDK TransformRecognizer (must stay wider — Schmitt) |
| Pose hold to confirm | 120 ms | — | default | `GestureInterpreter` (also the latency-budget lever, §4.3) |
| Minimum inter-chord interval | 100 ms | — | default | `GestureInterpreter` (debounce) |
| Consecutive confirming frames | 3 | — | default | `GestureInterpreter` (~60 Hz hand update) |
| High-confidence frames to accept after tracking loss | 3 | — | default | `GestureInterpreter` (§3.5) |
| Tracking-loss cue delay | 200 ms | — | default | `GestureInterpreter` → presentation (§3.5.2) |

## Melody / touch targets — `Config/MelodyConfig.asset` (§3.3)

| Parameter | Default | Measured | Status | Notes |
|---|---|---|---|---|
| Target radius | 3.5 cm sphere | — | default | |
| Inter-target spacing | 8 cm centre-to-centre | — | default | > 2×radius + tracking jitter |
| Entry velocity gate | 0.15 m/s | — | default | prevents resting-hand triggers |
| Per-target retrigger cooldown | 80 ms | — | default | |
| Fixed note sustain | `[OPEN]` | — | default | struck-piano model, no stuck notes |
| MIDI velocity range (from fingertip speed) | 40–110, clamp; floor 30 | — | default | never emit < 30 (§3.3) |

## Register assignment — `Config/RegisterConfig.asset` (§3.1)

| Parameter | Default | Measured | Status | Notes |
|---|---|---|---|---|
| Left-hand voicing lowest note | MIDI 48–60, close voicing | — | default | needs a pianist's ear (§7 open item). Code: `Voicing.DefaultRootFloorMidi` / `DefaultRootCeilingMidi` |
| Right-hand lower octave — lowest target | ≥ MIDI 72 | — | default | keeps melody above harmony in spectrum. Code: `ChordToneSet.DefaultLowestTargetFloorMidi` |

Until the Config asset exists (M8, Phase 8), these live as named `const` in
`Assets/Jazztures/Core/Music/`. Phase 8 wires the asset to the parameterised overloads
(`Voicing.Close(chord, floor, ceiling)`, `ChordToneSet.For(chord, floor)`) and these
constants become fallback defaults only.

## Timing — `Config/TimingConfig.asset` / per-lesson (§3.6)

| Parameter | Default | Measured | Status | Notes |
|---|---|---|---|---|
| Default tempo | 80 BPM | — | default | >100 BPM gated behind a config flag (unvalidated). Code: `Tempo.Default` |
| Swing ratio | 0.66 | — | default | per-lesson; L4 introduces swing, L1–3 straight. Code: `SwingRatio.Default` (straight = `SwingRatio.Straight`); warp in `SwingQuantizer` |
| Metronome bar length | 4 beats | — | default | click grid only, not swung. Code: `Metronome.DefaultBeatsPerBar` |

## Onset scoring — `Config/OnsetScoringConfig.asset` (§3.7)

Code: `Core.Evaluation.OnsetWindows` (`DefaultOnTimeSeconds` / `DefaultCloseSeconds` /
`DefaultMatchSeconds`); scoring in `OnsetScorer.Evaluate` → `AttemptResult`.

| Window | Default | Measured | Status |
|---|---|---|---|
| "on time" | ≤ 80 ms | — | default |
| "close" | ≤ 160 ms | — | default |
| "off" | > 160 ms | — | default |
| match gate (beyond → missed + extra, not "off") | 300 ms | — | default |

## Audio — `Config/AudioConfig.asset` (§4.2)

| Parameter | Default | Measured | Status | Notes |
|---|---|---|---|---|
| Sample rate | 48 kHz | — | default | |
| DSP buffer | "Best Latency" | — | default | |
| Voice pool size | ≥ 32 | — | default | never DecompressOnPlay in trigger path. `SamplerNoteSink._voiceCount` |
| Sample load | preloaded + decompressed on load | — | default | set on import; not yet enforced |
| Sampler velocity-layer split | MIDI velocity 64 | — | default | ≥ → Hard layer, < → Soft. `SampleLibrary.DefaultLayerSplitVelocity` |
| Note-off release fade | 80 ms | — | default | avoids a click on the sample tail. `SamplerNoteSink._releaseSeconds` |
| Velocity → gain trim | lerp 0.35→1.0 over vel 0→127, × master | — | default | layers carry the big dynamics; this is a trim. `SamplerNoteSink` |
| Master gain | 0.5 | — | default | `SamplerNoteSink._masterGain` |
| Keyboard debug entry speed | 0.8 m/s | — | debug-only | `KeyboardPerformanceDriver.KeyedEntrySpeed` — not a study parameter |

## Feedback — `Config/TensionColorConfig.asset` (§3.10)

| Parameter | Default | Measured | Status | Notes |
|---|---|---|---|---|
| ii colour | cool / neutral | — | default | redundant channel only, never sole carrier |
| V colour | warm / saturated | — | default | |
| I colour | resolved / settled | — | default | |

## Tracking-loss policy — `Config/TrackingLossConfig.asset` (§3.5)

| Parameter | Default | Measured | Status |
|---|---|---|---|
| Loss duration before visual cue | 200 ms | — | default |

---

## Latency budget measurements (§4.3) — filled by `Diagnostics/LatencyProbe`

Report percentiles (p50 / p95 / p99), not single numbers. **These go in the thesis.**

| Segment | Target | p50 | p95 | p99 | Measured on |
|---|---|---|---|---|---|
| Hand tracking → pose available | ~30–50 ms (platform) | — | — | — | — |
| Pose confirmation (hold + frames) | ≤ 120 ms | — | — | — | — |
| Domain processing | < 2 ms | — | — | — | — |
| Note event → audible | < 20 ms | — | — | — | — |
| **End-to-end (gesture → sound)** | — | — | — | — | — |
