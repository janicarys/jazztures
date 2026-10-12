# Calibration Log — Jazztures

Every `[TUNABLE]` value in `CLAUDE.md` starts here as an **engineering default**.
During M8 (pilot calibration, 1–2 pilot users) each row gets a **measured value** and a
one-line note on how it was measured. After the build is frozen at the end of M8, this
file is the record of what the participants actually experienced.

**Rule (from `CLAUDE.md` §0):** a value here is _not_ a co-design finding. It must not
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
**ii = `OpenPalm` + `WristUp`; I = `OpenPalm` + `PalmDown`** (ADR-0014 / ADR-0026 — the SDK
has no lateral axis and no fingers-vs-forward axis either, so ii is recognised by wrist
roll — the thumb points up when the hand is held horizontal, fingers away from the
learner, palm right — not by palm azimuth). `WristUp` is at the SDK default (30°/50°);
widen it if ii is hard to trigger.

| Parameter                                            | Default   | Measured | Status  | Consumed by                                                                                            |
| ---------------------------------------------------- | --------- | -------- | ------- | ------------------------------------------------------------------------------------------------------ |
| Finger "extended" curl                               | < 0.25    | —        | default | SDK ShapeRecognizer (`Poses/OpenPalm.asset`, `Poses/Fist.asset`)                                       |
| Finger "curled" curl                                 | > 0.75    | —        | default | SDK ShapeRecognizer (`Poses/Fist.asset`)                                                               |
| Palm-down cone — enter (I pose)                      | 35°       | —        | default | `GesturePalmConeThresholds.asset` → SDK TransformRecognizer (midpoint 42.5 − width/2)                  |
| Palm-down cone — exit (I pose)                       | 50°       | —        | default | `GesturePalmConeThresholds.asset` → SDK TransformRecognizer (midpoint 42.5 + width/2; wider — Schmitt) |
| Wrist-up cone — enter/exit (ii pose)                 | 25° / 55° | —        | default | `GesturePalmConeThresholds.asset` feature `WristUp` (widened from the SDK default 30°/50° — ADR-0041; was `FingersUp` per ADR-0014, moved to `WristUp` per ADR-0026) |
| Pose hold to confirm                                 | 150 ms    | —        | default | `GestureInterpreter` (also the latency-budget lever, §4.3). Was 120 ms; raised in ADR-0025 now that selection no longer gates musical timing |
| Minimum inter-chord interval                         | 100 ms    | —        | default | `GestureInterpreter` (debounce)                                                                        |
| Consecutive confirming frames                        | 3         | —        | default | `GestureInterpreter` (~60 Hz hand update; need not be consecutive — see miss tolerance below, ADR-0025) |
| Confirmation miss tolerance                          | 2 frames  | —        | default | `GestureInterpreter` — consecutive non-matching frames tolerated without restarting the hold window (ADR-0025; fixes the measured flicker-reset bug) |
| Release hold (from an already-confirmed function)    | 400 ms    | —        | default | `GestureInterpreter` — always ≥ pose hold; a false release is audible, a strike can disrupt the pose reading this long (ADR-0027) |
| Release miss tolerance                               | 6 frames  | —        | default | `GestureInterpreter` — always ≥ confirmation miss tolerance (ADR-0027). Only applies to weak evidence (`None`/`Ambiguous`/reverting to what's confirmed) — a different concrete pose always uses the ordinary tolerance instead, however the pending attempt started (ADR-0029) |
| High-confidence frames to accept after tracking loss | 3         | —        | default | `GestureInterpreter` (§3.5)                                                                            |
| Tracking-loss cue delay                              | 200 ms    | —        | default | `GestureInterpreter` → presentation (§3.5.2)                                                           |
| Strike enter speed (downward)                        | 0.25 m/s  | —        | default | `ChordStrikeDetector` (ADR-0025) — starts a chord articulation                                          |
| Strike exit speed                                    | 0.10 m/s  | —        | default | `ChordStrikeDetector` — must decelerate below this to re-arm (Schmitt)                                  |
| Minimum inter-strike interval                        | 120 ms    | —        | default | `ChordStrikeDetector` (retrigger cooldown)                                                              |
| Strike settle frames                                 | 3 frames  | —        | default | `ChordStrikeDetector` — consecutive frames at/below exit speed needed to re-arm; a real strike's own rebound could otherwise re-arm and fire a second, unintended strike (ADR-0030) |
| Strike speed sample window                           | 3 frames  | —        | default | `MetaXRHandPoseSource._speedSampleFrames` — peak downward fingertip speed over this window, not instantaneous; mirrors `MelodyConfig.SpeedSampleFrames` (ADR-0018 / ADR-0028). Lives on the component, not the shared config asset — see ADR-0028 |

## Melody / touch targets — `Config/MelodyConfig.asset` (§3.3, §3.1, ADR-0015/0019)

Asset: `Assets/Jazztures/Config/MelodyConfig.asset` (`Jazztures/Config/Melody`). The
geometry + pivot group is read live by the touch-target rig. The melody-engine
group **mirrors** the `[TUNABLE]` constants still in `Jazztures.Core.Melody`
(`MelodyEngine`, `VelocityCurve`); Core consumes its own constants until Phase 8 (M8)
wires this asset to the parameterised overloads — keep the two in sync by hand.

The ten targets sit on an **arc centred on the right shoulder** (ADR-0019): five
scale-degree columns swept about the pivot, two octave rows — **near (lower octave) and
far (higher octave)**, each target lying flat and **facing up** (ADR-0049). Within a row every
target is the same distance from the shoulder, so a glissando is one shoulder rotation and
the outer degrees are no harder to reach than the centre. The pivot is **body-anchored with a lazy recenter** (ADR-0015): head position +
flattened yaw, offset down and right to the real shoulder; recenters toward the current
facing only after the yaw diverges past `_recenterAngleDegrees` for `_recenterDwellSeconds`,
easing over `_recenterEaseSeconds`. World-locked and head-locked were rejected (spatial-map
stability, §3.1; comfort).

Targets compacted 2026-09-10 (student call) — smaller faces, shorter tubes, tighter
arc. The visual/hit volume tracks the config, so face and volume shrink together.

| Parameter                                  | Default                 | Measured | Status  | Notes                                                                                                                                                                                                                                                     |
| ------------------------------------------ | ----------------------- | -------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Target face radius                         | 3 cm                    | —        | default | circular face; selects scale degree + octave. Was 4 cm (and 3.5 cm before ADR-0019). Bump back toward 3.5–4 cm if mid-air jitter makes a target hard to hit                                                                                                |
| Target depth                               | 12 cm total (±6 cm)     | —        | default | cylinder along the vertical approach axis (struck from above, ADR-0049); depth is unaimable in mid-air VR (ADR-0018) — still generous. Was 15 cm. Raise if the glissando slide or a deliberate strike feels finicky                                                                        |
| Column angle                               | 12° at the pivot        | —        | adjusted by feel | ADR-0049, student request ("a bit more spacing"). The saved asset was **8°** (docs said 12°) — a 6.3 cm chord, a 3 mm gap between 3 cm-radius neighbours. Now **8.3 cm** on the near row and **10.6 cm** on the far row (2.3 / 4.6 cm gaps). Tune up if a finger reaching for one target clips its neighbours (ADR-0019) |
| Row spacing                                | 11 cm                   | —        | adjusted by feel | **near/far** octave separation, centre to centre (ADR-0049); was 10 cm *vertical* before the targets lay flat. Near row (lower octave) at reach − 5.5 cm, far row (higher octave) at reach + 5.5 cm — this one now costs reach, so keep the far row inside an arm's length |
| Hover glow scale                           | 1.6× the trigger volume | —        | default | a nearing fingertip lights the target before the note fires (ADR-0018)                                                                                                                                                                                    |
| Speed sample window                        | 3 frames                | —        | default | binder fires on peak recent speed, so a decelerating approach still registers (ADR-0018)                                                                                                                                                                  |
| Arc reach (radius)                         | 0.45 m                  | —        | default | shoulder pivot to the middle of the two rows (near 0.395 m, far 0.505 m); the arc radius, not a forward offset                                                                                                                                            |
| Shoulder height offset                     | −0.25 m from head       | —        | default | down toward the real shoulder. With the targets lying flat (ADR-0049) both rows sit at this height — the lever for raising/lowering the whole set                                                                                                          |
| Shoulder lateral offset                    | +0.08 m (right)         | —        | default | small rightward bias (arc centre ~10°, right edge ~+40°). Pulled in from +0.20 m: on Quest 2 the hand-tracking cone is narrower than §1.4's ~140°, and a big offset made the learner turn their head to see the arc and lose left-hand tracking. ADR-0020 |
| Recenter divergence angle                  | 35°                     | —        | default | head-yaw offset before a recenter begins                                                                                                                                                                                                                  |
| Recenter dwell                             | 0.6 s                   | —        | default | divergence must hold this long first                                                                                                                                                                                                                      |
| Recenter ease                              | 0.5 s                   | —        | default | time to ease to the new facing                                                                                                                                                                                                                            |
| Entry velocity gate                        | 0.08 m/s                | —        | default | "was this a deliberate strike?" — distinct from the loudness curve's minimum (ADR-0018); mirrors `MelodyEngine.EntryVelocityGateMetresPerSecond`                                                                                                          |
| Velocity-curve min speed                   | 0.15 m/s                | —        | default | anchors the quietest struck note; an entry between the gate and this sounds at MinVelocity. `VelocityCurve.MinSpeed`                                                                                                                                      |
| Velocity-curve max speed                   | 1.5 m/s                 | —        | default | fingertip speed mapped to max MIDI velocity; mirrors `VelocityCurve.MaxSpeed`                                                                                                                                                                             |
| Per-target retrigger cooldown              | 80 ms                   | —        | default | mirrors `MelodyEngine.RetriggerCooldownSeconds`                                                                                                                                                                                                           |
| Fixed note sustain                         | 0.5 s `[OPEN]`          | —        | default | struck-piano model, no stuck notes; mirrors `MelodyEngine.DefaultSustainSeconds`                                                                                                                                                                          |
| MIDI velocity range (from fingertip speed) | 40–110, clamp; floor 30 | —        | default | never emit < 30 (§3.3); mirrors `VelocityCurve` bounds                                                                                                                                                                                                    |

## Harmony — `HarmonyEngine` (§3.2, ADR-0025)

Selection (pose) and articulation (strike) are separate events as of ADR-0025 — see the
gesture-recognition table above for the strike-detection thresholds. This row is the
harmony-side counterpart of the melody engine's fixed note sustain, above.

| Parameter                | Default        | Measured | Status  | Notes                                                                                     |
| ------------------------ | -------------- | -------- | ------- | ------------------------------------------------------------------------------------------ |
| Fixed chord ring length  | 1.5 s `[OPEN]` | —        | default | struck-piano model, mirrors `MelodyEngine.DefaultSustainSeconds`; long enough to read as sustained, short enough to have decayed before a typical re-strike. Code: `HarmonyEngine.DefaultSustainSeconds` |

## Harmonic field (Design A) — `Config/HarmonicFieldConfig.asset` (ADR-0038)

An alternative to the gesture-recognition table above, not yet the shipping mechanism —
selected via `PerformanceCompositionRoot.HarmonyCommitGesture = Pinch`. Replaces the three
discrete pose classifiers with one continuous (height, openness) space, and the velocity
threshold strike with a pinch. **Every value below is an invented placeholder — none has
been observed on a real hand.** The landmark coordinates and the two radii are especially
low-confidence: they were chosen only to keep the three ii/V/I basins from overlapping
given each other, not from any measurement. Read live `h=`/`o=` values off the console
(`MetaXRHandPostureSource._logPosture`) during the first device session and replace the
landmark rows *before* judging whether the mechanic works at all — the radii are derived
from the landmark coordinates, so re-tuning one means re-checking the other.

| Parameter                              | Default    | Measured | Status  | Notes                                                                                  |
| --------------------------------------- | ---------- | -------- | ------- | --------------------------------------------------------------------------------------- |
| ii landmark (height, openness)          | 0.80, 0.85 | —        | default | "up and open." `HarmonicFieldThresholds.IiHeight/IiOpenness`                             |
| V landmark (height, openness)           | 0.45, 0.10 | —        | default | "mid-height, closed: closing is tension." `VHeight/VOpenness`                            |
| I landmark (height, openness)           | 0.20, 0.85 | —        | default | "low and open." `IHeight/IOpenness`                                                      |
| Floor height (first latch)              | 0.05       | —        | default | `FloorHeight`                                                                             |
| Floor release height (Schmitt exit)     | −0.05      | —        | default | must stay < Floor Height. `FloorReleaseHeight`                                           |
| Lock radius (Schmitt enter)             | 0.22       | —        | default | derived from the landmark coordinates above — re-tune together. `LockRadius`             |
| Unlock radius (Schmitt exit)            | 0.34       | —        | default | must stay > Lock Radius. `UnlockRadius`                                                  |
| Shoulder height offset                  | −0.25 m    | —        | default | inherited from `MelodyConfig`'s right-hand equivalent (ADR-0015), not re-guessed          |
| Height-normalisation floor offset       | −0.35 m    | —        | default | wrist height below the shoulder anchor that maps to 0. `HarmonicFieldConfig.FloorOffsetMetres` |
| Height-normalisation ceiling offset     | +0.15 m    | —        | default | together with the floor, sets the 0.50 m span the whole height axis rests on. `CeilingOffsetMetres` |
| Min inter-pinch interval               | 0.12 s     | —        | default | inherited from `GestureThresholds.MinInterStrikeSeconds`'s value, not re-guessed. `PinchCommitThresholds.MinInterPinchSeconds` |
| Pinch rate at min velocity              | 3.0 s⁻¹    | —        | default | nobody has measured how fast a pinch closes on this tracker. `PinchRateAtMinVelocityPerSecond` |
| Pinch rate at max velocity              | 14.0 s⁻¹   | —        | default | `PinchRateAtMaxVelocityPerSecond`                                                         |
| Pinch-rate sample window                | 3 frames   | —        | default | inherited from `MetaXRHandPoseSource._speedSampleFrames`'s precedent (ADR-0028), not re-guessed. `MetaXRHandPostureSource._pinchRateSampleFrames` |
| Openness curl range — middle, ring      | 180°–250°  | —        | n/a     | not a tunable — copied verbatim from the SDK's own `PalmGrabAPI.CURL_RANGE`               |
| Openness curl range — pinky             | 180°–245°  | —        | n/a     | ditto                                                                                     |

## Touch commit (Design "virtual object") — `Presentation/ChordStrikeTarget` (ADR-0039)

A third articulation mechanism, alongside the strike (§Gesture recognition, above) and the
pinch (Harmonic field, above) — selected via `PerformanceCompositionRoot.HarmonyCommitGesture
= Touch`. Composes with either selection mechanism (discrete poses or the harmonic field);
only the commit event changes. Its cooldown and entry-velocity gate are **inherited**
values, not new guesses — see ADR-0039 for why that makes this the highest-confidence
starting point of the three. The grace window below (ADR-0042) is a fresh guess, added
after an on-device report of touches that registered nothing.

| Parameter                    | Default  | Measured | Status  | Notes                                                                                                                     |
| ----------------------------- | -------- | -------- | ------- | -------------------------------------------------------------------------------------------------------------------------- |
| Minimum inter-touch interval   | 0.12 s   | —        | default | inherited from `GestureThresholds.MinInterStrikeSeconds`, not re-guessed. `TouchCommitThresholds.MinInterTouchSeconds`     |
| Entry velocity gate            | 0.15 m/s | —        | default | *(was 0.08 until the ADR-0047 addendum — see below)* inherited from `MelodyConfig`'s own entry gate (ADR-0018) — the one commit threshold in this project's history already on-device validated, just for the other hand. `TouchCommitThresholds.EntryVelocityGateMetresPerSecond` |
| Max awaiting-confirmation window | 0.35 s | —      | default | ADR-0042 — how long a touch that arrived before selection confirmed keeps waiting (fingertip still resting inside) before being given up on. Sized to cover `GestureThresholds.PoseHoldSeconds` (150 ms) plus margin, not measured. `TouchCommitThresholds.MaxAwaitingConfirmationSeconds` |
| Footprint margin               | 2 cm     | —        | default | `ChordStrikeTarget._footprintMarginMetres` — how far the hit area extends past the visual's edges on every side (ADR-0048). Replaces the fixed 6 cm-radius disc |
| Hover above surface            | 2 cm     | —        | default | `ChordStrikeTarget._hoverAboveSurfaceMetres` — reach above the visual's top face (ADR-0048). **The lever for "hitbox feels too high/low"**: 0 = joint centre on the slab; raise to make it easier |
| Depth below surface            | 10 cm    | —        | default | `ChordStrikeTarget._depthBelowSurfaceMetres` — depth is unaimable in mid-air VR (ADR-0018), so an overshoot must still count (ADR-0048). Was a ±8 cm half-depth centred on the origin |
| Speed sample window            | 3 frames | —        | default | `ChordStrikeTarget._speedSampleFrames` — peak entry speed, mirrors `MelodyConfig`'s window (ADR-0018)                     |
| Contact / speed joints         | see ADR-0047 | —    | default | the single `_fingertip` (Index tip) of ADR-0039 became `_contactJoints` / `_speedJoints` — see the ADR-0047 section below |
| Hit-flash decay                | 0.18 s   | —        | default | `ChordStrikeTarget._flashDecaySeconds` — cosmetic only                                                                    |

**Optional body anchor — `Presentation/ChordStrikeTargetAnchor` (ADR-0040).** Assign it
alongside `ChordStrikeTarget` (same GameObject) to make the plate follow the learner the
way the melody arc does, instead of sitting at one fixed world position. Reuses the melody
arc's own `LazyRecenterSettings.Default` (35°/0.6s/0.5s) unchanged — only the offsets below
are new, and both are unmeasured guesses (a description of intended placement, not a
measurement).

| Parameter               | Default  | Measured | Status  | Notes                                                                                   |
| ------------------------ | -------- | -------- | ------- | ----------------------------------------------------------------------------------------- |
| Height offset from head  | −0.25 m  | −0.30 m  | adjusted by feel | **Scene value −0.30 m (student, 2026-10-12): the plate rendered too close and too high.** Set in the scene, not by a pilot measurement; the code default is unchanged. No longer level with the melody arc's default offset. Original rationale: matches `MelodyConfig.ShoulderHeightOffsetMetres` (ADR-0015) so both hands' target objects sit at the same level — revised from an initial guess of −0.45 m before any device test (ADR-0040). `ChordStrikeTargetAnchor._heightOffsetMetres` |
| Lateral offset from head | −0.20 m  | —        | default | mirrors melody's rightward bias (+0.08 m, ADR-0020) onto the other hand, at a larger magnitude since this is one plate, not a ten-target arc to keep in frame. `_lateralOffsetMetres` |
| Forward offset from head | +0.20 m  | +0.30 m  | adjusted by feel | **Scene value +0.30 m (student, 2026-10-12): the plate rendered too close.** By feel, not a pilot measurement; the code default is unchanged. How far in front of the body the plate sits. `_forwardOffsetMetres` |

Leaving `_strikeTargetAnchor` unassigned on `PerformanceCompositionRoot` keeps
`ChordStrikeTarget` exactly as ADR-0039 shipped it — a plain, world-fixed `Transform`
positioned by hand in the Editor.

## Tension colour — `Config/TensionPalette.asset` (§3.10, ADR-0017)

Asset: `Assets/Jazztures/Config/TensionPalette.asset` (`Jazztures/Config/Tension Palette`).
Read by `Jazztures.Presentation.TensionColorDriver`, which eases one shared colour on every
chord change and pushes it to the touch targets (base tint) and the left-hand outline.
Colour is a **redundant** channel (§3.10) — the chord is always also audible and spatially
shown. ii is a desaturated yellow-green rather than a cool hue: §3.10 says "cool/neutral for
ii" but the student's palette direction was mustard; sage/olive is the compromise (ADR-0017).

| Parameter               | Default (RGBA)         | Measured | Status  | Notes                                                            |
| ----------------------- | ---------------------- | -------- | ------- | ---------------------------------------------------------------- |
| Neutral — no chord held | 0.58, 0.58, 0.62, 0.35 | —        | default | cool grey, low alpha; targets stay visible but read as inactive  |
| ii — preparation        | 0.53, 0.60, 0.42, 0.80 | —        | default | sage/olive; deviates from §3.10 "cool" (ADR-0017)                |
| V — peak tension        | 0.74, 0.33, 0.18, 0.85 | —        | default | burnt sienna; warm, saturated                                    |
| I — resolution          | 0.52, 0.36, 0.60, 0.85 | —        | default | warm purple; deep, settled                                       |
| Strike flash            | 1.00, 0.96, 0.85, 1.00 | —        | default | near-white warm; fingertip-trigger flash                         |
| Highlight boost         | 0.35                   | —        | default | push toward white while a chord-change highlight is active       |
| Transition              | 0.25 s                 | —        | default | ease time between chord colours (SmoothStep); not a jarring snap |

## Ghost hand color [TUNABLE]

Asset: `Assets/Jazztures/Presentation/Materials/GhostHand.mat`.
Uses top color #99CECF and bottom color #78BAC3 to avoid confusion with the ii/V/I tension palette (ADR-0012). The ghost is translucent (`_Opacity 0.25`) and tinted so it never reads as the real hand. The ghost's `HandGhost` component is left alone; the `GhostHandView` component drives the `HandPuppet` directly.

## Register assignment — `Config/RegisterConfig.asset` (§3.1)

| Parameter                               | Default                   | Measured | Status  | Notes                                                                                                 |
| --------------------------------------- | ------------------------- | -------- | ------- | ----------------------------------------------------------------------------------------------------- |
| Left-hand voicing lowest note           | MIDI 48–60, close voicing | —        | default | needs a pianist's ear (§7 open item). Code: `Voicing.DefaultRootFloorMidi` / `DefaultRootCeilingMidi` |
| Right-hand lower octave — lowest target | ≥ MIDI 72                 | —        | default | keeps melody above harmony in spectrum. Code: `ChordToneSet.DefaultLowestTargetFloorMidi`             |

Until the Config asset exists (M8, Phase 8), these live as named `const` in
`Assets/Jazztures/Core/Music/`. Phase 8 wires the asset to the parameterised overloads
(`Voicing.Close(chord, floor, ceiling)`, `ChordToneSet.For(chord, floor)`) and these
constants become fallback defaults only.

## Timing — `Config/TimingConfig.asset` / per-lesson (§3.6)

| Parameter            | Default | Measured | Status  | Notes                                                                                                                                   |
| -------------------- | ------- | -------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| Default tempo        | 80 BPM  | —        | default | >100 BPM gated behind a config flag (unvalidated). Code: `Tempo.Default`                                                                |
| Swing ratio          | 0.66    | —        | default | per-lesson; L4 introduces swing, L1–3 straight. Code: `SwingRatio.Default` (straight = `SwingRatio.Straight`); warp in `SwingQuantizer` |
| Metronome bar length | 4 beats | —        | default | click grid only, not swung. Code: `Metronome.DefaultBeatsPerBar`                                                                        |

### Audible metronome — `Config/MetronomeSettings.asset` (§3.6, ADR-0043)

Lessons carry their own tempo, bar length and swing (the table above); for a lesson only the
**sound** and **scheduling** rows below apply. The **pulse** rows are free play's starting
state. Every value is `[TUNABLE]`, set by ear or by engineering judgement — none came from the
co-design sessions. The **Measured** column stays empty until M8.

| Parameter                 | Default           | Measured | Status  | Notes                                                                                                                                                      |
| ------------------------- | ----------------- | -------- | ------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Click in free play        | on                | —        | default | Switch off to leave free play silent until desk key `M`. Revisit once the timing-test menu (ADR-0043 step 4) can start a pulse on device                  |
| Tempo                     | 80 BPM            | —        | default | Validated range 40–100 (`TempoRange`); above that needs `Allow Unvalidated Tempo`; hard ceiling 240 as a typo guard only                                   |
| Beats per bar             | 4                 | —        | default | `Metronome.DefaultBeatsPerBar`                                                                                                                             |
| Subdivision               | none              | —        | default | none / eighths / swung eighths. The quarter pulse is never swung; only the off-beat click moves                                                           |
| Swing ratio (off-beat)    | 0.66              | —        | default | Swung eighths only. Same warp as the melody: `SwingQuantizer.Swing(beat + 0.5, ratio)`                                                                     |
| Accent downbeat           | on                | —        | default | Beat 1 gets its own, higher click                                                                                                                          |
| Click volume              | 0.6               | —        | default | `AudioSource.volume`. Balance against the piano by ear                                                                                                     |
| Off-beat volume scale     | 0.5               | —        | default | **Not in ADR-0043's table** — added so the subdivision reads lighter than the pulse                                                                        |
| Beat click pitch          | 2000 Hz           | —        | default | Above the right-hand ladder's register (fundamentals ≈ 523–1319 Hz) so it does not mask the learner's notes                                                |
| Downbeat click pitch      | 2800 Hz           | —        | default | **Not in ADR-0043's table** — "separate click sound" needed a value; judgement, not measured                                                               |
| Click length              | 20 ms             | —        | default | Generated at start-up by `ClickWaveform` (no audio asset). Attack/decay/tail-fade shape are consts in that class                                           |
| Look-ahead                | 100 ms            | —        | default | Scheduling horizon only; adds no latency to a click already due                                                                                           |
| Pre-roll                  | 150 ms            | —        | default | **Not in ADR-0043's table** (it says "≥ look-ahead"). A metronome lesson's phrase clock, system playback and pulse all start this long after "now"         |
| Voice pool                | 4 sources         | —        | default | `MetronomeVoice` inspector field; round-robin. A 20 ms click needs only a few                                                                              |
| Tempo step                | 5 BPM             | —        | default | Per press of the menu's +/- buttons and the `-` / `=` desk keys (Editor / Link). `MetronomeSettings.TempoStepBpm`                                          |
| Tempo preview             | 2 bars            | —        | default | Pressing +/- on the menu plays this many bars at the new tempo (free play isn't running there). `MetronomeSettings.TempoPreviewBars`                       |
| Menu stepper layout       | 9 cm buttons      | —        | default | `LessonSelector` side-control fields (size, gap, pitch, hit margin, readout scale). First guesses — never seen on a headset; adjust in the Inspector       |

Count-in (ADR-0043 §2a) is **not** in this asset: only timing drills use it, so it is a field
of each drill preset (below). Lessons have none.

### Left-hand pose and plate — ADR-0047

All `[TUNABLE]`, none measured. The plate values are code defaults in `TouchCommitThresholds` (no config
asset for that path yet — wired from `Default` in `PerformanceCompositionRoot`); the recogniser values live
on the Meta SDK's own assets/components.

| Parameter                  | Default                          | Measured | Status  | Notes                                                                                                                                              |
| -------------------------- | -------------------------------- | -------- | ------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| ii rule                    | open palm + palm sideways        | —        | default | Four `IsNot` conditions (palm down / up / toward face / away from face) on the open-palm shape; accepts thumb up or thumb forward. Was `WristUp` only |
| "Up" reference (ii and I)  | Tracking (gravity)               | —        | default | `UpVectorType` on the two `TransformRecognizerActiveState`s. Was Head, which tilts ~40° when looking down at the plate                             |
| Palm cones                 | SDK asset: PalmDown 35°/50°, PalmUp 40°/50°, toward 30°/50°, away 40°/60° (enter / exit) | — | default | `Config/GesturePalmConeThresholds.asset`. A sideways palm is ~90° from all four, well outside every exit threshold                              |
| Plate contact joints (where) | 5 tips, 4 knuckles, pinky base, wrist | — | default | `ChordStrikeTarget` ► Contact Joints. Any one inside the volume is a touch. Remove a joint here if it sets the plate off by accident            |
| Plate speed joints (how hard) | wrist, 4 knuckles, pinky base  | —        | default | `ChordStrikeTarget` ► Speed Joints. The strike speed is their peak **downward** speed over 3 frames — never the fingertips, never sideways/upward motion (ADR-0047 addendum) |
| Plate entry speed          | 0.15 m/s (downward hand speed)   | —        | default | `TouchCommitThresholds`. Was 0.08, inherited from the melody targets' aimed fingertip (ADR-0018); too low for a whole hand — it let unintended chords through |
| Plate tap speed            | 0.25 m/s                         | —        | default | A tap while already inside plays. The left-hand strike's tuned enter speed, unmeasured for the plate                                                |
| Plate tap settle           | ≤ 0.10 m/s for 3 frames          | —        | default | Re-arms a tap after a note                                                                                                                         |

### Right-hand touch — `Config/MelodyConfig.asset` (§3.3, ADR-0045)

A fingertip plays a target by entering it (at the entry speed, below) **or by tapping while already
inside it**. All `[TUNABLE]`; the three tap values are unmeasured for the right hand — they start
from the left-hand strike's.

| Parameter           | Default  | Measured | Status  | Notes                                                                                                                                       |
| ------------------- | -------- | -------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| Entry speed         | 0.08 m/s | —        | default | Existing (§3.3, ADR-0018): a fresh entry at or above this plays. Mirrors `MelodyEngine.EntryVelocityGateMetresPerSecond`, which still gates the note |
| Tap speed           | 0.25 m/s | —        | default | A tap inside the volume at or above this plays. Too high: a gentle tap does nothing. Too low: a resting hand's jitter fires notes             |
| Tap settle speed    | 0.10 m/s | —        | default | After a note the fingertip must slow to this before another tap plays (hysteresis — below the tap speed)                                      |
| Tap settle frames   | 3        | —        | default | Consecutive frames at or below the settle speed that re-arm a tap                                                                            |

### Right-hand target look — `Config/MelodyConfig.asset` (ADR-0046)

| Parameter    | Default | Measured | Status  | Notes                                                                                                                                                       |
| ------------ | ------- | -------- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Target look  | Paddle  | —        | default | Paddle: a pad (the trigger volume's radius, 1.2 cm thick) on a faint rail, pushed in front of a fingertip. Prefab: the rig's `Target Prefab`, as-is (the scene's is a 5 cm sphere) |
| Pad / rail dimensions, spring-back (60 ms), pad opacity (≥ 0.8) | consts | — | default | Cosmetics in `TouchTargetRig` / `TouchTarget`, not behaviour — change in code if the look needs it                                                          |

### Right-hand finger bend — EXPLORATION, branch `explore/finger-bend-keys` only — `Config/MelodyConfig.asset` (ADR-0050)

All `[TUNABLE]` and **entirely unmeasured** — engineering starting points for a key-sized bend, nothing more.
Every level is a change in a finger's normalised bend (0 flat … 1 fully bent) from that finger's own last turning
point — its lowest recent point while free, its peak while held — never from a fixed resting level (Revision 1).

| Parameter          | Default | Measured | Status  | Notes |
| ------------------ | ------- | -------- | ------- | ----- |
| Press delta        | 0.14    | 0.15 (scene asset) | adjusted by feel | How far a free finger must rise above its lowest recent point to press. Too high: presses missed. Too low: ordinary movement sounds notes |
| Release delta      | 0.07    | 0.05 (scene asset) | adjusted by feel | How far a pressed finger must **fall from its peak** to re-arm (hysteresis; below the press delta). Too high: a quick repeat on one finger is missed. Too low: wobble at the peak can re-fire |
| Baseline seconds   | 2.0 s   | 2.0 s (scene asset) | revised | How fast a finger's lowest-recent-point memory forgets upward. Bends slower than about press ÷ this per second are drift and never press. **Was 0.4 s, which silently missed moderately slow presses** (Revision 1) |
| Isolation margin   | 0.06    | 0.05 (scene asset) | adjusted by feel | A finger presses only if it rose this much further than every other free finger. Stops a fist / tendon-coupled neighbours. Raise if neighbours fire; lower if presses are ignored. The log reports each press this suppresses |
| Signal             | `(2·curl + flexion)/3`, thumb curl only | — | verbatim | The SDK's own blend (`PalmGrabAPI`). Curl alone ignores the knuckle, where a key press mostly happens |
| Thumb press span   | 0.08 m  | — | default | **Thumb only.** The thumb is pressed DOWN as often as it is curled in, and curl only sees it curl in. Its bend is curl **plus** its tip's offset toward the palm side (in the wrist's own frame) ÷ this span. Smaller = more sensitive; 0 = curl only; negative flips direction. Was 0.04, which let ~6 mm of ordinary thumb movement press. Unverified on device: with `JAZZTURES_DIAG` the thumb press log prints the offset in mm, which should rise when you press |
| Thumb curl span    | 60°     | — | default | **Thumb only.** Degrees of thumb curl that count as 1.0 of bend. The SDK's thumb range is 30° (its grab test needs the thumb to move little), which made the thumb ~2× as sensitive as the other fingers; deliberately wider than the SDK's value. Raise it, with the press span, if the thumb is too easy to trigger |
| Bend range         | SDK `PalmGrabAPI.CURL_RANGE` | — | verbatim | Thumb 190–220°, index/middle/ring 180–250°, pinky 180–245°; not tunables (as `MetaXRHandPostureSource`) |
| Require high confidence | off | — | default | `FingerBendBinder` toggle. On: skip any frame where ANY finger is low-confidence (the first version's behaviour). Off by default — one flickering finger silenced all five |
| Loudness           | tip speed, floored at the engine's entry gate | — | default | `TODO(OPEN)`: a slow deliberate press sounds quietly instead of being dropped; curl speed may be a better source |

### Right-hand note labels — `Config/MelodyConfig.asset` (ADR-0044)

| Parameter       | Default            | Measured | Status  | Notes                                                                                                                            |
| --------------- | ------------------ | -------- | ------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Show note labels | on                | —        | default | Letters only, no octave numbers (§1.5). Off removes the labels; the targets then carry no chord information at all (ADR-0044)    |
| Label size      | 0.005 (character size) | —   | default | In world metres, independent of the target's scale (ADR-0046). Estimated ~3 cm capital on a 6 cm pad; columns are ~6 cm apart so much above 0.010 crowds neighbours. The first labels looked tiny because they inherited a 0.05-scaled sphere's scale, not because this was too small — tune by eye |
| Label colour    | near-white         | —        | default | Fixed; the labels never change colour                                                                                            |

### Timing drills — `TimingDrillDefinition` presets (§3.7, ADR-0043)

A drill is a measuring instrument: change any of these and earlier runs are no longer
comparable, so do it between sessions, not during one. All `[TUNABLE]`; none came from the
co-design sessions. The onset windows (80 / 160 ms, match 300 ms) are the existing §3.7 values
above — drills use `OnsetWindows.Default` unchanged.

| Parameter                 | Default                    | Measured | Status  | Notes                                                                                                                                      |
| ------------------------- | -------------------------- | -------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| Count-in                  | 1 bar                      | —        | default | Per preset (`_countInBars`). Beat 0 of the run's shared start; no onsets are expected inside it                                           |
| Scored bars               | 4                          | —        | default | Per preset (`_bars`); the click-only preset runs 8                                                                                         |
| Tempo                     | 60 and 80 BPM              | —        | default | Per preset, inside the validated 40–100 range. The ADR's bake-off runs both                                                                |
| Fixed chord               | I (Cmaj7), every bar line  | —        | default | Struck each bar because a piano chord decays; audible by default (ADR-0043 decision 3). Per preset                                         |
| Run tail                  | 1.0 s                      | —        | default | `TimingDrillRunner._tailSeconds`: wait after the last scored beat before ending, so a late final note still counts                         |
| Strike coincidence window | 5 ms                       | —        | default | `OnsetCapture.DefaultCoincidenceSeconds`: left-hand note-ons this close are one strike's voicing. A guard against float noise, not a measured value |
| Starter presets           | beats 60/80, eighths 60/80, comp 1+3 80, click only 80 | — | default | Made by `Jazztures ▸ Timing ▸ Create Starter Drills`. The ladder's pitch-specific drills are not here yet                       |

The CSV (`Application.persistentDataPath/timing/`) records the preset's values in its header,
so a run is self-describing. A negative mean error ("early") is ordinary — people tap ahead of
a click — and is not a system fault.

## Lesson pacing — per-lesson `LessonDefinition` asset (§3.9, ADR-0021)

Phrase length is a **pedagogical** parameter, not only a musical one: the same ii-V-I needs
a longer phrase to _practise_ than to _demonstrate_. `LessonTimeline.DurationBeats` is
simply the last event's beat, so pacing is set by where the chords are placed.
A pose costs the learner a 120 ms hold plus 3 confirming frames before it registers (§3.4)
— on top of the time to recall and form an unfamiliar shape.

| Parameter        | Default                             | Measured | Status  | Notes                                                                                        |
| ---------------- | ----------------------------------- | -------- | ------- | -------------------------------------------------------------------------------------------- |
| L1 chord spacing | 4 beats (3.0 s at 80 BPM)           | —        | default | one chord per bar. Was 2 beats, giving a 3 s phrase — enough to hear, not to play (ADR-0021) |
| L1 phrase length | 20 beats (15 s), ii-V-I twice       | —        | default | the repeat gives a second attempt without restarting the phase                               |
| L1 mode phases   | GestureLearning → W&L → TryYourself | —        | default | deviates from §3.9's "W&L, TY"; §3.8 puts Gesture Learning first for pose fluency (ADR-0021) |
| Phrase tail      | 1.0 s                               | —        | default | grace after the last beat before auto-advance. `LessonRunner._phraseTailSeconds`             |

## Onset scoring — `Config/OnsetScoringConfig.asset` (§3.7)

Code: `Core.Evaluation.OnsetWindows` (`DefaultOnTimeSeconds` / `DefaultCloseSeconds` /
`DefaultMatchSeconds`); scoring in `OnsetScorer.Evaluate` → `AttemptResult`.

| Window                                          | Default  | Measured | Status  |
| ----------------------------------------------- | -------- | -------- | ------- |
| "on time"                                       | ≤ 80 ms  | —        | default |
| "close"                                         | ≤ 160 ms | —        | default |
| "off"                                           | > 160 ms | —        | default |
| match gate (beyond → missed + extra, not "off") | 300 ms   | —        | default |

## Audio — `Config/AudioConfig.asset` (§4.2)

| Parameter                    | Default                                | Measured | Status     | Notes                                                                 |
| ---------------------------- | -------------------------------------- | -------- | ---------- | --------------------------------------------------------------------- |
| Sample rate                  | 48 kHz                                 | —        | default    |                                                                       |
| DSP buffer                   | "Best Latency"                         | —        | default    |                                                                       |
| Voice pool size              | ≥ 32                                   | —        | default    | never DecompressOnPlay in trigger path. `SamplerNoteSink._voiceCount` |
| Sample load                  | preloaded + decompressed on load       | —        | default    | set on import; not yet enforced                                       |
| Sampler velocity-layer split | MIDI velocity 64                       | —        | default    | ≥ → Hard layer, < → Soft. `SampleLibrary.DefaultLayerSplitVelocity`   |
| Note-off release fade        | 80 ms                                  | —        | default    | avoids a click on the sample tail. `SamplerNoteSink._releaseSeconds`  |
| Velocity → gain trim         | lerp 0.35→1.0 over vel 0→127, × master | —        | default    | layers carry the big dynamics; this is a trim. `SamplerNoteSink`      |
| Master gain                  | 0.5                                    | —        | default    | `SamplerNoteSink._masterGain`                                         |
| Keyboard debug entry speed   | 0.8 m/s                                | —        | debug-only | `KeyboardPerformanceDriver.KeyedEntrySpeed` — not a study parameter   |

## Feedback — `Config/TensionColorConfig.asset` (§3.10)

| Parameter | Default            | Measured | Status  | Notes                                      |
| --------- | ------------------ | -------- | ------- | ------------------------------------------ |
| ii colour | cool / neutral     | —        | default | redundant channel only, never sole carrier |
| V colour  | warm / saturated   | —        | default |                                            |
| I colour  | resolved / settled | —        | default |                                            |

## Tracking-loss policy — `Config/TrackingLossConfig.asset` (§3.5)

| Parameter                       | Default | Measured | Status  |
| ------------------------------- | ------- | -------- | ------- |
| Loss duration before visual cue | 200 ms  | —        | default |

---

## Latency budget measurements (§4.3) — filled by `Diagnostics/LatencyProbe`

Report percentiles (p50 / p95 / p99), not single numbers. **These go in the thesis.**

| Segment                           | Target               | p50 | p95 | p99 | Measured on |
| --------------------------------- | -------------------- | --- | --- | --- | ----------- |
| Hand tracking → pose available    | ~30–50 ms (platform) | —   | —   | —   | —           |
| Pose confirmation (hold + frames) | ≤ 120 ms             | —   | —   | —   | —           |
| Domain processing                 | < 2 ms               | —   | —   | —   | —           |
| Note event → audible              | < 20 ms              | —   | —   | —   | —           |
| **End-to-end (gesture → sound)**  | —                    | —   | —   | —   | —           |
