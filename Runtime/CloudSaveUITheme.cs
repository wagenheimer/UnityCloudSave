using TMPro;
using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Optional visual theme for the built-in Cloud Save UIs. Drop a
    /// <c>Resources/CloudSaveUITheme.asset</c> in your project (Assets → Create → Cloud Save → UI Theme)
    /// and the UIs pick it up automatically — no code changes. Any field left at its default keeps
    /// the built-in look.
    /// </summary>
    [CreateAssetMenu(menuName = "Cloud Save/UI Theme", fileName = "CloudSaveUITheme")]
    public sealed class CloudSaveUITheme : ScriptableObject
    {
        [Header("Surfaces")]
        public Color Overlay = new(0f, 0f, 0f, 0.72f);
        public Color Panel = new(0.12f, 0.12f, 0.14f, 0.97f);
        public Color LocalCard = new(0.18f, 0.18f, 0.22f, 1f);
        public Color CloudCard = new(0.10f, 0.22f, 0.38f, 1f);

        [Header("Semantic")]
        public Color Accent = new(0.22f, 0.60f, 1f, 1f);
        public Color Success = new(0.20f, 0.75f, 0.35f, 1f);
        public Color Warning = new(1f, 0.75f, 0.10f, 1f);
        public Color Error = new(0.85f, 0.25f, 0.20f, 1f);

        [Header("Text")]
        public Color Text = new(0.92f, 0.92f, 0.95f, 1f);
        public Color TextDim = new(0.65f, 0.65f, 0.70f, 1f);
        public TMP_FontAsset Font;

        [Header("Shape & motion")]
        [Range(4, 64)] public int CornerRadius = 28;
        public bool EnableAnimations = true;
        [Range(1f, 8f)] public float ToastSeconds = 2.6f;

        static CloudSaveUITheme _loaded;
        static bool _tried;

        /// <summary>The project theme from Resources, or null. Cached.</summary>
        public static CloudSaveUITheme Current
        {
            get
            {
                if (_tried) return _loaded;
                _tried = true;
                _loaded = Resources.Load<CloudSaveUITheme>("CloudSaveUITheme");
                return _loaded;
            }
        }
    }
}
