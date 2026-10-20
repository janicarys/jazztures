using Jazztures.Config;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// One floating label on a ghost hand (ADR-0058): a note letter over a ghost fingertip
    /// (<see cref="RightGhostHandView"/>) or the chord's name over the ghost's hand
    /// (<see cref="GhostHandView"/>). It looks and lights exactly as the same label on the learner's own hand
    /// does (<see cref="FingerNoteLabels"/>, <see cref="ChordNameLabel"/>) - the same size and colour from
    /// <see cref="MelodyConfig"/>, the same <see cref="LabelGlow"/> - so a learner reads the ghost's label and
    /// their own as the same thing.
    ///
    /// <para>
    /// A plain object, not a component: it owns one <see cref="TextMesh"/> under the view that made it, and the
    /// view tells it each frame where to hang and whether to show.
    /// </para>
    /// </summary>
    internal sealed class GhostLabel
    {
        private readonly TextMesh _text;
        private readonly LabelGlow _glow;
        private float _flash;
        private float _emphasis = 1f;       // 1 = as the learner's own labels rest, 0 = quiet
        private float _emphasisTarget = 1f; // a label is at full strength unless a view says otherwise

        /// <param name="parent">The view's transform; the label is its child and goes with it.</param>
        /// <param name="sizeVersusLetter">1 for a single letter; larger for a longer name.</param>
        public GhostLabel(Transform parent, string name, MelodyConfig config, float sizeVersusLetter = 1f)
        {
            _glow = new LabelGlow(config);
            float size = (config != null ? config.LabelCharacterSize : 0.005f) * sizeVersusLetter;

            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);

            _text = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.font = font;
            _text.fontSize = 90;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.characterSize = size;
            _text.color = _glow.Resting;
            _text.text = string.Empty;

            if (font != null && go.TryGetComponent(out MeshRenderer renderer))
            {
                renderer.sharedMaterial = font.material;
                renderer.sortingOrder = 20;
            }

            go.SetActive(false);
        }

        /// <summary>What the label says; empty hides it.</summary>
        public string Text
        {
            get => _text.text;
            set => _text.text = value;
        }

        /// <summary>
        /// How opaque the label is on top of everything else, 0..1 (1 = as it is). A view that moves a label in
        /// or out of sight - the note lanes (ADR-0060) - sets it each frame; it does not ease.
        /// </summary>
        public float Fade { get; set; } = 1f;

        /// <summary>Light up now - the thing the label names has just been played.</summary>
        public void Flash() => _flash = 1f;

        /// <summary>
        /// Whether this is a label to look at now: true is full strength, false fades it and shrinks it a little.
        /// A label nobody calls this on stays at full strength. The change eases in, not snaps.
        /// </summary>
        public void Cue(bool cued) => _emphasisTarget = cued ? 1f : 0f;

        /// <summary>Out of sight now. Safe to call as a scene is torn down, when the label may already be gone.</summary>
        public void Hide()
        {
            if (_text != null && _text.gameObject.activeSelf)
            {
                _text.gameObject.SetActive(false);
            }
        }

        /// <summary>Settle at once, with no glow left over - a new phrase starts at rest.</summary>
        public void Reset()
        {
            _flash = 0f;
            _emphasis = _emphasisTarget;
        }

        /// <summary>
        /// Count the glow down, then hang the label at <paramref name="position"/>, turned to face
        /// <paramref name="viewer"/>, if it is <paramref name="visible"/> and has something to say.
        /// </summary>
        public void Place(bool visible, Vector3 position, Vector3 viewer, float deltaSeconds)
        {
            _flash = _glow.Decay(_flash, deltaSeconds); // counts down out of sight too
            _emphasis = Mathf.MoveTowards(_emphasis, _emphasisTarget, deltaSeconds / _glow.CueSeconds);

            bool show = visible && _text.text.Length > 0;
            if (_text.gameObject.activeSelf != show)
            {
                _text.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            _text.transform.position = position;

            Vector3 away = position - viewer;
            if (away.sqrMagnitude > 1e-6f)
            {
                _text.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }

            _glow.Apply(_text, _flash, _emphasis);

            if (Fade < 1f)
            {
                Color color = _text.color;
                color.a *= Mathf.Clamp01(Fade);
                _text.color = color;
            }
        }
    }
}
