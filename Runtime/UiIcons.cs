using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.CloudSave
{
    internal enum UiIcon { Cloud, Device, Check, Warn, Cross, Sync }

    /// <summary>
    /// Simple vector-style icons composed from <see cref="UiSprites"/> primitives so the built-in UIs
    /// don't depend on an emoji-capable font. Each builder returns the icon root (a square RectTransform).
    /// </summary>
    internal static class UiIcons
    {
        public static RectTransform Build(GameObject parent, UiIcon kind, Color color, float size)
        {
            var root = MakeGO(parent, kind.ToString() + "Icon", size);
            switch (kind)
            {
                case UiIcon.Cloud:  Cloud(root, color);  break;
                case UiIcon.Device: Device(root, color); break;
                case UiIcon.Check:  Check(root, color);   break;
                case UiIcon.Cross:  Cross(root, color);   break;
                case UiIcon.Warn:   Warn(root, color);    break;
                case UiIcon.Sync:   Sync(root, color);    break;
            }
            return root;
        }

        static RectTransform MakeGO(GameObject parent, string name, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        static Image Blob(RectTransform parent, UiGeneratedSprite.Kind kind, int radius, Color color,
            Vector2 anchoredPos, Vector2 size, float rotation = 0f)
        {
            var go = new GameObject("part", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            rt.localRotation = Quaternion.Euler(0, 0, rotation);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            UiGeneratedSprite.Attach(img, kind, radius);
            return img;
        }

        const int K = 12;

        static void Cloud(RectTransform r, Color c)
        {
            float s = r.sizeDelta.x;
            Blob(r, UiGeneratedSprite.Kind.Circle, K, c, new Vector2(-s * 0.20f, -s * 0.02f), Vector2.one * s * 0.42f);
            Blob(r, UiGeneratedSprite.Kind.Circle, K, c, new Vector2(s * 0.05f, s * 0.14f), Vector2.one * s * 0.52f);
            Blob(r, UiGeneratedSprite.Kind.Circle, K, c, new Vector2(s * 0.26f, -s * 0.02f), Vector2.one * s * 0.40f);
            Blob(r, UiGeneratedSprite.Kind.RoundedRect, 16, c, new Vector2(s * 0.03f, -s * 0.16f), new Vector2(s * 0.72f, s * 0.30f));
        }

        static void Device(RectTransform r, Color c)
        {
            float s = r.sizeDelta.x;
            Blob(r, UiGeneratedSprite.Kind.RoundedRect, 18, c, Vector2.zero, new Vector2(s * 0.58f, s * 0.92f));
            Blob(r, UiGeneratedSprite.Kind.RoundedRect, 12, Multiply(c, 0.35f), new Vector2(0, s * 0.02f), new Vector2(s * 0.44f, s * 0.62f));
            Blob(r, UiGeneratedSprite.Kind.Circle, K, Multiply(c, 0.35f), new Vector2(0, -s * 0.36f), Vector2.one * s * 0.10f);
        }

        static void Check(RectTransform r, Color c)
        {
            float s = r.sizeDelta.x;
            Blob(r, UiGeneratedSprite.Kind.RoundedRect, 8, c, new Vector2(-s * 0.20f, -s * 0.10f), new Vector2(s * 0.30f, s * 0.16f), 45f);
            Blob(r, UiGeneratedSprite.Kind.RoundedRect, 8, c, new Vector2(s * 0.10f, s * 0.02f), new Vector2(s * 0.62f, s * 0.16f), -45f);
        }

        static void Cross(RectTransform r, Color c)
        {
            float s = r.sizeDelta.x;
            Blob(r, UiGeneratedSprite.Kind.RoundedRect, 8, c, Vector2.zero, new Vector2(s * 0.72f, s * 0.16f), 45f);
            Blob(r, UiGeneratedSprite.Kind.RoundedRect, 8, c, Vector2.zero, new Vector2(s * 0.72f, s * 0.16f), -45f);
        }

        static void Warn(RectTransform r, Color c)
        {
            float s = r.sizeDelta.x;
            Blob(r, UiGeneratedSprite.Kind.Triangle, K, c, Vector2.zero, Vector2.one * s);
            var white = new Color(1f, 1f, 1f, 0.95f);
            Blob(r, UiGeneratedSprite.Kind.RoundedRect, 6, white, new Vector2(0, s * 0.02f), new Vector2(s * 0.12f, s * 0.36f));
            Blob(r, UiGeneratedSprite.Kind.Circle, K, white, new Vector2(0, -s * 0.24f), Vector2.one * s * 0.12f);
        }

        static void Sync(RectTransform r, Color c)
        {
            var img = Blob(r, UiGeneratedSprite.Kind.Ring, K, c, Vector2.zero, r.sizeDelta);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillAmount = 0.78f;
        }

        static Color Multiply(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);
    }
}
