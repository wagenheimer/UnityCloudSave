using System;
using System.Collections.Generic;

namespace Wagenheimer.CloudSave.Editor.Setup
{
    /// <summary>
    /// A single external link shown on a step (docs, dashboard, console).
    /// </summary>
    public readonly struct StepLink
    {
        public readonly string Label;
        public readonly string Url;

        public StepLink(string label, string url)
        {
            Label = label;
            Url = url;
        }
    }

    /// <summary>
    /// Structured, developer-facing copy for a step. This is the single source for the
    /// wizard screen, the dashboard card body, generated docs, and the CLI report — no drift.
    /// Every field is plain prose; the internal machinery (state machine, graph, fingerprints)
    /// is never surfaced as jargon.
    /// </summary>
    public sealed class StepCopy
    {
        public string WhatIsThis = "";
        public string WhyNeeded = "";
        public string WhatYouDo = "";
        public string WhatWeAutoVerify = "";
        public string WhatYouConfirmManually = "";
        public string HowToTest = "";
        public string ExpectedResult = "";
        public StepLink[] Links = Array.Empty<StepLink>();
    }

    /// <summary>
    /// A dependency edge from this step to an upstream step.
    /// <see cref="Cascade"/>: when the upstream step goes Stale/Failed, edges with cascade=true
    /// push this step back to NeedsValidation; edges without it don't.
    /// </summary>
    public readonly struct DependencyEdge
    {
        public readonly string DependsOnId;
        public readonly DependencyGate Gate;
        public readonly bool Cascade;

        public DependencyEdge(string dependsOnId, DependencyGate gate, bool cascade)
        {
            DependsOnId = dependsOnId;
            Gate = gate;
            Cascade = cascade;
        }
    }

    /// <summary>
    /// Pure data. Describes one setup step: identity, ordering, obligation, scope, dependencies,
    /// which config inputs form its staleness fingerprint, and its copy. Behaviour (detect / fix /
    /// validate) is composed separately and wired by <see cref="SetupRegistry"/>.
    /// </summary>
    public sealed class StepDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public StepCategory Category { get; }
        public Obligation Obligation { get; }

        /// <summary>Returns true when this step applies to the current project (platform / provider scope).</summary>
        public Func<SetupContext, bool> AppliesTo { get; }

        public IReadOnlyList<DependencyEdge> DependsOn { get; }

        /// <summary>
        /// Ordered names of the configuration inputs that matter to THIS step. The detector reports
        /// their current values; the State Engine hashes them into the step's ConfigFingerprint.
        /// Scoped on purpose — changing Google's inputs must not invalidate the Anonymous test.
        /// </summary>
        public IReadOnlyList<string> FingerprintInputs { get; }

        public StepCopy Copy { get; }

        /// <summary>True when this step has an external-console checklist the developer must confirm.</summary>
        public bool HasManualRequirement { get; }

        /// <summary>True when this step has a runtime validation case.</summary>
        public bool HasRuntimeValidator { get; }

        /// <summary>
        /// Optional ready-to-paste prompt for an AI assistant to add or fix exactly this step.
        /// When null, the Hub generates one from <see cref="Copy"/>.
        /// </summary>
        public string AiPrompt { get; }

        public StepDefinition(
            string id,
            string title,
            StepCategory category,
            Obligation obligation,
            StepCopy copy,
            Func<SetupContext, bool> appliesTo = null,
            IReadOnlyList<DependencyEdge> dependsOn = null,
            IReadOnlyList<string> fingerprintInputs = null,
            bool hasManualRequirement = false,
            bool hasRuntimeValidator = false,
            string aiPrompt = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Step id is required", nameof(id));
            Id = id;
            Title = title ?? id;
            Category = category;
            Obligation = obligation;
            Copy = copy ?? new StepCopy();
            AppliesTo = appliesTo ?? (_ => true);
            DependsOn = dependsOn ?? Array.Empty<DependencyEdge>();
            FingerprintInputs = fingerprintInputs ?? Array.Empty<string>();
            HasManualRequirement = hasManualRequirement;
            HasRuntimeValidator = hasRuntimeValidator;
            AiPrompt = aiPrompt;
        }
    }
}
