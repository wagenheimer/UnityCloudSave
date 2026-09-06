using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Optional host for the built-in Cloud Save UIs. Set <see cref="Canvas"/> to your game's
    /// existing Canvas and the UIs will parent their content under it instead of creating their own
    /// ScreenSpaceOverlay canvas (and they will not call <c>DontDestroyOnLoad</c>).
    ///
    /// <code>CloudSaveUiHost.Canvas = myGameCanvas;   // before CloudSaveUI.Create()</code>
    ///
    /// <see cref="CloudSaveController"/> sets this automatically from
    /// <c>CloudSaveOptions.UiCanvas</c>. Leave it null for the zero-config own-canvas behaviour.
    /// </summary>
    public static class CloudSaveUiHost
    {
        public static Canvas Canvas;

        /// <summary>The Canvas a built-in UI should build into: an ancestor Canvas, else the host, else null (create one).</summary>
        internal static Canvas Resolve(Component ui)
        {
            var parent = ui.GetComponentInParent<Canvas>();
            if (parent != null) return parent;
            return Canvas != null ? Canvas : null;
        }
    }
}
