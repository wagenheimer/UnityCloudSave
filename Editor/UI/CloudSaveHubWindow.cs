using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Wagenheimer.CloudSave.Editor.Setup;
using Wagenheimer.CloudSave.Verification;
using Wagenheimer.PackageHub.Editor;

namespace Wagenheimer.CloudSave.Editor.UI
{
    /// <summary>
    /// Master UI Toolkit Hub & Dashboard for Unity Cloud Save.
    /// Provides full diagnostics checklist, verification engine, interactive cloud tester, and integration guides.
    /// </summary>
    public sealed class CloudSaveHubWindow : EditorWindow
    {
        public enum Tab
        {
            Diagnostics = 0,
            LiveQA = 1,
            Guide = 2,
            About = 3
        }

        private const string PackageJsonPath = "Packages/com.wagenheimer.cloudsave/package.json";
        private const string RepoUrl = "https://github.com/wagenheimer/UnityCloudSave";
        private const string IssuesUrl = "https://github.com/wagenheimer/UnityCloudSave/issues";

        private string _version = "4.26.0";
        private Tab _currentTab = Tab.Diagnostics;
        private VisualElement _contentContainer;
        private Button[] _tabButtons;

        // Diagnostics Cache
        private SetupRegistry _registry;
        private SetupContext _ctx;
        private CloudSaveSetupState _state;
        private SetupSnapshot _snapshot;
        private readonly HashSet<string> _expandedSteps = new();
        private bool _isBusy;

        // Tester state
        private string _testKey = "PlayerSave";
        private string _testValue = "{\"coins\":100,\"level\":5}";
        private string _testConsoleLog = "";

        [MenuItem("Tools/Wagenheimer/Cloud Save/Dashboard...", priority = 0)]
        public static void OpenDashboard()
        {
            Open(Tab.Diagnostics);
        }

        [MenuItem("Tools/Wagenheimer/Cloud Save/Setup & Verification...", priority = 10)]
        public static void OpenSetup()
        {
            Open(Tab.Diagnostics);
        }

        [MenuItem("Tools/Wagenheimer/Cloud Save/Cloud Tester...", priority = 11)]
        public static void OpenTester()
        {
            Open(Tab.LiveQA);
        }

        [MenuItem("Window/Wagenheimer/Cloud Save/Dashboard", priority = 200)]
        public static void OpenWindowAlt()
        {
            Open(Tab.Diagnostics);
        }

        public static CloudSaveHubWindow Open(Tab tab = Tab.Diagnostics)
        {
            var window = GetWindow<CloudSaveHubWindow>("Cloud Save");
            window.minSize = new Vector2(700, 560);
            window.SwitchTab(tab);
            window.Show();
            window.Focus();
            return window;
        }

        private void OnEnable()
        {
            LoadPackageVersion();
            _registry = new SetupRegistry();
            RecomputeDiagnostics();
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColBgDark);
            CloudSaveUIStyle.Apply(rootVisualElement);

            var root = new VisualElement();
            root.AddToClassList("cs-root");
            root.style.flexGrow = 1;
            root.style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColBgDark);
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;

            // Header Banner
            var header = CloudSaveUIStyle.CreateHeader(
                "Cloud Save",
                "Cross-Platform UGS Cloud Save, Authentication & Conflict Resolution Suite",
                _version,
                () => PackageHubWindow.OpenToPackage("com.wagenheimer.cloudsave")
            );
            root.Add(header);

            // Tab Bar
            var tabToolbar = new VisualElement();
            tabToolbar.AddToClassList("cs-tab-bar");
            tabToolbar.style.flexDirection = FlexDirection.Row;
            tabToolbar.style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColCardBg);
            tabToolbar.style.borderTopWidth = 1;
            tabToolbar.style.borderBottomWidth = 1;
            tabToolbar.style.borderLeftWidth = 1;
            tabToolbar.style.borderRightWidth = 1;
            tabToolbar.style.borderTopColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            tabToolbar.style.borderBottomColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            tabToolbar.style.borderLeftColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            tabToolbar.style.borderRightColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            tabToolbar.style.SetRadius(7);
            tabToolbar.style.paddingTop = 3;
            tabToolbar.style.paddingBottom = 3;
            tabToolbar.style.paddingLeft = 3;
            tabToolbar.style.paddingRight = 3;
            tabToolbar.style.marginBottom = 12;

            var tabNames = new[] { "Setup & Diagnostics", "Live QA & Tester", "Integration Guide", "About & Updates" };
            _tabButtons = new Button[tabNames.Length];

            for (var i = 0; i < tabNames.Length; i++)
            {
                var tabIndex = (Tab)i;
                var btn = new Button(() => SwitchTab(tabIndex))
                {
                    text = tabNames[i]
                };
                btn.AddToClassList("cs-tab-btn");
                btn.style.flexGrow = 1;
                btn.style.height = 30;
                btn.style.borderTopWidth = 0;
                btn.style.borderBottomWidth = 0;
                btn.style.borderLeftWidth = 0;
                btn.style.borderRightWidth = 0;
                btn.style.SetRadius(5);
                btn.style.backgroundColor = new StyleColor(Color.clear);
                btn.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
                btn.style.fontSize = 11.5f;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                btn.style.marginLeft = 2;
                btn.style.marginRight = 2;
                _tabButtons[i] = btn;
                tabToolbar.Add(btn);
            }
            root.Add(tabToolbar);

            // Dynamic Content Area
            _contentContainer = new VisualElement();
            _contentContainer.style.flexGrow = 1;
            root.Add(_contentContainer);

            rootVisualElement.Add(root);

            RenderActiveTab();
        }

        public void SwitchTab(Tab tab)
        {
            _currentTab = tab;
            RenderActiveTab();
        }

        private void RenderActiveTab()
        {
            if (_contentContainer == null) return;
            _contentContainer.Clear();

            if (_tabButtons != null)
            {
                for (var i = 0; i < _tabButtons.Length; i++)
                {
                    if (i == (int)_currentTab)
                    {
                        _tabButtons[i].AddToClassList("cs-tab-btn--active");
                        _tabButtons[i].style.backgroundColor = new StyleColor(CloudSaveUIStyle.ColAccent);
                        _tabButtons[i].style.color = new StyleColor(Color.white);
                    }
                    else
                    {
                        _tabButtons[i].RemoveFromClassList("cs-tab-btn--active");
                        _tabButtons[i].style.backgroundColor = new StyleColor(Color.clear);
                        _tabButtons[i].style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
                    }
                }
            }

            switch (_currentTab)
            {
                case Tab.Diagnostics:
                    _contentContainer.Add(BuildDiagnosticsView());
                    break;
                case Tab.LiveQA:
                    _contentContainer.Add(BuildLiveQAView());
                    break;
                case Tab.Guide:
                    _contentContainer.Add(BuildGuideView());
                    break;
                case Tab.About:
                    _contentContainer.Add(BuildAboutView());
                    break;
            }
        }

        #region Tab 0: Setup & Diagnostics (CHECKER)

        private VisualElement BuildDiagnosticsView()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            if (_snapshot == null)
            {
                RecomputeDiagnostics();
            }

            // 1. Metric Meters Row
            var metricsRow = new VisualElement();
            metricsRow.AddToClassList("cs-metrics-row");
            metricsRow.style.flexDirection = FlexDirection.Row;
            metricsRow.style.marginBottom = 12;

            var integFraction = _snapshot?.Integration.ToString() ?? "0/0";
            var verifFraction = _snapshot?.Verification.ToString() ?? "0/0";

            var (readinessText, readinessCol) = _snapshot?.Readiness switch
            {
                ReadinessVerdict.Green => ("READY", CloudSaveUIStyle.ColGreen),
                ReadinessVerdict.Amber => ("ALMOST", CloudSaveUIStyle.ColAmber),
                _ => ("NOT READY", CloudSaveUIStyle.ColRed)
            };

            metricsRow.Add(CloudSaveUIStyle.CreateMetricCard("Integration Setup", integFraction, out var intLbl, CloudSaveUIStyle.ColAccent));
            metricsRow.Add(CloudSaveUIStyle.CreateMetricCard("Verification Passed", verifFraction, out var verLbl, CloudSaveUIStyle.ColGreen));
            metricsRow.Add(CloudSaveUIStyle.CreateMetricCard("Production Readiness", readinessText, out var readLbl, readinessCol));

            scroll.Add(metricsRow);

            // 2. Next Best Action Card
            if (_snapshot?.NextAction != null)
            {
                var next = _snapshot.NextAction;
                var nextCard = CloudSaveUIStyle.CreateCard("RECOMMENDED NEXT ACTION: " + next.Step.Definition.Title, next.Why);
                nextCard.style.borderLeftColor = new StyleColor(CloudSaveUIStyle.ColAccent);
                nextCard.style.borderLeftWidth = 3;

                var actionRow = new VisualElement();
                actionRow.style.flexDirection = FlexDirection.Row;
                actionRow.style.alignItems = Align.Center;
                actionRow.style.marginTop = 6;

                var showBtn = CloudSaveUIStyle.CreateButton("Inspect Step", "cs-btn-primary", () =>
                {
                    _expandedSteps.Clear();
                    _expandedSteps.Add(next.Step.Definition.Id);
                    RenderActiveTab();
                });
                actionRow.Add(showBtn);

                if (next.Step.Definition.HasRuntimeValidator)
                {
                    var runBtn = CloudSaveUIStyle.CreateButton("Run Validation Test", "cs-btn-secondary", () =>
                    {
                        RunValidation(next.Step);
                    });
                    runBtn.style.marginLeft = 6;
                    actionRow.Add(runBtn);
                }

                nextCard.Add(actionRow);
                scroll.Add(nextCard);
            }

            // 3. Actions Toolbar
            var toolCard = CloudSaveUIStyle.CreateCard("Diagnostic Controls", $"Target: {EditorUserBuildSettings.activeBuildTarget}");
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;

            toolbar.Add(CloudSaveUIStyle.CreateButton("Re-Run Diagnostic Scan", "cs-btn-primary", () =>
            {
                RecomputeDiagnostics();
                RenderActiveTab();
            }));

            toolbar.Add(CloudSaveUIStyle.CreateButton("Generate All UI Prefabs", "cs-btn-secondary", () =>
            {
                CloudSaveUIPrefabGenerator.GenerateAll();
                RecomputeDiagnostics();
                RenderActiveTab();
            }));

            toolbar.Add(CloudSaveUIStyle.CreateButton("Copy Diagnostic Report / AI Prompt", "cs-btn-secondary", () =>
            {
                CopyDiagnosticPrompt();
            }));

            toolCard.Add(toolbar);
            scroll.Add(toolCard);

            // 4. Grouped Steps List
            if (_snapshot != null)
            {
                foreach (var group in _snapshot.Steps
                             .Where(e => e.State != StepState.NotApplicable)
                             .GroupBy(e => e.Definition.Category)
                             .OrderBy(g => (int)g.Key))
                {
                    var groupCard = CloudSaveUIStyle.CreateCard(group.Key.ToString() + " Verification");

                    foreach (var eval in group)
                    {
                        groupCard.Add(BuildStepCard(eval));
                    }

                    scroll.Add(groupCard);
                }
            }

            return scroll;
        }

        private VisualElement BuildStepCard(StepEvaluation eval)
        {
            var isExpanded = _expandedSteps.Contains(eval.Definition.Id);
            var card = new VisualElement();
            card.AddToClassList("cs-step-card");
            card.style.backgroundColor = new StyleColor(new Color(0.14f, 0.16f, 0.22f));
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            card.style.borderBottomColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            card.style.borderRightColor = new StyleColor(CloudSaveUIStyle.ColCardBorder);
            card.style.SetRadius(6);
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.marginBottom = 6;
            card.style.borderLeftWidth = 3;

            var (badgeText, badgeClass) = eval.State switch
            {
                StepState.Validated => ("VALIDATED", "cs-badge-pass"),
                StepState.ManuallyConfirmed => ("CONFIRMED", "cs-badge-info"),
                StepState.NeedsValidation => ("NEEDS VALIDATION", "cs-badge-warn"),
                StepState.NeedsAttention => ("NEEDS ATTENTION", "cs-badge-warn"),
                StepState.Failed => ("FAILED", "cs-badge-fail"),
                StepState.Blocked => ("BLOCKED", "cs-badge-warn"),
                _ => ("NOT CONFIGURED", "cs-badge-neutral")
            };

            card.style.borderLeftColor = new StyleColor(eval.State == StepState.Validated ? CloudSaveUIStyle.ColGreen :
                (eval.State == StepState.Failed ? CloudSaveUIStyle.ColRed : CloudSaveUIStyle.ColAmber));

            // Header Row
            var header = new VisualElement();
            header.AddToClassList("cs-step-header");
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;

            var toggleIcon = new Label(isExpanded ? "▾" : "▸");
            toggleIcon.style.fontSize = 12;
            toggleIcon.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
            toggleIcon.style.marginRight = 6;
            titleRow.Add(toggleIcon);

            var titleLbl = new Label(eval.Definition.Title);
            titleLbl.style.fontSize = 12;
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);
            titleRow.Add(titleLbl);

            var oblLbl = new Label($"({eval.Definition.Obligation})");
            oblLbl.style.fontSize = 10;
            oblLbl.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
            oblLbl.style.marginLeft = 6;
            titleRow.Add(oblLbl);

            header.Add(titleRow);

            var badge = CloudSaveUIStyle.CreateBadge(badgeText, badgeClass);
            header.Add(badge);

            header.RegisterCallback<ClickEvent>(_ =>
            {
                if (isExpanded) _expandedSteps.Remove(eval.Definition.Id);
                else _expandedSteps.Add(eval.Definition.Id);
                RenderActiveTab();
            });

            card.Add(header);

            // Expanded Body
            if (isExpanded)
            {
                var body = new VisualElement();
                body.AddToClassList("cs-step-details");
                body.style.marginTop = 8;
                body.style.paddingTop = 8;
                body.style.borderTopWidth = 1;
                body.style.borderTopColor = new StyleColor(new Color(1f, 1f, 1f, 0.05f));

                var c = eval.Definition.Copy;
                AddStepProp(body, "What is this?", c.WhatIsThis);
                AddStepProp(body, "Why needed?", c.WhyNeeded);
                AddStepProp(body, "What you do:", c.WhatYouDo);
                AddStepProp(body, "What we verify:", c.WhatWeAutoVerify);

                // Config found / missing
                if (eval.ConfigFound.Count > 0)
                {
                    foreach (var f in eval.ConfigFound)
                    {
                        var row = new Label($"✓ {f}");
                        row.style.fontSize = 10.5f;
                        row.style.color = new StyleColor(CloudSaveUIStyle.ColGreen);
                        row.style.marginLeft = 8;
                        body.Add(row);
                    }
                }

                if (eval.ConfigMissing.Count > 0)
                {
                    foreach (var m in eval.ConfigMissing)
                    {
                        var row = new Label($"✗ {m}");
                        row.style.fontSize = 10.5f;
                        row.style.color = new StyleColor(CloudSaveUIStyle.ColAmber);
                        row.style.marginLeft = 8;
                        body.Add(row);
                    }
                }

                // Action controls
                var actionToolbar = new VisualElement();
                actionToolbar.style.flexDirection = FlexDirection.Row;
                actionToolbar.style.alignItems = Align.Center;
                actionToolbar.style.marginTop = 8;

                if (eval.Definition.HasRuntimeValidator && eval.State != StepState.Blocked)
                {
                    var runBtn = CloudSaveUIStyle.CreateButton(
                        eval.Runtime == RuntimeVerificationStatus.Passed ? "Run Validation Again" : "Run Validation",
                        "cs-btn-primary",
                        () => RunValidation(eval)
                    );
                    runBtn.style.marginRight = 6;
                    actionToolbar.Add(runBtn);
                }

                var copyAiBtn = CloudSaveUIStyle.CreateButton("🤖 Copy AI Prompt", "cs-btn-secondary", () =>
                {
                    EditorGUIUtility.systemCopyBuffer = AiPromptFor(eval);
                    Debug.Log($"[CloudSave] AI Prompt copied for {eval.Definition.Title}");
                    EditorUtility.DisplayDialog("Cloud Save", "AI Prompt copied to clipboard!", "OK");
                });
                copyAiBtn.style.marginRight = 6;
                actionToolbar.Add(copyAiBtn);

                if (c.Links != null)
                {
                    foreach (var link in c.Links)
                    {
                        var linkBtn = CloudSaveUIStyle.CreateButton(link.Label, "cs-btn-secondary", () =>
                        {
                            Application.OpenURL(link.Url);
                        });
                        linkBtn.style.marginRight = 4;
                        actionToolbar.Add(linkBtn);
                    }
                }

                body.Add(actionToolbar);
                card.Add(body);
            }

            return card;
        }

        private static void AddStepProp(VisualElement container, string label, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var row = new VisualElement();
            row.style.marginBottom = 4;

            var lbl = new Label(label);
            lbl.style.fontSize = 11;
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);

            var val = new Label(text);
            val.style.fontSize = 11;
            val.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);
            val.style.whiteSpace = WhiteSpace.Normal;
            val.style.marginLeft = 4;

            row.Add(lbl);
            row.Add(val);
            container.Add(row);
        }

        private void RecomputeDiagnostics()
        {
            _state = CloudSaveSetupState.GetOrCreate();
            _ctx = SetupContext.ForCurrentProject(_state.CustomUis);
            _snapshot = SetupModel.Compute(_registry, _ctx, _state);
        }

        private async void RunValidation(StepEvaluation e)
        {
            var testCase = _registry.CreateCaseFor(e.Definition.Id);
            if (testCase == null) return;

            _isBusy = true;
            RenderActiveTab();

            ValidationResult result;
            try
            {
                result = await CloudSaveVerifier.RunAsync(testCase);
            }
            catch (Exception ex)
            {
                result = ValidationResult.Fail(testCase.Id, "Unexpected error.", ex);
            }

            _state.RecordValidation(new ValidationRecord
            {
                StepId = e.Definition.Id,
                CaseId = result.CaseId,
                Outcome = result.Outcome.ToString(),
                Fingerprint = e.CurrentFingerprint,
                StartedAtUtc = result.StartedAtUtc.ToString("o"),
                DurationMs = result.DurationMs,
                PackageVersion = _version,
                UgsEnvironment = "",
                Message = result.Message,
            });

            _isBusy = false;
            RecomputeDiagnostics();
            RenderActiveTab();
        }

        private static string AiPromptFor(StepEvaluation e)
        {
            if (!string.IsNullOrEmpty(e.Definition.AiPrompt))
                return e.Definition.AiPrompt;

            var c = e.Definition.Copy;
            return $"In this Unity project, complete this Cloud Save setup step: \"{e.Definition.Title}\".\n" +
                   $"What it is: {c.WhatIsThis}\n" +
                   $"What to do: {c.WhatYouDo}\n" +
                   $"Verify by: {c.HowToTest}\n" +
                   $"Expected result: {c.ExpectedResult}\n" +
                   "Make the minimal change, matching the project's existing conventions.";
        }

        private void CopyDiagnosticPrompt()
        {
            if (_snapshot == null) return;
            var issues = _snapshot.Steps.Where(s => s.State == StepState.Failed || s.State == StepState.NeedsAttention || s.State == StepState.Blocked).ToList();
            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Cloud Save", "All integration steps are validated and ready!", "OK");
                return;
            }

            var lines = new List<string> { "Please fix the following issues in the Unity CloudSave setup:" };
            int i = 1;
            foreach (var step in issues)
            {
                lines.Add($"\n{i++}. {step.Definition.Title} ({step.State})");
                lines.Add($"   Details: {step.Definition.Copy.WhatIsThis}");
                lines.Add($"   Fix: {step.Definition.Copy.WhatYouDo}");
            }

            EditorGUIUtility.systemCopyBuffer = string.Join("\n", lines);
            EditorUtility.DisplayDialog("Cloud Save", "Issue report copied to clipboard!", "OK");
        }

        #endregion

        #region Tab 1: Live QA & Cloud Tester (HELPER)

        private VisualElement BuildLiveQAView()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            // 1. Live Runtime Status Card
            var liveCard = CloudSaveUIStyle.CreateCard("Live Authentication & Cloud State");

            AddStateRow(liveCard, "Play Mode Active", Application.isPlaying ? "YES (Active)" : "NO (Edit Mode)");
            AddStateRow(liveCard, "UGS Initialized & Ready", CloudAuth.IsReady ? "YES" : "NO");
            AddStateRow(liveCard, "Authenticated", CloudAuth.IsSignedIn ? $"YES ({CloudAuth.PlayerId})" : "NO");
            AddStateRow(liveCard, "Sync Engine Status", CloudSync.LastResult.HasValue ? CloudSync.LastResult.Value.ToString() : "No sync yet");
            AddStateRow(liveCard, "Active Provider", CloudAuth.Provider.ToString());

            scroll.Add(liveCard);

            // 2. Interactive Cloud Key/Value Tester
            var testCard = CloudSaveUIStyle.CreateCard("Interactive Cloud Storage Tester", "Save, load, and test key-value payloads against Unity Cloud Save.");

            var keyRow = new VisualElement();
            keyRow.style.flexDirection = FlexDirection.Row;
            keyRow.style.alignItems = Align.Center;
            keyRow.style.marginBottom = 6;

            var keyLbl = new Label("Save Key:");
            keyLbl.style.width = 110;
            keyLbl.style.fontSize = 11;
            keyLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            keyRow.Add(keyLbl);

            var keyField = new TextField { value = _testKey };
            keyField.style.flexGrow = 1;
            keyField.RegisterValueChangedCallback(e => _testKey = e.newValue);
            keyRow.Add(keyField);
            testCard.Add(keyRow);

            var valRow = new VisualElement();
            valRow.style.flexDirection = FlexDirection.Row;
            valRow.style.alignItems = Align.Center;
            valRow.style.marginBottom = 8;

            var valLbl = new Label("Payload / Data:");
            valLbl.style.width = 110;
            valLbl.style.fontSize = 11;
            valLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            valRow.Add(valLbl);

            var valField = new TextField { value = _testValue };
            valField.style.flexGrow = 1;
            valField.RegisterValueChangedCallback(e => _testValue = e.newValue);
            valRow.Add(valField);
            testCard.Add(valRow);

            var testerToolbar = new VisualElement();
            testerToolbar.style.flexDirection = FlexDirection.Row;
            testerToolbar.style.flexWrap = Wrap.Wrap;

            testerToolbar.Add(CloudSaveUIStyle.CreateButton("💾 Save Key to Cloud", "cs-btn-primary", async () =>
            {
                if (!Application.isPlaying)
                {
                    EditorUtility.DisplayDialog("Cloud Save", "Enter Play Mode to test runtime CloudSave API calls.", "OK");
                    return;
                }
                _testConsoleLog = $"[{DateTime.Now:HH:mm:ss}] Saving {_testKey}...";
                RenderActiveTab();
                try
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(_testValue);
                    await CloudSync.SaveAsync(bytes, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    _testConsoleLog += $"\n✔ Saved {_testKey} successfully!";
                }
                catch (Exception ex)
                {
                    _testConsoleLog += $"\n✗ Save failed: {ex.Message}";
                }
                RenderActiveTab();
            }));

            testerToolbar.Add(CloudSaveUIStyle.CreateButton("📥 Load Key from Cloud", "cs-btn-secondary", async () =>
            {
                if (!Application.isPlaying)
                {
                    EditorUtility.DisplayDialog("Cloud Save", "Enter Play Mode to test runtime CloudSave API calls.", "OK");
                    return;
                }
                _testConsoleLog = $"[{DateTime.Now:HH:mm:ss}] Loading {_testKey}...";
                RenderActiveTab();
                try
                {
                    var (bytes, ts) = await CloudSync.LoadRawCloudDataAsync();
                    var text = bytes != null ? System.Text.Encoding.UTF8.GetString(bytes) : "(empty)";
                    _testConsoleLog += $"\n✔ Loaded: {text} (timestamp: {ts})";
                }
                catch (Exception ex)
                {
                    _testConsoleLog += $"\n✗ Load failed: {ex.Message}";
                }
                RenderActiveTab();
            }));

            testerToolbar.Add(CloudSaveUIStyle.CreateButton("🗑️ Delete Key", "cs-btn-secondary", async () =>
            {
                if (!Application.isPlaying) return;
                try
                {
                    await CloudSync.DeleteCloudSaveAsync();
                    _testConsoleLog = $"✔ Deleted {_testKey} from cloud.";
                }
                catch (Exception ex)
                {
                    _testConsoleLog += $"\n✗ Delete failed: {ex.Message}";
                }
                RenderActiveTab();
            }));

            testerToolbar.Add(CloudSaveUIStyle.CreateButton("🔄 Force Full Sync", "cs-btn-secondary", async () =>
            {
                if (!Application.isPlaying) return;
                try
                {
                    await CloudSync.InitAndSyncAsync(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), b =>
                    {
                        _testConsoleLog = $"✔ Full sync resolved cloud data ({b?.Length ?? 0} bytes).";
                    });
                }
                catch (Exception ex)
                {
                    _testConsoleLog += $"✗ Sync error: {ex.Message}";
                }
                RenderActiveTab();
            }));

            testCard.Add(testerToolbar);

            if (!string.IsNullOrEmpty(_testConsoleLog))
            {
                var consoleBox = new VisualElement();
                consoleBox.AddToClassList("cs-code-box");
                var logLbl = new Label(_testConsoleLog);
                logLbl.style.fontSize = 11;
                logLbl.style.color = new StyleColor(new Color(0.43f, 0.91f, 0.72f));
                logLbl.style.whiteSpace = WhiteSpace.Normal;
                consoleBox.Add(logLbl);
                testCard.Add(consoleBox);
            }

            scroll.Add(testCard);

            // 3. UI Prefabs Generator Card
            var prefabCard = CloudSaveUIStyle.CreateCard("UI Prefabs & Drop-in Components", "Generate ready-made uGUI Canvas prefabs for your game.");

            bool hasCloudUI = File.Exists("Assets/Resources/CloudSaveUI.prefab") || File.Exists("Packages/com.wagenheimer.cloudsave/Runtime/Resources/CloudSaveUI.prefab");
            bool hasAuthUI = File.Exists("Assets/Resources/CloudAuthUI.prefab") || File.Exists("Packages/com.wagenheimer.cloudsave/Runtime/Resources/CloudAuthUI.prefab");
            bool hasSyncUI = File.Exists("Assets/Resources/SyncStatusUI.prefab") || File.Exists("Packages/com.wagenheimer.cloudsave/Runtime/Resources/SyncStatusUI.prefab");

            AddStateRow(prefabCard, "CloudSaveUI.prefab", hasCloudUI ? "FOUND (Ready)" : "MISSING");
            AddStateRow(prefabCard, "CloudAuthUI.prefab", hasAuthUI ? "FOUND (Ready)" : "MISSING");
            AddStateRow(prefabCard, "SyncStatusUI.prefab", hasSyncUI ? "FOUND (Ready)" : "MISSING");

            var prefToolbar = new VisualElement();
            prefToolbar.style.flexDirection = FlexDirection.Row;
            prefToolbar.style.marginTop = 8;

            prefToolbar.Add(CloudSaveUIStyle.CreateButton("Generate / Update All Prefabs", "cs-btn-primary", () =>
            {
                CloudSaveUIPrefabGenerator.GenerateAll();
                RenderActiveTab();
            }));

            prefToolbar.Add(CloudSaveUIStyle.CreateButton("Open Dedicated Test Window", "cs-btn-secondary", () =>
            {
                EditorApplication.ExecuteMenuItem("Tools/Wagenheimer/Cloud Save/Cloud Tester (Legacy)...");
            }));

            prefToolbar.Add(CloudSaveUIStyle.CreateButton("Add In-Game Debug Overlay to Scene", "cs-btn-primary", () =>
            {
                CloudSaveDebugOverlayEditor.AddDebugOverlayToScene();
            }));

            prefabCard.Add(prefToolbar);
            scroll.Add(prefabCard);

            return scroll;
        }

        private static void AddStateRow(VisualElement card, string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;

            var lbl = new Label(label);
            lbl.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
            lbl.style.fontSize = 11;

            var val = new Label(value);
            val.style.fontSize = 11;
            val.style.unityFontStyleAndWeight = FontStyle.Bold;
            val.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);

            row.Add(lbl);
            row.Add(val);
            card.Add(row);
        }

        #endregion

        #region Tab 2: Integration Guide & Code Reference

        private VisualElement BuildGuideView()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            var quickStartCard = CloudSaveUIStyle.CreateCard("Cloud Save Quick Start Guide", "Four simple steps to cross-platform cloud persistence.");

            AddGuideStep(quickStartCard, "1. Link Unity Services (UGS)",
                "Open Project Settings > Services and link your project with a Unity Gaming Services Organization. Enable Cloud Save in your Unity Cloud Dashboard.");

            AddGuideStep(quickStartCard, "2. Authenticate the Player",
                "Sign in anonymously on boot, or upgrade seamlessly to Google Play Games, Apple Game Center, or Steam: CloudAuth.SignInAnonymouslyAsync()");

            AddGuideStep(quickStartCard, "3. Save & Load Game Data",
                "Call CloudSaveController.SaveAsync(\"Slot1\", bytes) when saving game progress. On boot, call CloudSaveController.LoadAsync<byte[]>(\"Slot1\").");

            AddGuideStep(quickStartCard, "4. Add Sync Status UI",
                "Drop 'SyncStatusUI' into your HUD or Settings panel. It automatically displays a spinner while uploading and a checkmark when synced!");

            scroll.Add(quickStartCard);

            // Code Snippets Card
            var codeCard = CloudSaveUIStyle.CreateCard("Runtime API Code Snippets");

            AddCodeSnippet(codeCard, "Save Game Data to Cloud",
                "byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);\nawait Wagenheimer.CloudSave.CloudSync.SaveAsync(bytes, DateTimeOffset.UtcNow.ToUnixTimeSeconds());");

            AddCodeSnippet(codeCard, "Load Game Data from Cloud",
                "var (bytes, ts) = await Wagenheimer.CloudSave.CloudSync.LoadRawCloudDataAsync();\nif (bytes != null) string json = System.Text.Encoding.UTF8.GetString(bytes);");

            AddCodeSnippet(codeCard, "Listen to Sync Status Events",
                "Wagenheimer.CloudSave.CloudSync.OnSyncCompleted += result => {\n    Debug.Log($\"Cloud Sync Result: {result}\");\n};");

            scroll.Add(codeCard);

            return scroll;
        }

        private static void AddGuideStep(VisualElement container, string title, string description)
        {
            var item = new VisualElement();
            item.style.marginBottom = 10;

            var t = new Label(title);
            t.style.fontSize = 12;
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);

            var d = new Label(description);
            d.style.fontSize = 11;
            d.style.color = new StyleColor(CloudSaveUIStyle.ColTextMuted);
            d.style.whiteSpace = WhiteSpace.Normal;
            d.style.marginLeft = 6;
            d.style.marginTop = 2;

            item.Add(t);
            item.Add(d);
            container.Add(item);
        }

        private static void AddCodeSnippet(VisualElement container, string title, string code)
        {
            var item = new VisualElement();
            item.style.marginBottom = 10;

            var t = new Label(title);
            t.style.fontSize = 11.5f;
            t.style.unityFontStyleAndWeight = FontStyle.Bold;
            t.style.color = new StyleColor(CloudSaveUIStyle.ColTextWhite);
            item.Add(t);

            var codeBox = new VisualElement();
            codeBox.AddToClassList("cs-code-box");
            codeBox.style.backgroundColor = new StyleColor(new Color(0.08f, 0.09f, 0.12f));
            codeBox.style.SetRadius(6);
            codeBox.style.paddingTop = 8;
            codeBox.style.paddingBottom = 8;
            codeBox.style.paddingLeft = 10;
            codeBox.style.paddingRight = 10;
            codeBox.style.marginTop = 4;

            var codeLbl = new Label(code);
            codeLbl.style.fontSize = 10.5f;
            codeLbl.style.color = new StyleColor(new Color(0.43f, 0.91f, 0.72f));
            codeBox.Add(codeLbl);

            var copyBtn = CloudSaveUIStyle.CreateButton("Copy Snippet", "cs-btn-secondary", () =>
            {
                EditorGUIUtility.systemCopyBuffer = code;
                EditorUtility.DisplayDialog("Cloud Save", "Code snippet copied to clipboard!", "OK");
            });
            copyBtn.style.marginTop = 6;
            copyBtn.style.alignSelf = Align.FlexEnd;
            codeBox.Add(copyBtn);

            item.Add(codeBox);
            container.Add(item);
        }

        #endregion

        #region Tab 3: About & Updates

        private VisualElement BuildAboutView()
        {
            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;

            var infoCard = CloudSaveUIStyle.CreateCard("Package Information", "Production-grade cross-platform cloud save system by Cezar Wagenheimer.");
            AddStateRow(infoCard, "Package Name", "com.wagenheimer.cloudsave");
            AddStateRow(infoCard, "Installed Version", _version);
            AddStateRow(infoCard, "Author", "Cezar Wagenheimer");
            AddStateRow(infoCard, "License", "MIT");

            var infoToolbar = new VisualElement();
            infoToolbar.style.flexDirection = FlexDirection.Row;
            infoToolbar.style.marginTop = 10;

            infoToolbar.Add(CloudSaveUIStyle.CreateButton("Check for Updates", "cs-btn-primary", () =>
            {
                PackageHubWindow.OpenToPackage("com.wagenheimer.cloudsave");
            }));

            infoToolbar.Add(CloudSaveUIStyle.CreateButton("GitHub Repository", "cs-btn-secondary", () =>
            {
                Application.OpenURL(RepoUrl);
            }));

            infoToolbar.Add(CloudSaveUIStyle.CreateButton("Report Issue", "cs-btn-secondary", () =>
            {
                Application.OpenURL(IssuesUrl);
            }));

            infoCard.Add(infoToolbar);
            scroll.Add(infoCard);

            // Capabilities Card
            var capCard = CloudSaveUIStyle.CreateCard("Key Capabilities");
            AddStateRow(capCard, "Unity Cloud Save (UGS)", "Server-authoritative JSON/byte data storage with optimistic concurrency");
            AddStateRow(capCard, "Multi-Provider Auth", "Anonymous, Google Play Games (Android), Game Center (iOS), Steam");
            AddStateRow(capCard, "Timestamp Conflict Resolution", "Deterministic conflict merging with offline caching");
            AddStateRow(capCard, "Drop-in UI Prefabs", "Pre-styled Canvas components for cloud sync indicators and account linking");
            scroll.Add(capCard);

            return scroll;
        }

        #endregion

        private void LoadPackageVersion()
        {
            try
            {
                if (File.Exists(PackageJsonPath))
                {
                    var json = File.ReadAllText(PackageJsonPath);
                    var match = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"");
                    if (match.Success)
                    {
                        _version = match.Groups[1].Value;
                        return;
                    }
                }
            }
            catch { }
            _version = "4.26.0";
        }
    }
}
