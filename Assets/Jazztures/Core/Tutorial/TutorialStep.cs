using System;

namespace Jazztures.Core.Tutorial
{
    /// <summary>
    /// One line of the tutorial script (ADR-0053): what to say, what the learner must do before the
    /// next line, what the ghost hand shows meanwhile, and whether the menu is on screen.
    /// Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct TutorialStep
    {
        public TutorialStep(
            string text,
            TutorialGate gate,
            double gateSeconds = 0.0,
            TutorialDemo demo = TutorialDemo.None,
            bool showsMenu = false,
            TutorialForward forward = TutorialForward.None)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("A tutorial step needs text.", nameof(text));
            }

            if (gate == TutorialGate.AfterSeconds && !(gateSeconds > 0.0))
            {
                throw new ArgumentOutOfRangeException(nameof(gateSeconds), gateSeconds, "A timed step needs a positive duration.");
            }

            Text = text;
            Gate = gate;
            GateSeconds = gateSeconds;
            Demo = demo;
            ShowsMenu = showsMenu;
            Forward = forward;
        }

        /// <summary>What the tutorial says. Plain language (§1.5) — no jargon.</summary>
        public string Text { get; }

        public TutorialGate Gate { get; }

        /// <summary>The duration of an <see cref="TutorialGate.AfterSeconds"/> step; unused otherwise.</summary>
        public double GateSeconds { get; }

        public TutorialDemo Demo { get; }

        /// <summary>True from this step on, the real lesson menu is on screen (and the sky comes up).</summary>
        public bool ShowsMenu { get; }

        /// <summary>The menu gestures this step lets through to the real menu.</summary>
        public TutorialForward Forward { get; }

        public override string ToString() => $"[{Gate}] {Text}";
    }
}
