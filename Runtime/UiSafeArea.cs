using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Keeps a RectTransform inside <see cref="Screen.safeArea"/> (notches, home indicators, rounded
    /// corners). Attach to a full-rect child of a ScreenSpaceOverlay / ScreenSpaceCamera canvas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiSafeArea : MonoBehaviour
    {
        RectTransform _rt;
        Rect _last = new(-1, -1, -1, -1);
        ScreenOrientation _lastOrientation;

        void Awake() => _rt = (RectTransform)transform;
        void OnEnable() => Apply();
        void Update()
        {
            if (Screen.safeArea != _last || Screen.orientation != _lastOrientation) Apply();
        }

        void Apply()
        {
            if (_rt == null) return;
            _last = Screen.safeArea;
            _lastOrientation = Screen.orientation;

            var area = Screen.safeArea;
            Vector2 min = area.position;
            Vector2 max = area.position + area.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;

            if (float.IsNaN(min.x) || float.IsNaN(max.x)) return;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
