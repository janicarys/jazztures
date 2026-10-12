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

        [Test]
        public void ANextFinger_CanPress_WhileThePreviousIsStillHeldDown()
        {
            Move(Thumb, 0.40f, 6);

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
        }
    }
}
