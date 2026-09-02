using System;

namespace Jazztures.Core.Lessons
{
    /// <summary>
    /// The effect half of a <see cref="LessonCue"/> (ADR-0011). A tagged value — the
    /// <see cref="Kind"/> says which fields matter. Immutable value type (ADR-0007).
    /// </summary>
    public readonly struct CueAction
    {
        private CueAction(
            CueActionKind kind,
            string? text,
            int slot,
            int targetIndex,
            TensionColor color,
            bool flag)
        {
            Kind = kind;
            Text = text;
            Slot = slot;
            TargetIndex = targetIndex;
            Color = color;
            Flag = flag;
        }

        public CueActionKind Kind { get; }

        /// <summary>Caption text — <see cref="CueActionKind.ShowText"/>.</summary>
        public string? Text { get; }

        /// <summary>Which caption region — <see cref="CueActionKind.ShowText"/> / <see cref="CueActionKind.HideText"/>.</summary>
        public int Slot { get; }

        /// <summary>Target to emphasise — <see cref="CueActionKind.HighlightTarget"/>.</summary>
        public int TargetIndex { get; }

        /// <summary>Colour band — <see cref="CueActionKind.SetTensionColor"/>.</summary>
        public TensionColor Color { get; }

        /// <summary>On/off — <see cref="CueActionKind.SetScoring"/>.</summary>
        public bool Flag { get; }

        public static CueAction ShowText(string text, int slot = 0)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new CueAction(CueActionKind.ShowText, text, slot, 0, default, false);
        }

        public static CueAction HideText(int slot = 0) =>
            new CueAction(CueActionKind.HideText, null, slot, 0, default, false);

        public static CueAction HighlightTarget(int targetIndex)
        {
            if (targetIndex < 0 || targetIndex >= Music.ChordToneSet.TargetCount)
            {
                throw new ArgumentOutOfRangeException(nameof(targetIndex), targetIndex, null);
            }

            return new CueAction(CueActionKind.HighlightTarget, null, 0, targetIndex, default, false);
        }

        public static CueAction ClearHighlights() =>
            new CueAction(CueActionKind.ClearHighlights, null, 0, 0, default, false);

        public static CueAction SetTensionColor(TensionColor color) =>
            new CueAction(CueActionKind.SetTensionColor, null, 0, 0, color, false);

        public static CueAction WaitForInput() =>
            new CueAction(CueActionKind.WaitForInput, null, 0, 0, default, false);

        public static CueAction SetScoring(bool enabled) =>
            new CueAction(CueActionKind.SetScoring, null, 0, 0, default, enabled);

        public static CueAction AdvancePhase() =>
            new CueAction(CueActionKind.AdvancePhase, null, 0, 0, default, false);

        public override string ToString() => Kind switch
        {
            CueActionKind.ShowText => $"show[{Slot}] \"{Text}\"",
            CueActionKind.HideText => $"hide[{Slot}]",
            CueActionKind.HighlightTarget => $"highlight {TargetIndex}",
            CueActionKind.SetTensionColor => $"colour {Color}",
            CueActionKind.SetScoring => $"scoring {(Flag ? "on" : "off")}",
            _ => Kind.ToString(),
        };
    }
}
