using System.Collections.Generic;
using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Runtime-generated 9-sliced sprites so the procedural UIs get rounded corners, pills and soft
    /// shadows without shipping any texture assets. Sprites are cached by (radius, feather).
    /// </summary>
    internal static class UiSprites
    {
        static readonly Dictionary<int, Sprite> _rounded = new();
        static readonly Dictionary<int, Sprite> _shadow = new();
        static Sprite _circle;
        static Sprite _ring;

        const float PixelsPerUnit = 100f;

        /// <summary>Solid white rounded rectangle, 9-sliced on a border of <paramref name="radius"/> px. Tint with Image.color.</summary>
        public static Sprite RoundedRect(int radius = 24)
        {
            radius = Mathf.Clamp(radius, 2, 96);
            if (_rounded.TryGetValue(radius, out var s) && s != null) return s;

            int size = radius * 2 + 4;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, CornerAlpha(x, y, size, radius, 1.25f));
            tex.SetPixels32(px);
            tex.Apply(false, false);

            var border = new Vector4(radius, radius, radius, radius);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect, border);
            s.name = $"ucs_rounded_{radius}";
            _rounded[radius] = s;
            return s;
        }

        /// <summary>Soft round shadow blob, 9-sliced. Put behind a card, tint dark + low alpha, offset a few px.</summary>
        public static Sprite Shadow(int radius = 28)
        {
            radius = Mathf.Clamp(radius, 4, 96);
            if (_shadow.TryGetValue(radius, out var s) && s != null) return s;

            int size = radius * 2 + 4;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, CornerAlpha(x, y, size, radius, radius * 0.9f));
            tex.SetPixels32(px);
            tex.Apply(false, false);

            var border = new Vector4(radius, radius, radius, radius);
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect, border);
            s.name = $"ucs_shadow_{radius}";
            _shadow[radius] = s;
            return s;
        }

        /// <summary>Solid white circle. Tint with Image.color.</summary>
        public static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int size = 64;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            float r = size / 2f - 1f;
            var c = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            _circle.name = "ucs_circle";
            return _circle;
        }

        /// <summary>White ring (annulus). Use as an Image with type=Filled / Radial360 for a spinner arc.</summary>
        public static Sprite Ring()
        {
            if (_ring != null) return _ring;
            const int size = 128;
            var tex = NewTex(size);
            var px = new Color32[size * size];
            var c = new Vector2(size / 2f, size / 2f);
            float outer = size / 2f - 2f;
            float inner = outer * 0.62f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            _ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            _ring.name = "ucs_ring";
            return _ring;
        }

        static Texture2D NewTex(int size) => new(size, size, TextureFormat.RGBA32, false)
        {
            name = "ucs_tex",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };

        /// <summary>Alpha for a rounded corner: 1 inside, feathered to 0 across <paramref name="feather"/> px past the radius.</summary>
        static byte CornerAlpha(int x, int y, int size, int radius, float feather)
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float cx = Mathf.Clamp(fx, radius, size - radius);
            float cy = Mathf.Clamp(fy, radius, size - radius);
            float d = Mathf.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy));
            if (d <= radius - feather) return 255;
            if (d >= radius) return 0;
            return (byte)(Mathf.Clamp01((radius - d) / feather) * 255f);
        }
    }
}
