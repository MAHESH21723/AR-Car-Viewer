using System;
using UnityEngine;

namespace CarViewer
{
    /// <summary>
    /// Procedurally drawn white icons and rounded-rect sprites, so the UI needs no
    /// extra image files. Shapes are defined as "inside" tests in 0..1 space and
    /// anti-aliased with 3x3 supersampling.
    /// </summary>
    public static class UiIcons
    {
        const int Size = 96;

        static Sprite Draw(Func<Vector2, bool> inside)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int cov = 0;
                    for (int sy = 0; sy < 3; sy++)
                        for (int sx = 0; sx < 3; sx++)
                            if (inside(new Vector2((x + (sx + 0.5f) / 3f) / Size, (y + (sy + 0.5f) / 3f) / Size))) cov++;
                    px[y * Size + x] = new Color32(255, 255, 255, (byte)(cov * 255 / 9));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---- geometry helpers ----
        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
        static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        static bool InRect(Vector2 p, float x0, float y0, float x1, float y1) => p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1;
        static bool InDisc(Vector2 p, Vector2 c, float r) => (p - c).sqrMagnitude <= r * r;
        static bool NearSeg(Vector2 p, Vector2 a, Vector2 b, float halfWidth)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).sqrMagnitude <= halfWidth * halfWidth;
        }

        /// <summary>Circular arrow. mirror = counter-clockwise (used for "reset").</summary>
        static Func<Vector2, bool> CircleArrow(bool mirror)
        {
            return p =>
            {
                if (mirror) p.x = 1f - p.x;
                Vector2 c = new Vector2(0.5f, 0.5f), d = p - c;
                float r = d.magnitude;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; // -180..180
                if (ang < 0) ang += 360f;
                bool band = r > 0.22f && r < 0.32f;
                // Gap between 20 and 80 degrees where the arrow head sits.
                bool arc = band && (ang > 80f || ang < 20f);
                Vector2 tip = c + new Vector2(Mathf.Cos(80f * Mathf.Deg2Rad), Mathf.Sin(80f * Mathf.Deg2Rad)) * 0.27f;
                Vector2 dir = new Vector2(-Mathf.Sin(80f * Mathf.Deg2Rad), Mathf.Cos(80f * Mathf.Deg2Rad)); // ccw tangent
                Vector2 nrm = new Vector2(Mathf.Cos(80f * Mathf.Deg2Rad), Mathf.Sin(80f * Mathf.Deg2Rad));
                // Arrow points clockwise (towards lower angle), so flip tangent.
                dir = -dir;
                bool head = InTri(p, tip + nrm * 0.12f, tip - nrm * 0.12f, tip + dir * 0.17f);
                return arc || head;
            };
        }

        public static Sprite Play() => Draw(p => InTri(p, new Vector2(0.34f, 0.24f), new Vector2(0.34f, 0.76f), new Vector2(0.78f, 0.5f)));
        public static Sprite Pause() => Draw(p => InRect(p, 0.28f, 0.24f, 0.42f, 0.76f) || InRect(p, 0.58f, 0.24f, 0.72f, 0.76f));
        public static Sprite Rotate() => Draw(CircleArrow(false));
        public static Sprite Reset() => Draw(CircleArrow(true));

        public static Sprite Explode() => Draw(p =>
        {
            if (InRect(p, 0.40f, 0.40f, 0.60f, 0.60f)) return true;
            Vector2 c = new Vector2(0.5f, 0.5f);
            Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
            foreach (var d in dirs)
            {
                Vector2 n = new Vector2(-d.y, d.x);
                Vector2 b = c + d * 0.28f;
                if (InTri(p, b + n * 0.09f, b - n * 0.09f, c + d * 0.42f)) return true;
            }
            return false;
        });

        public static Sprite Paint() => Draw(p =>
        {
            Vector2 c = new Vector2(0.5f, 0.5f);
            if (!InDisc(p, c, 0.36f)) return false;
            if (InDisc(p, new Vector2(0.5f, 0.68f), 0.07f)) return false;
            if (InDisc(p, new Vector2(0.34f, 0.5f), 0.07f)) return false;
            if (InDisc(p, new Vector2(0.66f, 0.5f), 0.07f)) return false;
            if (InDisc(p, new Vector2(0.5f, 0.28f), 0.09f)) return false;
            return true;
        });

        public static Sprite Planes() => Draw(p =>
        {
            // A tilted grid: outline of a rhombus plus two cross lines.
            Vector2 a = new Vector2(0.5f, 0.78f), b = new Vector2(0.84f, 0.5f), c = new Vector2(0.5f, 0.22f), d = new Vector2(0.16f, 0.5f);
            float w = 0.035f;
            return NearSeg(p, a, b, w) || NearSeg(p, b, c, w) || NearSeg(p, c, d, w) || NearSeg(p, d, a, w) ||
                   NearSeg(p, Vector2.Lerp(a, d, 0.5f), Vector2.Lerp(b, c, 0.5f), w) ||
                   NearSeg(p, Vector2.Lerp(a, b, 0.5f), Vector2.Lerp(d, c, 0.5f), w);
        });

        public static Sprite Close() => Draw(p =>
            NearSeg(p, new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f), 0.045f) ||
            NearSeg(p, new Vector2(0.3f, 0.7f), new Vector2(0.7f, 0.3f), 0.045f));

        /// <summary>Filled circle.</summary>
        public static Sprite Circle() => Draw(p => InDisc(p, new Vector2(0.5f, 0.5f), 0.5f));

        /// <summary>Ring outline (used to mark the selected swatch).</summary>
        public static Sprite Ring() => Draw(p =>
        {
            float r = (p - new Vector2(0.5f, 0.5f)).magnitude;
            return r <= 0.5f && r >= 0.42f;
        });

        /// <summary>9-sliceable rounded rectangle.</summary>
        public static Sprite RoundedRect(int radius = 28)
        {
            int s = radius * 2 + 4;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, s - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, s - radius);
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - dist + 0.5f);
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
