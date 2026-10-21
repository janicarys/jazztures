using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jazztures.Presentation
{
    /// <summary>
    /// The menu's standing reminder of how to drive it (ADR-0062): one dim row under the cards —
    /// <c>Right hand   ◀ ▶ Flick to browse   ▼ Strike to choose</c> — drawn with the arrows as geometry, not
    /// font glyphs. The arrows say <i>which way</i> (the flick runs left or right, the strike runs down) so the words can
    /// stay two or three; and a glyph the built-in font turned out not to carry would show as a blank box on the
    /// headset, where this project cannot look until it is already built. The words say what the motion
    /// <i>does</i>, so the arrows are never the only carrier (§3.10).
    ///
    /// <para>
    /// A plain object, not a component (as <see cref="GhostLabel"/> is): it owns a few <see cref="TextMesh"/>es and
    /// quads under the menu view that made it, and the view tells it how visible to be. It is laid out once, when
    /// built; nothing here runs per frame.
    /// </para>
    ///
    /// <para>
    /// Only the right hand is named, once, because only the right hand is listened to (ADR-0051) — a learner with both
    /// hands up who flicks the left one would otherwise decide the menu is broken. The fist is left off: the Back
    /// cards cover going back, and the fist's other job, leaving a lesson, is not on a screen with a menu.
    /// </para>
    /// </summary>
    internal sealed class MenuControlsHint
    {
        // Used only to space the pieces: a plain text line is about this many ems wide per character. The gaps
        // between pieces are far wider than any error in it, so a text that runs longer than this never touches the next.
        private const float AverageCharEm = 0.55f;

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorIdLegacy = Shader.PropertyToID("_Color");

        private readonly GameObject _root;
        private readonly Mesh _arrowMesh;
        private readonly TextMesh[] _texts;
        private readonly Color[] _textColors;
        private readonly Renderer[] _arrows;
        private readonly Color _arrowColor;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private float _alpha = -1f;

        /// <param name="parent">The menu view's transform; the hint is its child and goes with it.</param>
        /// <param name="arrowMaterial">The view's translucent unlit material (<see cref="RuntimeMaterial"/>); not owned.</param>
        /// <param name="y">Where the row sits, metres below the panel's centre (negative is down).</param>
        /// <param name="characterSize">The view's own text size; <see cref="Style.textScale"/> is taken of it.</param>
        public MenuControlsHint(
            Transform parent,
            Material arrowMaterial,
            Style style,
            float characterSize,
            int fontSize,
            float y,
            Color leadColor,
            Color wordColor,
            Color arrowColor)
        {
            _arrowColor = arrowColor;

            _root = new GameObject("Controls Hint");
            _root.transform.SetParent(parent, worldPositionStays: false);
            _root.transform.localPosition = new Vector3(0f, y, 0f);

            _arrowMesh = CreateArrowMesh();

            TextMesh lead = CreateText("Hand", style.lead, characterSize * style.textScale, fontSize);
            TextMesh flick = CreateText("Flick", style.flick, characterSize * style.textScale, fontSize);
            TextMesh strike = CreateText("Strike", style.strike, characterSize * style.textScale, fontSize);
            _texts = new[] { lead, flick, strike };
            _textColors = new[] { leadColor, wordColor, wordColor };

            Renderer left = CreateArrow("Flick Left", arrowMaterial, style.arrowMetres, flipX: true, turnDown: false);
            Renderer right = CreateArrow("Flick Right", arrowMaterial, style.arrowMetres, flipX: false, turnDown: false);
            Renderer down = CreateArrow("Strike Down", arrowMaterial, style.arrowMetres, flipX: false, turnDown: true);
            _arrows = new[] { left, right, down };

            Layout(style, characterSize * style.textScale * fontSize * 0.1f, left, right, down, lead, flick, strike);
            _root.SetActive(false);
        }

        /// <summary>
        /// How visible the row is, 0..1; 0 puts it out of sight. The view eases this, so the row fades in with the
        /// menu and when the tutorial hands over, and does not flicker with the cards as the screen changes.
        /// </summary>
        public void SetAlpha(float alpha)
        {
            bool visible = alpha > 0.001f;
            if (_root.activeSelf != visible)
            {
                _root.SetActive(visible);
            }

            if (!visible || Mathf.Approximately(alpha, _alpha))
            {
                return;
            }

            _alpha = alpha;

            for (int i = 0; i < _texts.Length; i++)
            {
                _texts[i].color = WithAlpha(_textColors[i], alpha);
            }

            _block.Clear();
            Color tint = WithAlpha(_arrowColor, alpha);
            _block.SetColor(ColorId, tint);
            _block.SetColor(ColorIdLegacy, tint);
            for (int i = 0; i < _arrows.Length; i++)
            {
                _arrows[i].SetPropertyBlock(_block);
            }
        }

        /// <summary>Frees the arrow mesh the hint made. The shared material is the view's to free.</summary>
        public void Destroy()
        {
            if (_arrowMesh != null)
            {
                UnityEngine.Object.Destroy(_arrowMesh);
            }
        }

        /// <summary>
        /// Left to right, centred as a whole: the lead, then the flick (both arrows, then its words), then the strike
        /// (its arrow, then its words). Text is left-aligned at the piece's start so the arrow beside it never moves
        /// when the wording is edited; only the pieces after it do.
        /// </summary>
        private static void Layout(
            Style style,
            float emMetres,
            Renderer left,
            Renderer right,
            Renderer down,
            TextMesh lead,
            TextMesh flick,
            TextMesh strike)
        {
            float charWidth = emMetres * AverageCharEm;
            float arrowW = style.arrowMetres * ArrowAspect; // a sideways arrow's width along the row
            float downW = style.arrowMetres;                // the downward one stands on its side: its base lies along the row
            float arrowGap = arrowW * 0.5f;

            float leadW = string.IsNullOrEmpty(style.lead) ? 0f : style.lead.Length * charWidth;
            float flickW = arrowW * 2f + arrowGap + style.arrowToWordMetres + style.flick.Length * charWidth;
            float strikeW = downW + style.arrowToWordMetres + style.strike.Length * charWidth;
            float total = flickW + style.gapMetres + strikeW + (leadW > 0f ? leadW + style.gapMetres : 0f);

            float x = -total * 0.5f;

            if (leadW > 0f)
            {
                lead.transform.localPosition = new Vector3(x, 0f, 0f);
                x += leadW + style.gapMetres;
            }
            else
            {
                lead.gameObject.SetActive(false);
            }

            left.transform.localPosition = new Vector3(x + arrowW * 0.5f, 0f, 0f);
            x += arrowW + arrowGap;
            right.transform.localPosition = new Vector3(x + arrowW * 0.5f, 0f, 0f);
            x += arrowW + style.arrowToWordMetres;
            flick.transform.localPosition = new Vector3(x, 0f, 0f);
            x += style.flick.Length * charWidth + style.gapMetres;

            down.transform.localPosition = new Vector3(x + downW * 0.5f, 0f, 0f);
            x += downW + style.arrowToWordMetres;
            strike.transform.localPosition = new Vector3(x, 0f, 0f);
        }

        // An arrow is a little wider than tall when it points along its row, so the pair reads as ◀ ▶ and not as dots.
        private const float ArrowAspect = 0.85f;

        private TextMesh CreateText(string name, string text, float characterSize, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, worldPositionStays: false);

            TextMesh mesh = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font;
            mesh.fontSize = fontSize;
            mesh.characterSize = characterSize;
            mesh.anchor = TextAnchor.MiddleLeft;
            mesh.alignment = TextAlignment.Left;
            mesh.text = text ?? string.Empty;

            if (font != null && go.TryGetComponent(out MeshRenderer renderer))
            {
                renderer.sharedMaterial = font.material;
            }

            return mesh;
        }

        private Renderer CreateArrow(string name, Material material, float heightMetres, bool flipX, bool turnDown)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, worldPositionStays: false);

            go.AddComponent<MeshFilter>().sharedMesh = _arrowMesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // The mesh points along +x, `width` long and `heightMetres` across. Flipping x points it left; a quarter turn
            // about z (applied after the scale) points it down, so the same shape stands `width` tall and `heightMetres` wide.
            float width = heightMetres * ArrowAspect;
            go.transform.localScale = new Vector3(flipX ? -width : width, heightMetres, 1f);
            if (turnDown)
            {
                go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            }

            return renderer;
        }

        /// <summary>
        /// A triangle pointing along +x, wound as Unity's own quad is. The runtime material draws both faces, so the
        /// winding is for tidiness, not for being seen.
        /// </summary>
        private static Mesh CreateArrowMesh()
        {
            var mesh = new Mesh { name = "Menu hint arrow" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0f, 0f),
            });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f) });
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        /// <summary>How the row looks and what it says. Cosmetic, so not <c>[TUNABLE]</c> engineering defaults.</summary>
        [Serializable]
        public sealed class Style
        {
            [Tooltip("Metres below the bottom edge of the card row. Clear of the footer (two lines, or three on the " +
                     "post-run screen), which hangs below the cards.")]
            [Min(0f)] public float dropBelowCardsMetres = 0.215f;

            [Tooltip("Size of the words, as a fraction of the menu's own type. Smaller than the footer's on purpose: " +
                     "this is the least important line on the screen.")]
            [Range(0.2f, 1f)] public float textScale = 0.55f;

            [Tooltip("Height of an arrow, metres (its length is a little less).")]
            [Min(0.005f)] public float arrowMetres = 0.026f;

            [Tooltip("Metres between an arrow and the words beside it.")]
            [Min(0f)] public float arrowToWordMetres = 0.015f;

            [Tooltip("Metres between the pieces of the row (the hand, the flick, the strike).")]
            [Min(0f)] public float gapMetres = 0.07f;

            [Tooltip("How visible the row is at rest, 0..1, against the cards' own text.")]
            [Range(0f, 1f)] public float opacity = 0.8f;

            [Tooltip("Seconds the row takes to fade in or out - when the menu appears, and when the tutorial hands over.")]
            [Min(0.01f)] public float fadeSeconds = 0.4f;

            [Header("Words (plain, no jargon - §1.5; keep to a few characters each)")]
            [Tooltip("Which hand drives the menu. Empty leaves it out.")]
            public string lead = "Right hand";

            [Tooltip("Beside the two sideways arrows.")]
            public string flick = "Flick to browse";

            [Tooltip("Beside the downward arrow.")]
            public string strike = "Strike to choose";
        }
    }
}
