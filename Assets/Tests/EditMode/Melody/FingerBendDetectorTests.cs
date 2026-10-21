using System;
using Jazztures.Core.Melody;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    public class FingerBendDetectorTests
    {
        private const double Frame = 1.0 / 60.0;
        private const int Thumb = 0, Index = 1, Middle = 2, Ring = 3, Pinky = 4;

        private static readonly float Press = FingerBendThresholds.DefaultPressDelta;
        private static readonly float Release = FingerBendThresholds.DefaultReleaseDelta;

        private FingerBendDetector _detector = null!;
        private float[] _curls = null!;

        [SetUp]
        public void SetUp()
        {
            _detector = new FingerBendDetector(FingerBendThresholds.Default);
            _curls = new[] { 0.30f, 0.35f, 0.35f, 0.40f, 0.40f }; // a relaxed hand: already partly bent
            Step(); // the first frame only learns where the fingers are
        }

        private void Step(int frames = 1)
        {
            for (int n = 0; n < frames; n++)
            {
                _detector.Update(_curls, Frame);
            }
        }

        // Moves one finger by `amount` over `frames` frames, returning how many presses each finger got.
        private int[] Move(int finger, float amount, int frames)
        {
            var presses = new int[FingerBendDetector.FingerCount];
            float step = amount / frames;
            for (int n = 0; n < frames; n++)
            {
                _curls[finger] += step;
                _detector.Update(_curls, Frame);
                for (int f = 0; f < presses.Length; f++)
                {
                    if (_detector.JustPressed(f))
                    {
                        presses[f]++;
                    }
                }
            }

            return presses;
        }

        private static int Total(int[] presses)
        {
            int total = 0;
            foreach (int p in presses)
            {
                total += p;
            }

            return total;
        }

        [Test]
        public void ARestingHand_NeverPresses_EvenAtAPartlyBentPosture()
        {
            for (int n = 0; n < 300; n++)
            {
                _detector.Update(_curls, Frame);
                for (int f = 0; f < FingerBendDetector.FingerCount; f++)
                {
                    Assert.That(_detector.JustPressed(f), Is.False);
                }
            }
        }

        [Test]
        public void TrackingNoise_AroundRest_DoesNotPress()
        {
            var rng = new Random(7);
            for (int n = 0; n < 600; n++)
            {
                var noisy = new float[FingerBendDetector.FingerCount];
                for (int f = 0; f < noisy.Length; f++)
                {
                    noisy[f] = _curls[f] + (float)(rng.NextDouble() - 0.5) * 0.06f;
                }

                _detector.Update(noisy, Frame);
                for (int f = 0; f < FingerBendDetector.FingerCount; f++)
                {
                    Assert.That(_detector.JustPressed(f), Is.False, $"noise pressed finger {f} on frame {n}");
                }
            }
        }

        [Test]
        public void ACleanBend_OfOneFinger_PressesThatFingerOnce()
        {
            int[] presses = Move(Middle, 0.40f, frames: 6); // a key-press-sized bend in 100 ms

            Assert.That(presses[Middle], Is.EqualTo(1));
            Assert.That(Total(presses), Is.EqualTo(1), "no other finger sounded");
            Assert.That(_detector.IsDown(Middle), Is.True);
        }

        [Test]
        public void ASmallKeySizedBend_Presses()
        {
            int[] presses = Move(Middle, Press + 0.04f, frames: 6);

            Assert.That(presses[Middle], Is.EqualTo(1), "a gentle bend is enough");
            Assert.That(Total(presses), Is.EqualTo(1));
        }

        [Test]
        public void ABendUnderThePressLevel_DoesNotPress()
        {
            int[] presses = Move(Middle, Press - 0.03f, frames: 6);

            Assert.That(Total(presses), Is.Zero);
        }

        [Test]
        public void AModeratelySlowBend_StillPresses()
        {
            // 0.30 over 0.6 s: a deliberate, unhurried press must not be mistaken for drift.
            int[] presses = Move(Index, 0.30f, frames: 36);

            Assert.That(presses[Index], Is.EqualTo(1));
        }

        [Test]
        public void AHeldBend_DoesNotRepeat()
        {
            Move(Index, 0.40f, 6);

            int held = 0;
            for (int n = 0; n < 600; n++)
            {
                _detector.Update(_curls, Frame);
                held += _detector.JustPressed(Index) ? 1 : 0;
            }

            Assert.That(held, Is.Zero);
            Assert.That(_detector.IsDown(Index), Is.True, "still bent, so still down");
        }

        [Test]
        public void ReleasingThenBendingAgain_PressesAgain()
        {
            Move(Index, 0.40f, 6);
            Move(Index, -0.40f, 6);
            Step(10);

            int[] second = Move(Index, 0.40f, 6);

            Assert.That(second[Index], Is.EqualTo(1));
        }

        [Test]
        public void ARestLevelThatChanges_StillReArms_AndPressesAgain()
        {
            // The finger presses, then comes back up a good way — but settles MORE bent than it began.
            // Measured from the old resting level it never "released", so the next press was silent.
            // Measured from its own peak it is re-armed, and the next bend sounds.
            Move(Middle, 0.45f, 6);                  // press: 0.35 -> 0.80
            Move(Middle, -(0.45f - 0.17f), 6);       // settles at 0.52: 0.17 above where it began
            Step(10);

            int[] again = Move(Middle, 0.30f, 6);

            Assert.That(again[Middle], Is.EqualTo(1), "a second note, though the rest level moved");
        }

        [Test]
        public void AWobbleAtThePeak_SmallerThanTheFall_DoesNotReTrigger()
        {
            Move(Ring, 0.40f, 6);
            float peak = _curls[Ring];

            int again = 0;
            for (int n = 0; n < 120; n++)
            {
                _curls[Ring] = peak - (n % 2 == 0 ? 0f : Release - 0.02f);
                _detector.Update(_curls, Frame);
                again += _detector.JustPressed(Ring) ? 1 : 0;
            }

            Assert.That(again, Is.Zero, "hysteresis");
        }

        [Test]
        public void AfterReArming_ANewPressNeedsAFullRiseFromTheDip()
        {
            Move(Ring, 0.40f, 6);
            Move(Ring, -0.20f, 6);                   // falls well past the release level: re-armed
            Step(5);

            int[] smallRise = Move(Ring, Press - 0.04f, 6);
            int[] restOfRise = Move(Ring, 0.08f, 3);

            Assert.That(Total(smallRise), Is.Zero, "not yet a full rise from the dip");
            Assert.That(restOfRise[Ring], Is.EqualTo(1));
        }

        [Test]
        public void ASlowDrift_OfOneFinger_IsNotAPress()
        {
            int[] presses = Move(Middle, 0.25f, frames: 600); // 0.25 over ten seconds

            Assert.That(Total(presses), Is.Zero);
        }

        [Test]
        public void ClosingTheWholeHand_PressesNothing()
        {
            int total = 0;
            for (int n = 0; n < 8; n++)
            {
                for (int f = 0; f < _curls.Length; f++)
                {
                    _curls[f] += 0.08f;
                }

                _detector.Update(_curls, Frame);
                for (int f = 0; f < FingerBendDetector.FingerCount; f++)
                {
                    total += _detector.JustPressed(f) ? 1 : 0;
                }
            }

            Assert.That(total, Is.Zero, "a fist is not five key presses");
        }

        [Test]
        public void ANeighbourDraggedAlong_ByNearlyAsMuch_PressesNeitherFinger_AndReportsBothBlocked()
        {
            var presses = new int[FingerBendDetector.FingerCount];
            bool ringBlocked = false;
            for (int n = 0; n < 6; n++)
            {
                _curls[Ring] += 0.050f;   // 0.30 total
                _curls[Pinky] += 0.047f;  // 0.28 total — within the isolation margin of the ring
                _detector.Update(_curls, Frame);
                for (int f = 0; f < presses.Length; f++)
                {
                    presses[f] += _detector.JustPressed(f) ? 1 : 0;
                }

                ringBlocked |= _detector.Blocked(Ring);
            }

            Assert.That(Total(presses), Is.Zero, "ambiguous which finger was meant — say nothing");
            Assert.That(ringBlocked, Is.True, "…but say why, for diagnostics");
        }

        [Test]
        public void ANeighbourDraggedAlong_ByMuchLess_StillLetsTheBentFingerPress()
        {
            var presses = new int[FingerBendDetector.FingerCount];
            for (int n = 0; n < 6; n++)
            {
                _curls[Ring] += 0.065f;   // 0.39 total
                _curls[Pinky] += 0.020f;  // 0.12 total
                _detector.Update(_curls, Frame);
                for (int f = 0; f < presses.Length; f++)
                {
                    presses[f] += _detector.JustPressed(f) ? 1 : 0;
                }
            }

            Assert.That(presses[Ring], Is.EqualTo(1));
            Assert.That(presses[Pinky], Is.Zero);
        }

        // Tendon coupling: bend the middle finger and the ring follows, a little behind and a little less, and the pinky
        // follows the ring. Each finger below rises by `ratio` times the driver's stroke, starting `lag` frames later.
        private int[] Stroke(int driver, float amount, int frames, params (int finger, float ratio, int lagFrames)[] dragged)
        {
            var presses = new int[FingerBendDetector.FingerCount];
            for (int n = 0; n < frames + 6; n++)
            {
                if (n < frames)
                {
                    _curls[driver] += amount / frames;
                }

                foreach ((int finger, float ratio, int lagFrames) in dragged)
                {
                    int k = n - lagFrames;
                    if (k >= 0 && k < frames)
                    {
                        _curls[finger] += amount * ratio / frames;
                    }
                }

                _detector.Update(_curls, Frame);
                for (int f = 0; f < presses.Length; f++)
                {
                    presses[f] += _detector.JustPressed(f) ? 1 : 0;
                }
            }

            return presses;
        }

        // The bug this guards: the held driver used to be dropped from the isolation comparison (so legato works), and the
        // driver is exactly the finger dragging its neighbours - so once it had pressed, the ring only had to beat the pinky,
        // and then the pinky only had to beat everything left. Middle -> ring -> pinky, one bend, three notes.
        [Test]
        public void ADraggedNeighbour_DoesNotPress_AfterTheFingerDraggingItHasPressed()
        {
            int[] presses = Stroke(Middle, 0.42f, frames: 6, (Ring, 0.70f, 1), (Pinky, 0.45f, 2));
            int[] after = Move(Middle, 0f, 120); // and nothing fires late, either

            Assert.That(presses[Middle], Is.EqualTo(1));
            Assert.That(presses[Ring] + after[Ring], Is.Zero, "the ring was only dragged along");
            Assert.That(presses[Pinky] + after[Pinky], Is.Zero, "and the pinky was dragged by the ring");
        }

        [Test]
        public void TheSameDrag_WithTheScenesRealThresholds_DoesNotCascadeEither()
        {
            // Config/MelodyConfig.asset as it stands: press 0.20, release 0.05, drift 2 s, isolation margin 0.10.
            _detector = new FingerBendDetector(new FingerBendThresholds(0.20f, 0.05f, 2.0f, 0.10f));
            _curls = new[] { 0.30f, 0.35f, 0.35f, 0.40f, 0.40f };
            Step();

            int[] presses = Stroke(Middle, 0.45f, frames: 6, (Ring, 0.75f, 1), (Pinky, 0.50f, 2));
            int[] after = Move(Middle, 0f, 120);

            Assert.That(presses[Middle], Is.EqualTo(1));
            Assert.That(presses[Ring] + after[Ring] + presses[Pinky] + after[Pinky], Is.Zero);
        }

        [Test]
        public void ADragAcrossTheRingAndPinky_FromTheRing_PressesOnlyTheRing()
        {
            int[] presses = Stroke(Ring, 0.42f, frames: 6, (Pinky, 0.70f, 1), (Middle, 0.40f, 1));
            int[] after = Move(Ring, 0f, 120);

            Assert.That(presses[Ring], Is.EqualTo(1));
            Assert.That(presses[Pinky] + after[Pinky] + presses[Middle] + after[Middle], Is.Zero);
        }

        [Test]
        public void ASlowDeliberatePress_DoesNotLetItsNeighbourFireLater_EitherWhileItIsStillMoving()
        {
            // A 0.6 s press: the ring, at half the middle's rise, crosses the press level long after the middle did.
            int[] presses = Stroke(Middle, 0.45f, frames: 36, (Ring, 0.50f, 2));
            int[] after = Move(Middle, 0f, 120);

            Assert.That(presses[Middle], Is.EqualTo(1));
            Assert.That(presses[Ring] + after[Ring], Is.Zero);
        }

        [Test]
        public void AFingerThatBendsClearlyMoreThanTheDriver_StillPresses()
        {
            // Not every simultaneous movement is drag: a ring that really out-bends the middle is the finger meant.
            int[] presses = Stroke(Middle, 0.30f, frames: 6, (Ring, 1.60f, 0));

            Assert.That(presses[Ring], Is.EqualTo(1));
            Assert.That(presses[Middle], Is.Zero);
        }

        [Test]
        public void ANeighbourThatPressesOnItsOwn_AfterTheDriverHasStopped_PressesNormally()
        {
            Stroke(Middle, 0.40f, frames: 6, (Ring, 0.60f, 1));
            Step(12); // the middle has stopped moving; the hand is still

            int[] second = Move(Ring, 0.45f, frames: 6);

            Assert.That(second[Ring], Is.EqualTo(1), "legato, a beat later");
        }

        [Test]
        public void ADraggedFinger_CanPressAgain_OnceItHasRelaxedAndIsBentOnPurpose()
        {
            Stroke(Middle, 0.42f, frames: 6, (Ring, 0.70f, 1));
            Move(Middle, -0.42f, 6);   // everything lets go
            Move(Ring, -0.29f, 6);
            Step(20);

            int[] ring = Move(Ring, 0.40f, 6);

            Assert.That(ring[Ring], Is.EqualTo(1), "a deliberate press, after the drag is over");
        }

        [Test]
        public void TheDragVeto_CanBeSwitchedOff_AndThenBehavesAsBefore()
        {
            // couplingSeconds 0 = the old rule: a held finger is never consulted. Documented so the knob means something.
            _detector = new FingerBendDetector(new FingerBendThresholds(0.14f, 0.07f, 2.0f, 0.06f, couplingSeconds: 0f));
            _curls = new[] { 0.30f, 0.35f, 0.35f, 0.40f, 0.40f };
            Step();

            int[] presses = Stroke(Middle, 0.42f, frames: 6, (Ring, 0.70f, 1), (Pinky, 0.45f, 2));

            Assert.That(presses[Ring], Is.EqualTo(1), "the old behaviour, on request");
        }

        [Test]
        public void ADraggedFinger_IsReported_WithTheFingerThatDraggedIt()
        {
            bool dragged = false;
            int by = -1;
            for (int n = 0; n < 12; n++)
            {
                if (n < 6) _curls[Middle] += 0.07f;
                if (n >= 1 && n < 7) _curls[Ring] += 0.05f;
                _detector.Update(_curls, Frame);
                if (_detector.WasDragged(Ring))
                {
                    dragged = true;
                    by = _detector.BlockedBy(Ring);
                }
            }

            Assert.That(dragged, Is.True);
            Assert.That(by, Is.EqualTo(Middle), "for the diagnostic log: who it was dragged by");
        }

        [Test]
        public void AHeldFingersJitter_IsNotMovement_SoItNeverBlocksTheNextNote()
        {
            Move(Thumb, 0.40f, 6);
            var rng = new Random(3);
            float held = _curls[Thumb];
            for (int n = 0; n < 60; n++) // a second of a noisy, held thumb
            {
                _curls[Thumb] = held + (float)(rng.NextDouble() * 0.03);
                _detector.Update(_curls, Frame);
            }

            _curls[Thumb] = held;
            int[] next = Move(Index, 0.40f, 6);

            Assert.That(next[Index], Is.EqualTo(1), "tracking noise on a held finger is not a finger on the move");
        }

        // The thumb is the noisiest finger. A single bad frame is not a press a thumb can make.
        [Test]
        public void AOneFrameThumbSpike_IsNotAPress()
        {
            int pressed = 0;
            _curls[Thumb] += 0.50f;
            _detector.Update(_curls, Frame);
            pressed += _detector.JustPressed(Thumb) ? 1 : 0;

            _curls[Thumb] -= 0.50f;
            for (int n = 0; n < 30; n++)
            {
                _detector.Update(_curls, Frame);
                pressed += _detector.JustPressed(Thumb) ? 1 : 0;
            }

            Assert.That(pressed, Is.Zero);
        }

        [Test]
        public void AThumbBend_ThatHolds_PressesOnTheFrameItIsConfirmed()
        {
            _curls[Thumb] += 0.50f;
            _detector.Update(_curls, Frame);
            Assert.That(_detector.JustPressed(Thumb), Is.False, "the first frame only qualifies it");

            _detector.Update(_curls, Frame);
            Assert.That(_detector.JustPressed(Thumb), Is.True, "the second confirms it");
        }

        [Test]
        public void AFingerThatIsNotTheThumb_IsJudgedOnTheFirstFrame()
        {
            _curls[Index] += 0.50f;
            _detector.Update(_curls, Frame);

            Assert.That(_detector.JustPressed(Index), Is.True, "no added latency for the four fingers");
        }

        [Test]
        public void TheThumbConfirmation_CanBeSwitchedOff()
        {
            _detector = new FingerBendDetector(new FingerBendThresholds(0.14f, 0.07f, 2.0f, 0.06f, thumbConfirmFrames: 1));
            _curls = new[] { 0.30f, 0.35f, 0.35f, 0.40f, 0.40f };
            Step();

            _curls[Thumb] += 0.50f;
            _detector.Update(_curls, Frame);

            Assert.That(_detector.JustPressed(Thumb), Is.True);
        }

        // ---- Near misses and stroke peaks: diagnostics only (ADR-0075) - they say why a note did not sound. ----

        // Steps the hand through `frames` frames, applying `perFrame` to the curls each time, and returns what the detector said.
        private (int nearMisses, float peak, int driver, int presses) Watch(int finger, int frames, System.Action<int> perFrame)
        {
            int nearMisses = 0, driver = -2, presses = 0;
            float peak = 0f;
            for (int n = 0; n < frames; n++)
            {
                perFrame(n);
                _detector.Update(_curls, Frame);
                if (_detector.NearMiss(finger))
                {
                    nearMisses++;
                    peak = _detector.NearMissPeak(finger);
                    driver = _detector.NearMissDrivenBy(finger);
                }

                presses += _detector.JustPressed(finger) ? 1 : 0;
            }

            return (nearMisses, peak, driver, presses);
        }

        [Test]
        public void ACurlThatFallsShortOfThePressLevel_IsReportedAsANearMiss_WithHowFarItGot()
        {
            // Default press level 0.14; the ring rises 0.10 (past 0.6 of it, so an attempt) and goes back down.
            var result = Watch(Ring, 24, n => _curls[Ring] += n < 6 ? 0.10f / 6 : n < 10 ? 0f : -0.10f / 8);

            Assert.That(result.presses, Is.Zero);
            Assert.That(result.nearMisses, Is.EqualTo(1));
            Assert.That(result.peak, Is.EqualTo(0.10f).Within(0.02f));
            Assert.That(result.driver, Is.EqualTo(-1), "nothing else was pressed: a weak curl of its own");
            Assert.That(_detector.PressLevel(Ring), Is.EqualTo(Press));
        }

        [Test]
        public void ACurlThatPresses_IsNotANearMiss_AndItsStrokeIsReportedWhenItLetsGo()
        {
            int nearMisses = 0, releases = 0;
            float strokePeak = 0f;
            for (int n = 0; n < 30; n++)
            {
                _curls[Ring] += n < 6 ? 0.30f / 6 : n < 12 ? 0f : -0.30f / 8;
                _detector.Update(_curls, Frame);
                nearMisses += _detector.NearMiss(Ring) ? 1 : 0;
                if (_detector.Released(Ring))
                {
                    releases++;
                    strokePeak = _detector.ReleasePeak(Ring);
                }
            }

            Assert.That(nearMisses, Is.Zero);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(strokePeak, Is.EqualTo(0.30f).Within(0.03f), "the whole stroke, from the trough it pressed from");
        }

        [Test]
        public void TrackingNoise_IsNotANearMiss()
        {
            var rng = new Random(7);
            for (int n = 0; n < 600; n++)
            {
                var noisy = new float[FingerBendDetector.FingerCount];
                for (int f = 0; f < noisy.Length; f++)
                {
                    noisy[f] = _curls[f] + (float)(rng.NextDouble() - 0.5) * 0.06f;
                }

                _detector.Update(noisy, Frame);
                for (int f = 0; f < FingerBendDetector.FingerCount; f++)
                {
                    Assert.That(_detector.NearMiss(f), Is.False, $"noise was reported as an attempt by finger {f} on frame {n}");
                }
            }
        }

        [Test]
        public void AFingerThatWasBlockedAboveThePressLevel_IsNotANearMiss()
        {
            // The ring and pinky rise together, within the isolation margin: neither is pressed, both are blocked - not "too small".
            int nearMisses = 0;
            for (int n = 0; n < 30; n++)
            {
                float k = n < 6 ? 1f / 6 : n < 12 ? 0f : -1f / 8;
                _curls[Ring] += 0.30f * k;
                _curls[Pinky] += 0.28f * k;
                _detector.Update(_curls, Frame);
                nearMisses += _detector.NearMiss(Ring) ? 1 : 0;
                nearMisses += _detector.NearMiss(Pinky) ? 1 : 0;
            }

            Assert.That(nearMisses, Is.Zero);
        }

        [Test]
        public void ANeighbourDraggedAlongBelowThePressLevel_IsReportedAsANearMiss_WithTheFingerThatDraggedIt()
        {
            // The middle finger presses (0.30) while it moves; the ring follows at 0.4 of it (0.12): past 0.6 of 0.14, under 0.14.
            var result = Watch(Ring, 30, n =>
            {
                float k = n < 6 ? 1f / 6 : n < 12 ? 0f : -1f / 8;
                _curls[Middle] += 0.30f * k;
                _curls[Ring] += 0.12f * k;
            });

            Assert.That(result.presses, Is.Zero);
            Assert.That(result.nearMisses, Is.EqualTo(1));
            Assert.That(result.driver, Is.EqualTo(Middle), "the middle finger was pressed and moving: the ring was dragged, not played");
        }

        [Test]
        public void TheRingsNearMiss_IsJudgedAgainstItsOwnLowerPressLevel()
        {
            // press 0.20, ring x0.5 = 0.10: a 0.09 curl is past 0.6 of 0.10 and under it, so a near miss; the index's 0.09 is not an attempt.
            _detector = new FingerBendDetector(new FingerBendThresholds(
                0.20f, 0.05f, 2.0f, 0.10f, coupledPressScale: 1f, coupledIsolationScale: 1f, ringPinkyPressScale: 0.5f));
            _curls = new[] { 0.30f, 0.35f, 0.35f, 0.40f, 0.40f };
            Step();

            Assert.That(_detector.PressLevel(Ring), Is.EqualTo(0.10f).Within(1e-6));
            Assert.That(Watch(Ring, 24, n => _curls[Ring] += n < 6 ? 0.09f / 6 : n < 10 ? 0f : -0.09f / 8).nearMisses, Is.EqualTo(1));
            Assert.That(Watch(Index, 24, n => _curls[Index] += n < 6 ? 0.09f / 6 : n < 10 ? 0f : -0.09f / 8).nearMisses, Is.Zero);
        }

        // ADR-0073: the middle, ring and pinky share a flexor muscle, so the detector asks less of them. The scene's levels
        // (press 0.20, margin 0.10) with the config's default scales (press x0.75, margin x0.6).
        private void UseScaledThresholds(float pressScale = 0.75f, float isolationScale = 0.6f)
        {
            _detector = new FingerBendDetector(new FingerBendThresholds(
                0.20f, 0.05f, 2.0f, 0.10f, coupledPressScale: pressScale, coupledIsolationScale: isolationScale));
            _curls = new[] { 0.30f, 0.35f, 0.35f, 0.40f, 0.40f };
            Step(); // the first frame only learns where the fingers are
        }

        [Test]
        public void TheCoupledFingers_PressOnASmallerBend_ThanTheIndexNeeds()
        {
            UseScaledThresholds(); // index 0.20; middle, ring and pinky 0.15

            Assert.That(Move(Index, 0.17f, 6)[Index], Is.Zero, "the index still needs its full press level");
            Move(Index, -0.17f, 6);
            Step(10);

            foreach (int finger in new[] { Middle, Ring, Pinky })
            {
                Assert.That(Move(finger, 0.17f, 6)[finger], Is.EqualTo(1), $"finger {finger} presses on 0.17");
                Move(finger, -0.17f, 6);
                Step(10);
            }
        }

        [Test]
        public void TheRingAndPinky_CanHaveTheirOwnPressLevel_LowerThanTheMiddlesAndTheIndexs()
        {
            // press 0.20: middle x0.75 = 0.15, ring and pinky x0.6 = 0.12, index and thumb 0.20.
            _detector = new FingerBendDetector(new FingerBendThresholds(
                0.20f, 0.05f, 2.0f, 0.10f, coupledPressScale: 0.75f, coupledIsolationScale: 0.3f, ringPinkyPressScale: 0.6f));
            _curls = new[] { 0.30f, 0.35f, 0.35f, 0.40f, 0.40f };
            Step();

            Assert.That(Move(Middle, 0.13f, 6)[Middle], Is.Zero, "0.13 is under the middle finger's 0.15");
            Move(Middle, -0.13f, 6);
            Step(10);
            foreach (int finger in new[] { Ring, Pinky })
            {
                Assert.That(Move(finger, 0.13f, 6)[finger], Is.EqualTo(1), $"finger {finger} presses on 0.13 (its own level is 0.12)");
                Move(finger, -0.13f, 6);
                Step(10);
            }
        }

        [Test]
        public void TheRingAndPinkyScale_FallsBackToTheCoupledScale_WhenNotGiven()
        {
            var t = new FingerBendThresholds(0.20f, 0.05f, 2f, 0.10f, coupledPressScale: 0.75f);

            Assert.That(t.RingPinkyPressScale, Is.EqualTo(0.75f));
            Assert.That(t.PressDeltaFor(Ring), Is.EqualTo(t.PressDeltaFor(Middle)));
        }

        [Test]
        public void TheCoupledScale_IsNeutralByDefault_SoEveryFingerKeepsTheOneLevel()
        {
            // SetUp's detector is FingerBendThresholds.Default, with no scales.
            Assert.That(Move(Ring, 0.12f, 6)[Ring], Is.Zero, "under the press level, scaled or not");
            Assert.That(FingerBendThresholds.Default.PressDeltaFor(Ring), Is.EqualTo(FingerBendThresholds.Default.PressDeltaFor(Index)));
            Assert.That(FingerBendThresholds.Default.IsolationMarginFor(Ring, Pinky), Is.EqualTo(FingerBendThresholds.Default.IsolationMargin));
        }

        [Test]
        public void TwoFingers_DraggingEachOther_NeedASmallerLead_ToTellWhichWasMeant()
        {
            // The ring rises 0.40 and drags the pinky to 0.38 a frame behind: it leads by under 0.10 throughout. The full
            // margin calls that a draw and says nothing; the scaled one (0.06) gives it to the ring.
            UseScaledThresholds(pressScale: 1f, isolationScale: 1f);
            int[] unscaled = Stroke(Ring, 0.40f, frames: 6, (Pinky, 0.95f, 1));
            Assert.That(unscaled[Ring] + unscaled[Pinky], Is.Zero, "control: with no scaling the ring is not given it");

            UseScaledThresholds(pressScale: 1f, isolationScale: 0.6f);
            int[] presses = Stroke(Ring, 0.40f, frames: 6, (Pinky, 0.95f, 1));
            int[] after = Move(Ring, 0f, 120);

            Assert.That(presses[Ring], Is.EqualTo(1), "the ring led by more than the scaled margin");
            Assert.That(presses[Pinky] + after[Pinky], Is.Zero, "and the pinky was only dragged, now or later");
        }

        [Test]
        public void TheThumb_IsNotAFinger_AndKeepsTheFullMargin()
        {
            // The same near-draw as above, between the index and the thumb: they do not drag each other, so a lead under the
            // full margin is still ambiguous and nothing sounds.
            UseScaledThresholds(pressScale: 1f, isolationScale: 0.6f);
            int[] presses = Stroke(Index, 0.40f, frames: 6, (Thumb, 0.95f, 1));

            Assert.That(Total(presses), Is.Zero);
        }

        [TestCase(Middle, Ring, Pinky)]
        [TestCase(Ring, Pinky, Middle)]
        [TestCase(Index, Middle, Ring)]
        public void AStrokeThatDragsItsNeighbours_StillSoundsOnlyTheFingerMeant_WithTheScaledThresholds(int driver, int near, int far)
        {
            UseScaledThresholds();

            int[] presses = Stroke(driver, 0.45f, frames: 6, (near, 0.75f, 1), (far, 0.50f, 2));
            int[] after = Move(driver, 0f, 120);

            Assert.That(presses[driver], Is.EqualTo(1));
            Assert.That(presses[near] + after[near] + presses[far] + after[far], Is.Zero, "a dragged neighbour sounded");
        }

        [Test]
        public void Thresholds_RejectCoupledScalesThatBreakTheHysteresis()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0.20f, 0.05f, 2f, 0.10f, coupledPressScale: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0.20f, 0.05f, 2f, 0.10f, coupledPressScale: 1.2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0.20f, 0.05f, 2f, 0.10f, coupledIsolationScale: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FingerBendThresholds(0.20f, 0.16f, 2f, 0.10f, coupledPressScale: 0.75f),
                "a release level at or above a scaled press level would never re-arm");
            Assert.DoesNotThrow(() => new FingerBendThresholds(0.20f, 0.05f, 2f, 0.10f, coupledPressScale: 0.75f, coupledIsolationScale: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0.20f, 0.05f, 2f, 0.10f, ringPinkyPressScale: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new FingerBendThresholds(0.20f, 0.05f, 2f, 0.10f, coupledPressScale: 0.75f, ringPinkyPressScale: 0.2f),
                "the release level must sit below the ring and pinky's own, smaller, press level");
        }

        // Many strokes, with the coupling a real hand has: whichever finger is bent drags the others by amounts that fall
        // off with distance, a frame or two late, with tracking noise on every finger throughout. Whatever happens, a
        // neighbour must NEVER sound; and the finger that was meant should sound for most of them (an ambiguous stroke -
        // a neighbour dragged almost as far - is allowed to say nothing, as the isolation margin has always said).
        [TestCase(0.06f, 0.14f, 1f, 1f)]
        [TestCase(0.10f, 0.20f, 1f, 1f)] // the scene asset's margin and press level
        [TestCase(0.10f, 0.20f, 0.75f, 0.6f)] // ...with the coupled fingers' scales (ADR-0073)
        [TestCase(0.10f, 0.20f, 0.75f, 0.3f)] // ...and the smaller margin ADR-0074 settles on
        public void ManyCoupledStrokes_NeverSoundANeighbour_AndMostSoundTheFingerMeant(
            float margin, float press, float pressScale, float isolationScale)
        {
            _detector = new FingerBendDetector(new FingerBendThresholds(
                press, 0.05f, 2.0f, margin, coupledPressScale: pressScale, coupledIsolationScale: isolationScale));
            var rng = new Random(11);
            _curls = new[] { 0.30f, 0.35f, 0.35f, 0.40f, 0.40f };
            float[] rest = (float[])_curls.Clone();
            Step();

            int strokes = 0, hits = 0, falsePresses = 0;
            for (int s = 0; s < 300; s++)
            {
                int driver = Index + rng.Next(4); // index .. pinky
                float amount = 0.40f + (float)rng.NextDouble() * 0.25f;
                int frames = 5 + rng.Next(4);
                var ratio = new float[FingerBendDetector.FingerCount];
                var lag = new int[FingerBendDetector.FingerCount];
                for (int f = Index; f <= Pinky; f++)
                {
                    int distance = Math.Abs(f - driver);
                    ratio[f] = distance == 0 ? 1f : (0.80f - 0.20f * (distance - 1)) * (0.75f + 0.25f * (float)rng.NextDouble());
                    lag[f] = distance == 0 ? 0 : 1 + rng.Next(2);
                }

                var pressed = new bool[FingerBendDetector.FingerCount];
                for (int n = 0; n < frames + 8; n++)
                {
                    for (int f = Index; f <= Pinky; f++)
                    {
                        int k = n - lag[f];
                        float target = rest[f] + amount * ratio[f] * Math.Min(1f, Math.Max(0f, (k + 1f) / frames));
                        _curls[f] = target + ((float)rng.NextDouble() - 0.5f) * 0.02f;
                    }

                    _detector.Update(_curls, Frame);
                    for (int f = 0; f < pressed.Length; f++)
                    {
                        pressed[f] |= _detector.JustPressed(f);
                    }
                }

                strokes++;
                hits += pressed[driver] ? 1 : 0;
                for (int f = 0; f < pressed.Length; f++)
                {
                    falsePresses += f != driver && pressed[f] ? 1 : 0;
                }

                // let go and rest between strokes
                for (int n = 0; n < 8; n++)
                {
                    for (int f = Index; f <= Pinky; f++)
                    {
                        _curls[f] = Math.Max(rest[f], _curls[f] - 0.12f);
                    }

                    _detector.Update(_curls, Frame);
                }

                for (int f = Index; f <= Pinky; f++)
                {
                    _curls[f] = rest[f];
                }

                Step(36);
            }

            TestContext.WriteLine($"press {press:0.00} margin {margin:0.00} scales {pressScale:0.00}/{isolationScale:0.00}: {hits} of {strokes} strokes sounded the finger meant, {falsePresses} neighbours sounded");
            Assert.That(falsePresses, Is.Zero, "a dragged neighbour sounded");
            Assert.That(hits, Is.GreaterThanOrEqualTo((int)(strokes * 0.70)), $"only {hits} of {strokes} strokes sounded the finger meant");
        }

        [Test]
        public void ANextFinger_CanPress_WhileThePreviousIsStillHeldDown()
        {
            Move(Thumb, 0.40f, 6);
            Step(12); // the thumb has stopped moving: this is a second note, not a neighbour dragged by the first

            int[] second = Move(Middle, 0.40f, 6);

            Assert.That(second[Middle], Is.EqualTo(1), "legato");
            Assert.That(_detector.IsDown(Thumb), Is.True);
        }

        [Test]
        public void AHeldFinger_ReArmsTheMomentItRelaxes_HoweverLongItWasHeld()
        {
            Move(Index, 0.40f, 6);
            Step(60 * 10);
            Assert.That(_detector.IsDown(Index), Is.True);

            Move(Index, -(Release + 0.03f), 3);

            Assert.That(_detector.IsDown(Index), Is.False, "a finger can never be left dead");
        }

        [Test]
        public void AfterReset_TheFirstFrameOnlyLearnsWhereTheFingersAre()
        {
            _detector.Reset();
            _curls[Middle] = 0.95f; // the hand comes back already bent

            _detector.Update(_curls, Frame);

            Assert.That(_detector.JustPressed(Middle), Is.False);
            Assert.That(_detector.IsDown(Middle), Is.False);
        }

        [Test]
        public void Reset_ClearsAHeldFinger()
        {
            Move(Index, 0.40f, 6);

            _detector.Reset();

            Assert.That(_detector.IsDown(Index), Is.False);
        }

        [Test]
        public void ABadCurlArray_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => _detector.Update(null!, Frame));
            Assert.Throws<ArgumentException>(() => _detector.Update(new float[4], Frame));
        }

        [Test]
        public void AnOutOfRangeFinger_IsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _detector.JustPressed(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _detector.IsDown(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => _detector.Blocked(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => _detector.LastDelta(5));
        }

        [Test]
        public void Thresholds_RejectNonsense()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0f, 0f, 0.4f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0.2f, 0.2f, 0.4f, 0.1f), "no hysteresis");
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0.2f, 0.1f, 0f, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0.2f, 0.1f, 0.4f, -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0.2f, 0.1f, 0.4f, 0.1f, couplingSeconds: -0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0.2f, 0.1f, 0.4f, 0.1f, couplingSeconds: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FingerBendThresholds(0.2f, 0.1f, 0.4f, 0.1f, thumbConfirmFrames: 0));
        }
    }
}
