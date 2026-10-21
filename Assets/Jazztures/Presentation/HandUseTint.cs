using System.Collections.Generic;
using Jazztures.Core.Ports;
using Oculus.Interaction;
using UnityEngine;

namespace Jazztures.Presentation
{
    /// <summary>
    /// How the learner's own hands are drawn, as the running lesson uses them. Two things, both answering the question
    /// "which hand is playing, and what is it playing?" at a glance:
    ///
    /// <list type="bullet">
    ///   <item><b>The hand the lesson does not use is darker and more transparent</b> (ADR-0064). A right-hand lesson
    ///   dims the left, a left-hand one dims the right. With no lesson, or one that uses both, nothing is touched.</item>
    ///   <item><b>The left hand takes the colour of the chord it holds</b> (ADR-0065) — sage for ii, burnt orange for V,
    ///   violet for I, the same colours the coming letters wear on the note lanes
    ///   (<see cref="NoteLanes"/>) — from the moment the pose is held until it is let go. Its <b>outline</b> takes the
    ///   colour in full, which is where CLAUDE.md §3.10 puts it, and its body is tinted toward it. Only the left hand, only
    ///   while it is a hand the lesson uses; the chord's name and the sound say the same thing, so colour is never the
    ///   only carrier.</item>
    /// </list>
    ///
    /// <para>
    /// <b>Which hands these are (ADR-0070).</b> The hands the learner sees are the Interaction SDK's
    /// <c>OVRHandVisualLeft</c> / <c>OVRHandVisualRight</c> (inside the <c>OVRHands</c> prefab), drawn with the
    /// <c>Interaction/OculusHand</c> shader. Each carries <see cref="MaterialPropertyBlockEditor"/>s - one for the OVR
    /// mesh path, one for the OpenXR path - that <b>rewrite the renderer's property block every frame</b> from their own
    /// lists. So this does not touch the renderers or their blocks: it puts its values in those lists, which is how the SDK
    /// expects a hand to be coloured and means nothing can overwrite them. (An earlier version tinted the two Building
    /// Blocks hand meshes in the scene instead; those are switched off and nothing was ever drawn from them.)
    /// </para>
    ///
    /// <para>
    /// <b>This is the only component that writes these properties</b>: the dimming and the tint are worked out together,
    /// here, because two writers to the same lists would overwrite each other. Presentation only (§2.3): it asks
    /// <see cref="IHandsInUse"/> and <see cref="TensionColorDriver"/> and writes to nothing in the domain; the hand is
    /// tracked and recognised exactly as before (§3.8). A hand is dimmed, never hidden.
    /// </para>
    /// </summary>
    public sealed class HandUseTint : MonoBehaviour
    {
        // The hand shader's property names ("Interaction/OculusHand").
        private const string ColorTopName = "_ColorTop";
        private const string ColorBottomName = "_ColorBottom";
        private const string OutlineColorName = "_OutlineColor";
        private const string OpacityName = "_Opacity";
        private const string OutlineOpacityName = "_OutlineOpacity";

        // A dimmed hand stays at least this opaque, so it is always faintly there (§3.5: a hand you cannot see is a hand
        // you cannot tell is tracked).
        private const float MinimumOpacity = 0.12f;

        [Header("Hands")]
        [Tooltip("The LEFT hand visual's MaterialPropertyBlockEditors (OVRHandVisualLeft: the OVR mesh path and the " +
                 "OpenXR path - write to both). Jazztures > Hands > Set Up ... fills this in.")]
        [SerializeField] private MaterialPropertyBlockEditor[] _leftHandBlocks;

        [Tooltip("The RIGHT hand visual's MaterialPropertyBlockEditors (OVRHandVisualRight).")]
        [SerializeField] private MaterialPropertyBlockEditor[] _rightHandBlocks;

        [Tooltip("What says which hands the running lesson uses (an IHandsInUse). Leave empty to use the one on this " +
                 "GameObject - the performance root.")]
        [SerializeField] private Behaviour _source;

        [Header("A hand the lesson does not use (ADR-0064)")]
        [Tooltip("How bright it is, against a hand in use: 1 = the same, 0 = black. [TUNABLE], unmeasured.")]
        [Range(0f, 1f)] [SerializeField] private float _brightness = 0.4f;

        [Tooltip("How opaque it is, against a hand in use: 1 = the same, 0 = invisible. Never goes below a floor, so it is " +
                 "always faintly there. [TUNABLE], unmeasured.")]
        [Range(0f, 1f)] [SerializeField] private float _opacity = 0.35f;

        [Header("The left hand holding a chord (ADR-0065)")]
        [Tooltip("Where the chord colour comes from: the same driver that eases it on every chord change. Empty leaves " +
                 "the left hand its usual colour.")]
        [SerializeField] private TensionColorDriver _tension;

        [Tooltip("How far the left hand takes the chord's colour while it holds the pose: 1 = all the way, 0 = not at " +
                 "all. [TUNABLE], unmeasured.")]
        [Range(0f, 1f)] [SerializeField] private float _chordTint = 0.85f;

        [Tooltip("How bright the hand's BODY gets when tinted, against the outline (which takes the colour at full " +
                 "brightness). The body is dark grey; at 1 it would be as bright as the outline. [TUNABLE], unmeasured.")]
        [Range(0f, 1f)] [SerializeField] private float _bodyTintBrightness = 0.45f;

        [Header("Both")]
        [Tooltip("Seconds a hand takes to dim, come back, take a chord's colour or let it go, so none of it is a pop.")]
        [Min(0.01f)] [SerializeField] private float _fadeSeconds = 0.35f;

        private IHandsInUse _hands;
        private Behaviour _performance; // the thing that says which hands are in use, when it is a component: on = a lesson or free play
        private HandState _left;
        private HandState _right;

        private void Awake()
        {
            _hands = (_source as IHandsInUse) ?? GetComponent<IHandsInUse>();
            if (_hands == null)
            {
                Debug.LogWarning(
                    $"{nameof(HandUseTint)}: nothing says which hands the lesson uses (no {nameof(IHandsInUse)} on " +
                    "this object or in 'Source') - the hands will not be dimmed or coloured.", this);
                enabled = false;
                return;
            }

            _performance = _hands as Behaviour;
            _left = HandState.Of(_leftHandBlocks, "left", this);
            _right = HandState.Of(_rightHandBlocks, "right", this);

            // Once, at start: said plainly so a build that is not doing what it should can be read off adb logcat. The
            // success path logs nothing else, and per-frame loggers scroll the start-up lines away.
            Debug.Log(
                $"[HandUseTint] ready: left hand {Describe(_left)}, right hand {Describe(_right)}, " +
                $"chord colour from {(_tension != null ? "the tension driver" : "nothing (no TensionColorDriver assigned)")}, " +
                $"lesson hands from {_hands.GetType().Name}.", this);
        }

        private static string Describe(HandState hand) =>
            hand == null ? "NOT usable (no property-block editor with the hand shader assigned)" : $"ok ({hand.Editors.Length} editor(s), shader '{hand.ShaderName}')";

        private void Update()
        {
            float step = Time.deltaTime / _fadeSeconds;
            Move(_left, Handedness.Left, step, takesChord: true);
            Move(_right, Handedness.Right, step, takesChord: false); // the right hand plays notes, not chords
        }

        // Switched off, or torn down mid-lesson: leave no hand dim or coloured behind.
        private void OnDisable()
        {
            Restore(_left);
            Restore(_right);
        }

        private void Move(HandState hand, Handedness which, float step, bool takesChord)
        {
            if (hand == null)
            {
                return;
            }

            bool inUse = _hands.IsInUse(which);
            bool changed = false;

            float dimTarget = inUse ? 0f : 1f;
            if (hand.Dim != dimTarget)
            {
                hand.Dim = Mathf.MoveTowards(hand.Dim, dimTarget, step);
                changed = true;
            }

            if ((dimTarget > 0f) != hand.DimLogged)
            {
                hand.DimLogged = dimTarget > 0f;
                Debug.Log($"[HandUseTint] {which} hand {(hand.DimLogged ? "dimmed: the lesson does not use it" : "back to normal")}.", this);
            }

            if (takesChord && _tension != null)
            {
                // Coloured only while the learner's own hand holds a pose: a hand the lesson does not use has its
                // chords selected for it (ADR-0063), and a colour on a hand that is doing nothing would only say it
                // was meant to be doing something. And only while something is being performed: on the menu nothing
                // reads the hand, but the engine still remembers the last chord, which would leave the hand coloured.
                bool performing = _performance == null || _performance.isActiveAndEnabled;
                float tintTarget = performing && inUse && _tension.HasChord ? 1f : 0f;
                if (hand.Tint != tintTarget)
                {
                    hand.Tint = Mathf.MoveTowards(hand.Tint, tintTarget, step);
                    changed = true;
                }

                // Said when it flips, with the three things it depends on, so "the colour never came" can be told
                // apart from "the chord never arrived" and "the lesson says this hand is not in use".
                if ((tintTarget > 0f) != hand.TintLogged)
                {
                    hand.TintLogged = tintTarget > 0f;
                    Debug.Log(
                        $"[HandUseTint] {which} hand chord colour {(hand.TintLogged ? "ON" : "off")} " +
                        $"(chord held: {_tension.HasChord}, hand in use: {inUse}, performing: {performing}, hue {ChordHue.Of(_tension.Current)}).", this);
                }

                // Follow the driver's eased colour while a chord is held; once it is let go, keep the last hue and fade
                // out of it, rather than drifting through the palette's neutral on the way.
                if (_tension.HasChord)
                {
                    Color hue = ChordHue.Of(_tension.Current);
                    if (hue != hand.Hue)
                    {
                        hand.Hue = hue;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Apply(hand);
            }
        }

        private void Restore(HandState hand)
        {
            if (hand == null || !hand.Written)
            {
                return;
            }

            hand.Dim = 0f;
            hand.Tint = 0f;
            Apply(hand);
        }

        /// <summary>Put the hand's look at its current dimming and tint: into the editors' lists, which the SDK applies every frame.</summary>
        private void Apply(HandState hand)
        {
            float tint = hand.Tint * _chordTint;
            float brightness = Mathf.Lerp(1f, _brightness, hand.Dim);

            // Tint first, then dim. The outline takes the chord's colour in full (§3.10: the left hand's outline); the
            // body, which is dark grey, is tinted toward it at a lower brightness, keeping its own top-to-bottom ratio.
            Color bodyHue = Scaled(hand.Hue, _bodyTintBrightness);
            Color top = Color.Lerp(hand.Top, bodyHue, tint);
            Color bottom = Color.Lerp(hand.Bottom, Scaled(bodyHue, hand.BottomShade), tint);
            Color outline = Color.Lerp(hand.Outline, hand.Hue, tint);
            float outlineOpacity = Mathf.Lerp(hand.OutlineOpacity, 1f, tint);

            float opacity = Mathf.Lerp(hand.Opacity, Mathf.Max(hand.Opacity * _opacity, MinimumOpacity), hand.Dim);
            outlineOpacity = Mathf.Lerp(outlineOpacity, Mathf.Max(outlineOpacity * _opacity, MinimumOpacity), hand.Dim);

            foreach (MaterialPropertyBlockEditor editor in hand.Editors)
            {
                SetColor(editor, ColorTopName, Scaled(top, brightness));
                SetColor(editor, ColorBottomName, Scaled(bottom, brightness));
                SetColor(editor, OutlineColorName, Scaled(outline, brightness));
                SetFloat(editor, OpacityName, opacity);
                SetFloat(editor, OutlineOpacityName, outlineOpacity);
                editor.UpdateMaterialPropertyBlock();
            }

            hand.Written = true;
        }

        private static void SetColor(MaterialPropertyBlockEditor editor, string name, Color value)
        {
            List<MaterialPropertyColor> list = editor.ColorProperties;
            if (list == null)
            {
                list = new List<MaterialPropertyColor>();
                editor.ColorProperties = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].name == name)
                {
                    MaterialPropertyColor entry = list[i];
                    entry.value = value;
                    list[i] = entry;
                    return;
                }
            }

            list.Add(new MaterialPropertyColor { name = name, value = value });
        }

        private static void SetFloat(MaterialPropertyBlockEditor editor, string name, float value)
        {
            List<MaterialPropertyFloat> list = editor.FloatProperties;
            if (list == null)
            {
                list = new List<MaterialPropertyFloat>();
                editor.FloatProperties = list;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].name == name)
                {
                    MaterialPropertyFloat entry = list[i];
                    entry.value = value;
                    list[i] = entry;
                    return;
                }
            }

            list.Add(new MaterialPropertyFloat { name = name, value = value });
        }

        private static Color Scaled(Color color, float factor) => new Color(color.r * factor, color.g * factor, color.b * factor, color.a);

        private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>One hand: its editors, the look the material gives it, and how far it is dimmed and tinted.</summary>
        private sealed class HandState
        {
            public MaterialPropertyBlockEditor[] Editors;
            public string ShaderName;
            public Color Top;
            public Color Bottom;
            public Color Outline;
            public float Opacity;
            public float OutlineOpacity;
            public float BottomShade; // the material's bottom colour against its top, so a tinted body keeps its gradient

            public float Dim;          // 0 = as the material has it, 1 = fully dimmed
            public float Tint;         // 0 = no chord colour, 1 = all of it (before the component's own strength)
            public Color Hue = Color.white; // the chord colour being worn, or last worn
            public bool Written;       // true once values have been put in the lists, so they are put back
            public bool DimLogged;     // the last dim state said in the log
            public bool TintLogged;    // the last chord-colour state said in the log

            public static HandState Of(MaterialPropertyBlockEditor[] editors, string side, Object context)
            {
                var usable = new List<MaterialPropertyBlockEditor>();
                Material material = null;
                if (editors != null)
                {
                    foreach (MaterialPropertyBlockEditor editor in editors)
                    {
                        if (editor == null)
                        {
                            continue;
                        }

                        usable.Add(editor);
                        if (material == null && editor.Renderers != null)
                        {
                            foreach (Renderer renderer in editor.Renderers)
                            {
                                if (renderer != null && renderer.sharedMaterial != null)
                                {
                                    material = renderer.sharedMaterial;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (usable.Count == 0 || material == null
                    || !material.HasProperty(ColorTopName) || !material.HasProperty(ColorBottomName)
                    || !material.HasProperty(OutlineColorName) || !material.HasProperty(OpacityName))
                {
                    Debug.LogWarning(
                        $"{nameof(HandUseTint)}: the {side} hand has no property-block editor on a material with " +
                        "_ColorTop / _ColorBottom / _OutlineColor / _Opacity (the Interaction/OculusHand shader) - that hand " +
                        "will not be dimmed or coloured. Run Jazztures > Hands > Set Up ... to assign them.", context);
                    return null;
                }

                Color top = material.GetColor(ColorTopName);
                Color bottom = material.GetColor(ColorBottomName);
                return new HandState
                {
                    Editors = usable.ToArray(),
                    ShaderName = material.shader != null ? material.shader.name : "?",
                    Top = top,
                    Bottom = bottom,
                    Outline = material.GetColor(OutlineColorName),
                    Opacity = material.GetFloat(OpacityName),
                    OutlineOpacity = material.HasProperty(OutlineOpacityName) ? material.GetFloat(OutlineOpacityName) : 1f,
                    BottomShade = Mathf.Clamp(Luma(bottom) / Mathf.Max(Luma(top), 0.01f), 0.2f, 1f),
                };
            }
        }
    }
}
