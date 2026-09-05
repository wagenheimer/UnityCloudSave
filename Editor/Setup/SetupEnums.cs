namespace Wagenheimer.CloudSave.Editor.Setup
{
    /// <summary>
    /// What the step's <see cref="IStepDetector"/> found in project files / settings / packages.
    /// This is the "are the artifacts present?" signal — never "does it work?".
    /// </summary>
    public enum ConfigurationStatus
    {
        Unknown,
        Missing,
        Partial,
        Present,
    }

    /// <summary>
    /// Result of the step's runtime validation (may not apply to every step).
    /// <see cref="Stale"/> = it passed once, but the step's configuration fingerprint changed since.
    /// </summary>
    public enum RuntimeVerificationStatus
    {
        NotApplicable,
        NotRun,
        Running,
        Passed,
        Failed,
        Stale,
    }

    /// <summary>
    /// External-console confirmation. <see cref="Confirmed"/> means "the developer ticked it",
    /// which is explicitly NOT the same as "the system verified it".
    /// </summary>
    public enum ManualVerificationStatus
    {
        NotRequired,
        Unconfirmed,
        Confirmed,
    }

    /// <summary>
    /// The single value a developer reads off a step. Derived, never stored (see <see cref="StateEngine"/>).
    /// </summary>
    public enum StepState
    {
        /// <summary>Excluded from this project (e.g. Apple on an Android-only build).</summary>
        NotApplicable,

        /// <summary>A dependency is below its required gate — the step can't be acted on yet.</summary>
        Blocked,

        /// <summary>Nothing detected.</summary>
        NotConfigured,

        /// <summary>Some artifacts found, some missing.</summary>
        NeedsAttention,

        /// <summary>Configured, but not proven — runtime never ran / went stale, or a manual item is unconfirmed.</summary>
        NeedsValidation,

        /// <summary>Configured + developer-confirmed, and the step has no runtime validator. Distinct from <see cref="Validated"/>.</summary>
        ManuallyConfirmed,

        /// <summary>Runtime validation ran and failed.</summary>
        Failed,

        /// <summary>Configured + runtime passed with a current fingerprint + manual (if any) confirmed.</summary>
        Validated,

        /// <summary>Developer explicitly skipped it.</summary>
        Skipped,
    }

    public enum Obligation
    {
        Required,
        Recommended,
        Optional,
    }

    /// <summary>Ordering also drives the "logical order" tiebreak in <see cref="DependencyEngine"/> NextBestAction.</summary>
    public enum StepCategory
    {
        Prerequisites = 0,
        Services = 1,
        StartupCode = 2,
        SaveData = 3,
        Ui = 4,
        Providers = 5,
        StoreCompliance = 6,
        Migration = 7,
        Verification = 8,
    }

    /// <summary>How strong a dependency is: does the upstream step only need to be configured, or fully validated?</summary>
    public enum DependencyGate
    {
        RequiresConfigured,
        RequiresValidated,
    }
}
