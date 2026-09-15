# Design Space — Alternative Interaction Models

**Status:** Proposed, not yet decided. This document is the record of an exploration, not
an ADR — when the bake-off in §6 runs, its outcome becomes an ADR in `Docs/DECISIONS.md`
(ADR-0025+) that cites this file, the way ADR-0011 cites the SMF-vs-MusicXML reasoning it
grew out of. Until then, the shipping interaction model is still `CLAUDE.md` §1.3 exactly
as written.

**Mandate.** The student authorised a full pivot exploration (2026-09-15), releasing
`CLAUDE.md` §1.3's specific gesture rules for this document only, while holding the
top-level goal fixed: teach a complete novice jazz improvisation concepts in VR, through
gesture, with no physical instrument or implement. Nothing here supersedes §1.3 until an
ADR says so.

**Why now.** The right-hand melody mechanic has been redesigned six times in three weeks
(ADR-0016 → 0018 → 0019 → 0020, an unbuilt plane-crossing variant, and the in-tree
`_pinchSpike` on `TouchTargetBinder`), and mid-air fingertip-poke has failed on device
three separate times across three different features (melody cylinder, redesigned
targets, lesson selector — see the `midair-poke-lacks-contact` project memory). That
pattern is not bad luck. §2 names the actual mismatch.

---

## 1. What does not change

Independent of gesture vocabulary:

- VR headset, hand tracking only. No controllers, no haptics, no instrument, no implement.
- Complete novice, zero theory vocabulary. Unexplained jargon on screen is a bug (§1.5).
- Must teach: harmonic function (prepare → tension → resolve), timing and swing,
  chord-tone-constrained melodic choice, motif construction and variation, real-time
  improvisational decision-making (§1.2).
- Cognitive Load Theory governs every affordance; progressive disclosure; visual restraint
  is a requirement, not a preference (§1.5, §3.10).
- Auditory feedback is primary and immediate; visual is secondary and strictly **redundant**
  — never the sole carrier of information (§3.10).
- The learner stands; interaction happens in front of them.
- Tracking loss degrades gracefully — sustain, never glitch (§3.5).
- Five sessions, each ≤ 2 hours (§3.9).

---

## 2. What the device has already taught us

These are findings earned from real on-device failures, cited to their ADRs — not
speculation. They are the single most valuable output of the last month of iteration, and
this table is the first place they've been assembled in one view. **Any alternative design
that violates one of these will fail the way the last six iterations failed.**

| # | Finding | Source |
|---|---|---|
| D1 | Mid-air has no contact event. Fingertip-entry-into-a-volume as a *discrete commit* has failed three times on device. The only reliable proprioceptive "click" available without haptics is thumb-to-finger self-contact. | project memory (`midair-poke-lacks-contact`); ADR-0016, ADR-0018 |
| D2 | Depth is unaimable. With no contact cue and no haptics, a person cannot judge distance along the approach axis in mid-air; lateral and vertical position they *can* judge. | ADR-0018 |
| D3 | Two hands compete for one tracking cone. The usable two-hand cone on Quest 2 is nearer 100–120°, not §1.4's ~140° (which describes head/controller tracking). Turning to look at one hand's workspace drops the other hand's tracking. | ADR-0020 |
| D4 | Finger shape is the least reliable classification signal available; gross posture and position are the most reliable. Palm azimuth ("faces the user's right") is not observable at all by the SDK's feature set — only head/gravity-relative cones exist. | ADR-0014 |
| D5 | Discrete pose classification has a fixed cost: ~120 ms hold + 3 confirming frames, before the time to recall and physically form the shape. L1's phrase length needed a 5× increase to absorb this for three simple static poses. | ADR-0021; §3.4 |
| D6 | Isolation and continuity trade against each other in target design. Dense targets make a good glissando and are unhittable individually; sparse targets are hittable individually and slide badly. No spacing wins both. | ADR-0019 |
| D7 | A lateral sweep has near-zero velocity component along an approach-axis normal. Deriving loudness from push-through depth silences every expressive sideways gesture — this is specifically why the plane-crossing redesign was built and then set aside. | ADR-0019 |

### The structural insight

D1, D2, D6, and D7 are one problem wearing four hats. They all dissolve with a single move:

> **Separate selection from commitment.** Continuous hand *position*, on the axes a person
> can actually judge (lateral × vertical), selects **which** note. Self-contact — a
> pinch — commits **when**. The system quantises position to the nearest legal note, so
> mid-air imprecision stops being a failure mode and becomes structurally harmless.

There is no "miss" state. The learner always produces *a* note, and the system guarantees
it is a legal chord tone — this is exactly the "position picks, system quantises" model,
and it is a strict reading of the co-design finding that the learner must choose every
note: the system is never picking pitches, only rounding a continuous choice to the
nearest one that's legal. `LessonSelector`'s point-and-pinch (ADR-0023, pending) already
demonstrates that pinch-as-commit works reliably on this device for a discrete choice; the
in-tree `_pinchSpike` on `TouchTargetBinder` is an unfinished first attempt at the same
move for melody.

Applied to D4 and D5, the same move reframes the left hand: stop *classifying* hand shapes
discretely and start *locating* the hand continuously in a space where the three chords are
landmarks, not categories.

---

## 3. Candidate designs

Six families. **A, B, and E are the recommended bake-off set** (§6) — cheap, testable
before the December build freeze, and buildable almost entirely from existing `Core` code.
C and D are curriculum-level changes gated on which instrument wins. F is the fallback of
last resort.

### A — Harmonic Field (left hand)

Replace three discrete pose classifiers with one continuous 2D posture space: hand
**height** (relative to a body-anchored shoulder) × hand **openness** (a single aggregate
curl scalar). The three chords are landmarks in that space, selected by nearest-neighbour
with one radial hysteresis ring — the Schmitt-trigger requirement of §3.4 still applies,
just to a distance rather than to three independent feature cones.

- **ii** — hand up and open (reaching, unsettled)
- **V** — hand closed, mid-height (closing *is* tension — the co-design finding, verbatim)
- **I** — hand low and open (settling, released)
- Below a floor height — neutral, no chord held

The learner is taught the *same three gestures* they'd be taught under §1.3. Only the
recognition mechanism changes, from classification to localisation. This is the cheapest
item in this document and the one with the least thesis narrative cost (§7) — it also
retires ADR-0014's uncomfortable compromise ("ii is recognised by verticality because
azimuth isn't observable") by replacing a proxy classifier with an honest continuous one.

**What it buys:** no per-feature Schmitt trigger, no ambiguity-hold state, arguably no
fixed 120 ms confirmation window at all (settling into a landmark is distinguishable from
passing through it by velocity, which the SDK already exposes reliably — D4). Wrist
position is the most robustly tracked signal on this hardware. Tension becomes a
continuous, felt gradient instead of three snapshots — arguably a *better* embodiment of
prepare → tension → resolve than three discrete symbols, since the voicing or a visual
parameter can track position continuously between landmarks.

**Risk:** "is this still a gesture?" Yes — a postural one. It preserves every co-design
*principle*: broad, fluid, ergonomic, no individual finger bends, one clear semantic
metaphor per hand.

### B — Point-and-Pinch Melody (right hand)

Keep ADR-0019's ten stops on the shoulder-centred arc exactly as they are geometrically —
five scale-degree columns × two octave rows, **re-pitched, never re-arranged**, so §3.1's
stable spatial map is untouched. Change what a stop *is*: not a volume the fingertip
enters, but a label on a continuum.

- Selection point = thumb/index midpoint. The nearest stop is always armed and glows.
- A pinch rising edge sounds it. Holding the pinch and sweeping re-arms and re-fires on
  each new nearest stop — a glissando, debounced by the existing 80 ms per-target
  cooldown (`MelodyEngine.RetriggerCooldownSeconds`, unchanged).
- **Dynamics come from pinch-closing speed**, not translation along an approach axis. This
  is visible to the tracker, expressive, and independent of sweep direction — directly
  answering D7, the failure that killed the plane-crossing redesign.
- Stops become flat discs facing the learner. Depth carries no meaning (D2 dissolved).

`TargetVolume`, the entry-velocity gate, and the depth-readout disc all become unused
under this mechanic; kept behind the bake-off switch during evaluation, deleted from
whichever loses (CLAUDE.md rule 9 — prefer deleting to adding).

### C — Call and Response (curriculum)

Promote L8 (currently last) toward the centre of the curriculum instead of its capstone.
System plays two bars; learner answers two bars. The constraint relaxes across repetitions:
copy exactly → copy the rhythm with any legal notes → copy the melodic shape → answer
freely. This is close to how jazz phrasing is actually taught, and it targets the hardest,
least-served item in §1.2 — real-time improvisational decision-making. The present
curriculum hands the learner ten legal notes and no *reason* to choose among them; a
question demands an answer. §7's `[OPEN]` preference for a pre-authored phrase bank over
generated phrases still stands and gets more load-bearing under this design.

### D — Motif Sculpting (curriculum, optional)

A phrase the learner just played becomes a visible, graspable object. Pinch-grab it and
stretch it (augment the rhythm), lift it (transpose within the chord), flip it (invert),
pull off a copy (repeat). The transformation *is* the concept: "variation" stops being a
word on a slide and becomes a physical act performed on a thing.

Grab-and-manipulate is the best-proven idiom in hand-tracked VR generally, it's turn-based
so it carries no real-time latency pressure, and a tracking dropout mid-manipulation is
fully recoverable (nothing is lost, the object just stops moving). It is the mechanic
L6/L7 currently lack entirely. But it is an *editor*, not an *instrument* — it cannot carry
real-time improvisation on its own, so it complements A and B rather than replacing them.
Scoped here; not part of the near-term bake-off.

### E — Embodied Pulse (timing/swing)

Teach timing and swing through whole-body motion before any note exists. The learner sways
or bounces; the system reads pulse from head-Y and wrist-Y trajectories and shows their
groove against the beat as one restrained, redundant visual cue — never a number, never a
mid-phrase correctness interruption (§3.7 already forbids that for onset scoring, and the
same logic applies here). Swing becomes a *felt* long–short lopsided sway rather than a
0.66 ratio recited at the learner.

Today L2 and L4 teach rhythm through discrete note onsets — the least reliable signal this
system reads. Gross body motion is the most reliable one available. `Core/Timing/Metronome`
and `SwingQuantizer`, and `Core/Evaluation/OnsetScorer`, are already pure and already
tested against arbitrary onset streams; none of them care whether the onsets came from a
finger or a torso. Estimated at 2–3 engineering days, and it stands on its own regardless
of which melody/harmony mechanic wins the bake-off.

### F — Sequential Attention (fallback of last resort)

System loops the harmony; the learner plays melody only, then the two roles swap. Never
both hands demanding attention at once. This dissolves D3 completely and is the cheapest
item in this document to build — but it spends §1.3's cognitive-split-across-hands, stated
as *the system's central design claim*. That is the largest thesis cost anything here
carries. Held in reserve only for the case where simultaneous two-hand tracking proves
unworkable on whatever headset the study actually runs on (§7's open item).

---

## 4. Reuse map

Every candidate is a different Unity-side adapter feeding the same pure domain — this is
`ARCHITECTURE.md`'s ports-and-adapters design (§2.2 of `CLAUDE.md`) paying for itself
exactly as intended, and it's the reason a pivot is affordable inside the remaining
schedule at all.

| Existing code | Reused by |
|---|---|
| `Core/Melody/LazyRecenter.cs`, `LazyRecenterSettings.cs` | A — the body-anchored posture frame |
| `Core/Harmony/HarmonyEngine.cs` (`SetHeldFunction`) | A — unchanged entry point |
| `Core/Melody/MelodyEngine.cs` (`TriggerTarget`) | B — unchanged entry point |
| `Core/Music/ChordToneSet.cs` | B — degree slots, floor MIDI 72, not pitch-sorted |
| `Presentation/TouchTargetRig.cs` | B — arc geometry kept exactly as ADR-0019 left it |
| `Core/Timing/Metronome.cs`, `SwingQuantizer.cs` | E |
| `Core/Evaluation/OnsetScorer.cs` | E — already onset-source-agnostic |
| `Core/Gesture/HandPoseRecording.cs` + `ReplayHandPoseSource.cs` | all — offline evaluation against fixtures |
| `Core/Lessons/ModeGatedNoteSink.cs`, `ModePolicy.cs` | all — mode gating doesn't care which mechanic produced the note |

**One new port, added alongside the existing one, not in place of it.** `HandPoseFrame`
carries a `HandPoseCandidate` enum (`None/Ii/V/I/Ambiguous`) that a continuous field like A
cannot express. A second port, `IHandPostureSource` (anchored position + an openness
scalar), lets both the discrete and continuous recognisers run side by side during the
bake-off; whichever loses, its port is deleted afterward rather than left half-used. A's
2D-to-landmark resolution is a pure `HarmonicField` type in `Core`, unit-tested and
runnable against recorded fixtures headless, matching §2.6's standing instruction not to
iterate on thresholds by repeatedly donning the headset.

---

## 5. Process finding: decide on-device, in one session, not across six

Six ADRs, six rebuilds, six headset sessions, and a still-unvalidated `_pinchSpike` sitting
behind a bool field. That pattern, not any single mechanic, is the highest-leverage thing
to fix here.

**A bake-off harness:** one scene, every candidate mechanic behind the same seam, switched
at runtime, telemetry tagged per mechanic. A, B, and E get evaluated in **one** headset
session instead of three. And because `HandPoseRecording`/`ReplayHandPoseSource` already
exist (§2.6), the harness can score every candidate against **one** recorded freeform
session offline, before any headset time is spent at all — `HandPoseFixture_M3.txt` (5356
frames, already recorded) is a ready template for that first pass.

---

## 6. Bake-off plan and decision gate

**Phase 0 — this document.** Done.

**Phase 1 — harness (≈2 days).** Runtime mechanic switch; `IHandPostureSource` port;
per-mechanic telemetry tags. No new mechanics yet.

**Phase 2 — build the three candidates (≈5 days).**
`HarmonicField` in `Core` plus its adapter (A), with edit-mode tests in the same change
(CLAUDE.md rule 6). Promote `_pinchSpike` into the real mechanic described in §3 (B),
removing its current radius-based miss state. Build the embodied-pulse readout (E).

**Phase 3 — one device session (≈1 day).** A, B, and E against the current mechanic,
measured, not estimated: intended-vs-sounded note ratio, spurious chord changes,
tracking-loss interval count/duration, and the §4.3 latency percentiles from the existing
`LatencyProbe`/`LatencyRecorder`.

**Phase 4 — decide and record (≈1 day).** Write the ADR. Update `CLAUDE.md` §1.3/§3.3/§3.4
to whichever mechanic wins. Delete the code for whichever loses. Add the resulting Chapter
4/5 prose changes to `DECISIONS.md`'s "known thesis-prose changes pending" list — the
agent does not edit the paper (CLAUDE.md rule 7).

**Decision gate.** If nothing beats the current mechanic on the Phase 3 numbers, ship the
current mechanic and keep this document as a Chapter 7 future-work contribution. The two
weeks are not wasted either way — the D1–D7 synthesis in §2 is new, real, and citable
regardless of what gets built next.

**Phase 5 — curriculum, deferred.** C and D are scoped here but not committed; build only
what Session 1 needs, and only if A/B win outright.

---

## 7. The thesis narrative

The predictable panel question: *"You co-designed gestures with expert pianists, then
didn't use them. Why did Objective 1 matter?"* Two true answers belong in the write-up
together:

**Co-design produced principles, not only poses.** Broad fluid ergonomic motion; no
individual finger bends; a cognitive split across hands; the learner constructs every
melody; tension-and-release as the organising metaphor. Every one of those survives intact
in A, B, C, and E — only the *realisation* changes. ADR-0014 already sets the precedent:
a co-designed pose ("palm facing the user's right") turned out to be physically
unobservable by the tracker and had to be re-keyed on a reliable correlate while the
*taught* gesture stayed identical. Design A is the same move, made once, honestly, instead
of patched pose-by-pose.

**The failure to realise the poses literally is itself a finding.** Which co-designed
gestures survive contact with real optical hand tracking, and why, is a genuine
contribution — there are seven ADRs of evidence for it (§2 of this document is their
synthesis). Framed this way, the pivot is not "co-design was abandoned"; it is "Objective
1's output was tested against Objective 2's engineering reality, and the mismatch is
documented" — which defends better than a thesis that quietly pretended the first pass
worked.

The generalisable claim this exploration is building toward:

> Design mid-air musical interaction for the *actual* reliability profile of optical hand
> tracking — gross posture, self-contact, lateral and vertical position — rather than for
> an idealised one, and let the system perform the quantisation a learner structurally
> cannot perform unaided in mid-air.

---

## 8. Verification standard for anything built from this document

- **Headless first.** Every new `Core` type ships with edit-mode tests in the same change
  (CLAUDE.md rule 6). `dotnet test DotNet/Jazztures.sln` must stay green — 294 tests as of
  this document (PATH needs `C:\Program Files\dotnet`).
- **Offline against fixtures before device time** (§2.6). Score each candidate against a
  recorded freeform session first.
- **On-device numbers are measured, not estimated** (§4.3): intended-vs-sounded notes,
  spurious chord changes, tracking-loss intervals, latency percentiles. These numbers go
  in the resulting ADR and are candidates for Chapter 7.
- **The current mechanic stays runnable throughout.** The bake-off switch means the study
  build is never broken while this is explored.

---

## Open questions carried forward

- **Study headset is still `[OPEN]`** (§7 of `CLAUDE.md`). This document designs for
  Quest 2 as the floor. A confirmed Quest 3/3S would relax D3 and permit a wider melody
  arc; resolving this before Phase 3 changes what "success" looks like in the bake-off.
- **C and D are larger than the likely remaining budget.** Scoped, not committed.
- **Backing track for Compose-on-the-Fly** and the **Q&A phrase bank** remain `[OPEN]`
  (§7) and become load-bearing if C is adopted.
