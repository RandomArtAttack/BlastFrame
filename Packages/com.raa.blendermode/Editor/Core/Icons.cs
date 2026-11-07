using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RAA.BlenderMode
{
    /// <summary>
    /// Icon provider. Looks for a user-supplied PNG at <c>Editor/Icons/{name}.png</c> (drop Blender icon
    /// extractions there); when absent, renders an anti-aliased 32px procedural fallback tinted for the
    /// current editor skin and caches it. Procedural icons are rasterized at 4× supersampling and box-
    /// downsampled for smooth edges, and are drawn to visually match Blender's viewport-header glyphs
    /// (light-grey strokes on transparent, active blue #4772B3). See DESIGN.md §12 / §15.
    /// </summary>
    public static class IconLibrary
    {
        const int Size = 32;
        const int SS = 4;                 // supersample factor
        const int HS = Size * SS;         // hi-res side
        const string IconFolder = "Packages/com.raa.blendermode/Editor/Icons/";

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>Get an icon by logical name, generating a fallback if no PNG is present.</summary>
        public static Texture2D Get(string name)
        {
            string key = name + (EditorGUIUtility.isProSkin ? "#d" : "#l");
            if (Cache.TryGetValue(key, out Texture2D tex) && tex != null)
                return tex;

            tex = LoadFromDisk(name) ?? Generate(name);
            Cache[key] = tex;
            return tex;
        }

        static Texture2D LoadFromDisk(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconFolder + name + ".png");
        }

        // ------------------------------------------------------------------ palette

        static Color Ink => EditorGUIUtility.isProSkin
            ? new Color(0.87f, 0.87f, 0.87f, 1f)   // #DDD on dark
            : new Color(0.20f, 0.20f, 0.20f, 1f);  // dark ink on light

        static readonly Color AxisR = new Color(0.86f, 0.34f, 0.34f);
        static readonly Color AxisG = new Color(0.46f, 0.78f, 0.42f);
        static readonly Color AxisB = new Color(0.36f, 0.56f, 0.95f);
        static readonly Color Blue = new Color(0.28f, 0.45f, 0.70f);

        // ------------------------------------------------------------------ generation

        static Texture2D Generate(string name)
        {
            Color[] hi = new Color[HS * HS]; // transparent
            Color c = Ink;

            switch (name)
            {
                case "orientation": DrawOrientation(hi, c); break;
                case "pivot": DrawPivot(hi, c); break;
                case "cursor": DrawCursor(hi); break;

                // per-value orientation glyphs (dropdown rows + toolbar face)
                case "orient_global": DrawOrientation(hi, c); break;          // globe
                case "orient_local": DrawOrientLocal(hi, c); break;           // box with axes
                case "orient_view": DrawOrientView(hi, c); break;             // monitor
                case "orient_normal": DrawOrientNormal(hi, c); break;         // face + normal arrow
                case "orient_custom": DrawOrientCustom(hi, c); break;         // bookmark + plus

                // per-value pivot-mode glyphs
                case "pivot_median": DrawPivotMedian(hi, c); break;           // dot between dots
                case "pivot_active": DrawPivotActive(hi, c); break;           // outlined dot
                case "pivot_individual": DrawPivotIndividual(hi, c); break;   // multiple dots
                case "pivot_bounds": DrawPivotBounds(hi, c); break;           // dashed box + dot

                // snap-base ("Snap With") glyphs (snapping popover)
                case "snap_base_closest": DrawSnapBaseClosest(hi, c); break;   // box + corner dot
                case "snap_base_center": DrawSnapBaseCenter(hi, c); break;     // box + centre dot
                case "snap_base_median": DrawSnapBaseMedian(hi, c); break;     // three dots + centre mark
                case "snap_base_active": DrawSnapBaseActive(hi, c); break;     // outlined dot

                // transform-tool glyphs (EditorTool overlay)
                case "tool_move": DrawToolMove(hi, c); break;                  // 4-way arrow
                case "tool_rotate": DrawToolRotate(hi, c); break;             // rotate arc + arrowhead
                case "tool_scale": DrawToolScale(hi, c); break;              // box + corner handle

                case "magnet": DrawMagnet(hi, c); break;
                case "snap_increment": DrawSnapIncrement(hi, c); break;
                case "snap_vertex": DrawSnapVertex(hi, c); break;
                case "snap_face": DrawSnapFace(hi, c); break;
                case "snap_grid":
                case "grid": DrawGrid(hi, c); break;
                case "snap": DrawSnapIncrement(hi, c); break;
                case "gizmo": DrawGizmo(hi); break;
                case "overlays": DrawOverlays(hi, c); break;
                case "xray": DrawXRay(hi, c); break;
                case "shade_wire": DrawShadeWire(hi, c); break;
                case "shade_solid": DrawShadeSolid(hi); break;
                case "shade_material": DrawShadeMaterial(hi); break;
                case "shade_rendered": DrawShadeRendered(hi); break;
                case "camera": DrawCamera(hi, c); break;
                case "eye": DrawEye(hi, c); break;
                case "axisX": DrawLetter(hi, 'X', AxisR); break;
                case "axisY": DrawLetter(hi, 'Y', AxisG); break;
                case "axisZ": DrawLetter(hi, 'Z', AxisB); break;
                default: DrawFallbackBox(hi, c); break;
            }

            return Downsample(hi);
        }

        // ------------------------------------------------------------------ named shapes
        // Coordinates use U(fraction) so shapes are resolution-independent; origin bottom-left.

        static void DrawOrientation(Color[] b, Color c)
        {
            float cx = U(0.5f), cy = U(0.5f), r = U(0.34f), t = U(0.05f);
            Ring(b, cx, cy, r, t, c);
            // vertical meridian (thin ellipse) + horizontal equator
            Ellipse(b, cx, cy, U(0.13f), r, t * 0.8f, c);
            Seg(b, cx - r, cy, cx + r, cy, t * 0.8f, c);
        }

        static void DrawPivot(Color[] b, Color c)
        {
            float cx = U(0.5f), cy = U(0.5f);
            FillDisc(b, cx, cy, U(0.12f), c);
            Ring(b, cx, cy, U(0.30f), U(0.03f), c);
            // four little outward arrows
            Arrow(b, cx, cy + U(0.30f), cx, cy + U(0.46f), U(0.04f), c);
            Arrow(b, cx, cy - U(0.30f), cx, cy - U(0.46f), U(0.04f), c);
            Arrow(b, cx + U(0.30f), cy, cx + U(0.46f), cy, U(0.04f), c);
            Arrow(b, cx - U(0.30f), cy, cx - U(0.46f), cy, U(0.04f), c);
        }

        static void DrawCursor(Color[] b)
        {
            float cx = U(0.5f), cy = U(0.5f), r = U(0.26f), t = U(0.045f);
            // red/white split ring (Blender 3D cursor)
            Arc(b, cx, cy, r, t, 0f, Mathf.PI, Color.white);
            Arc(b, cx, cy, r, t, Mathf.PI, Mathf.PI * 2f, new Color(0.86f, 0.24f, 0.24f));
            Color c = Ink;
            Seg(b, cx, cy + U(0.34f), cx, cy + U(0.48f), t, c);
            Seg(b, cx, cy - U(0.34f), cx, cy - U(0.48f), t, c);
            Seg(b, cx + U(0.34f), cy, cx + U(0.48f), cy, t, c);
            Seg(b, cx - U(0.34f), cy, cx - U(0.48f), cy, t, c);
        }

        static void DrawMagnet(Color[] b, Color c)
        {
            float cx = U(0.5f), cy = U(0.56f), r = U(0.26f), t = U(0.10f);
            // horseshoe: arc opening downward
            Arc(b, cx, cy, r, t, Mathf.PI * 0.12f, Mathf.PI * 0.88f, c);
            // two legs down to the poles
            float legY0 = cy - U(0.02f), legY1 = U(0.16f);
            float lx = cx - r, rx = cx + r;
            FillRect(b, lx - t, legY1, lx + t, legY0, c);
            FillRect(b, rx - t, legY1, rx + t, legY0, c);
            // pole tips (red / grey), Blender colouring
            FillRect(b, lx - t, U(0.10f), lx + t, U(0.18f), new Color(0.82f, 0.30f, 0.30f));
            FillRect(b, rx - t, U(0.10f), rx + t, U(0.18f), new Color(0.55f, 0.60f, 0.65f));
        }

        static void DrawSnapIncrement(Color[] b, Color c)
        {
            float t = U(0.035f);
            // right-angle ruler with tick marks
            Seg(b, U(0.22f), U(0.24f), U(0.78f), U(0.24f), t, c);
            Seg(b, U(0.22f), U(0.24f), U(0.22f), U(0.78f), t, c);
            for (int i = 1; i <= 4; i++)
            {
                float x = U(0.22f + i * 0.13f);
                Seg(b, x, U(0.24f), x, U(0.24f + (i % 2 == 0 ? 0.14f : 0.09f)), t, c);
                float y = U(0.24f + i * 0.13f);
                Seg(b, U(0.22f), y, U(0.22f + (i % 2 == 0 ? 0.14f : 0.09f)), y, t, c);
            }
        }

        static void DrawSnapVertex(Color[] b, Color c)
        {
            // diamond outline with filled corner vertices
            float cx = U(0.5f), cy = U(0.5f), r = U(0.30f), t = U(0.035f);
            Vector2 top = new Vector2(cx, cy + r), bot = new Vector2(cx, cy - r);
            Vector2 lft = new Vector2(cx - r, cy), rgt = new Vector2(cx + r, cy);
            Seg(b, top.x, top.y, rgt.x, rgt.y, t, c);
            Seg(b, rgt.x, rgt.y, bot.x, bot.y, t, c);
            Seg(b, bot.x, bot.y, lft.x, lft.y, t, c);
            Seg(b, lft.x, lft.y, top.x, top.y, t, c);
            float vr = U(0.055f);
            FillDisc(b, top.x, top.y, vr, c);
            FillDisc(b, bot.x, bot.y, vr, c);
            FillDisc(b, lft.x, lft.y, vr, c);
            FillDisc(b, rgt.x, rgt.y, vr, c);
        }

        static void DrawSnapFace(Color[] b, Color c)
        {
            // filled parallelogram (a face) with a short normal
            Color fill = new Color(c.r, c.g, c.b, 0.55f);
            FillQuad(b,
                new Vector2(U(0.24f), U(0.30f)), new Vector2(U(0.62f), U(0.24f)),
                new Vector2(U(0.74f), U(0.56f)), new Vector2(U(0.36f), U(0.62f)), fill);
            float mx = U(0.49f), my = U(0.43f);
            Arrow(b, mx, my, U(0.66f), U(0.80f), U(0.04f), c);
        }

        static void DrawGrid(Color[] b, Color c)
        {
            float t = U(0.03f);
            for (int i = 0; i <= 4; i++)
            {
                float p = U(0.18f + i * 0.16f);
                Seg(b, U(0.18f), p, U(0.82f), p, t, c);
                Seg(b, p, U(0.18f), p, U(0.82f), t, c);
            }
        }

        // ---- per-value orientation glyphs ----

        static void DrawOrientLocal(Color[] b, Color c)
        {
            // small box with a tiny RGB axis tripod at its corner
            RectStroke(b, U(0.24f), U(0.24f), U(0.66f), U(0.66f), U(0.04f), c);
            float ox = U(0.30f), oy = U(0.30f), t = U(0.04f);
            Arrow(b, ox, oy, U(0.74f), oy, t, AxisR);
            Arrow(b, ox, oy, ox, U(0.74f), t, AxisG);
            Arrow(b, ox, oy, U(0.56f), U(0.56f), t, AxisB);
        }

        static void DrawOrientView(Color[] b, Color c)
        {
            // monitor: screen rect + stand
            RectStroke(b, U(0.20f), U(0.34f), U(0.80f), U(0.74f), U(0.045f), c);
            FillRect(b, U(0.44f), U(0.24f), U(0.56f), U(0.34f), c);   // neck
            FillRect(b, U(0.34f), U(0.20f), U(0.66f), U(0.245f), c);  // base
        }

        static void DrawOrientNormal(Color[] b, Color c)
        {
            // a face quad (outlined) with an outward normal arrow
            FillQuad(b,
                new Vector2(U(0.22f), U(0.30f)), new Vector2(U(0.58f), U(0.22f)),
                new Vector2(U(0.66f), U(0.50f)), new Vector2(U(0.30f), U(0.58f)),
                new Color(c.r, c.g, c.b, 0.45f));
            float mx = U(0.44f), my = U(0.40f);
            Arrow(b, mx, my, U(0.72f), U(0.80f), U(0.045f), c);
        }

        static void DrawOrientCustom(Color[] b, Color c)
        {
            // bookmark shape with a small plus
            FillQuad(b,
                new Vector2(U(0.30f), U(0.20f)), new Vector2(U(0.58f), U(0.20f)),
                new Vector2(U(0.58f), U(0.80f)), new Vector2(U(0.30f), U(0.80f)),
                new Color(c.r, c.g, c.b, 0.28f));
            RectStroke(b, U(0.30f), U(0.20f), U(0.58f), U(0.80f), U(0.035f), c);
            // notch at the bottom
            FillQuad(b,
                new Vector2(U(0.30f), U(0.20f)), new Vector2(U(0.44f), U(0.32f)),
                new Vector2(U(0.58f), U(0.20f)), new Vector2(U(0.44f), U(0.20f)),
                new Color(0f, 0f, 0f, 0f));
            float t = U(0.04f);
            Seg(b, U(0.68f), U(0.62f), U(0.68f), U(0.80f), t, c);
            Seg(b, U(0.59f), U(0.71f), U(0.77f), U(0.71f), t, c);
        }

        // ---- per-value pivot-mode glyphs ----

        static void DrawPivotMedian(Color[] b, Color c)
        {
            // filled centre dot flanked by two smaller dots (the "median" of a set)
            FillDisc(b, U(0.5f), U(0.5f), U(0.11f), c);
            FillDisc(b, U(0.24f), U(0.5f), U(0.06f), c);
            FillDisc(b, U(0.76f), U(0.5f), U(0.06f), c);
        }

        static void DrawPivotActive(Color[] b, Color c)
        {
            // an outlined (selected/active) dot: ring + solid centre
            Ring(b, U(0.5f), U(0.5f), U(0.30f), U(0.04f), c);
            FillDisc(b, U(0.5f), U(0.5f), U(0.11f), c);
        }

        static void DrawPivotIndividual(Color[] b, Color c)
        {
            // three separate dots (each object about its own origin)
            FillDisc(b, U(0.30f), U(0.64f), U(0.09f), c);
            FillDisc(b, U(0.68f), U(0.62f), U(0.09f), c);
            FillDisc(b, U(0.48f), U(0.32f), U(0.09f), c);
        }

        static void DrawPivotBounds(Color[] b, Color c)
        {
            // dashed bounding box with a centre dot
            DashedRect(b, U(0.22f), U(0.22f), U(0.78f), U(0.78f), U(0.03f), c);
            FillDisc(b, U(0.5f), U(0.5f), U(0.075f), c);
        }

        // ---- snap-base ("Snap With") glyphs ----

        static void DrawSnapBaseClosest(Color[] b, Color c)
        {
            // outlined box with a solid dot at one corner (the nearest point of the selection)
            RectStroke(b, U(0.24f), U(0.24f), U(0.76f), U(0.76f), U(0.04f), c);
            FillDisc(b, U(0.76f), U(0.24f), U(0.11f), c);
        }

        static void DrawSnapBaseCenter(Color[] b, Color c)
        {
            // outlined box with a solid dot at its centre (the pivot)
            RectStroke(b, U(0.24f), U(0.24f), U(0.76f), U(0.76f), U(0.04f), c);
            FillDisc(b, U(0.5f), U(0.5f), U(0.11f), c);
        }

        static void DrawSnapBaseMedian(Color[] b, Color c)
        {
            // three dots with a small centre cross marking the median
            FillDisc(b, U(0.24f), U(0.62f), U(0.08f), c);
            FillDisc(b, U(0.76f), U(0.62f), U(0.08f), c);
            FillDisc(b, U(0.5f), U(0.30f), U(0.08f), c);
            float t = U(0.035f);
            Seg(b, U(0.5f), U(0.44f), U(0.5f), U(0.60f), t, c);
            Seg(b, U(0.42f), U(0.52f), U(0.58f), U(0.52f), t, c);
        }

        static void DrawSnapBaseActive(Color[] b, Color c)
        {
            // an outlined (active) dot: ring + solid centre
            Ring(b, U(0.5f), U(0.5f), U(0.30f), U(0.045f), c);
            FillDisc(b, U(0.5f), U(0.5f), U(0.12f), c);
        }

        // ---- transform-tool glyphs ----

        static void DrawToolMove(Color[] b, Color c)
        {
            // four-way arrow
            float t = U(0.045f), lo = U(0.16f), hi = U(0.84f), cx = U(0.5f), cy = U(0.5f);
            Arrow(b, cx, cy, cx, hi, t, c);
            Arrow(b, cx, cy, cx, lo, t, c);
            Arrow(b, cx, cy, hi, cy, t, c);
            Arrow(b, cx, cy, lo, cy, t, c);
        }

        static void DrawToolRotate(Color[] b, Color c)
        {
            // an open arc (three-quarter ring) with an arrowhead at one end
            float cx = U(0.5f), cy = U(0.5f), r = U(0.32f), t = U(0.05f);
            Arc(b, cx, cy, r, t, Mathf.PI * 0.15f, Mathf.PI * 1.75f, c);
            // arrowhead at the arc's start (angle ~ 0.15π), pointing tangentially
            float a = Mathf.PI * 0.15f;
            float ex = cx + r * Mathf.Cos(a), ey = cy + r * Mathf.Sin(a);
            Vector2 tan = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));   // clockwise tangent
            float h = U(0.11f);
            Seg(b, ex, ey, ex - tan.x * h + Mathf.Cos(a) * h * 0.6f, ey - tan.y * h + Mathf.Sin(a) * h * 0.6f, t, c);
            Seg(b, ex, ey, ex - tan.x * h - Mathf.Cos(a) * h * 0.6f, ey - tan.y * h - Mathf.Sin(a) * h * 0.6f, t, c);
        }

        static void DrawToolScale(Color[] b, Color c)
        {
            // a box with a diagonal handle line to a small solid corner square (Blender scale glyph)
            float t = U(0.045f);
            RectStroke(b, U(0.22f), U(0.22f), U(0.62f), U(0.62f), t, c);
            Seg(b, U(0.62f), U(0.62f), U(0.80f), U(0.80f), t, c);
            FillRect(b, U(0.72f), U(0.72f), U(0.86f), U(0.86f), c);
        }

        static void DashedRect(Color[] b, float x0, float y0, float x1, float y1, float half, Color c)
        {
            DashSeg(b, x0, y0, x1, y0, half, c);
            DashSeg(b, x0, y1, x1, y1, half, c);
            DashSeg(b, x0, y0, x0, y1, half, c);
            DashSeg(b, x1, y0, x1, y1, half, c);
        }

        static void DashSeg(Color[] b, float x0, float y0, float x1, float y1, float half, Color c)
        {
            const int dashes = 6;
            for (int i = 0; i < dashes; i++)
            {
                if ((i & 1) == 1) continue;
                float t0 = i / (float)dashes, t1 = (i + 0.7f) / dashes;
                Seg(b, Mathf.Lerp(x0, x1, t0), Mathf.Lerp(y0, y1, t0),
                       Mathf.Lerp(x0, x1, t1), Mathf.Lerp(y0, y1, t1), half, c);
            }
        }

        static void DrawGizmo(Color[] b)
        {
            float ox = U(0.30f), oy = U(0.30f), t = U(0.045f);
            Arrow(b, ox, oy, U(0.82f), oy, t, AxisR);            // X
            Arrow(b, ox, oy, ox, U(0.82f), t, AxisG);            // Y
            Arrow(b, ox, oy, U(0.62f), U(0.62f), t, AxisB);      // Z (diagonal)
            FillDisc(b, ox, oy, U(0.06f), Ink);
        }

        static void DrawOverlays(Color[] b, Color c)
        {
            float cx = U(0.5f), cy = U(0.5f);
            Ring(b, cx, cy, U(0.38f), U(0.035f), c);
            Ring(b, cx, cy, U(0.20f), U(0.035f), c);
            FillDisc(b, cx, cy, U(0.05f), c);
        }

        static void DrawXRay(Color[] b, Color c)
        {
            float t = U(0.04f), lo = U(0.20f), hi = U(0.80f), len = U(0.16f);
            // four corner brackets
            Seg(b, lo, hi, lo + len, hi, t, c); Seg(b, lo, hi, lo, hi - len, t, c);
            Seg(b, hi, hi, hi - len, hi, t, c); Seg(b, hi, hi, hi, hi - len, t, c);
            Seg(b, lo, lo, lo + len, lo, t, c); Seg(b, lo, lo, lo, lo + len, t, c);
            Seg(b, hi, lo, hi - len, lo, t, c); Seg(b, hi, lo, hi, lo + len, t, c);
            FillDisc(b, U(0.5f), U(0.5f), U(0.055f), c);
        }

        static void DrawShadeWire(Color[] b, Color c)
        {
            float cx = U(0.5f), cy = U(0.5f), r = U(0.38f), t = U(0.03f);
            Ring(b, cx, cy, r, t, c);
            Ellipse(b, cx, cy, U(0.16f), r, t, c);          // meridian
            Ellipse(b, cx, cy, r, U(0.16f), t, c);          // equator (horizontal ellipse)
        }

        static void DrawShadeSolid(Color[] b)
        {
            FillDisc(b, U(0.5f), U(0.5f), U(0.40f), new Color(0.62f, 0.62f, 0.64f));
        }

        static void DrawShadeMaterial(Color[] b)
        {
            float cx = U(0.5f), cy = U(0.5f), r = U(0.40f);
            Color a = new Color(0.78f, 0.78f, 0.80f);
            Color d = new Color(0.42f, 0.42f, 0.46f);
            int r2 = Mathf.CeilToInt(r);
            for (int y = -r2; y <= r2; y++)
                for (int x = -r2; x <= r2; x++)
                {
                    if (x * x + y * y > r * r) continue;
                    bool q = (x < 0) ^ (y < 0);
                    Set(b, cx + x, cy + y, q ? a : d);
                }
        }

        static void DrawShadeRendered(Color[] b)
        {
            float cx = U(0.5f), cy = U(0.5f), r = U(0.40f);
            DiscGradient(b, cx, cy, r, new Color(0.85f, 0.85f, 0.88f), new Color(0.28f, 0.30f, 0.36f));
            FillDisc(b, cx - U(0.14f), cy + U(0.15f), U(0.09f), new Color(1f, 1f, 1f, 0.85f)); // highlight
        }

        static void DrawCamera(Color[] b, Color c)
        {
            float t = U(0.035f);
            RectStroke(b, U(0.16f), U(0.30f), U(0.60f), U(0.66f), t, c);
            Seg(b, U(0.60f), U(0.42f), U(0.82f), U(0.30f), t, c);
            Seg(b, U(0.60f), U(0.54f), U(0.82f), U(0.66f), t, c);
            Seg(b, U(0.82f), U(0.30f), U(0.82f), U(0.66f), t, c);
        }

        static void DrawEye(Color[] b, Color c)
        {
            float cx = U(0.5f), cy = U(0.5f);
            float t = U(0.035f);
            Arc(b, cx, cy - U(0.18f), U(0.34f), t, Mathf.PI * 0.25f, Mathf.PI * 0.75f, c);
            Arc(b, cx, cy + U(0.18f), U(0.34f), t, Mathf.PI * 1.25f, Mathf.PI * 1.75f, c);
            FillDisc(b, cx, cy, U(0.10f), c);
        }

        static void DrawLetter(Color[] b, char ch, Color c)
        {
            float t = U(0.06f);
            switch (ch)
            {
                case 'X':
                    Seg(b, U(0.28f), U(0.22f), U(0.72f), U(0.78f), t, c);
                    Seg(b, U(0.72f), U(0.22f), U(0.28f), U(0.78f), t, c);
                    break;
                case 'Y':
                    Seg(b, U(0.28f), U(0.78f), U(0.5f), U(0.5f), t, c);
                    Seg(b, U(0.72f), U(0.78f), U(0.5f), U(0.5f), t, c);
                    Seg(b, U(0.5f), U(0.5f), U(0.5f), U(0.22f), t, c);
                    break;
                case 'Z':
                    Seg(b, U(0.28f), U(0.78f), U(0.72f), U(0.78f), t, c);
                    Seg(b, U(0.72f), U(0.78f), U(0.28f), U(0.22f), t, c);
                    Seg(b, U(0.28f), U(0.22f), U(0.72f), U(0.22f), t, c);
                    break;
            }
        }

        static void DrawFallbackBox(Color[] b, Color c)
        {
            RectStroke(b, U(0.2f), U(0.2f), U(0.8f), U(0.8f), U(0.04f), c);
            Seg(b, U(0.2f), U(0.2f), U(0.8f), U(0.8f), U(0.04f), c);
        }

        // ------------------------------------------------------------------ raster primitives (hi-res)

        static float U(float frac) => frac * HS;

        static void Set(Color[] b, float fx, float fy, Color c)
        {
            int x = Mathf.RoundToInt(fx), y = Mathf.RoundToInt(fy);
            if (x < 0 || y < 0 || x >= HS || y >= HS) return;
            b[y * HS + x] = c;
        }

        static void FillDisc(Color[] b, float cx, float cy, float r, Color c)
        {
            int r2 = Mathf.CeilToInt(r);
            float rr = r * r;
            for (int y = -r2; y <= r2; y++)
                for (int x = -r2; x <= r2; x++)
                    if (x * x + y * y <= rr) Set(b, cx + x, cy + y, c);
        }

        static void DiscGradient(Color[] b, float cx, float cy, float r, Color top, Color bot)
        {
            int r2 = Mathf.CeilToInt(r);
            float rr = r * r;
            for (int y = -r2; y <= r2; y++)
                for (int x = -r2; x <= r2; x++)
                {
                    if (x * x + y * y > rr) continue;
                    float t = Mathf.Clamp01((y + r) / (2f * r));
                    Set(b, cx + x, cy + y, Color.Lerp(bot, top, t));
                }
        }

        static void Ring(Color[] b, float cx, float cy, float r, float half, Color c)
        {
            int r2 = Mathf.CeilToInt(r + half + 1f);
            for (int y = -r2; y <= r2; y++)
                for (int x = -r2; x <= r2; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    if (Mathf.Abs(d - r) <= half) Set(b, cx + x, cy + y, c);
                }
        }

        static void Ellipse(Color[] b, float cx, float cy, float rx, float ry, float half, Color c)
        {
            int r2 = Mathf.CeilToInt(Mathf.Max(rx, ry) + half + 1f);
            for (int y = -r2; y <= r2; y++)
                for (int x = -r2; x <= r2; x++)
                {
                    float e = Mathf.Sqrt((x * x) / (rx * rx) + (y * y) / (ry * ry));
                    // distance from unit ellipse, scaled back to pixels approximately
                    float d = Mathf.Abs(e - 1f) * Mathf.Min(rx, ry);
                    if (d <= half) Set(b, cx + x, cy + y, c);
                }
        }

        static void Arc(Color[] b, float cx, float cy, float r, float half, float a0, float a1, Color c)
        {
            int r2 = Mathf.CeilToInt(r + half + 1f);
            for (int y = -r2; y <= r2; y++)
                for (int x = -r2; x <= r2; x++)
                {
                    float d = Mathf.Sqrt(x * x + y * y);
                    if (Mathf.Abs(d - r) > half) continue;
                    float ang = Mathf.Atan2(y, x);
                    if (ang < 0f) ang += Mathf.PI * 2f;
                    float aa = ang;
                    while (aa < a0) aa += Mathf.PI * 2f;
                    if (aa <= a1) Set(b, cx + x, cy + y, c);
                }
        }

        static void Seg(Color[] b, float x0, float y0, float x1, float y1, float half, Color c)
        {
            float minx = Mathf.Min(x0, x1) - half - 1f, maxx = Mathf.Max(x0, x1) + half + 1f;
            float miny = Mathf.Min(y0, y1) - half - 1f, maxy = Mathf.Max(y0, y1) + half + 1f;
            float dx = x1 - x0, dy = y1 - y0;
            float len2 = dx * dx + dy * dy;
            for (int y = Mathf.FloorToInt(miny); y <= Mathf.CeilToInt(maxy); y++)
                for (int x = Mathf.FloorToInt(minx); x <= Mathf.CeilToInt(maxx); x++)
                {
                    float t = len2 > 1e-4f ? ((x - x0) * dx + (y - y0) * dy) / len2 : 0f;
                    t = Mathf.Clamp01(t);
                    float px = x0 + t * dx, py = y0 + t * dy;
                    float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                    if (d <= half) Set(b, x, y, c);
                }
        }

        static void Arrow(Color[] b, float x0, float y0, float x1, float y1, float half, Color c)
        {
            Seg(b, x0, y0, x1, y1, half, c);
            Vector2 dir = new Vector2(x1 - x0, y1 - y0);
            if (dir.sqrMagnitude < 1e-3f) return;
            dir.Normalize();
            Vector2 n = new Vector2(-dir.y, dir.x);
            float head = U(0.08f);
            Vector2 tip = new Vector2(x1, y1);
            Vector2 back = tip - dir * head;
            Seg(b, tip.x, tip.y, back.x + n.x * head * 0.7f, back.y + n.y * head * 0.7f, half, c);
            Seg(b, tip.x, tip.y, back.x - n.x * head * 0.7f, back.y - n.y * head * 0.7f, half, c);
        }

        static void FillRect(Color[] b, float x0, float y0, float x1, float y1, Color c)
        {
            for (int y = Mathf.FloorToInt(Mathf.Min(y0, y1)); y <= Mathf.CeilToInt(Mathf.Max(y0, y1)); y++)
                for (int x = Mathf.FloorToInt(Mathf.Min(x0, x1)); x <= Mathf.CeilToInt(Mathf.Max(x0, x1)); x++)
                    Set(b, x, y, c);
        }

        static void RectStroke(Color[] b, float x0, float y0, float x1, float y1, float half, Color c)
        {
            Seg(b, x0, y0, x1, y0, half, c);
            Seg(b, x0, y1, x1, y1, half, c);
            Seg(b, x0, y0, x0, y1, half, c);
            Seg(b, x1, y0, x1, y1, half, c);
        }

        static void FillQuad(Color[] b, Vector2 a, Vector2 bb, Vector2 cc, Vector2 dd, Color col)
        {
            float minx = Mathf.Min(Mathf.Min(a.x, bb.x), Mathf.Min(cc.x, dd.x));
            float maxx = Mathf.Max(Mathf.Max(a.x, bb.x), Mathf.Max(cc.x, dd.x));
            float miny = Mathf.Min(Mathf.Min(a.y, bb.y), Mathf.Min(cc.y, dd.y));
            float maxy = Mathf.Max(Mathf.Max(a.y, bb.y), Mathf.Max(cc.y, dd.y));
            for (int y = Mathf.FloorToInt(miny); y <= Mathf.CeilToInt(maxy); y++)
                for (int x = Mathf.FloorToInt(minx); x <= Mathf.CeilToInt(maxx); x++)
                    if (InTri(x, y, a, bb, cc) || InTri(x, y, a, cc, dd))
                        Set(b, x, y, col);
        }

        static bool InTri(float px, float py, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(px, py, a, b);
            float d2 = Sign(px, py, b, c);
            float d3 = Sign(px, py, c, a);
            bool neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool pos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(neg && pos);
        }

        static float Sign(float px, float py, Vector2 a, Vector2 b)
            => (px - b.x) * (a.y - b.y) - (a.x - b.x) * (py - b.y);

        // ------------------------------------------------------------------ downsample

        static Texture2D Downsample(Color[] hi)
        {
            var lo = new Color[Size * Size];
            int n = SS * SS;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float ar = 0f, ag = 0f, ab = 0f, aa = 0f;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            Color s = hi[(y * SS + sy) * HS + (x * SS + sx)];
                            ar += s.r * s.a; ag += s.g * s.a; ab += s.b * s.a; aa += s.a;
                        }
                    if (aa > 1e-5f)
                        lo[y * Size + x] = new Color(ar / aa, ag / aa, ab / aa, aa / n);
                    else
                        lo[y * Size + x] = new Color(0f, 0f, 0f, 0f);
                }

            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                name = "BlenderMode_icon"
            };
            t.SetPixels(lo);
            t.Apply(false, false);
            return t;
        }
    }
}
