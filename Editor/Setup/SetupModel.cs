using System;
using System.Collections.Generic;
using System.Linq;

namespace Wagenheimer.CloudSave.Editor.Setup
{
    public enum ReadinessVerdict { Red, Amber, Green }

    public readonly struct Meter
    {
        public readonly int Done;
        public readonly int Total;
        public Meter(int done, int total) { Done = done; Total = total; }
        public float Fraction => Total > 0 ? (float)Done / Total : 1f;
        public override string ToString() => $"{Done} / {Total}";
    }

    /// <summary>
    /// The whole derived picture at one refresh: every step's evaluation, the three independent
    /// meters, the production-readiness verdict, and the single Next Best Action.
    /// </summary>
    public sealed class SetupSnapshot
    {
        public IReadOnlyList<StepEvaluation> Steps { get; }
        public Meter Integration { get; }
        public Meter Verification { get; }
        public ReadinessVerdict Readiness { get; }
        public IReadOnlyList<string> ReadinessBlockers { get; }
        public NextBestAction NextAction { get; }
        public DateTime ComputedAtUtc { get; }

        internal SetupSnapshot(
            IReadOnlyList<StepEvaluation> steps, Meter integration, Meter verification,
            ReadinessVerdict readiness, IReadOnlyList<string> readinessBlockers, NextBestAction nextAction)
        {
            Steps = steps;
            Integration = integration;
            Verification = verification;
            Readiness = readiness;
            ReadinessBlockers = readinessBlockers;
            NextAction = nextAction;
            ComputedAtUtc = DateTime.UtcNow;
        }

        public StepEvaluation Find(string stepId) => Steps.FirstOrDefault(s => s.Definition.Id == stepId);
    }

    /// <summary>
    /// Orchestrates one full recompute: topological order → per-step <see cref="StateEngine"/> pass
    /// (dependencies resolved from already-computed evaluations) → meters → readiness → next action.
    /// Everything is live; only the <see cref="CloudSaveSetupState"/> asset is persisted.
    /// </summary>
    public static class SetupModel
    {
        public static SetupSnapshot Compute(SetupRegistry registry, SetupContext ctx, IPersistedState persisted)
        {
            var ordered = DependencyEngine.TopologicalOrder(registry.Steps);
            var byId = new Dictionary<string, StepEvaluation>();

            foreach (var def in ordered)
            {
                var eval = StateEngine.Evaluate(
                    def, ctx,
                    registry.DetectorFor(def.Id),
                    persisted,
                    registry.ManualItemIdsFor(def.Id),
                    depId => byId.TryGetValue(depId, out var e) ? e : null);
                byId[def.Id] = eval;
            }

            // Present steps in registry order (stable for the UI), not topo order.
            var evals = registry.Steps.Select(s => byId[s.Id]).ToList();

            var integration = ComputeIntegration(evals);
            var verification = ComputeVerification(evals);
            var (verdict, blockers) = ComputeReadiness(evals);
            var next = DependencyEngine.PickNextAction(evals);

            return new SetupSnapshot(evals, integration, verification, verdict, blockers, next);
        }

        static Meter ComputeIntegration(IReadOnlyList<StepEvaluation> evals)
        {
            // Setup steps only. Verification-category steps have nothing of their own to configure —
            // they are measured solely by the Verification meter.
            var relevant = evals.Where(e => e.Applicable &&
                e.Definition.Category != StepCategory.Verification &&
                e.Definition.Obligation is Obligation.Required or Obligation.Recommended).ToList();
            int done = relevant.Count(e =>
                e.Configuration == ConfigurationStatus.Present &&
                e.Manual != ManualVerificationStatus.Unconfirmed);
            return new Meter(done, relevant.Count);
        }

        static Meter ComputeVerification(IReadOnlyList<StepEvaluation> evals)
        {
            var relevant = evals.Where(e => e.Applicable && e.Definition.HasRuntimeValidator).ToList();
            int done = relevant.Count(e => e.Runtime == RuntimeVerificationStatus.Passed);
            return new Meter(done, relevant.Count);
        }

        // Phase 0 placeholder for the Phase 4 ReadinessRule engine: a Required step that is not
        // finished is a blocker; Failed / Blocked / NotConfigured are hard RED.
        static (ReadinessVerdict, IReadOnlyList<string>) ComputeReadiness(IReadOnlyList<StepEvaluation> evals)
        {
            var required = evals.Where(e => e.Applicable && e.Definition.Obligation == Obligation.Required).ToList();
            var blockers = new List<string>();
            bool hardFail = false;

            foreach (var e in required)
            {
                switch (e.State)
                {
                    case StepState.Validated:
                    case StepState.ManuallyConfirmed:
                        break;
                    case StepState.Failed:
                    case StepState.Blocked:
                    case StepState.NotConfigured:
                    case StepState.NeedsAttention:
                        hardFail = true;
                        blockers.Add($"{e.Definition.Title}: {Humanize(e.State)}");
                        break;
                    default:
                        blockers.Add($"{e.Definition.Title}: {Humanize(e.State)}");
                        break;
                }
            }

            var verdict = blockers.Count == 0 ? ReadinessVerdict.Green
                : hardFail ? ReadinessVerdict.Red
                : ReadinessVerdict.Amber;
            return (verdict, blockers);
        }

        public static string Humanize(StepState s) => s switch
        {
            StepState.NotApplicable => "Not applicable",
            StepState.Blocked => "Blocked",
            StepState.NotConfigured => "Not configured",
            StepState.NeedsAttention => "Needs attention",
            StepState.NeedsValidation => "Needs validation",
            StepState.ManuallyConfirmed => "Manually confirmed",
            StepState.Failed => "Failed",
            StepState.Validated => "Done",
            StepState.Skipped => "Skipped",
            _ => s.ToString(),
        };
    }
}
