using System;
using System.Collections.Generic;
using System.Linq;
using Wagenheimer.CloudSave.Editor.Setup.Detectors;
using Wagenheimer.CloudSave.Verification;

namespace Wagenheimer.CloudSave.Editor.Setup
{
    /// <summary>
    /// Phase 0 registry: a hand-authored 4-step chain that exercises the whole engine
    /// (state derivation, dependency blocking, Next Best Action, fingerprint staleness) end to end.
    /// Phase 1 replaces the body of <see cref="BuildSteps"/> with the refactored CloudSaveAudit checks,
    /// and Phase 3 adds provider-module discovery — the Hub UI does not change.
    /// </summary>
    public sealed class SetupRegistry
    {
        public static class Ids
        {
            public const string UgsProject = "ugs.project";
            public const string CloudSaveService = "ugs.cloudsave";
            public const string AnonymousAuth = "ugs.auth.anonymous";
            public const string AnonymousVerification = "verify.auth.anonymous";
        }

        readonly List<StepDefinition> _steps;
        readonly Dictionary<string, IStepDetector> _detectors;
        readonly Dictionary<string, Func<ValidationCase>> _cases;
        readonly Dictionary<string, string[]> _manualItems;

        public IReadOnlyList<StepDefinition> Steps => _steps;

        public IStepDetector DetectorFor(string stepId)
            => _detectors.TryGetValue(stepId, out var d) ? d : null;

        public IReadOnlyList<string> ManualItemIdsFor(string stepId)
            => _manualItems.TryGetValue(stepId, out var m) ? m : Array.Empty<string>();

        /// <summary>Returns a fresh <see cref="ValidationCase"/> for a step, or null if it has no runtime check.</summary>
        public ValidationCase CreateCaseFor(string stepId)
            => _cases.TryGetValue(stepId, out var f) ? f() : null;

        public SetupRegistry()
        {
            _detectors = new Dictionary<string, IStepDetector>
            {
                [Ids.UgsProject] = new UgsProjectDetector(),
                [Ids.CloudSaveService] = new CloudSaveServiceDetector(),
                [Ids.AnonymousAuth] = new AnonymousAuthDetector(),
                [Ids.AnonymousVerification] = new AnonymousVerificationDetector(),
            };

            _cases = new Dictionary<string, Func<ValidationCase>>
            {
                [Ids.AnonymousVerification] = () => new AnonymousSignInCase(),
            };

            _manualItems = new Dictionary<string, string[]>();

            _steps = BuildSteps();

            // Fail fast on authoring cycles.
            DependencyEngine.TopologicalOrder(_steps);
        }

        static List<StepDefinition> BuildSteps() => new()
        {
            new StepDefinition(
                id: Ids.UgsProject,
                title: "Link your Unity project",
                category: StepCategory.Prerequisites,
                obligation: Obligation.Required,
                fingerprintInputs: new[] { UgsProjectDetector.CloudProjectId, UgsProjectDetector.OrganizationId },
                copy: new StepCopy
                {
                    WhatIsThis = "Connects this Unity project to a project in Unity Cloud, where Cloud Save data lives.",
                    WhyNeeded = "Every Unity Gaming Services feature — Authentication, Cloud Save — is scoped to a linked cloud project.",
                    WhatYouDo = "Edit → Project Settings → Services → sign in and select or create a project.",
                    WhatWeAutoVerify = "That a cloud project id and organization are written to ProjectSettings.",
                    WhatYouConfirmManually = "Nothing for this step.",
                    HowToTest = "The next steps' runtime checks fail if the link is wrong.",
                    ExpectedResult = "A cloud project id and organization appear here.",
                    Links = new[]
                    {
                        new StepLink("Unity Dashboard", "https://cloud.unity.com/"),
                        new StepLink("Linking a project", "https://docs.unity.com/ugs/manual/overview/manual/getting-started"),
                    },
                }),

            new StepDefinition(
                id: Ids.CloudSaveService,
                title: "Add the Cloud Save package",
                category: StepCategory.Services,
                obligation: Obligation.Required,
                dependsOn: new[] { new DependencyEdge(Ids.UgsProject, DependencyGate.RequiresConfigured, cascade: false) },
                fingerprintInputs: new[] { CloudSaveServiceDetector.PackageVersionInput },
                copy: new StepCopy
                {
                    WhatIsThis = "The com.unity.services.cloudsave package — the client SDK for reading and writing player cloud data.",
                    WhyNeeded = "This package's API is what the Cloud Save SDK calls under the hood.",
                    WhatYouDo = "It is a dependency of this package and normally resolves automatically. If missing, add it via Package Manager.",
                    WhatWeAutoVerify = "That the package resolves in packages-lock.json / manifest.json.",
                    WhatYouConfirmManually = "That the Cloud Save service is enabled for your environment in the Unity Dashboard.",
                    HowToTest = "The anonymous sign-in check and (Phase 2) the save round-trip prove the service is on.",
                    ExpectedResult = "The resolved package version appears here.",
                    Links = new[] { new StepLink("Cloud Save docs", "https://docs.unity.com/ugs/manual/cloud-save/manual") },
                }),

            new StepDefinition(
                id: Ids.AnonymousAuth,
                title: "Add the Authentication package",
                category: StepCategory.Services,
                obligation: Obligation.Required,
                dependsOn: new[] { new DependencyEdge(Ids.UgsProject, DependencyGate.RequiresConfigured, cascade: false) },
                fingerprintInputs: new[] { AnonymousAuthDetector.PackageVersionInput },
                copy: new StepCopy
                {
                    WhatIsThis = "The com.unity.services.authentication package. Anonymous sign-in gives every device a stable player id.",
                    WhyNeeded = "Cloud Save stores data per authenticated player; anonymous auth is the baseline identity.",
                    WhatYouDo = "It is a dependency of this package and normally resolves automatically.",
                    WhatWeAutoVerify = "That the package resolves.",
                    WhatYouConfirmManually = "That Anonymous sign-in is enabled in Dashboard → Authentication (it is on by default).",
                    HowToTest = "Run the anonymous sign-in check in the next step.",
                    ExpectedResult = "The resolved package version appears here.",
                    Links = new[] { new StepLink("Authentication docs", "https://docs.unity.com/ugs/manual/authentication/manual") },
                }),

            new StepDefinition(
                id: Ids.AnonymousVerification,
                title: "Verify anonymous sign-in",
                category: StepCategory.Verification,
                obligation: Obligation.Required,
                hasRuntimeValidator: true,
                dependsOn: new[]
                {
                    new DependencyEdge(Ids.CloudSaveService, DependencyGate.RequiresConfigured, cascade: true),
                    new DependencyEdge(Ids.AnonymousAuth, DependencyGate.RequiresConfigured, cascade: true),
                },
                fingerprintInputs: new[]
                {
                    UgsProjectDetector.CloudProjectId,
                    UgsProjectDetector.OrganizationId,
                    AnonymousAuthDetector.PackageVersionInput,
                },
                copy: new StepCopy
                {
                    WhatIsThis = "Actually initialises Unity Services, signs in anonymously, and confirms a stable player id comes back.",
                    WhyNeeded = "It is the first proof that the whole chain — project link, service, auth — works, not just that files exist.",
                    WhatYouDo = "Click Run. It writes no cloud data.",
                    WhatWeAutoVerify = "UGS initialises, sign-in succeeds, PlayerId is non-empty.",
                    WhatYouConfirmManually = "Nothing — this one is fully automated.",
                    HowToTest = "Press Run here. Re-run after changing the linked project or the Authentication package.",
                    ExpectedResult = "A green PASS with a masked PlayerId and a timestamp.",
                }),
        };
    }
}
