#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>The promo's panel trim (promo/final/03 and 05), drawn with Painter2D over a panel: a soft shadow outside its
    /// edge, a thin gold rule inset inside the border, and a gold corner bracket with a small diamond at each corner; with
    /// <see cref="Filigree"/>, also a second rule and a scroll at each corner (HUD round 1, #398). It is
    /// an absolute overlay that fills its parent (StoryOfTheLand.uss ".ui-frame"), takes no clicks and lays nothing out, so a
    /// panel's layout is the same with or without it. Inside a panel that clips (overflow: hidden) the shadow is clipped
    /// away and only the trim shows. In UXML: <c>&lt;StoryOfTheLand.Game.UI.UiFrame /&gt;</c> as a panel's last child, so the trim is drawn over the content (the
    /// top bar's shadow-only frame is its first child: it draws nothing inside).</summary>
    [UxmlElement]
    public partial class UiFrame : VisualElement
    {
        public const string ClassName = "ui-frame";
        static readonly Color Gold = new Color32(196, 160, 92, 255);

        /// <summary>False drops the corner brackets (a slim bar keeps only the rule and the shadow); UXML "corners".</summary>
        [UxmlAttribute] public bool Corners { get => corners; set { corners = value; MarkDirtyRepaint(); } }
        /// <summary>False drops the outer shadow; UXML "shadow".</summary>
        [UxmlAttribute] public bool Shadow { get => shadow; set { shadow = value; MarkDirtyRepaint(); } }
        /// <summary>False drops the inset gold rule; UXML "rule".</summary>
        [UxmlAttribute] public bool Rule { get => rule; set { rule = value; MarkDirtyRepaint(); } }
        /// <summary>True adds the promo's filigree: a second inset rule and a scroll at each corner; UXML "filigree".</summary>
        [UxmlAttribute] public bool Filigree { get => filigree; set { filigree = value; MarkDirtyRepaint(); } }
        bool corners = true, shadow = true, rule = true, filigree;

        public UiFrame()
        {
            AddToClassList(ClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        void Draw(MeshGenerationContext mgc)
        {
            var r = new Rect(0, 0, layout.width, layout.height);
            if (!(r.width > 8) || !(r.height > 8)) return;
            var p = mgc.painter2D;
            p.lineJoin = LineJoin.Miter;
            if (shadow)
            {
                // Rings outside the parent's 1 px border, fading out over 12 px and weighted downwards (light from above).
                for (int i = 1; i <= 12; i++)
                {
                    var t = 1f - i / 13f;
                    p.strokeColor = new Color(0f, 0f, 0f, 0.20f * t * t);
                    p.lineWidth = 1.05f;
                    var g = 1f + i;
                    StrokeRect(p, new Rect(r.x - g, r.y - g + i * 0.25f, r.width + 2 * g, r.height + 2 * g + i * 0.25f));
                }
            }
            // The inner rule.
            const float inset = 4f;
            if (rule)
            {
                p.strokeColor = new Color(Gold.r, Gold.g, Gold.b, 0.30f);
                p.lineWidth = 1f;
                StrokeRect(p, new Rect(r.x + inset, r.y + inset, r.width - 2 * inset, r.height - 2 * inset));
                // The promo's double rule: a fainter second line just inside the first, as four plain quads.
                if (filigree)
                {
                    const float inner = inset + FiligreeGap;
                    InnerRule(mgc, new Rect(r.x + inner, r.y + inner, r.width - 2 * inner, r.height - 2 * inner), new Color(Gold.r, Gold.g, Gold.b, 0.16f));
                }
            }
            if (!corners) return;
            // Corner brackets over the rule, with a diamond on each corner.
            float leg = Mathf.Min(16f, Mathf.Min(r.width, r.height) / 4f), d = 2.6f;
            p.strokeColor = Gold; p.lineWidth = 1.6f; p.fillColor = Gold;
            foreach (var (cx, cy, sx, sy) in new[] { (r.xMin + inset, r.yMin + inset, 1f, 1f), (r.xMax - inset, r.yMin + inset, -1f, 1f),
                                                     (r.xMin + inset, r.yMax - inset, 1f, -1f), (r.xMax - inset, r.yMax - inset, -1f, -1f) })
            {
                p.BeginPath(); p.MoveTo(new Vector2(cx + sx * leg, cy)); p.LineTo(new Vector2(cx, cy)); p.LineTo(new Vector2(cx, cy + sy * leg)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(cx + sx * 4f, cy + sy * 2.4f)); p.LineTo(new Vector2(cx + sx * 9f, cy + sy * 2.4f)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(cx + sx * 2.4f, cy + sy * 4f)); p.LineTo(new Vector2(cx + sx * 2.4f, cy + sy * 9f)); p.Stroke();
                p.BeginPath(); p.MoveTo(new Vector2(cx, cy - d)); p.LineTo(new Vector2(cx + d, cy)); p.LineTo(new Vector2(cx, cy + d)); p.LineTo(new Vector2(cx - d, cy)); p.ClosePath(); p.Fill();
            }
            if (filigree)
            {
                var ornament = Ornament.Get(r.height >= TallPanel);
                float size = Ornament.Units * leg / 16f;
                foreach (var (cx, cy, sx, sy) in new[] { (r.xMin + inset, r.yMin + inset, 1f, 1f), (r.xMax - inset, r.yMin + inset, -1f, 1f),
                                                         (r.xMin + inset, r.yMax - inset, 1f, -1f), (r.xMax - inset, r.yMax - inset, -1f, -1f) })
                    Ornament.Quad(mgc, ornament, cx, cy, sx, sy, size, Gold);
            }
        }

        static void InnerRule(MeshGenerationContext mgc, Rect r, Color colour)
        {
            const float w = 1f;
            Solid(mgc, new Rect(r.xMin, r.yMin, r.width, w), colour); Solid(mgc, new Rect(r.xMin, r.yMax - w, r.width, w), colour);
            Solid(mgc, new Rect(r.xMin, r.yMin + w, w, r.height - 2 * w), colour); Solid(mgc, new Rect(r.xMax - w, r.yMin + w, w, r.height - 2 * w), colour);
        }

        static void Solid(MeshGenerationContext mgc, Rect r, Color colour)
        {
            var m = mgc.Allocate(4, 6);
            foreach (var v in new[] { new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax) })
                m.SetNextVertex(new Vertex { position = new Vector3(v.x, v.y, Vertex.nearZ), tint = colour });
            m.SetNextIndex(0); m.SetNextIndex(1); m.SetNextIndex(2); m.SetNextIndex(0); m.SetNextIndex(2); m.SetNextIndex(3);
        }

        /// <summary>Below this height a panel (the date plate) gets no scroll down its sides: there the side scrolls would
        /// reach its lines of text.</summary>
        public const float TallPanel = 120f;
        /// <summary>The space between the rule and the filigree's second rule.</summary>
        public const float FiligreeGap = 3f;

        /// <summary>The promo's corner filigree (promo/final/03): from the end of each bracket leg a scroll curls back
        /// towards the panel's inside (down the sides only on a panel at least TallPanel high), and a leaf lies on the
        /// corner's diagonal. Rasterised once, in code, into a small alpha texture (no asset) and drawn as one textured quad
        /// per corner, mirrored, tinted gold and scaled with the bracket's leg. Drawn as Painter2D curves instead, the
        /// filigree cost 3-5 ms of p95 at DPR 2 on the heavy diorama02 frame (#401, bisected to the filigree); a textured
        /// quad is the cheapest thing UI Toolkit draws.</summary>
        public static class Ornament
        {
            /// <summary>The corner's extent in design units (a bracket leg is 16).</summary>
            public const float Units = 27f;
            /// <summary>The texture's side in pixels: about 2.4 pixels a unit, enough for DPR 2 at the full 16-unit leg.</summary>
            public const int Size = 64;
            static Texture2D? withSides, topOnly;

            /// <summary>The ornament texture (made on first use and kept): with the scroll down the side, or without it.</summary>
            public static Texture2D Get(bool sides)
            {
                ref var t = ref sides ? ref withSides : ref topOnly;
                if (t == null) t = Make(sides);
                return t;
            }

            /// <summary>One quad from the corner (cx, cy) inwards (sx, sy), size x size, tinted.</summary>
            public static void Quad(MeshGenerationContext mgc, Texture2D texture, float cx, float cy, float sx, float sy, float size, Color tint)
            {
                // UVs in the texture's own 0..1 space: UI Toolkit remaps them into the atlas itself (MeshWriteData.uvRegion is
                // obsolete in Unity 6.3 for that reason).
                var m = mgc.Allocate(4, 6, texture);
                (float u, float v)[] corners = { (0, 0), (1, 0), (1, 1), (0, 1) };
                foreach (var (u, v) in corners)
                    m.SetNextVertex(new Vertex
                    {
                        position = new Vector3(cx + sx * u * size, cy + sy * v * size, Vertex.nearZ),
                        tint = tint,
                        // texture row 0 is the bottom, design y grows downwards
                        uv = new Vector2(u, 1f - v),
                    });
                // UI Toolkit wants clockwise triangles on screen; a quad mirrored on one axis reverses the order
                if (sx * sy > 0) { m.SetNextIndex(0); m.SetNextIndex(1); m.SetNextIndex(2); m.SetNextIndex(0); m.SetNextIndex(2); m.SetNextIndex(3); }
                else { m.SetNextIndex(0); m.SetNextIndex(2); m.SetNextIndex(1); m.SetNextIndex(0); m.SetNextIndex(3); m.SetNextIndex(2); }
            }

            /// <summary>Coverage, 2x2 supersampled, of the scroll strokes (half width 0.55 units) and the filled leaf.</summary>
            static Texture2D Make(bool sides)
            {
                var strokes = new System.Collections.Generic.List<Vector2[]>
                {
                    Curve(new Vector2(16, 0), new Vector2(22, 0), new Vector2(26, 3.6f), new Vector2(23, 6.4f), new Vector2(21, 8.2f), new Vector2(18.6f, 6.6f), new Vector2(20.2f, 4.6f)),
                };
                if (sides) strokes.Add(Curve(new Vector2(0, 16), new Vector2(0, 22), new Vector2(3.6f, 26), new Vector2(6.4f, 23), new Vector2(8.2f, 21), new Vector2(6.6f, 18.6f), new Vector2(4.6f, 20.2f)));
                var leaf = Curve(new Vector2(5.2f, 5.2f), new Vector2(8.8f, 5.8f), new Vector2(10.6f, 7.6f), new Vector2(11.2f, 11.2f), new Vector2(7.6f, 10.6f), new Vector2(5.8f, 8.8f), new Vector2(5.2f, 5.2f));
                const float half = 0.55f, unitsPerPixel = Units / Size;
                var pixels = new Color32[Size * Size];
                for (int py = 0; py < Size; py++)
                    for (int px = 0; px < Size; px++)
                    {
                        float a = 0f;
                        for (int sy = 0; sy < 2; sy++)
                            for (int sx = 0; sx < 2; sx++)
                            {
                                var q = new Vector2((px + 0.25f + 0.5f * sx) * unitsPerPixel, (py + 0.25f + 0.5f * sy) * unitsPerPixel);
                                float d = float.MaxValue;
                                foreach (var line in strokes) d = Mathf.Min(d, Distance(line, q));
                                bool hit = d <= half || Inside(leaf, q);
                                if (hit) a += 0.25f;
                            }
                        // design y grows downwards; texture row 0 is the bottom
                        pixels[(Size - 1 - py) * Size + px] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = sides ? "UiFrameOrnament" : "UiFrameOrnamentTop", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                t.SetPixels32(pixels); t.Apply(false, false);
                return t;
            }

            /// <summary>Two cubic Bezier segments (p0, c1, c2, p3, c4, c5, p6), flattened to 32 points.</summary>
            static Vector2[] Curve(params Vector2[] k)
            {
                var pts = new Vector2[33];
                for (int i = 0; i <= 32; i++)
                {
                    int seg = i < 16 ? 0 : 1; float t = (i - seg * 16) / 16f; int o = seg * 3;
                    float u = 1 - t;
                    pts[i] = u * u * u * k[o] + 3 * u * u * t * k[o + 1] + 3 * u * t * t * k[o + 2] + t * t * t * k[o + 3];
                }
                return pts;
            }

            static float Distance(Vector2[] line, Vector2 q)
            {
                float best = float.MaxValue;
                for (int i = 0; i + 1 < line.Length; i++)
                {
                    var a = line[i]; var ab = line[i + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                    best = Mathf.Min(best, (a + t * ab - q).magnitude);
                }
                return best;
            }

            static bool Inside(Vector2[] poly, Vector2 q)
            {
                bool inside = false;
                for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                    if ((poly[i].y > q.y) != (poly[j].y > q.y) && q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                        inside = !inside;
                return inside;
            }
        }

        static void StrokeRect(Painter2D p, Rect r)
        {
            p.BeginPath(); p.MoveTo(new Vector2(r.xMin, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMin)); p.LineTo(new Vector2(r.xMax, r.yMax));
            p.LineTo(new Vector2(r.xMin, r.yMax)); p.ClosePath(); p.Stroke();
        }
    }
}
