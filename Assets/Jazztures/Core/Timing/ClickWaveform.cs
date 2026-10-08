using System;

namespace Jazztures.Core.Timing
{
    /// <summary>
    /// Synthesises the metronome tick (ADR-0043: generated at start-up, so there is no
    /// audio asset to license or import). A short sine burst with a fast attack, an
    /// exponential decay and a cosine fade to exactly zero at the end — so neither edge
    /// leaves a step that would pop on playback.
    ///
    /// <para>
    /// Pure and Unity-free so it is unit-tested on the desktop CLR; the Unity adapter only
    /// copies the result into an <c>AudioClip</c>.
    /// </para>
    /// </summary>
    public static class ClickWaveform
    {
        /// <summary>Linear fade-in so the burst does not start with a step. `[TUNABLE]`.</summary>
        public const double AttackSeconds = 0.001;

        /// <summary>Decay is <c>exp(-DecayTimeConstants · t / duration)</c>; 5 ≈ −43 dB at the end. `[TUNABLE]`.</summary>
        public const double DecayTimeConstants = 5.0;

        /// <summary>Fraction of the burst, at its end, that is faded to zero. `[TUNABLE]`.</summary>
        public const double TailFadeFraction = 0.1;

        /// <summary>Samples needed for a burst of <paramref name="durationSeconds"/> (at least 1).</summary>
        public static int SampleCountFor(int sampleRate, double durationSeconds)
        {
            RequirePositive(sampleRate, nameof(sampleRate));
            if (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationSeconds), durationSeconds, "Must be finite and positive.");
            }

            return Math.Max(1, (int)Math.Round(sampleRate * durationSeconds));
        }

        /// <summary>
        /// Fill <paramref name="buffer"/> with one click. Its length sets the duration;
        /// <paramref name="peak"/> (0–1) scales the amplitude.
        /// </summary>
        public static void Render(float[] buffer, int sampleRate, double frequencyHz, double peak = 1.0)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            RequirePositive(sampleRate, nameof(sampleRate));
            if (double.IsNaN(frequencyHz) || frequencyHz <= 0.0 || frequencyHz >= sampleRate / 2.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frequencyHz), frequencyHz, "Must be positive and below Nyquist.");
            }

            if (double.IsNaN(peak) || peak < 0.0 || peak > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(peak), peak, "Must be in [0, 1].");
            }

            int n = buffer.Length;
            if (n == 0)
            {
                return;
            }

            int attack = Math.Max(1, (int)Math.Round(AttackSeconds * sampleRate));
            int tail = Math.Max(1, (int)Math.Round(n * TailFadeFraction));
            double omega = 2.0 * Math.PI * frequencyHz / sampleRate;

            for (int i = 0; i < n; i++)
            {
                double envelope = Math.Exp(-DecayTimeConstants * i / n);

                if (i < attack)
                {
                    envelope *= (double)i / attack;
                }

                int intoTail = i - (n - tail);
                if (intoTail > 0)
                {
                    envelope *= 0.5 * (1.0 + Math.Cos(Math.PI * intoTail / tail));
                }

                buffer[i] = (float)(peak * envelope * Math.Sin(omega * i));
            }

            buffer[n - 1] = 0f;
        }

        private static void RequirePositive(int sampleRate, string name)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(name, sampleRate, "Must be positive.");
            }
        }
    }
}
