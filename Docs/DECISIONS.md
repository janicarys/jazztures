# Architecture Decision Record — Jazztures

Every `[OPEN]` resolution and every deviation from the thesis prose is logged here.
Format: newest first. Each entry states the decision, the context, and — where relevant —
what must change in the thesis text (Chapter 6 in particular).

Status legend: **Accepted** · **Superseded** · **Proposed**

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
anchor *offsets* are new — mirrored to the left (`_lateralOffsetMetres = −0.20`) and lower
than melody's arc reach (`_heightOffsetMetres = −0.45` vs. melody's −0.25), since this plate
is struck downward near waist height rather than reached out to at shoulder height. Both are
new, undemonstrated guesses.

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
