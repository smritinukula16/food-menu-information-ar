using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARMenu
{
    /// <summary>
    /// Procedurally generated, anti-aliased UI sprites and icons (white, tint with Image.color).
    /// Generated once at startup from signed-distance functions, so the project needs no imported UI art.
    /// </summary>
    public static class UISprites
    {
        /// <summary>Corner radius (px) baked into the 9-sliced rounded sprites.</summary>
        public const float SliceRadius = 64f;

        public static Sprite Rounded, RoundedOutline, Shadow, Circle, Ring, Glow, FadeUp, White;

        public static Sprite Heart, HeartOutline, Cart, Plus, Minus, Back, Close, Check, ChevronRight, ChevronDown,
            Cube, Scan, Info, Rotate, Recenter, Star, Flame, VegMark, NonVegMark, Plate, Spinner, Chef;

        static bool sReady;
        static readonly List<UnityEngine.Object> sCreated = new List<UnityEngine.Object>();

        public static void Init()
        {
            if (sReady && Rounded != null) return;
            sReady = true;

            White = Make("White", 4, 4, (x, y) => 1f, Vector4.zero);

            // 9-sliced rounded rectangle: radius 64 px, use Image.pixelsPerUnitMultiplier = 64 / radius.
            const int rs = 160;
            Rounded = Make("Rounded", rs, rs, (x, y) =>
                Fill(SdRoundBox(new Vector2(x - rs / 2f, y - rs / 2f), new Vector2(rs / 2f, rs / 2f), SliceRadius)),
                new Vector4(SliceRadius, SliceRadius, SliceRadius, SliceRadius));

            RoundedOutline = Make("RoundedOutline", rs, rs, (x, y) =>
            {
                var d = SdRoundBox(new Vector2(x - rs / 2f, y - rs / 2f), new Vector2(rs / 2f - 1f, rs / 2f - 1f), SliceRadius - 1f);
                return Fill(Mathf.Abs(d + 3f) - 3f);
            }, new Vector4(SliceRadius, SliceRadius, SliceRadius, SliceRadius));

            // Soft drop shadow (9-sliced, border 96 px).
            const int ss = 256; const float blur = 56f;
            Shadow = Make("Shadow", ss, ss, (x, y) =>
            {
                var d = SdRoundBox(new Vector2(x - ss / 2f, y - ss / 2f), new Vector2(ss / 2f - blur, ss / 2f - blur), 40f);
                var a = 1f - Mathf.Clamp01(d / blur);
                return a * a * a;
            }, new Vector4(96, 96, 96, 96));

            Circle = Icon("Circle", p => Circ(p, C, 0.48f));
            Ring = Icon("Ring", p => Mathf.Abs(Circ(p, C, 0.44f)) - 0.035f);
            Glow = Make("Glow", 128, 128, (x, y) =>
            {
                var d = new Vector2(x - 64f, y - 64f).magnitude / 64f;
                var a = 1f - Mathf.Clamp01(d);
                return a * a;
            }, Vector4.zero);
            // Vertical gradient: opaque at the top, transparent at the bottom.
            FadeUp = Make("Fade", 4, 256, (x, y) => Mathf.SmoothStep(0f, 1f, y / 256f), Vector4.zero);

            const float w = 0.055f;
            Heart = Icon("Heart", p => SdHeart(p));
            HeartOutline = Icon("HeartOutline", p => Mathf.Abs(SdHeart(p)) - 0.04f);
            Cart = Icon("Cart", p => Mathf.Min(
                Lines(p, w * 0.85f, V(0.10f, 0.80f), V(0.24f, 0.80f), V(0.34f, 0.40f), V(0.80f, 0.40f), V(0.88f, 0.68f), V(0.29f, 0.68f)),
                Mathf.Min(Circ(p, V(0.39f, 0.23f), 0.075f), Circ(p, V(0.75f, 0.23f), 0.075f))));
            Plus = Icon("Plus", p => Mathf.Min(Seg(p, V(0.5f, 0.22f), V(0.5f, 0.78f)), Seg(p, V(0.22f, 0.5f), V(0.78f, 0.5f))) - w);
            Minus = Icon("Minus", p => Seg(p, V(0.22f, 0.5f), V(0.78f, 0.5f)) - w);
            Back = Icon("Back", p => Lines(p, w, V(0.60f, 0.20f), V(0.32f, 0.50f), V(0.60f, 0.80f)));
            ChevronRight = Icon("ChevronRight", p => Lines(p, w, V(0.40f, 0.20f), V(0.68f, 0.50f), V(0.40f, 0.80f)));
            ChevronDown = Icon("ChevronDown", p => Lines(p, w, V(0.20f, 0.62f), V(0.50f, 0.34f), V(0.80f, 0.62f)));
            Close = Icon("Close", p => Mathf.Min(Seg(p, V(0.27f, 0.27f), V(0.73f, 0.73f)), Seg(p, V(0.27f, 0.73f), V(0.73f, 0.27f))) - w);
            Check = Icon("Check", p => Lines(p, w * 1.15f, V(0.22f, 0.52f), V(0.42f, 0.32f), V(0.80f, 0.70f)));

            var hex = new Vector2[6];
            for (var i = 0; i < 6; i++)
            {
                var a = Mathf.Deg2Rad * (90f - 60f * i);
                hex[i] = C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.38f;
            }
            Cube = Icon("Cube", p => Mathf.Min(
                Lines(p, w * 0.8f, hex[0], hex[1], hex[2], hex[3], hex[4], hex[5], hex[0]),
                Mathf.Min(Seg(p, C, hex[5]), Mathf.Min(Seg(p, C, hex[1]), Seg(p, C, hex[3]))) - w * 0.8f));

            Scan = Icon("Scan", p => Mathf.Min(Mathf.Min(
                    Mathf.Min(Lines(p, w, V(0.14f, 0.36f), V(0.14f, 0.14f), V(0.36f, 0.14f)),
                        Lines(p, w, V(0.64f, 0.14f), V(0.86f, 0.14f), V(0.86f, 0.36f))),
                    Mathf.Min(Lines(p, w, V(0.86f, 0.64f), V(0.86f, 0.86f), V(0.64f, 0.86f)),
                        Lines(p, w, V(0.36f, 0.86f), V(0.14f, 0.86f), V(0.14f, 0.64f)))),
                Seg(p, V(0.28f, 0.5f), V(0.72f, 0.5f)) - w));

            Info = Icon("Info", p => Mathf.Min(Mathf.Abs(Circ(p, C, 0.40f)) - 0.045f,
                Mathf.Min(Circ(p, V(0.5f, 0.70f), 0.06f), Seg(p, V(0.5f, 0.30f), V(0.5f, 0.52f)) - 0.055f)));

            Rotate = Icon("Rotate", p =>
            {
                var d = p - C;
                var ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg; // -180..180
                var e1 = C + Polar(-30f, 0.30f);
                var e2 = C + Polar(50f, 0.30f);
                float arc = ang > -30f && ang < 50f
                    ? Mathf.Min((p - e1).magnitude, (p - e2).magnitude) - w
                    : Mathf.Abs(d.magnitude - 0.30f) - w;
                var dir = new Vector2(0.5f, 0.866f);
                var perp = new Vector2(0.866f, -0.5f);
                var head = SdPolygon(p, new[] { e1 + dir * 0.14f, e1 + perp * 0.12f - dir * 0.02f, e1 - perp * 0.12f - dir * 0.02f });
                return Mathf.Min(arc, head);
            });

            Recenter = Icon("Recenter", p => Mathf.Min(Mathf.Min(Mathf.Abs(Circ(p, C, 0.25f)) - 0.045f, Circ(p, C, 0.07f)),
                Mathf.Min(Mathf.Min(Seg(p, V(0.5f, 0.06f), V(0.5f, 0.2f)), Seg(p, V(0.5f, 0.8f), V(0.5f, 0.94f))),
                    Mathf.Min(Seg(p, V(0.06f, 0.5f), V(0.2f, 0.5f)), Seg(p, V(0.8f, 0.5f), V(0.94f, 0.5f)))) - 0.045f));

            var star = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var a = Mathf.Deg2Rad * (90f + 36f * i);
                star[i] = C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? 0.44f : 0.19f);
            }
            Star = Icon("Star", p => SdPolygon(p, star));

            Flame = Icon("Flame", p => SmoothMin(Circ(p, V(0.5f, 0.38f), 0.25f),
                SdPolygon(p, new[] { V(0.5f, 0.92f), V(0.29f, 0.44f), V(0.71f, 0.44f) }), 0.08f));

            VegMark = Icon("Veg", p => Mathf.Min(Mathf.Abs(SdBox(p - C, V(0.40f, 0.40f))) - 0.045f, Circ(p, C, 0.20f)));
            NonVegMark = Icon("NonVeg", p => Mathf.Min(Mathf.Abs(SdBox(p - C, V(0.40f, 0.40f))) - 0.045f,
                SdPolygon(p, new[] { V(0.5f, 0.73f), V(0.27f, 0.32f), V(0.73f, 0.32f) })));

            Plate = Icon("Plate", p => Mathf.Min(Mathf.Abs(Circ(p, C, 0.40f)) - 0.025f, Mathf.Abs(Circ(p, C, 0.27f)) - 0.02f));

            Chef = Icon("Chef", p => Mathf.Min(
                SmoothMin(SmoothMin(Circ(p, V(0.34f, 0.62f), 0.16f), Circ(p, V(0.66f, 0.62f), 0.16f), 0.05f),
                    Circ(p, V(0.5f, 0.70f), 0.18f), 0.05f),
                SdBox(p - V(0.5f, 0.36f), V(0.17f, 0.13f))));

            Spinner = Make("Spinner", 128, 128, (x, y) =>
            {
                var p = new Vector2(x / 128f, y / 128f) - C;
                var ring = Fill((Mathf.Abs(p.magnitude - 0.40f) - 0.06f) * 128f);
                var ang = (Mathf.Atan2(p.y, p.x) + Mathf.PI) / (2f * Mathf.PI);
                return ring * Mathf.SmoothStep(0f, 1f, ang);
            }, Vector4.zero);
        }

        // ---------------------------------------------------------------- helpers

        static readonly Vector2 C = new Vector2(0.5f, 0.5f);
        static Vector2 V(float x, float y) => new Vector2(x, y);
        static Vector2 Polar(float deg, float r) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad)) * r;

        /// <summary>Distance (px) to alpha with a 1px anti-aliased edge.</summary>
        static float Fill(float distancePx) => Mathf.Clamp01(0.5f - distancePx);

        static Sprite Icon(string name, Func<Vector2, float> sdf, int size = 128)
        {
            return Make(name, size, size, (x, y) => Fill(sdf(new Vector2(x / size, y / size)) * size), Vector4.zero);
        }

        static Sprite Make(string name, int w, int h, Func<float, float, float> alpha, Vector4 border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "ARMenu_" + name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var px = new Color32[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var a = Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f));
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sCreated.Add(tex);
            sCreated.Add(sprite);
            return sprite;
        }

        static float Circ(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float SdBox(Vector2 p, Vector2 half)
        {
            var d = new Vector2(Mathf.Abs(p.x) - half.x, Mathf.Abs(p.y) - half.y);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        static float SdRoundBox(Vector2 p, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x) - half.x + r, Mathf.Abs(p.y) - half.y + r);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a; var ba = b - a;
            var h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        static float Lines(Vector2 p, float width, params Vector2[] pts)
        {
            var d = float.MaxValue;
            for (var i = 0; i < pts.Length - 1; i++) d = Mathf.Min(d, Seg(p, pts[i], pts[i + 1]));
            return d - width;
        }

        static float SmoothMin(float a, float b, float k)
        {
            var h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        static float SdPolygon(Vector2 p, Vector2[] v)
        {
            var d = Vector2.Dot(p - v[0], p - v[0]);
            var s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var wv = p - v[i];
                var b = wv - e * Mathf.Clamp01(Vector2.Dot(wv, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                var c1 = p.y >= v[i].y; var c2 = p.y < v[j].y; var c3 = e.x * wv.y > e.y * wv.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        /// <summary>Heart SDF (after Inigo Quilez), mapped into the 0..1 icon square.</summary>
        static float SdHeart(Vector2 uv)
        {
            const float scale = 1.32f;
            var p = new Vector2((uv.x - 0.5f) * scale, (uv.y - 0.10f) * scale);
            p.x = Mathf.Abs(p.x);
            float d;
            if (p.y + p.x > 1f)
            {
                d = (p - new Vector2(0.25f, 0.75f)).magnitude - Mathf.Sqrt(2f) / 4f;
            }
            else
            {
                var a = (p - new Vector2(0f, 1f)).sqrMagnitude;
                var m = 0.5f * Mathf.Max(p.x + p.y, 0f);
                var b = (p - new Vector2(m, m)).sqrMagnitude;
                d = Mathf.Sqrt(Mathf.Min(a, b)) * Mathf.Sign(p.x - p.y);
            }
            return d / scale;
        }
    }
}
