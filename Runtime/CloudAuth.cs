using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Manages Unity Authentication lifecycle: anonymous sign-in at startup and
    /// optional upgrade to a linked provider (Google Play Games or Apple).
    ///
    /// Usage:
    ///   1. await CloudAuth.EnsureSignedInAsync()          — called automatically by CloudSync
    ///   2. await CloudAuth.LinkGooglePlayGamesAsync(code)  — Android, after GPGS auth
    ///   3. await CloudAuth.LinkAppleAsync(idToken)         — iOS, after Sign in with Apple
    ///   4. await CloudAuth.LinkAppleGameCenterAsync(...)   — iOS, after Game Center auth
    /// </summary>
    public static class CloudAuth
    {
        static CloudAuthProvider _provider = CloudAuthProvider.Anonymous;

        /// <summary>True once UGS is initialized and the user is signed in.</summary>
        public static bool IsReady { get; private set; }

        /// <summary>True when signed in (anonymous or linked).</summary>
        public static bool IsSignedIn => IsReady && AuthenticationService.Instance.IsSignedIn;

        /// <summary>True when signed in with anonymous identity only (no linked provider).</summary>
        public static bool IsAnonymous => IsSignedIn && _provider == CloudAuthProvider.Anonymous;

        /// <summary>True when the anonymous account has been upgraded to a linked provider.</summary>
        public static bool IsLinked => IsSignedIn && _provider != CloudAuthProvider.Anonymous;

        /// <summary>The active auth provider.</summary>
        public static CloudAuthProvider Provider => _provider;

        /// <summary>Unity-assigned player ID. Stable across anonymous sign-ins on the same device.
        /// Preserved after linking — same ID before and after upgrade.</summary>
        public static string PlayerId => IsReady ? AuthenticationService.Instance.PlayerId : null;

        /// <summary>Fires after a successful link or sign-in to a provider.</summary>
        public static event Action<CloudAuthProvider> OnLinked;

        /// <summary>
        /// Fires when sign-in results in a different account than the current anonymous one
        /// (i.e. <see cref="CloudLinkStatus.SignedInExisting"/>). The PlayerId changes.
        /// Re-run cloud sync after this event to pull saves from the recovered account.
        /// </summary>
        public static event Action<CloudAuthProvider> OnAccountSwitched;

        // ── Init ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes Unity Services and signs in anonymously if not already signed in.
        /// Idempotent — safe to call multiple times.
        /// </summary>
        public static async Task EnsureSignedInAsync()
        {
            if (IsReady) return;
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                IsReady = true;
                Debug.Log($"[CloudAuth] Ready. PlayerId={PlayerId} Provider={_provider}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudAuth] Init failed: {e.Message}");
            }
        }

        // ── Android ───────────────────────────────────────────────────────────

        /// <summary>
        /// Links the anonymous account to Google Play Games, or signs in to the
        /// existing linked account if already linked on another device.
        ///
        /// On Android, get <paramref name="serverAuthCode"/> from:
        ///   PlayGamesPlatform.Instance.RequestServerSideAccess(forceRefreshToken: false, code => ...)
        ///
        /// CloudLinkStatus meanings:
        ///   Linked           — first time linking; PlayerId unchanged.
        ///   SignedInExisting — credential was already linked to another Unity account
        ///                      (e.g. player reinstalled). PlayerId switches to existing account.
        ///                      Re-run cloud sync to pull saves from that account.
        ///   Failed           — check Message for details.
        /// </summary>
        public static async Task<CloudLinkResult> LinkGooglePlayGamesAsync(string serverAuthCode)
        {
            if (string.IsNullOrEmpty(serverAuthCode))
                return CloudLinkResult.Fail("serverAuthCode is null or empty.");

            await EnsureSignedInAsync();
            if (!IsReady)
                return CloudLinkResult.Fail("CloudAuth not initialized.");

            try
            {
                await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(serverAuthCode);
                return FinalizeLink(CloudAuthProvider.GooglePlayGames, CloudLinkStatus.Linked);
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                return await SignInWithProviderAsync(
                    CloudAuthProvider.GooglePlayGames,
                    () => AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(serverAuthCode));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudAuth] LinkGooglePlayGames failed: {e.Message}");
                return CloudLinkResult.Fail(e.Message);
            }
        }

        // ── iOS — Sign in with Apple ──────────────────────────────────────────

        /// <summary>
        /// Links the anonymous account to Apple (Sign in with Apple), or signs in
        /// to the existing linked account.
        ///
        /// Requires the Apple Authentication Unity Plugin to obtain the identity token:
        ///   https://github.com/lupidan/apple-signin-unity
        ///
        /// Pass the <c>identityToken</c> from the plugin's credential response.
        /// </summary>
        public static async Task<CloudLinkResult> LinkAppleAsync(string identityToken)
        {
            if (string.IsNullOrEmpty(identityToken))
                return CloudLinkResult.Fail("identityToken is null or empty.");

            await EnsureSignedInAsync();
            if (!IsReady)
                return CloudLinkResult.Fail("CloudAuth not initialized.");

            try
            {
                await AuthenticationService.Instance.LinkWithAppleAsync(identityToken);
                return FinalizeLink(CloudAuthProvider.Apple, CloudLinkStatus.Linked);
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                return await SignInWithProviderAsync(
                    CloudAuthProvider.Apple,
                    () => AuthenticationService.Instance.SignInWithAppleAsync(identityToken));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudAuth] LinkApple failed: {e.Message}");
                return CloudLinkResult.Fail(e.Message);
            }
        }

        // ── iOS — Apple Game Center ───────────────────────────────────────────

        /// <summary>
        /// Links the anonymous account to Apple Game Center (no Sign in with Apple plugin needed).
        ///
        /// Requires a native iOS bridge to call GKLocalPlayer.generateIdentityVerificationSignature.
        /// See README for a sample native plugin / Apple.GameKit package integration.
        ///
        /// Parameters come from the GKLocalPlayer identity verification callback:
        ///   publicKeyUrl  — URL of Apple's public key
        ///   signature     — base-64 encoded signature
        ///   salt          — base-64 encoded salt
        ///   timestamp     — timestamp from the callback
        ///   teamPlayerId  — GKLocalPlayer.teamPlayerID
        /// </summary>
        public static async Task<CloudLinkResult> LinkAppleGameCenterAsync(
            string publicKeyUrl,
            string signature,
            string salt,
            ulong  timestamp,
            string teamPlayerId)
        {
            if (string.IsNullOrEmpty(signature))
                return CloudLinkResult.Fail("Game Center signature is null or empty.");

            await EnsureSignedInAsync();
            if (!IsReady)
                return CloudLinkResult.Fail("CloudAuth not initialized.");

            try
            {
                await AuthenticationService.Instance.LinkWithAppleGameCenterAsync(
                    signature, teamPlayerId, publicKeyUrl, salt, timestamp);
                return FinalizeLink(CloudAuthProvider.AppleGameCenter, CloudLinkStatus.Linked);
            }
            catch (AuthenticationException e) when (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                return await SignInWithProviderAsync(
                    CloudAuthProvider.AppleGameCenter,
                    () => AuthenticationService.Instance.SignInWithAppleGameCenterAsync(
                        signature, teamPlayerId, publicKeyUrl, salt, timestamp));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudAuth] LinkAppleGameCenter failed: {e.Message}");
                return CloudLinkResult.Fail(e.Message);
            }
        }

        // ── private helpers ───────────────────────────────────────────────────

        static CloudLinkResult FinalizeLink(CloudAuthProvider provider, CloudLinkStatus status)
        {
            _provider = provider;
            Debug.Log($"[CloudAuth] {status}: provider={provider} PlayerId={PlayerId}");
            OnLinked?.Invoke(provider);
            if (status == CloudLinkStatus.SignedInExisting)
                OnAccountSwitched?.Invoke(provider);
            return CloudLinkResult.Ok(status);
        }

        static async Task<CloudLinkResult> SignInWithProviderAsync(
            CloudAuthProvider provider,
            Func<Task> signInCall)
        {
            try
            {
                await signInCall();
                return FinalizeLink(provider, CloudLinkStatus.SignedInExisting);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CloudAuth] SignIn fallback failed: {ex.Message}");
                return CloudLinkResult.Fail(ex.Message);
            }
        }
    }
}
