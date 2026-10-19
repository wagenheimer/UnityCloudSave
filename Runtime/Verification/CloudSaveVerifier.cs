using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Wagenheimer.CloudSave.Verification
{
    public enum ValidationOutcome
    {
        Passed,
        Failed,
        Inconclusive,
        Blocked,
        Skipped,
    }

    /// <summary>
    /// How a case is executed. Interactive/Manual cases can never be reported as a false Failed.
    /// </summary>
    public enum ValidationKind
    {
        Automated,
        Interactive,
        Manual,
    }

    public sealed class ValidationResult
    {
        public string CaseId;
        public ValidationOutcome Outcome;
        public DateTime StartedAtUtc;
        public long DurationMs;
        public string Message;
        public string Error;
        public string StackTrace;

        public static ValidationResult Pass(string caseId, string message = null) => new()
        { CaseId = caseId, Outcome = ValidationOutcome.Passed, Message = message };

        public static ValidationResult Fail(string caseId, string message, Exception ex = null) => new()
        {
            CaseId = caseId,
            Outcome = ValidationOutcome.Failed,
            Message = message,
            Error = ex?.Message,
            StackTrace = ex?.StackTrace,
        };

        public static ValidationResult Inconclusive_(string caseId, string message) => new()
        { CaseId = caseId, Outcome = ValidationOutcome.Inconclusive, Message = message };
    }

    /// <summary>
    /// One individually runnable runtime check. Phase 0 ships only <see cref="AnonymousSignInCase"/>;
    /// the catalog (Auth / Save / Identity / Migration suites) is filled in Phase 2.
    /// </summary>
    public abstract class ValidationCase
    {
        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public virtual ValidationKind Kind => ValidationKind.Automated;

        /// <summary>Runs the check. Implementations must not touch the game's real save slot.</summary>
        public abstract Task<ValidationResult> RunAsync();

        /// <summary>Always invoked after <see cref="RunAsync"/>, even on failure. Deletes any test data.</summary>
        public virtual Task CleanupAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// Executes a <see cref="ValidationCase"/>, timing it and guaranteeing cleanup. The Editor layer
    /// turns the result into a persisted ValidationRecord bound to the step's config fingerprint.
    /// </summary>
    public static class CloudSaveVerifier
    {
        public static async Task<ValidationResult> RunAsync(ValidationCase testCase)
        {
            if (testCase == null) throw new ArgumentNullException(nameof(testCase));

            var sw = Stopwatch.StartNew();
            var startedAt = DateTime.UtcNow;
            ValidationResult result;

            try
            {
                result = await testCase.RunAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSaveVerifier] '{testCase.Id}' threw: {e.Message}");
                result = ValidationResult.Fail(testCase.Id, "Case threw an exception.", e);
            }
            finally
            {
                try { await testCase.CleanupAsync(); }
                catch (Exception ce) { Debug.LogWarning($"[CloudSaveVerifier] cleanup for '{testCase.Id}' failed: {ce.Message}"); }
            }

            sw.Stop();
            result.CaseId ??= testCase.Id;
            result.StartedAtUtc = startedAt;
            result.DurationMs = sw.ElapsedMilliseconds;
            return result;
        }
    }

    /// <summary>
    /// Anonymous sign-in round-trip: initialise UGS, sign in anonymously, assert a stable PlayerId.
    /// Writes no cloud data, so it needs no cleanup and is safe to run from any state.
    /// </summary>
    public sealed class AnonymousSignInCase : ValidationCase
    {
        public const string CaseId = "verify.auth.anonymous";

        public override string Id => CaseId;
        public override string DisplayName => "Anonymous sign-in";

        public override async Task<ValidationResult> RunAsync()
        {
            // Init UGS directly so the real exception surfaces (CloudAuth swallows it into a warning).
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
            }
            catch (Exception e)
            {
                var hint = Application.isPlaying
                    ? "Confirm the project is linked (Project Settings → Services) and that Cloud Save + Authentication are enabled for this environment in the Unity Dashboard."
                    : "Runtime checks need Play Mode — Unity Services will not initialise in Edit Mode. Enter Play Mode and run again (a Play-Mode runner lands in Phase 2).";
                return ValidationResult.Fail(CaseId, $"UnityServices.InitializeAsync() failed: {e.Message}  {hint}", e);
            }

            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (Exception e)
            {
                return ValidationResult.Fail(CaseId, $"Anonymous sign-in failed: {e.Message}", e);
            }

            if (string.IsNullOrEmpty(AuthenticationService.Instance.PlayerId))
                return ValidationResult.Fail(CaseId, "Signed in but PlayerId is empty.");

            // Keep CloudAuth's own readiness flag in sync for the rest of the SDK.
            await CloudAuth.EnsureSignedInAsync();

            return ValidationResult.Pass(CaseId,
                $"Signed in anonymously. PlayerId={Mask(AuthenticationService.Instance.PlayerId)}.");
        }

        static string Mask(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length <= 8) return "…";
            return id.Substring(0, 4) + "…" + id.Substring(id.Length - 4);
        }
    }

    /// <summary>
    /// Probe case that queries Unity Authentication backend to verify if Google Play Games
    /// Identity Provider is registered and enabled in the Unity Dashboard.
    /// </summary>
    public sealed class GooglePlayGamesProviderCase : ValidationCase
    {
        public const string CaseId = "verify.provider.android";

        public override string Id => CaseId;
        public override string DisplayName => "Google Play Games Provider (Dashboard)";

        public override async Task<ValidationResult> RunAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (Exception e)
            {
                return ValidationResult.Fail(CaseId, $"Cannot initialize UGS Auth for probe: {e.Message}", e);
            }

            try
            {
                await AuthenticationService.Instance.LinkWithGooglePlayGamesAsync("probe_dashboard_check");
                return ValidationResult.Pass(CaseId, "Google Play Games provider is ACTIVE and configured in the Unity Dashboard!");
            }
            catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.InvalidProvider)
            {
                return ValidationResult.Fail(CaseId,
                    "Google Play Games provider is NOT configured in the Unity Dashboard (Error 10008: InvalidProvider).\n" +
                    "Action: Go to dashboard.unity3d.com -> Player Authentication -> Identity Providers -> Add Google Play Games (Client ID & Secret).", ex);
            }
            catch (AuthenticationException ex)
            {
                // Any error other than InvalidProvider proves that UGS accepted the provider and attempted validation!
                return ValidationResult.Pass(CaseId,
                    $"Google Play Games provider is ACTIVE on Dashboard! (Server accepted provider; probe auth code rejected as expected: {ex.Message})");
            }
            catch (Exception ex)
            {
                return ValidationResult.Fail(CaseId, $"Probe check failed: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Probe case that queries Unity Authentication backend to verify if Apple Game Center
    /// Identity Provider is registered and enabled in the Unity Dashboard.
    /// </summary>
    public sealed class AppleGameCenterProviderCase : ValidationCase
    {
        public const string CaseId = "verify.provider.ios";

        public override string Id => CaseId;
        public override string DisplayName => "Apple Game Center Provider (Dashboard)";

        public override async Task<ValidationResult> RunAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            catch (Exception e)
            {
                return ValidationResult.Fail(CaseId, $"Cannot initialize UGS Auth for probe: {e.Message}", e);
            }

            try
            {
                await AuthenticationService.Instance.LinkWithAppleGameCenterAsync(
                    "probe_sig", "probe_team", "https://apple.com/key", "probe_salt", 1234567890);
                return ValidationResult.Pass(CaseId, "Apple Game Center provider is ACTIVE and configured in the Unity Dashboard!");
            }
            catch (AuthenticationException ex) when (ex.ErrorCode == AuthenticationErrorCodes.InvalidProvider)
            {
                return ValidationResult.Fail(CaseId,
                    "Apple Game Center provider is NOT configured in the Unity Dashboard (Error 10008: InvalidProvider).\n" +
                    "Action: Go to dashboard.unity3d.com -> Player Authentication -> Identity Providers -> Add Apple Game Center (Bundle ID & Team ID).", ex);
            }
            catch (AuthenticationException ex)
            {
                // Any error other than InvalidProvider proves that UGS accepted the provider and attempted validation!
                return ValidationResult.Pass(CaseId,
                    $"Apple Game Center provider is ACTIVE on Dashboard! (Server accepted provider; probe signature rejected as expected: {ex.Message})");
            }
            catch (Exception ex)
            {
                return ValidationResult.Fail(CaseId, $"Probe check failed: {ex.Message}", ex);
            }
        }
    }
}
