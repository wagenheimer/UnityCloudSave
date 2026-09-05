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
}
