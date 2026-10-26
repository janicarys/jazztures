# Handoff: lesson structure after the Oct 16 co-design session (2026-10-22)

From a planning conversation with the student in the claude.ai Jazztures project (not a coding session).
Read this alongside `CLAUDE.md` and `Docs/DECISIONS.md` (latest: ADR-0075). Nothing in the repo was changed
by that conversation; this file is the only addition.

## Status: what is decided vs. proposed

**Adviser sign-off is still pending** on the lesson set. Treat the structure below as the student's working
direction, not a settled `[THESIS]` change. Do **not** edit `CLAUDE.md` §1.2 / §3.8 / §3.9 or thesis prose;
record what you build in an ADR, as ADR-0069/0071 did.

Proposed lesson set (replaces CLAUDE.md §3.9's eight lessons):

| # | Lesson | Hands | State in repo |
|---|---|---|---|
| L1 | Chord tones | Right (system plays chords) | exists |
| L2 | Chords | Left | exists |
| L3 | Swing | Right (system plays chords) | exists (ADR-0071, ADR-0073) |
| L4 | Guided play | Both | `Lesson_L4_GuidedPlay.asset` exists, **uncommitted**, not in an ADR yet |
| L5 | Antecedent–consequent | Both | **to build** |

Free play stays as the open sandbox it already is (commit 0492a88); it is no longer the final *lesson*.
Sequences, motifs, variations as separate lessons are dropped (the Oct 16 teacher said everything after the
motif is already a variation; the student wants a simpler structure).

## Why (for the ADR's rationale)

- **Stopping at Swing stops short of improvisation.** In L1–L3 every note is one the system chose; the learner
  never makes a musical choice of their own. One lesson where they must choose is the minimum to keep the
  "real-time improvisational decision-making" claim. "Motif construction and variation" (§1.2) is dropped.
- **Both hands must appear.** The left = harmony / right = melody split is the central design claim (§1.3).
  A right-hand-only ending would never test it. Hence L4 (guided, both hands) before L5.
- **Antecedent–consequent over open free play:** novices freeze in open bars (Doc Raffy, Sep 19: melody
  exploration is the hardest part); a phrase to answer gives a starting point while every note stays the
  learner's; all participants answer the same prompts, so answers are comparable.
- **Attribution (get this right in the ADR):** the call-and-response *format* is ImproVisAR's Lesson 8 and the
  proposal's L8. The Oct 16 music teacher's contribution was the *name* — "antecedent–consequent" instead of
  "question and answer" (which sounds like a seminar Q&A) — and he suggested it as a lesson.
  Source: project doc `design-session-2026-10-16.md`, [P2 08:22–10:26; P3 00:21–00:36].

## L4 Guided play — notes

The existing asset reuses the Swing patterns with both hands ("up, up, down"). Keep it that way, and:
- Favour **thumb / index / middle** (root, 3rd, 5th). A 1-3-5 pattern is the same finger shape over every
  chord — exactly the teacher's sequence example (Oct 16, [P3 07:52–09:40]) — and avoids the ring/pinky
  reliability problem (ADR-0072–0074).
- Keep left-hand load low: one chord per bar or slower, strike on beat 1 only, slow tempo. This is the
  first time the left hand plays in time (no Timing lesson any more).

## L5 Antecedent–consequent — proposed design (open to the student's changes)

- **Form:** four bars, **ii | V | I | I**. System plays bars 1–2 (ii, V: the antecedent ends on tension);
  learner answers bars 3–4 over I (the consequent resolves). This maps onto the existing
  "getting ready, tension, home" story. Trade-off to raise with the student: the learner's answer only
  ever sits over Cmaj7. Alternative: alternate turns while looping the full progression.
- **Hands in the answer:** learner strikes I with the left hand and plays melody with the right — both
  hands, minimal left-hand load.
- **Flow:** Watch and Listen (system plays antecedent *and* a sample consequent) → answer turns with the
  ghost hand off. Use existing modes; don't invent a new learning mode.
- **Phrase bank:** pre-authored, 3–4 antecedents, same data format as the Swing note patterns, short,
  mostly thumb/index/middle. No generated phrases (§7 already prefers pre-authored).
- **Handover cue:** at 70 BPM two bars is < 7 s, so "your turn" must be unmistakable and not text — e.g.
  ghost hand fades, right-hand letters brighten. Needs a headset check.
- **No correctness scoring of answers** — there is no correct consequent. Log every note (silent-but-logged
  rules as usual); analysis is descriptive (ends on a chord tone? echoes the antecedent's rhythm? range used?).
- **Lesson data, not code** where possible (§3.9). If a new field is needed (e.g. which bars are the
  learner's turn), add it to `LessonDefinition` with a default that leaves L1–L4 unchanged, and write tests.

## Known gaps to surface, not fix silently

1. **No lesson scores timing** since the Timing lesson was deleted (ADR-0069): no Test Yourself, so §3.7
   onset scoring has nowhere to run. Ask the student whether a short Test Yourself pass belongs in Swing or
   Guided play, if the evaluation measures timing.
2. **Finger-bend input vs. co-design:** ADR-0050 says it can't ship until someone cites what in co-design
   distinguishes a key-sized bend from the rejected ones. The Oct 16 session (after ADR-0050) may be that
   evidence if the teacher used the finger-bend build — the student should confirm and record timestamps.
   CLAUDE.md §1.3 / §3.1 still describe touch targets over two octaves.
3. **Five-note limit is now physical** (five fingers). Earlier idea of unlocking the two passing tones from
   Swing onward is dead with finger bends — don't build it.
4. **Lock lesson order before real participants** (M8).
5. Small visual notes from headset screenshots: the left-hand chord name overlaps the hand and runs off the
   left of view; middle/ring fingertip letters crowd each other ("AC" on Dm7, "DF" on G7).

## Suggested first step

Commit or ADR the existing L4 asset (it's untracked), then draft L5's antecedent phrases as data and show
them to the student before wiring anything.
