using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Hidden runtime hook installed by <see cref="CloudSaveController"/> when
    /// <see cref="CloudSaveOptions.FlushOnApplicationPause"/> is on (default). It uploads the
    /// pending save when the app is backgrounded or closed, so games don't need their own
    /// <c>OnApplicationPause</c>/<c>OnApplicationQuit</c> handlers.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class CloudSaveLifecycle : MonoBehaviour
    {
        internal CloudSaveController Controller;

        void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        void OnApplicationQuit() => Flush();

        void Flush()
        {
            if (Controller == null) return;
            _ = Controller.FlushAsync();
        }
    }
}
