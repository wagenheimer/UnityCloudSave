using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// An optional set of ready-made sprites for the built-in Cloud Save UIs. Assign one to
    /// <see cref="CloudSaveUITheme.Skin"/> and the UIs use these instead of the procedural shapes:
    /// any slot left null falls back to the generated primitive, so a skin can be partial.
    ///
    /// A polished default skin ships as the <c>PolishedSkin</c> sample.
    /// </summary>
    [CreateAssetMenu(menuName = "Cloud Save/UI Skin", fileName = "CloudSaveUISkin")]
    public sealed class CloudSaveUISkin : ScriptableObject
    {
        [Header("Surfaces (9-sliced — set Sprite border in the importer)")]
        public Sprite Panel;
        public Sprite Button;
        public Sprite Shadow;

        [Header("Spinner")]
        public Sprite Ring;

        [Header("Full-screen backdrop (optional, sits behind the dim overlay)")]
        public Sprite Backdrop;
        [Range(0f, 1f)] public float BackdropAlpha = 0.5f;

        [Header("Icons (optional — replace the vector icons)")]
        public Sprite CloudIcon;
        public Sprite DeviceIcon;
        public Sprite CheckIcon;
        public Sprite WarnIcon;
        public Sprite CrossIcon;
        public Sprite SyncIcon;

        public Sprite ForShape(UiGeneratedSprite.Kind kind) => kind switch
        {
            UiGeneratedSprite.Kind.RoundedRect => Panel,
            UiGeneratedSprite.Kind.Shadow      => Shadow,
            UiGeneratedSprite.Kind.Ring        => Ring,
            _ => null,
        };

        internal Sprite ForIcon(UiIcon icon) => icon switch
        {
            UiIcon.Cloud  => CloudIcon,
            UiIcon.Device => DeviceIcon,
            UiIcon.Check  => CheckIcon,
            UiIcon.Warn   => WarnIcon,
            UiIcon.Cross  => CrossIcon,
            UiIcon.Sync   => SyncIcon,
            _ => null,
        };
    }
}
