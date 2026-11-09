using System.Collections.Generic;
using Jazztures.Core.Melody;
using Jazztures.Core.Music;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Melody
{
    // Cmaj7 is the fallback chord. Its fifteen regions are 72 74 76 79 83 84 86 88 91 95 96 98 100 103 107; Dm7's are
    // 72 74 76 77 81 84 86 88 89 93 96 98 100 101 105. Region 7 is the middle one; region k's centre is
    // (k - 7) * 4.5 cm above the lane origin. Smoothing is off (Core default).
    public class SwingSlicesPipelineTests
    {
        private const double Frame = 1.0 / 90.0;
        private const float Left = -0.08f;
        private const float Right = 0.08f;

        private static float Centre(int region) => (region - 7) * 0.045f;

        private sealed class Hand
        {
            private readonly SwingSlicesPipeline _pipeline;
            public readonly List<SwingSlicesNote> Notes = new List<SwingSlicesNote>();
            public double T;
            public float X = Left;
            public float H;
            public bool Gate = true;

            public Hand(SwingSlicesPipeline pipeline) => _pipeline = pipeline;

            public void Step()
            {
                T += Frame;
                if (_pipeline.Update(new PalmSwingSample(T, Gate, 0f, X, H), out SwingSlicesNote note))
                {
                    Notes.Add(note);
                }
            }

            public void Hold(double seconds)
            {
                for (double e = T + seconds; T < e;)
                {
                    Step();
                }
            }

            // Linear move of lateral position and height over `seconds`.
            public void Move(float toX, float toH, double seconds)
            {
                float x0 = X, h0 = H;
                int frames = (int)System.Math.Ceiling(seconds / Frame);
                for (int i = 1; i <= frames; i++)
                {
                    float f = (float)i / frames;
                    X = x0 + (toX - x0) * f;
                    H = h0 + (toH - h0) * f;
                    Step();
                }
            }

            public void Swing(float toX) => Move(toX, H, 0.1);
        }

        private static (SwingSlicesPipeline, Hand) Make()
        {
            var pipeline = new SwingSlicesPipeline(new PalmSwingParameters(), Chord.Cmaj7);
            return (pipeline, new Hand(pipeline));
        }

        [Test]
        public void ASwing_PlaysTheRegionThePalmIsIn()
        {
            (_, Hand hand) = Make();
            hand.H = Centre(3);
            hand.Hold(0.2);
            hand.Swing(Right);

            Assert.That(hand.Notes.Count, Is.EqualTo(1));
            Assert.That(hand.Notes[0].Region, Is.EqualTo(3));
            Assert.That(hand.Notes[0].Pitch.Midi, Is.EqualTo(79));
        }

        [Test]
        public void EndToEnd_Region3_ThenSilentMoveToRegion6_ThenTwoSwings()
        {
            (_, Hand hand) = Make();
            hand.H = Centre(3);
            hand.Hold(0.2);
            hand.Swing(Right);                       // plays region 3
            hand.Move(Right, Centre(6), 0.3);        // moving up: silent
            hand.Hold(0.2);
            hand.Swing(Left);                        // region 6
            hand.Hold(0.2);
            hand.Swing(Right);                       // region 6 again

            Assert.That(hand.Notes.ConvertAll(n => n.Region), Is.EqualTo(new[] { 3, 6, 6 }));
            Assert.That(hand.Notes.ConvertAll(n => n.Pitch.Midi), Is.EqualTo(new[] { 79, 86, 86 }));
            Assert.That(hand.Notes[1].Swing, Is.EqualTo(SwingDirection.RightToLeft));
            Assert.That(hand.Notes[2].Swing, Is.EqualTo(SwingDirection.LeftToRight));
        }

        [Test]
        public void MovingVertically_NeverSounds_AndRestingAtTheLine_NeverSounds()
        {
            (_, Hand hand) = Make();
            hand.Hold(0.2);
            hand.Move(Left, Centre(8), 0.5);
            hand.Move(Left, Centre(0), 0.5);
            hand.X = 0f;
            hand.Hold(2.0);
            Assert.That(hand.Notes, Is.Empty);
        }

        [Test]
        public void TheRegionIsReadFromBeforeTheCrossing()
        {
            (_, Hand hand) = Make();
            hand.H = Centre(3);
            hand.Hold(0.3);

            // x = -8 cm + 1 m/s * t clears the +1 cm edge of the dead band at t = 0.09 s (sampled at 0.1 s). The height
            // jumps to region 6 at t = 0.08 s: after the 50 ms look-back time (0.05 s), before the crossing.
            double start = hand.T;
            while (hand.T - start < 0.15)
            {
                double t = hand.T + Frame - start;
                hand.X = Left + (float)t;
                hand.H = t >= 0.08 ? Centre(6) : Centre(3);
                hand.Step();
            }

            Assert.That(hand.Notes.Count, Is.EqualTo(1));
            Assert.That(hand.Notes[0].Region, Is.EqualTo(3));
        }

        [Test]
        public void ChordChange_RetunesAtOnce_AndTheRegionsKeepTheirHeight()
        {
            (SwingSlicesPipeline pipeline, Hand hand) = Make();
            hand.H = Centre(3);
            hand.Hold(0.2);
            hand.Swing(Right);
            pipeline.OnChordChanged(Chord.Dm7);
            hand.Hold(0.2);
            hand.Swing(Left);

            Assert.That(hand.Notes.ConvertAll(n => n.Region), Is.EqualTo(new[] { 3, 3 }));
            Assert.That(hand.Notes.ConvertAll(n => n.Pitch.Midi), Is.EqualTo(new[] { 79, 77 }));
        }

        [Test]
        public void ReleasingToNoChord_KeepsTheLastOne()
        {
            (SwingSlicesPipeline pipeline, Hand hand) = Make();
            pipeline.OnChordChanged(Chord.G7);
            pipeline.OnChordChanged(null);
            hand.H = Centre(2);
            hand.Hold(0.2);
            hand.Swing(Right);

            Assert.That(hand.Notes[0].Pitch.Midi, Is.EqualTo(79)); // G7 region 2
        }

        [Test]
        public void NoChordEverHeld_StillPlays_ThroughTheFallback()
        {
            (_, Hand hand) = Make();
            hand.H = Centre(0);
            hand.Hold(0.2);
            hand.Swing(Right);

            Assert.That(hand.Notes[0].Pitch.Midi, Is.EqualTo(72));
        }

        [Test]
        public void HeightsBeyondTheStack_ClampToTheEndRegions()
        {
            (_, Hand hand) = Make();
            hand.H = 1.0f;
            hand.Hold(0.2);
            hand.Swing(Right);
            hand.H = -1.0f;
            hand.Hold(0.3);
            hand.Swing(Left);

            Assert.That(hand.Notes.ConvertAll(n => n.Region), Is.EqualTo(new[] { 14, 0 }));
        }

        [Test]
        public void TheGateClosing_AndReopeningAcrossTheLine_DoesNotSound()
        {
            (_, Hand hand) = Make();
            hand.Hold(0.2);
            hand.Gate = false;
            hand.Hold(0.2);
            hand.X = Right;
            hand.Gate = true;
            hand.Hold(0.5);

            Assert.That(hand.Notes, Is.Empty);
        }

        [Test]
        public void SwingingBackAndForthAtOneHeight_RepeatsTheNote()
        {
            (_, Hand hand) = Make();
            hand.H = Centre(5);
            hand.Hold(0.2);
            for (int i = 0; i < 3; i++)
            {
                hand.Swing(i % 2 == 0 ? Right : Left);
                hand.Hold(0.15);
            }

            Assert.That(hand.Notes.ConvertAll(n => n.Pitch.Midi), Is.EqualTo(new[] { 84, 84, 84 }));
        }
    }
}
