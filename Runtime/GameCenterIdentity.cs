using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// iOS bridge for Game Center identity verification, required by Unity Gaming Services'
    /// <c>LinkWithAppleGameCenterAsync</c>. Unity's Social API does not expose the signature, so the
    /// package ships a tiny native plugin (Plugins/iOS/GameCenterIdentity.mm) that calls
    /// <c>GKLocalPlayer.generateIdentityVerificationSignature</c> and replies via UnitySendMessage.
    /// </summary>
    internal static class GameCenterIdentity
    {
        internal sealed class Signature
        {
            public string publicKeyUrl;
            public string signature;
            public string salt;
            public string timestamp;
            public string teamPlayerId;
            public string error;
        }

        const string ReceiverName = "CloudSaveSignatureReceiver";

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void UCS_GameCenterRequestIdentitySignature(string callbackObject, string callbackMethod);

        static TaskCompletionSource<Signature> _pending;

        internal static Task<Signature> RequestAsync()
        {
            EnsureReceiver();
            _pending = new TaskCompletionSource<Signature>();

            try
            {
                UCS_GameCenterRequestIdentitySignature(ReceiverName, nameof(CloudSaveSignatureReceiver.OnSignature));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSave] Game Center signature request failed: {e.Message}");
                _pending.TrySetResult(null);
            }

            return _pending.Task;
        }

        internal static void Complete(string json)
        {
            Signature signature;
            try
            {
                signature = JsonUtility.FromJson<Signature>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSave] Invalid Game Center signature JSON: {e.Message}");
                _pending?.TrySetResult(null);
                return;
            }

            if (signature == null || !string.IsNullOrEmpty(signature.error) || string.IsNullOrEmpty(signature.signature))
            {
                Debug.LogWarning($"[CloudSave] Game Center signature unavailable: {(signature != null ? signature.error : "null")}");
                _pending?.TrySetResult(null);
                return;
            }

            _pending?.TrySetResult(signature);
        }

        static void EnsureReceiver()
        {
            if (GameObject.Find(ReceiverName) != null) return;

            var go = new GameObject(ReceiverName);
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<CloudSaveSignatureReceiver>();
        }
#else
        internal static Task<Signature> RequestAsync() => Task.FromResult<Signature>(null);
        internal static void Complete(string json) { }
#endif
    }

    /// <summary>Receives the native iOS callback (UnitySendMessage) and forwards it to the bridge.</summary>
    [AddComponentMenu("")]
    public sealed class CloudSaveSignatureReceiver : MonoBehaviour
    {
        public void OnSignature(string json) => GameCenterIdentity.Complete(json);
    }
}
