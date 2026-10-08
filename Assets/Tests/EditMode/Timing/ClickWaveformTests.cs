using System;
using Jazztures.Core.Timing;
using NUnit.Framework;

namespace Jazztures.Tests.EditMode.Timing
{
    public class ClickWaveformTests
    {
        private const int Rate = 48000;

        private static float[] Click(double frequencyHz, double durationSeconds, double peak = 1.0)
        {
            var buffer = new float[ClickWaveform.SampleCountFor(Rate, durationSeconds)];
            ClickWaveform.Render(buffer, Rate, frequencyHz, peak);
            return buffer;
        }

        private static int ZeroCrossings(float[] samples)
        {
            int crossings = 0;
            for (int i = 1; i < samples.Length; i++)
            {
                if ((samples[i - 1] < 0f && samples[i] > 0f) || (samples[i - 1] > 0f && samples[i] < 0f))
                {
                    crossings++;
                }
            }

            return crossings;
        }

        [Test]
        public void SampleCountFor_FollowsDurationAndRate()
        {
            Assert.That(ClickWaveform.SampleCountFor(48000, 0.020), Is.EqualTo(960));
            Assert.That(ClickWaveform.SampleCountFor(44100, 0.020), Is.EqualTo(882));
        }

        [Test]
        public void ATwentyMillisecondTwoKilohertzClick_HasTheRightLengthAndPitch()
        {
            float[] click = Click(2000.0, 0.020);

            Assert.That(click.Length, Is.EqualTo(960));

            // 40 cycles in 20 ms → 80 zero crossings; the cosine tail and the envelope
            // shave a couple off the ends, so allow a small tolerance.
            Assert.That(ZeroCrossings(click), Is.EqualTo(80).Within(4));
        }

        [Test]
        public void IsAudible_ButNeverClips()
        {
            float[] click = Click(2000.0, 0.020);

            float peak = 0f;
            foreach (float s in click)
            {
                peak = Math.Max(peak, Math.Abs(s));
            }

            Assert.That(peak, Is.GreaterThan(0.5f));
            Assert.That(peak, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void PeakScalesTheAmplitude()
        {
            float[] loud = Click(2000.0, 0.020, peak: 1.0);
            float[] quiet = Click(2000.0, 0.020, peak: 0.25);

            for (int i = 0; i < loud.Length; i++)
            {
                Assert.That(quiet[i], Is.EqualTo(loud[i] * 0.25f).Within(1e-5f), $"sample {i}");
            }
        }

        [Test]
        public void StartsAndEndsAtZero_SoItCannotPop()
        {
            float[] click = Click(2000.0, 0.020);

            Assert.That(click[0], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(click[click.Length - 1], Is.EqualTo(0f).Within(1e-6f));
        }

        [Test]
        public void DecaysOverTheBurst()
        {
            float[] click = Click(2000.0, 0.020);

            Assert.That(MaxAbs(click, 0, 240), Is.GreaterThan(MaxAbs(click, 240, 480)));
            Assert.That(MaxAbs(click, 240, 480), Is.GreaterThan(MaxAbs(click, 480, 720)));
            Assert.That(MaxAbs(click, 720, 960), Is.LessThan(0.05f), "near silence by the tail");
        }

        [Test]
        public void ADifferentFrequency_ChangesThePitch()
        {
            int low = ZeroCrossings(Click(1000.0, 0.020));
            int high = ZeroCrossings(Click(3000.0, 0.020));

            Assert.That(high, Is.GreaterThan(low * 2));
        }

        [Test]
        public void IsDeterministic()
        {
            Assert.That(Click(2800.0, 0.020), Is.EqualTo(Click(2800.0, 0.020)));
        }

        [Test]
        public void AnEmptyBuffer_IsLeftAlone()
        {
            Assert.DoesNotThrow(() => ClickWaveform.Render(new float[0], Rate, 2000.0));
        }

        [Test]
        public void BadArguments_AreRejected()
        {
            var buffer = new float[100];

            Assert.Throws<ArgumentNullException>(() => ClickWaveform.Render(null!, Rate, 2000.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClickWaveform.Render(buffer, 0, 2000.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClickWaveform.Render(buffer, Rate, 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClickWaveform.Render(buffer, Rate, Rate / 2.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClickWaveform.Render(buffer, Rate, 2000.0, peak: 1.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => ClickWaveform.SampleCountFor(Rate, 0.0));
        }

        private static float MaxAbs(float[] samples, int from, int to)
        {
            float max = 0f;
            for (int i = from; i < to; i++)
            {
                max = Math.Max(max, Math.Abs(samples[i]));
            }

            return max;
        }
    }
}
