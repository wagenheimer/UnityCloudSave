using System;
using System.Collections.Generic;
using Wagenheimer.CloudSave.Editor.Setup.Detectors;
using Wagenheimer.CloudSave.Verification;

namespace Wagenheimer.CloudSave.Editor.Setup
{
    /// <summary>
    /// The step catalog. Phase 1 folds the 19 CloudSaveAudit checks in as composed
    /// <see cref="IStepDetector"/>s + <see cref="StepDefinition"/>s with real dependencies and
    /// fingerprints. Phase 3 adds <c>IAuthProviderModule</c> discovery on top — the Hub UI is
    /// unaffected because it only ever reads <see cref="Steps"/> and the engines.
    /// </summary>
    public sealed class SetupRegistry
    {
        public static class Ids
        {
            public const string UgsProject = "ugs.project";
            public const string CloudSaveService = "ugs.cloudsave";
            public const string AnonymousAuth = "ugs.auth.anonymous";

            public const string CodeConfigure = "code.configure";
            public const string CodeInitSync = "code.initsync";
            public const string CodeSave = "code.save";

            public const string SaveTimestamp = "save.timestamp";
            public const string SaveSerializable = "save.serializable";

            public const string UiCloudSave = "ui.cloudsave";
            public const string UiSyncStatus = "ui.syncstatus";
            public const string UiAuth = "ui.auth";

            public const string CodeAuthUpgrade = "code.authupgrade";
            public const string ProviderFacebook = "provider.facebook";
            public const string ProviderAndroid = "provider.android";
            public const string ProviderApple = "provider.ios";

            public const string ComplianceDeletion = "compliance.deletion";
            public const string ComplianceDeletionUi = "compliance.deletionui";
            public const string ComplianceUrls = "compliance.urls";

            public const string MigrationLegacy = "migration.legacy";

            public const string AnonymousVerification = "verify.auth.anonymous";
        }

        readonly List<StepDefinition> _steps;
        readonly Dictionary<string, IStepDetector> _detectors;
        readonly Dictionary<string, Func<ValidationCase>> _cases;
        readonly Dictionary<string, string[]> _manualItems;

        public IReadOnlyList<StepDefinition> Steps => _steps;

        public IStepDetector DetectorFor(string stepId)
            => _detectors.TryGetValue(stepId, out var d) ? d : new AlwaysPresentDetector();

        public IReadOnlyList<string> ManualItemIdsFor(string stepId)
            => _manualItems.TryGetValue(stepId, out var m) ? m : Array.Empty<string>();

        public ValidationCase CreateCaseFor(string stepId)
            => _cases.TryGetValue(stepId, out var f) ? f() : null;

        public SetupRegistry()
        {
            _detectors = new Dictionary<string, IStepDetector>
            {
                [Ids.UgsProject] = new UgsProjectDetector(),
                [Ids.CloudSaveService] = new CloudSaveServiceDetector(),
                [Ids.AnonymousAuth] = new AnonymousAuthDetector(),

                [Ids.CodeConfigure] = new CodePresenceDetector(
                    @"CloudSync\.Configure", "code.configure",
                    "CloudSync.Configure() called", "Call CloudSync.Configure(\"my_save_key\") once at startup."),
                [Ids.CodeInitSync] = new CodePresenceDetector(
                    @"CloudSync\.InitAndSyncAsync", "code.initsync",
                    "CloudSync.InitAndSyncAsync() called", "Call CloudSync.InitAndSyncAsync(ts, onCloudNewer) at startup after Configure()."),
                [Ids.CodeSave] = new CodePresenceDetector(
                    @"CloudSync\.SaveAsync", "code.save",
                    "CloudSync.SaveAsync() called", "Call CloudSync.SaveAsync(bytes, ts) after each local save."),

                [Ids.SaveTimestamp] = new TimestampFieldDetector(),
                [Ids.SaveSerializable] = new CodePresenceDetector(
                    @"\[(System\.)?Serializable\]", "save.serializable",
                    "[Serializable] type found", "Mark your save class [Serializable] — JsonUtility requires it."),

                [Ids.UiCloudSave] = new CodePresenceDetector(
                    @"CloudSaveUI\.Create", "ui.cloudsave",
                    "CloudSaveUI.Create() called", "Optional: CloudSaveUI.Create() — loading overlay, toasts, conflict dialog."),
                [Ids.UiSyncStatus] = new CodePresenceDetector(
                    @"SyncStatusUI\.Create", "ui.syncstatus",
                    "SyncStatusUI.Create() called", "Optional: SyncStatusUI.Create() — corner Synced/Syncing/Offline indicator."),
                [Ids.UiAuth] = new CodePresenceDetector(
                    @"CloudAuthUI\.Create", "ui.auth",
                    "CloudAuthUI.Create() called", "Optional: CloudAuthUI.Create() — account link modal."),

                [Ids.CodeAuthUpgrade] = new CodePresenceDetector(
                    @"Link(GooglePlayGames|AppleGameCenter|Apple|Facebook|Google)Async", "code.authupgrade",
                    "Account-linking code found", "For cross-device saves, link the anonymous account: CloudAuth.Link*Async(...)."),
                [Ids.ProviderFacebook] = new CodePresenceDetector(
                    @"FB\.Init|LinkFacebookAsync|FacebookSDK", "provider.facebook",
                    "Facebook auth code found", "Optional: Facebook login → CloudAuth.LinkFacebookAsync(accessToken)."),
                [Ids.ProviderAndroid] = new GpgsPresenceDetector(),
                [Ids.ProviderApple] = new CodePresenceDetector(
                    @"Apple\.GameKit|GKLocalPlayer|LinkAppleAsync|LinkAppleGameCenterAsync", "provider.ios",
                    "Apple / Game Center auth code found", "Optional: Apple login → CloudAuth.LinkAppleAsync / LinkAppleGameCenterAsync."),

                [Ids.ComplianceDeletion] = new CodePresenceDetector(
                    @"DeleteAccountAsync", "compliance.deletion",
                    "CloudAuth.DeleteAccountAsync() call found",
                    "Apple 5.1.1(v) & Google Play require in-app account deletion when sign-in is offered: CloudAuth.DeleteAccountAsync()."),
                [Ids.ComplianceDeletionUi] = new CodePresenceDetector(
                    @"(?i)delete\s*account|deleteconfirmation|accountdeleted", "compliance.deletionui",
                    "Account-deletion UI referenced", "Provide a clear 'Delete account' button with a confirmation step."),
                [Ids.ComplianceUrls] = new CodePresenceDetector(
                    @"(?i)Application\.OpenURL\([^)]*(privacy|policy|terms|delete)", "compliance.urls",
                    "Privacy / deletion URL referenced", "Google Play & Meta require public HTTPS Privacy Policy + Data Deletion URLs."),

                [Ids.MigrationLegacy] = new LegacyMigrationDetector(),

                [Ids.AnonymousVerification] = new AnonymousVerificationDetector(),
            };

            _cases = new Dictionary<string, Func<ValidationCase>>
            {
                [Ids.AnonymousVerification] = () => new AnonymousSignInCase(),
            };

            _manualItems = new Dictionary<string, string[]>();
            _steps = BuildSteps();

            DependencyEngine.TopologicalOrder(_steps); // fail fast on authoring cycles
        }

        static DependencyEdge Dep(string id, DependencyGate gate = DependencyGate.RequiresConfigured, bool cascade = false)
            => new(id, gate, cascade);

        static StepCopy C(string what, string why = "", string doThis = "", string test = "", string expect = "",
            params StepLink[] links)
            => new()
            {
                WhatIsThis = what, WhyNeeded = why, WhatYouDo = doThis,
                HowToTest = test, ExpectedResult = expect,
                Links = links ?? Array.Empty<StepLink>(),
            };

        static List<StepDefinition> BuildSteps() => new()
        {
            // ── Prerequisites ──────────────────────────────────────────────
            new StepDefinition(Ids.UgsProject, "Link your Unity project", StepCategory.Prerequisites, Obligation.Required,
                C("Connects this Unity project to a project in Unity Cloud, where Cloud Save data lives.",
                  "Every UGS feature is scoped to a linked cloud project.",
                  "Edit → Project Settings → Services → sign in and select or create a project.",
                  "The runtime checks below fail if the link is missing or wrong.",
                  "A cloud project id and organization show here.",
                  new StepLink("Unity Dashboard", "https://cloud.unity.com/")),
                fingerprintInputs: new[] { UgsProjectDetector.CloudProjectId, UgsProjectDetector.OrganizationId }),

            // ── Services ───────────────────────────────────────────────────
            new StepDefinition(Ids.CloudSaveService, "Add the Cloud Save package", StepCategory.Services, Obligation.Required,
                C("The com.unity.services.cloudsave package — the client SDK this wrapper calls.",
                  "Its API is what CloudSync uses under the hood.",
                  "Normally auto-resolved as a dependency. If missing, add via Package Manager.",
                  "The anonymous sign-in check and the save round-trip (Phase 2) prove the service is on.",
                  "The resolved package version shows here."),
                dependsOn: new[] { Dep(Ids.UgsProject) },
                fingerprintInputs: new[] { CloudSaveServiceDetector.PackageVersionInput }),

            new StepDefinition(Ids.AnonymousAuth, "Add the Authentication package", StepCategory.Services, Obligation.Required,
                C("The com.unity.services.authentication package. Anonymous sign-in gives every device a stable player id.",
                  "Cloud Save stores data per authenticated player; anonymous is the baseline identity.",
                  "Normally auto-resolved as a dependency.",
                  "Run the anonymous sign-in check below.",
                  "The resolved package version shows here."),
                dependsOn: new[] { Dep(Ids.UgsProject) },
                fingerprintInputs: new[] { AnonymousAuthDetector.PackageVersionInput }),

            // ── Startup Code ───────────────────────────────────────────────
            new StepDefinition(Ids.CodeConfigure, "Call CloudSync.Configure()", StepCategory.StartupCode, Obligation.Required,
                C("Picks the Cloud Save key your game reads/writes.",
                  "Without it CloudSync has no slot to sync.",
                  "At startup: CloudSync.Configure(\"my_save_key\");",
                  "Grep proof: a CloudSync.Configure call in your Assets.",
                  "This step turns green when the call is found."),
                dependsOn: new[] { Dep(Ids.CloudSaveService, cascade: true) },
                fingerprintInputs: new[] { "code.configure" }),

            new StepDefinition(Ids.CodeInitSync, "Call CloudSync.InitAndSyncAsync()", StepCategory.StartupCode, Obligation.Required,
                C("Pulls the cloud save on launch and resolves conflicts against local.",
                  "Without it a returning player never gets their cloud progress.",
                  "At startup after Configure(): _ = CloudSync.InitAndSyncAsync(localTs, OnCloudNewer);",
                  "Grep proof + the anonymous verification below.",
                  "Green when the call is found."),
                dependsOn: new[] { Dep(Ids.CodeConfigure, cascade: true) },
                fingerprintInputs: new[] { "code.initsync" }),

            new StepDefinition(Ids.CodeSave, "Call CloudSync.SaveAsync()", StepCategory.StartupCode, Obligation.Required,
                C("Uploads local data to the cloud after each save.",
                  "Without it nothing ever reaches the cloud.",
                  "After each local save: _ = CloudSync.SaveAsync(bytes, timestamp);",
                  "Grep proof + the save round-trip (Phase 2).",
                  "Green when the call is found."),
                dependsOn: new[] { Dep(Ids.CodeConfigure, cascade: true) },
                fingerprintInputs: new[] { "code.save" }),

            // ── Save Data ──────────────────────────────────────────────────
            new StepDefinition(Ids.SaveTimestamp, "Save class has a long timestamp", StepCategory.SaveData, Obligation.Required,
                C("A `long LastSaved` (or SaveDateTime) field set to DateTime.UtcNow.Ticks.",
                  "Last-write-wins conflict resolution compares this value.",
                  "Add `public long LastSaved;` to your save class and set it on every save.",
                  "Grep proof for `long LastSaved / SaveDateTime`.",
                  "Green when a long timestamp field is found."),
                fingerprintInputs: new[] { "save.timestamp" }),

            new StepDefinition(Ids.SaveSerializable, "Save class is [Serializable]", StepCategory.SaveData, Obligation.Recommended,
                C("JsonUtility (the default serializer) needs [Serializable] on your save type.",
                  "Without it your bytes won't round-trip.",
                  "Add [System.Serializable] to the save class.",
                  "Grep proof for a [Serializable] type.",
                  "Green when found."),
                fingerprintInputs: new[] { "save.serializable" }),

            // ── UI (optional) ──────────────────────────────────────────────
            new StepDefinition(Ids.UiCloudSave, "Show the Cloud Save UI", StepCategory.Ui, Obligation.Optional,
                C("Loading overlay, toasts and the conflict dialog.",
                  "Optional — you can build your own.",
                  "CloudSaveUI.Create(); at startup, or wire your own dialog to CloudSync.ConflictResolver.",
                  "Grep proof for CloudSaveUI.Create().",
                  "Green when found (or leave it if you have your own UI)."),
                fingerprintInputs: new[] { "ui.cloudsave" }),

            new StepDefinition(Ids.UiSyncStatus, "Show the sync status indicator", StepCategory.Ui, Obligation.Optional,
                C("Corner badge: Synced / Syncing / Offline / Error.",
                  "Optional polish.",
                  "SyncStatusUI.Create();",
                  "Grep proof for SyncStatusUI.Create().",
                  "Green when found."),
                fingerprintInputs: new[] { "ui.syncstatus" }),

            new StepDefinition(Ids.UiAuth, "Show the account-link UI", StepCategory.Ui, Obligation.Optional,
                C("Modal that lets players link Facebook / Google / Apple.",
                  "Optional — needed only if you offer cross-device saves via a UI.",
                  "CloudAuthUI.Create(); and handle OnLinkRequested.",
                  "Grep proof for CloudAuthUI.Create().",
                  "Green when found."),
                fingerprintInputs: new[] { "ui.auth" }),

            // ── Providers ──────────────────────────────────────────────────
            new StepDefinition(Ids.CodeAuthUpgrade, "Wire account linking", StepCategory.Providers, Obligation.Recommended,
                C("Upgrading the anonymous account to Facebook / Google / Apple so saves follow the player across devices.",
                  "Anonymous-only saves are device-local.",
                  "After the provider SDK returns a token: await CloudAuth.Link*Async(token).",
                  "Grep proof for a CloudAuth.Link*Async call; verified for real per provider in Phase 3.",
                  "Green when at least one link call is present."),
                dependsOn: new[] { Dep(Ids.AnonymousAuth) },
                fingerprintInputs: new[] { "code.authupgrade" }),

            new StepDefinition(Ids.ProviderFacebook, "Facebook login", StepCategory.Providers, Obligation.Optional,
                C("Facebook SDK login feeding CloudAuth.LinkFacebookAsync.",
                  "Optional cross-device provider.",
                  "Install the Facebook SDK, then link the token. Full console setup lands in Phase 3.",
                  "Grep proof for FB.Init / LinkFacebookAsync.",
                  "Green when Facebook code is present."),
                fingerprintInputs: new[] { "provider.facebook" }),

            new StepDefinition(Ids.ProviderAndroid, "Google Play Games (Android)", StepCategory.Providers, Obligation.Optional,
                C("GPGS plugin + server-auth-code flow feeding CloudAuth.LinkGooglePlayGamesAsync.",
                  "Optional cross-device provider on Android.",
                  "Install com.google.play.games, wire PlayGamesPlatform. Full Play Console setup lands in Phase 3.",
                  "Detects the plugin package and PlayGamesPlatform usage.",
                  "Green when the plugin or its code is present.",
                  new StepLink("GPGS plugin", "https://github.com/playgameservices/play-games-plugin-for-unity")),
                fingerprintInputs: new[] { "gpgs.pkg", "gpgs.code" }),

            new StepDefinition(Ids.ProviderApple, "Sign in with Apple / Game Center (iOS)", StepCategory.Providers, Obligation.Optional,
                C("Apple auth feeding CloudAuth.LinkAppleAsync / LinkAppleGameCenterAsync.",
                  "Optional cross-device provider on iOS.",
                  "Apple sign-in plugin or Apple.GameKit. Full Apple Developer setup lands in Phase 3.",
                  "Grep proof for Apple.GameKit / GKLocalPlayer / LinkApple*Async.",
                  "Green when Apple code is present."),
                fingerprintInputs: new[] { "provider.ios" }),

            // ── Store Compliance ──────────────────────────────────────────
            new StepDefinition(Ids.ComplianceDeletion, "In-app account deletion", StepCategory.StoreCompliance, Obligation.Recommended,
                C("A player-triggered call to CloudAuth.DeleteAccountAsync().",
                  "Apple Guideline 5.1.1(v) and Google Play REQUIRE this once any sign-in / social linking is offered.",
                  "Add a 'Delete account' action that calls await CloudAuth.DeleteAccountAsync().",
                  "Grep proof for DeleteAccountAsync.",
                  "Green when the call is present."),
                dependsOn: new[] { Dep(Ids.CodeAuthUpgrade) },
                fingerprintInputs: new[] { "compliance.deletion" }),

            new StepDefinition(Ids.ComplianceDeletionUi, "Account-deletion confirmation UI", StepCategory.StoreCompliance, Obligation.Recommended,
                C("A visible 'Delete account' button with a confirmation safeguard.",
                  "Reviewers look for a discoverable, confirmed deletion flow.",
                  "Add the button + a confirm dialog in your settings/account screen.",
                  "Grep proof for deletion-UI naming.",
                  "Green when found."),
                dependsOn: new[] { Dep(Ids.ComplianceDeletion) },
                fingerprintInputs: new[] { "compliance.deletionui" }),

            new StepDefinition(Ids.ComplianceUrls, "Privacy & data-deletion URLs", StepCategory.StoreCompliance, Obligation.Recommended,
                C("Public HTTPS Privacy Policy and User Data Deletion URLs.",
                  "Required by Google Play Data Safety and the Meta Developer portal.",
                  "Publish the pages and link them in-app (and register them in the portals).",
                  "Grep proof for an Application.OpenURL to a privacy/terms/delete page.",
                  "Green when a link is referenced in code."),
                fingerprintInputs: new[] { "compliance.urls" }),

            // ── Migration ─────────────────────────────────────────────────
            new StepDefinition(Ids.MigrationLegacy, "Legacy save migration", StepCategory.Migration, Obligation.Optional,
                C("Importing existing players' saves from a previous backend (PlayFab, Firebase) into UGS.",
                  "Only relevant if you're migrating an existing game.",
                  "Wire CloudMigration.TryMigrateAsync with a fetch delegate — see Samples~/PlayFabMigration.",
                  "Detects CloudMigration.TryMigrateAsync, or flags a bare PlayFab/Firebase reference.",
                  "Green when wired, or when no legacy backend exists."),
                dependsOn: new[] { Dep(Ids.CloudSaveService) },
                fingerprintInputs: new[] { "migration" }),

            // ── Verification ──────────────────────────────────────────────
            new StepDefinition(Ids.AnonymousVerification, "Verify anonymous sign-in", StepCategory.Verification, Obligation.Required,
                C("Initialises Unity Services, signs in anonymously, confirms a stable player id — the first real proof the chain works.",
                  "Files existing isn't proof; this is.",
                  "Enter Play Mode, then click Run. It writes no cloud data.",
                  "Press Run. Re-run after changing the linked project or the Authentication package.",
                  "A green PASS with a masked PlayerId and a timestamp."),
                hasRuntimeValidator: true,
                dependsOn: new[]
                {
                    Dep(Ids.CloudSaveService, cascade: true),
                    Dep(Ids.AnonymousAuth, cascade: true),
                },
                fingerprintInputs: new[]
                {
                    UgsProjectDetector.CloudProjectId,
                    UgsProjectDetector.OrganizationId,
                    AnonymousAuthDetector.PackageVersionInput,
                }),
        };

        /// <summary>Fallback detector for steps with no explicit detector (never leaves a step Unknown).</summary>
        sealed class AlwaysPresentDetector : IStepDetector
        {
            public ConfigurationReport Detect(SetupContext ctx) => new(ConfigurationStatus.Present);
        }
    }
}
