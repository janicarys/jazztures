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
    /// to <see cref="ChordChangedChannel"/> and eases a single colour — the one the whole
    /// interface shares — from the old chord's tint to the new one over
    /// <see cref="TensionPalette.TransitionSeconds"/>, so a chord change is never a jarring
    /// snap. That colour is pushed to every <see cref="TouchTarget"/> as its base tint and
    /// to the left hand's outline, tying hand and targets into one visual system.
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
            PushBaseColor();
            PushHandOutline();
        }

        private void OnChordChanged(ChordChange change)
        {
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
            PushBaseColor();
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

        private void PushBaseColor()
        {
            if (_rig == null)
            {
                return;
            }

            IReadOnlyList<TouchTarget> targets = _rig.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                targets[i].SetBaseColor(_current);
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
