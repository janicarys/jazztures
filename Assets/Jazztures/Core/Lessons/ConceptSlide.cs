using System;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// One caption of a lesson's opening explanation (§3.9 co-design ask) and how long it stays up.
    /// <see cref="Seconds"/> of 0 means "the HUD's default hold, and don't delay the lesson for it" -
    /// how a lesson with a single plain explanation behaves.
    /// </summary>
    public readonly struct ConceptSlide
    {
        public ConceptSlide(string text, float seconds)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("A concept slide needs text.", nameof(text));
            }

            if (seconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "Slide duration cannot be negative.");
            }

            Text = text;
            Seconds = seconds;
        }

        public string Text { get; }

        public float Seconds { get; }
    }
}
