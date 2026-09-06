using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wagenheimer.CloudSave.Verification;

namespace Wagenheimer.CloudSave.Editor.Setup
{
    /// <summary>
    /// Phase 0 Hub skeleton: sticky header (three meters + Next Best Action), a category-grouped
    /// list of step cards with the Configuration / Runtime / Manual split, a Blocked view, a
    /// "configuration changed since last verification" banner, and one working Run button.
    /// </summary>
    public sealed class CloudSaveSetupWindow : EditorWindow
    {
        [MenuItem("Tools/Wagenheimer/Cloud Save/Setup && Verification", priority = 0)]
        public static void Open()
        {
            var w = GetWindow<CloudSaveSetupWindow>("Cloud Save Setup");
            w.minSize = new Vector2(560, 520);
        }

        SetupRegistry _registry;
        SetupContext _ctx;
        CloudSaveSetupState _state;
        SetupSnapshot _snapshot;
        Vector2 _scroll;
        readonly HashSet<string> _expanded = new();
        bool _busy;
        string _busyLabel;

        // Palette — matches CloudSaveAudit.
        static Color ColBg => EditorGUIUtility.isProSkin ? new(0.16f, 0.16f, 0.18f) : new(0.82f, 0.82f, 0.84f);
        static Color ColCard => EditorGUIUtility.isProSkin ? new(0.20f, 0.20f, 0.22f) : new(0.90f, 0.90f, 0.92f);
        static Color ColGreen => EditorGUIUtility.isProSkin ? new(0.20f, 0.75f, 0.35f) : new(0.10f, 0.55f, 0.20f);
        static Color ColRed => EditorGUIUtility.isProSkin ? new(0.85f, 0.25f, 0.20f) : new(0.70f, 0.15f, 0.10f);
        static Color ColOrange => EditorGUIUtility.isProSkin ? new(1.00f, 0.60f, 0.10f) : new(0.85f, 0.50f, 0.05f);
        static readonly Color ColAccent = new(0.22f, 0.60f, 1.00f);
        static readonly Color ColDim = new(0.55f, 0.55f, 0.60f);
        static readonly Color ColBlue = new(0.35f, 0.55f, 0.90f);

        void OnEnable()
        {
            _registry = new SetupRegistry();
            _ctx = SetupContext.ForCurrentProject();
            Recompute();
        }

        void Recompute()
        {
            _state = CloudSaveSetupState.GetOrCreate();
            _snapshot = SetupModel.Compute(_registry, _ctx, _state);
            Repaint();
        }

        void OnGUI()
        {
            DrawBanner();
            EditorGUI.DrawRect(new Rect(0, 54, position.width, position.height - 54), ColBg);

            using (new EditorGUILayout.VerticalScope(new GUIStyle { padding = new RectOffset(8, 8, 6, 6) }))
            {
                using (new EditorGUI.DisabledScope(_busy))
                {
                    if (GUILayout.Button(_busy ? $"Working… {_busyLabel}" : "↻  Refresh", GUILayout.Height(26)))
                        Recompute();
                }

                if (_snapshot == null) return;

                DrawMeters(_snapshot);
                DrawNextAction(_snapshot);

                EditorGUILayout.Space(6);
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (var group in _snapshot.Steps
                             .Where(e => e.State != StepState.NotApplicable)
                             .GroupBy(e => e.Definition.Category)
                             .OrderBy(g => (int)g.Key))
                {
                    EditorGUILayout.LabelField(group.Key.ToString(), EditorStyles.miniBoldLabel);
                    foreach (var eval in group) DrawStepCard(eval);
                    EditorGUILayout.Space(4);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        void DrawBanner()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, 54), ColAccent);
            GUILayout.Space(7);
            EditorGUILayout.LabelField("☁  Cloud Save — Setup & Verification", new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            });
            EditorGUILayout.LabelField("Where you are · what's left · what's next", new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.88f, 0.92f, 1f) },
            });
            GUILayout.Space(6);
        }

        void DrawMeters(SetupSnapshot s)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                MeterBox("INTEGRATION", s.Integration.ToString(), s.Integration.Fraction, ColBlue);
                MeterBox("VERIFICATION", s.Verification.ToString(), s.Verification.Fraction, ColGreen);
                var (label, col) = s.Readiness switch
                {
                    ReadinessVerdict.Green => ("READY", ColGreen),
                    ReadinessVerdict.Amber => ("ALMOST", ColOrange),
                    _ => ("NOT READY", ColRed),
                };
                MeterBox("PRODUCTION", label, s.Readiness == ReadinessVerdict.Green ? 1f : 0f, col, showBar: false);
            }
        }

        void MeterBox(string caption, string value, float fraction, Color color, bool showBar = true)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(caption, new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim } });
                EditorGUILayout.LabelField(value, new GUIStyle(EditorStyles.boldLabel) { fontSize = 15, normal = { textColor = color } });
                if (showBar)
                {
                    var r = EditorGUILayout.GetControlRect(false, 5);
                    EditorGUI.DrawRect(r, new Color(0, 0, 0, 0.25f));
                    EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), color);
                }
            }
        }

        void DrawNextAction(SetupSnapshot s)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (s.NextAction == null)
                {
                    EditorGUILayout.LabelField("NEXT", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim } });
                    EditorGUILayout.LabelField(
                        s.Readiness == ReadinessVerdict.Green
                            ? "Nothing left — every required step is validated."
                            : "Nothing actionable right now (waiting on blocked steps).",
                        EditorStyles.wordWrappedLabel);
                    return;
                }

                var next = s.NextAction;
                EditorGUILayout.LabelField("NEXT", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ColDim } });
                EditorGUILayout.LabelField(next.Step.Definition.Title,
                    new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, normal = { textColor = ColAccent } });
                EditorGUILayout.LabelField("Why?  " + next.Why, EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Show me", GUILayout.Width(90)))
                    {
                        _expanded.Clear();
                        _expanded.Add(next.Step.Definition.Id);
                    }
                }
            }
        }

        void DrawStepCard(StepEvaluation e)
        {
            var (glyph, col) = Badge(e.State);
            bool open = _expanded.Contains(e.Definition.Id);

            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, 3, rect.height + 4), col);
            GUILayout.Space(3);

            var tag = e.Definition.Obligation switch
            {
                Obligation.Optional => "  (optional)",
                Obligation.Recommended => "  (recommended)",
                _ => "",
            };

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button((open ? "▾ " : "▸ ") + glyph + "  " + e.Definition.Title + tag,
                        new GUIStyle(EditorStyles.label) { fontSize = 12, normal = { textColor = col }, alignment = TextAnchor.MiddleLeft },
                        GUILayout.ExpandWidth(true)))
                {
                    if (open) _expanded.Remove(e.Definition.Id); else _expanded.Add(e.Definition.Id);
                }
                EditorGUILayout.LabelField(SetupModel.Humanize(e.State),
                    new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = col } },
                    GUILayout.Width(120));
            }

            if (open) DrawStepBody(e);

            GUILayout.Space(3);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        void DrawStepBody(StepEvaluation e)
        {
            var c = e.Definition.Copy;
            EditorGUILayout.Space(2);
            Para("What is this?", c.WhatIsThis);
            Para("Why is this needed?", c.WhyNeeded);
            Para("What you do", c.WhatYouDo);
            Para("What we verify automatically", c.WhatWeAutoVerify);
            Para("How to test", c.HowToTest);
            Para("Expected result", c.ExpectedResult);

            // Signal split.
            EditorGUILayout.Space(2);
            SignalLine("Configuration", e.Configuration.ToString(),
                e.Configuration == ConfigurationStatus.Present ? ColGreen : e.Configuration == ConfigurationStatus.Partial ? ColOrange : ColRed);
            foreach (var f in e.ConfigFound) EditorGUILayout.LabelField("      ✓ " + f, Mini(ColDim));
            foreach (var m in e.ConfigMissing) EditorGUILayout.LabelField("      ✗ " + m, Mini(ColOrange));

            if (e.Definition.HasRuntimeValidator)
            {
                var rc = e.Runtime switch
                {
                    RuntimeVerificationStatus.Passed => ColGreen,
                    RuntimeVerificationStatus.Failed => ColRed,
                    RuntimeVerificationStatus.Stale => ColOrange,
                    _ => ColDim,
                };
                SignalLine("Runtime", e.Runtime.ToString(), rc);
                if (e.LastRecord != null)
                    EditorGUILayout.LabelField($"      last: {e.LastRecord.Outcome} · {FormatWhen(e.LastRecord.StartedAtUtc)} · {e.LastRecord.DurationMs} ms", Mini(ColDim));
                if (!string.IsNullOrEmpty(e.LastRecord?.Message))
                    EditorGUILayout.LabelField("      " + e.LastRecord.Message, Mini(ColDim));
            }

            if (e.Definition.HasManualRequirement)
                SignalLine("External console", e.Manual.ToString(),
                    e.Manual == ManualVerificationStatus.Confirmed ? ColBlue : ColOrange);

            // Blocked view.
            if (e.State == StepState.Blocked)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("🔒 Blocked — do these first:", new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = ColOrange } });
                foreach (var d in e.Dependencies)
                {
                    var mark = d.Met ? "✓" : "✗";
                    var depTitle = _snapshot.Find(d.DependsOnId)?.Definition.Title ?? d.DependsOnId;
                    EditorGUILayout.LabelField($"      {mark} {depTitle}  ({SetupModel.Humanize(d.UpstreamState)}, needs {d.Gate})",
                        Mini(d.Met ? ColDim : ColOrange));
                }
                if (!string.IsNullOrEmpty(e.BlockedByStepId) && GUILayout.Button("Go to " + (_snapshot.Find(e.BlockedByStepId)?.Definition.Title ?? e.BlockedByStepId), GUILayout.Width(220)))
                {
                    _expanded.Clear();
                    _expanded.Add(e.BlockedByStepId);
                }
            }

            // Staleness banner.
            if (e.Runtime == RuntimeVerificationStatus.Stale)
            {
                EditorGUILayout.HelpBox(
                    $"Configuration changed after the last successful verification ({FormatWhen(e.LastRecord?.StartedAtUtc)}). " +
                    "That verification is no longer valid — run it again.", MessageType.Warning);
            }

            // Run button.
            if (e.Definition.HasRuntimeValidator && e.State != StepState.Blocked)
            {
                using (new EditorGUI.DisabledScope(_busy))
                {
                    if (GUILayout.Button(e.Runtime == RuntimeVerificationStatus.Passed ? "Run again" : "Run", GUILayout.Height(24)))
                        RunValidation(e);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🤖  Copy AI prompt", EditorStyles.miniButton))
                {
                    EditorGUIUtility.systemCopyBuffer = AiPromptFor(e);
                    ShowNotification(new GUIContent("AI prompt copied"));
                }
                if (c.Links != null)
                    foreach (var link in c.Links)
                        if (GUILayout.Button(link.Label, EditorStyles.miniButton))
                            Application.OpenURL(link.Url);
            }
        }

        static string AiPromptFor(StepEvaluation e)
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

        async void RunValidation(StepEvaluation e)
        {
            var testCase = _registry.CreateCaseFor(e.Definition.Id);
            if (testCase == null) return;

            _busy = true;
            _busyLabel = e.Definition.Title;
            var fingerprint = e.CurrentFingerprint;
            Repaint();

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
                Fingerprint = fingerprint,
                StartedAtUtc = result.StartedAtUtc.ToString("o"),
                DurationMs = result.DurationMs,
                PackageVersion = PackageVersion(),
                UgsEnvironment = "",
                Message = result.Message,
            });

            _busy = false;
            _busyLabel = null;
            Recompute();
        }

        // ── helpers ─────────────────────────────────────────────────────────

        static (string glyph, Color color) Badge(StepState s) => s switch
        {
            StepState.Validated => ("✓", ColGreen),
            StepState.ManuallyConfirmed => ("☑", ColBlue),
            StepState.NeedsValidation => ("⚠", ColOrange),
            StepState.NeedsAttention => ("⚠", ColOrange),
            StepState.Failed => ("✗", ColRed),
            StepState.Blocked => ("🔒", ColOrange),
            StepState.NotConfigured => ("○", ColDim),
            StepState.Skipped => ("–", ColDim),
            _ => ("–", ColDim),
        };

        static void Para(string label, string body)
        {
            if (string.IsNullOrEmpty(body)) return;
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(body, EditorStyles.wordWrappedMiniLabel);
        }

        void SignalLine(string label, string value, Color color)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(140));
                EditorGUILayout.LabelField(value, new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = color } });
            }
        }

        static GUIStyle Mini(Color c) => new(EditorStyles.miniLabel) { normal = { textColor = c }, wordWrap = true };

        static string FormatWhen(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "never";
            return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : iso;
        }

        static string PackageVersion()
        {
            try
            {
                var pi = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CloudSaveSetupWindow).Assembly);
                return pi?.version ?? "";
            }
            catch { return ""; }
        }
    }
}
