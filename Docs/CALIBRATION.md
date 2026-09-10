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

The palm cone is enforced by `Assets/Jazztures/Config/GesturePalmConeThresholds.asset`
(a `TransformFeatureStateThresholds`), pointed at from each ii / I
`TransformRecognizerActiveState.TransformConfig.FeatureThresholds`, `UpVectorType = Head`.
`PalmDown` is set to midpoint 42.5° / width 15° → enter 35°, exit 50°.
**ii = `OpenPalm` + `FingersUp`; I = `OpenPalm` + `PalmDown`** (ADR-0014 — the SDK has no
lateral axis, so ii is recognised by hand verticality, not palm azimuth). `FingersUp` is
at the SDK default (30°/50°); widen it if ii is hard to trigger with fingers angled
forward.

| Parameter | Default | Measured | Status | Consumed by |
|---|---|---|---|---|
| Finger "extended" curl | < 0.25 | — | default | SDK ShapeRecognizer (`Poses/OpenPalm.asset`, `Poses/Fist.asset`) |
| Finger "curled" curl | > 0.75 | — | default | SDK ShapeRecognizer (`Poses/Fist.asset`) |
| Palm-down cone — enter (I pose) | 35° | — | default | `GesturePalmConeThresholds.asset` → SDK TransformRecognizer (midpoint 42.5 − width/2) |
| Palm-down cone — exit (I pose) | 50° | — | default | `GesturePalmConeThresholds.asset` → SDK TransformRecognizer (midpoint 42.5 + width/2; wider — Schmitt) |
| Fingers-up cone — enter/exit (ii pose) | 30° / 50° | — | default | `GesturePalmConeThresholds.asset` feature `FingersUp` (SDK default; ADR-0014) |
| Pose hold to confirm | 120 ms | — | default | `GestureInterpreter` (also the latency-budget lever, §4.3) |
| Minimum inter-chord interval | 100 ms | — | default | `GestureInterpreter` (debounce) |
| Consecutive confirming frames | 3 | — | default | `GestureInterpreter` (~60 Hz hand update) |
| High-confidence frames to accept after tracking loss | 3 | — | default | `GestureInterpreter` (§3.5) |
| Tracking-loss cue delay | 200 ms | — | default | `GestureInterpreter` → presentation (§3.5.2) |

## Melody / touch targets — `Config/MelodyConfig.asset` (§3.3, §3.1, ADR-0015/0019)

Asset: `Assets/Jazztures/Config/MelodyConfig.asset` (`Jazztures/Config/Melody`). The
geometry + pivot group is read live by the touch-target rig. The melody-engine
group **mirrors** the `[TUNABLE]` constants still in `Jazztures.Core.Melody`
(`MelodyEngine`, `VelocityCurve`); Core consumes its own constants until Phase 8 (M8)
wires this asset to the parameterised overloads — keep the two in sync by hand.

The ten targets sit on an **arc centred on the right shoulder** (ADR-0019): five
scale-degree columns swept about the pivot at a constant reach, two octave rows, each
target facing radially outward. Every target is the same distance from the shoulder, so a
glissando is one shoulder rotation and the outer degrees are no harder to reach than the
centre. The pivot is **body-anchored with a lazy recenter** (ADR-0015): head position +
flattened yaw, offset down and right to the real shoulder; recenters toward the current
facing only after the yaw diverges past `_recenterAngleDegrees` for `_recenterDwellSeconds`,
easing over `_recenterEaseSeconds`. World-locked and head-locked were rejected (spatial-map
stability, §3.1; comfort).

| Parameter | Default | Measured | Status | Notes |
|---|---|---|---|---|
| Target face radius | 4 cm | — | default | circular face; selects scale degree + octave. Up from 3.5 cm — the wider arc leaves room (ADR-0019) |
| Target depth | 15 cm total (±7.5 cm) | — | default | cylinder along the radial approach axis; depth is unaimable in mid-air VR (ADR-0018). Kept — this is what makes the glissando slide work |
| Column angle | 16° at the pivot | — | default | → 12.5 cm chord spacing, **4.5 cm gap** between neighbours (was 1 cm). Tune up to 18–20° if isolating one target is still fiddly (ADR-0019) |
| Row spacing | 14 cm | — | default | vertical octave separation; 6 cm gap. No reach cost so it can be generous |
| Hover glow scale | 1.6× the trigger volume | — | default | a nearing fingertip lights the target before the note fires (ADR-0018) |
| Speed sample window | 3 frames | — | default | binder fires on peak recent speed, so a decelerating approach still registers (ADR-0018) |
| Arc reach (radius) | 0.45 m | — | default | shoulder pivot to every target; the arc radius, not a forward offset |
| Shoulder height offset | −0.25 m from head | — | default | down toward the real shoulder |
| Shoulder lateral offset | +0.08 m (right) | — | default | small rightward bias (arc centre ~10°, right edge ~+40°). Pulled in from +0.20 m: on Quest 2 the hand-tracking cone is narrower than §1.4's ~140°, and a big offset made the learner turn their head to see the arc and lose left-hand tracking. ADR-0020 |
| Recenter divergence angle | 35° | — | default | head-yaw offset before a recenter begins |
| Recenter dwell | 0.6 s | — | default | divergence must hold this long first |
| Recenter ease | 0.5 s | — | default | time to ease to the new facing |
| Entry velocity gate | 0.08 m/s | — | default | "was this a deliberate strike?" — distinct from the loudness curve's minimum (ADR-0018); mirrors `MelodyEngine.EntryVelocityGateMetresPerSecond` |
| Velocity-curve min speed | 0.15 m/s | — | default | anchors the quietest struck note; an entry between the gate and this sounds at MinVelocity. `VelocityCurve.MinSpeed` |
| Velocity-curve max speed | 1.5 m/s | — | default | fingertip speed mapped to max MIDI velocity; mirrors `VelocityCurve.MaxSpeed` |
| Per-target retrigger cooldown | 80 ms | — | default | mirrors `MelodyEngine.RetriggerCooldownSeconds` |
| Fixed note sustain | 0.5 s `[OPEN]` | — | default | struck-piano model, no stuck notes; mirrors `MelodyEngine.DefaultSustainSeconds` |
| MIDI velocity range (from fingertip speed) | 40–110, clamp; floor 30 | — | default | never emit < 30 (§3.3); mirrors `VelocityCurve` bounds |

## Tension colour — `Config/TensionPalette.asset` (§3.10, ADR-0017)

Asset: `Assets/Jazztures/Config/TensionPalette.asset` (`Jazztures/Config/Tension Palette`).
Read by `Jazztures.Presentation.TensionColorDriver`, which eases one shared colour on every
chord change and pushes it to the touch targets (base tint) and the left-hand outline.
Colour is a **redundant** channel (§3.10) — the chord is always also audible and spatially
shown. ii is a desaturated yellow-green rather than a cool hue: §3.10 says "cool/neutral for
ii" but the student's palette direction was mustard; sage/olive is the compromise (ADR-0017).

| Parameter | Default (RGBA) | Measured | Status | Notes |
|---|---|---|---|---|
| Neutral — no chord held | 0.58, 0.58, 0.62, 0.35 | — | default | cool grey, low alpha; targets stay visible but read as inactive |
| ii — preparation | 0.53, 0.60, 0.42, 0.80 | — | default | sage/olive; deviates from §3.10 "cool" (ADR-0017) |
| V — peak tension | 0.74, 0.33, 0.18, 0.85 | — | default | burnt sienna; warm, saturated |
| I — resolution | 0.52, 0.36, 0.60, 0.85 | — | default | warm purple; deep, settled |
| Strike flash | 1.00, 0.96, 0.85, 1.00 | — | default | near-white warm; fingertip-trigger flash |
| Highlight boost | 0.35 | — | default | push toward white while a chord-change highlight is active |
| Transition | 0.25 s | — | default | ease time between chord colours (SmoothStep); not a jarring snap |

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

## Lesson pacing — per-lesson `LessonDefinition` asset (§3.9, ADR-0021)

Phrase length is a **pedagogical** parameter, not only a musical one: the same ii-V-I needs
a longer phrase to *practise* than to *demonstrate*. `LessonTimeline.DurationBeats` is
simply the last event's beat, so pacing is set by where the chords are placed.
A pose costs the learner a 120 ms hold plus 3 confirming frames before it registers (§3.4)
— on top of the time to recall and form an unfamiliar shape.

| Parameter | Default | Measured | Status | Notes |
|---|---|---|---|---|
| L1 chord spacing | 4 beats (3.0 s at 80 BPM) | — | default | one chord per bar. Was 2 beats, giving a 3 s phrase — enough to hear, not to play (ADR-0021) |
| L1 phrase length | 20 beats (15 s), ii-V-I twice | — | default | the repeat gives a second attempt without restarting the phase |
| L1 mode phases | GestureLearning → W&L → TryYourself | — | default | deviates from §3.9's "W&L, TY"; §3.8 puts Gesture Learning first for pose fluency (ADR-0021) |
| Phrase tail | 1.0 s | — | default | grace after the last beat before auto-advance. `LessonRunner._phraseTailSeconds` |

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
