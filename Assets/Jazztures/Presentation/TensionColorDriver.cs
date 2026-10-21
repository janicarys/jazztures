using System.Collections.Generic;
using Jazztures.Config;
using Jazztures.Core.Harmony;
using Jazztures.Events;
using Oculus.Interaction;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// Drives the tension-arc colour (CLAUDE.md §3.10) from the harmony state. Subscribes
    /// to <see cref="ChordChangedChannel"/> and eases a single colour from the old chord's
    /// tint to the new one over <see cref="TensionPalette.TransitionSeconds"/>, so a chord
    /// change is never a jarring snap. That colour is for the <b>left hand</b> only: it is pushed
    /// to the hand's outline here, and worn on the hand's body by <see cref="HandUseTint"/>, which reads
    /// <see cref="Current"/> and <see cref="HasChord"/> (ADR-0065) - the outline path needs a shader with an
    /// outline, which the current hand's does not have. The coming letters on the note lanes wear the same
    /// palette per chord (<see cref="NoteLanes"/>).
    ///
    /// <para>
    /// The right-hand <see cref="TouchTarget"/>s do <b>not</b> follow it (ADR-0044, which
    /// narrows ADR-0017): they are given the palette's neutral colour once and keep it. The
    /// notes they play are written on them as labels instead.
    /// </para>
    ///
    /// <para>
    /// Colour is a <b>redundant</b> channel here (§3.10): the chord is always also audible
    /// and spatially shown, so nothing is lost if a learner cannot resolve the hues.
    /// </para>
    ///
    /// <para>Direction rule (§2.3): this only reads the channel; it never raises it.</para>
    /// </summary>
    public sealed class TensionColorDriver : MonoBehaviour
    {
        [SerializeField] private TensionPalette _palette;

        [SerializeField] private ChordChangedChannel _chordChanged;

        [Tooltip("The touch-target rig whose targets take the chord colour as their base.")]
        [SerializeField] private TouchTargetRig _rig;

        [Tooltip("The left hand visual's MaterialPropertyBlockEditor(s). OVRHandVisual ships "
            + "two — the legacy and OpenXR mesh paths — so write to all of them.")]
        [SerializeField] private MaterialPropertyBlockEditor[] _leftHandMaterialBlocks;

        [Tooltip("Shader colour properties on the hand material to tint. Defaults match "
            + "OculusHand.mat's outline.")]
        [SerializeField] private string[] _handColorProperties = { "_OutlineColor", "_OutlineGlowColor" };

        private Color _from;
        private Color _target;
        private Color _current;
        private float _progress = 1f; // 0 = just changed, 1 = settled
        private bool _dirty = true;

        /// <summary>The colour currently shared by the whole interface.</summary>
        public Color Current => _current;

        /// <summary>
        /// True while a chord is selected (a pose is held); false when none is, and <see cref="Current"/> is easing
        /// to, or resting at, the palette's neutral. What a view that wears the colour asks, so it wears it only while
        /// a pose is held (ADR-0065).
        /// </summary>
        public bool HasChord { get; private set; }

        private void Awake()
        {
            Color neutral = _palette != null ? _palette.Neutral : Color.gray;
            _from = _target = _current = neutral;
        }

        private void OnEnable()
        {
            if (_chordChanged != null)
            {
                _chordChanged.Register(OnChordChanged);
            }

            _dirty = true;
        }

        private void OnDisable()
        {
            if (_chordChanged != null)
            {
                _chordChanged.Unregister(OnChordChanged);
            }
        }

        private void Start()
        {
            // Start, not OnEnable: the rig spawns its targets in Awake, and OnEnable can
            // run before that. By Start the grid exists.
            PushOverlayStyle();
            PushNeutralTargetColor();
            PushHandOutline();
        }

        private void OnChordChanged(ChordChange change)
        {
            HasChord = change.CurrentFunction.HasValue;

            if (_palette == null)
            {
                return;
            }

            _from = _current;
            _target = _palette.For(change.CurrentFunction);
            _progress = 0f;
        }

        private void Update()
        {
            if (_palette == null)
            {
                return;
            }

            if (_progress < 1f)
            {
                float duration = Mathf.Max(_palette.TransitionSeconds, 0.0001f);
                _progress = Mathf.Clamp01(_progress + Time.deltaTime / duration);
                _current = Color.Lerp(_from, _target, Mathf.SmoothStep(0f, 1f, _progress));
                _dirty = true;
            }

            if (!_dirty)
            {
                return;
            }

            _dirty = false;
            PushHandOutline();
        }

        private void PushOverlayStyle()
        {
            if (_palette == null || _rig == null)
            {
                return;
            }

            IReadOnlyList<TouchTarget> targets = _rig.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].SetOverlayStyle(_palette.Struck, _palette.HighlightBoost);
            }
        }

        /// <summary>Give every target the neutral colour — once. They never follow the chord (ADR-0044).</summary>
        private void PushNeutralTargetColor()
        {
            if (_rig == null)
            {
                return;
            }

            Color neutral = _palette != null ? _palette.Neutral : Color.gray;
            IReadOnlyList<TouchTarget> targets = _rig.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].SetBaseColor(neutral);
            }
        }

        private void PushHandOutline()
        {
            if (_leftHandMaterialBlocks == null)
            {
                return;
            }

            foreach (MaterialPropertyBlockEditor editor in _leftHandMaterialBlocks)
            {
                if (editor == null)
                {
                    continue;
                }

                List<MaterialPropertyColor> colors = editor.ColorProperties;
                if (colors == null)
                {
                    colors = new List<MaterialPropertyColor>();
                    editor.ColorProperties = colors;
                }

                foreach (string property in _handColorProperties)
                {
                    SetColorProperty(colors, property, _current);
                }

                editor.UpdateMaterialPropertyBlock();
            }
        }

        private static void SetColorProperty(List<MaterialPropertyColor> colors, string name, Color value)
        {
            for (int i = 0; i < colors.Count; i++)
            {
                if (colors[i].name == name)
                {
                    MaterialPropertyColor entry = colors[i];
                    entry.value = value;
                    colors[i] = entry;
                    return;
                }
            }

            colors.Add(new MaterialPropertyColor { name = name, value = value });
        }
    }
}
