using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Re-assigns a runtime-generated <see cref="UiSprites"/> sprite to this Image on load.
    /// Procedurally generated sprites are not saved into a prefab, so the built-in UIs attach this
    /// to every shaped Image; a generated <c>Resources/*.prefab</c> then rebuilds its look at runtime.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class UiGeneratedSprite : MonoBehaviour
    {
        public enum Kind { RoundedRect, Shadow, Ring, Circle, Triangle }

        public Kind Shape = Kind.RoundedRect;
        [Range(2, 96)] public int Radius = 24;

        void Awake() => Apply();
        void OnEnable() => Apply();
#if UNITY_EDITOR
        void OnValidate() { if (isActiveAndEnabled) Apply(); }
#endif

        public void Apply()
        {
            if (!TryGetComponent<Image>(out var img)) return;
            img.sprite = Shape switch
            {
                Kind.RoundedRect => UiSprites.RoundedRect(Radius),
                Kind.Shadow      => UiSprites.Shadow(Radius),
                Kind.Ring        => UiSprites.Ring(),
                Kind.Circle      => UiSprites.Circle(),
                Kind.Triangle    => UiSprites.Triangle(),
                _                => img.sprite,
            };
        }

        internal static UiGeneratedSprite Attach(Image img, Kind shape, int radius = 24)
        {
            var c = img.gameObject.AddComponent<UiGeneratedSprite>();
            c.Shape = shape;
            c.Radius = radius;
            c.Apply();
            return c;
        }
    }
}
