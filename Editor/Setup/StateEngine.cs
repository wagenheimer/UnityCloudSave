using System;
using System.Collections.Generic;
using System.Linq;
using Wagenheimer.CloudSave.Verification;

namespace Wagenheimer.CloudSave.Editor.Setup
{
    /// <summary>One resolved dependency edge with the upstream step's current state and whether the gate is met.</summary>
    public readonly struct DependencyResolution
    {
        public readonly string DependsOnId;
        public readonly DependencyGate Gate;
        public readonly bool Cascade;
        public readonly StepState UpstreamState;
        public readonly bool Met;

        public DependencyResolution(string dependsOnId, DependencyGate gate, bool cascade, StepState upstreamState, bool met)
        {
            DependsOnId = dependsOnId;
            Gate = gate;
            Cascade = cascade;
            UpstreamState = upstreamState;
            Met = met;
        }
    }

    /// <summary>
    /// The fully derived picture of one step at one point in time. Nothing here is persisted —
    /// it is recomputed on every refresh from the detector output + persisted records.
    /// </summary>
    public sealed class StepEvaluation
    {
        public StepDefinition Definition { get; }
        public bool Applicable { get; }
        public ConfigurationStatus Configuration { get; }
        public RuntimeVerificationStatus Runtime { get; }
        public ManualVerificationStatus Manual { get; }
        public StepState State { get; }

        public string CurrentFingerprint { get; }
        public ValidationRecord LastRecord { get; }

        public IReadOnlyList<string> ConfigFound { get; }
        public IReadOnlyList<string> ConfigMissing { get; }
        public IReadOnlyList<DependencyResolution> Dependencies { get; }

        /// <summary>Set when <see cref="State"/> is <see cref="StepState.Blocked"/> — names the first unmet dependency.</summary>
        public string BlockedByStepId { get; }

        /// <summary>True when a previous Passed record no longer matches the current configuration fingerprint.</summary>
        public bool RuntimeStale => Runtime == RuntimeVerificationStatus.Stale;

        internal StepEvaluation(
            StepDefinition definition, bool applicable,
            ConfigurationStatus configuration, RuntimeVerificationStatus runtime, ManualVerificationStatus manual,
            StepState state, string currentFingerprint, ValidationRecord lastRecord,
            IReadOnlyList<string> configFound, IReadOnlyList<string> configMissing,
            IReadOnlyList<DependencyResolution> dependencies, string blockedByStepId)
        {
            Definition = definition;
            Applicable = applicable;
            Configuration = configuration;
            Runtime = runtime;
            Manual = manual;
            State = state;
            CurrentFingerprint = currentFingerprint;
            LastRecord = lastRecord;
            ConfigFound = configFound;
            ConfigMissing = configMissing;
            Dependencies = dependencies;
            BlockedByStepId = blockedByStepId;
        }
    }

    /// <summary>
    /// Pure derivation of a single step's <see cref="StepState"/> from three independent signals
    /// (Configuration / Runtime / Manual) plus scope and already-resolved dependency states.
    /// This is the mechanism that stops "a Client ID was found" from ever reading as "it works".
    /// </summary>
    public static class StateEngine
    {
        /// <param name="resolveDependency">
        /// Returns the already-computed evaluation of an upstream step id, or null if unknown.
        /// The caller (SetupModel) walks steps in topological order so this is always populated.
        /// </param>
        public static StepEvaluation Evaluate(
            StepDefinition def,
            SetupContext ctx,
            IStepDetector detector,
            IPersistedState persisted,
            IReadOnlyList<string> manualItemIds,
            Func<string, StepEvaluation> resolveDependency)
        {
            bool applicable = def.AppliesTo(ctx);
            if (!applicable)
                return new StepEvaluation(def, false,
                    ConfigurationStatus.Unknown, RuntimeVerificationStatus.NotApplicable, ManualVerificationStatus.NotRequired,
                    StepState.NotApplicable, "", null,
                    Array.Empty<string>(), Array.Empty<string>(), Array.Empty<DependencyResolution>(), null);

            // ── Dependencies ────────────────────────────────────────────────
            var deps = new List<DependencyResolution>();
            string blockedBy = null;
            foreach (var edge in def.DependsOn)
            {
                var upstream = resolveDependency?.Invoke(edge.DependsOnId);
                var upstreamState = upstream?.State ?? StepState.NotConfigured;
                bool met = GateSatisfied(upstreamState, edge.Gate);
                deps.Add(new DependencyResolution(edge.DependsOnId, edge.Gate, edge.Cascade, upstreamState, met));
                if (!met && blockedBy == null) blockedBy = edge.DependsOnId;
            }

            // ── Configuration ──────────────────────────────────────────────
            var report = detector?.Detect(ctx) ?? new ConfigurationReport(ConfigurationStatus.Unknown);
            var configuration = report.Status;
            var fingerprint = Fingerprint.Compute(def.FingerprintInputs, report.FingerprintValues);

            // ── Manual ─────────────────────────────────────────────────────
            ManualVerificationStatus manual;
            if (!def.HasManualRequirement)
                manual = ManualVerificationStatus.NotRequired;
            else
                manual = persisted != null && persisted.IsStepManuallyConfirmed(def.Id, manualItemIds)
                    ? ManualVerificationStatus.Confirmed
                    : ManualVerificationStatus.Unconfirmed;

            // ── Runtime ────────────────────────────────────────────────────
            ValidationRecord last = persisted?.LatestRecordFor(def.Id);
            var runtime = DeriveRuntime(def, last, fingerprint);

            // Cascade: an upstream step this one depends on with cascade=true lost its validated status.
            if (runtime == RuntimeVerificationStatus.Passed)
            {
                bool upstreamInvalidated = deps.Any(d => d.Cascade &&
                    d.UpstreamState is not StepState.Validated and not StepState.ManuallyConfirmed and not StepState.NotApplicable);
                if (upstreamInvalidated) runtime = RuntimeVerificationStatus.Stale;
            }

            // ── Derive overall state ───────────────────────────────────────
            StepState state = DeriveState(def, blockedBy != null, configuration, runtime, manual);

            return new StepEvaluation(def, true, configuration, runtime, manual, state, fingerprint, last,
                report.Found, report.Missing, deps, blockedBy);
        }

        static bool GateSatisfied(StepState upstream, DependencyGate gate) => gate switch
        {
            DependencyGate.RequiresValidated => upstream == StepState.Validated,
            DependencyGate.RequiresConfigured => upstream is StepState.NeedsValidation
                or StepState.ManuallyConfirmed or StepState.Validated,
            _ => false,
        };

        static RuntimeVerificationStatus DeriveRuntime(StepDefinition def, ValidationRecord last, string currentFingerprint)
        {
            if (!def.HasRuntimeValidator) return RuntimeVerificationStatus.NotApplicable;
            if (last == null) return RuntimeVerificationStatus.NotRun;

            if (last.Outcome == nameof(ValidationOutcome.Failed))
                return RuntimeVerificationStatus.Failed;

            if (last.Outcome == nameof(ValidationOutcome.Passed))
            {
                bool hasFingerprint = def.FingerprintInputs.Count > 0;
                if (hasFingerprint && last.Fingerprint != currentFingerprint)
                    return RuntimeVerificationStatus.Stale;
                return RuntimeVerificationStatus.Passed;
            }

            // Inconclusive / Blocked / Skipped — not yet proven.
            return RuntimeVerificationStatus.NotRun;
        }

        static StepState DeriveState(
            StepDefinition def, bool blocked,
            ConfigurationStatus config, RuntimeVerificationStatus runtime, ManualVerificationStatus manual)
        {
            if (blocked) return StepState.Blocked;

            if (config is ConfigurationStatus.Missing or ConfigurationStatus.Unknown)
                return StepState.NotConfigured;

            if (config == ConfigurationStatus.Partial)
                return StepState.NeedsAttention;

            // config == Present
            if (runtime == RuntimeVerificationStatus.Failed) return StepState.Failed;

            if (runtime is RuntimeVerificationStatus.NotRun or RuntimeVerificationStatus.Stale or RuntimeVerificationStatus.Running)
                return StepState.NeedsValidation;

            if (manual == ManualVerificationStatus.Unconfirmed)
                return StepState.NeedsValidation;

            // runtime is Passed or NotApplicable; manual is Confirmed or NotRequired
            if (runtime == RuntimeVerificationStatus.NotApplicable && !def.HasRuntimeValidator
                && manual == ManualVerificationStatus.Confirmed)
                return StepState.ManuallyConfirmed;

            return StepState.Validated;
        }
    }
}
