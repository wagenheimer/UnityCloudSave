using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Wagenheimer.CloudSave.Editor
{
    public class CloudSaveAudit : EditorWindow
    {
        Vector2 _scroll;
        List<AuditItem> _results;
        bool _ranAudit;
        bool _showDetails;

        static Color ColBg => EditorGUIUtility.isProSkin
            ? new(0.16f, 0.16f, 0.18f) : new(0.82f, 0.82f, 0.84f);
        static Color ColCard => EditorGUIUtility.isProSkin
            ? new(0.20f, 0.20f, 0.22f) : new(0.90f, 0.90f, 0.92f);
        static Color ColGreen => EditorGUIUtility.isProSkin
            ? new(0.20f, 0.75f, 0.35f) : new(0.10f, 0.55f, 0.20f);
        static Color ColRed => EditorGUIUtility.isProSkin
            ? new(0.85f, 0.25f, 0.20f) : new(0.70f, 0.15f, 0.10f);
        static Color ColOrange => EditorGUIUtility.isProSkin
            ? new(1.00f, 0.60f, 0.10f) : new(0.85f, 0.50f, 0.05f);
        static readonly Color ColAccent = new(0.22f, 0.60f, 1.00f);
        static readonly Color ColCode = new(0.12f, 0.12f, 0.14f);
        static readonly Color ColCodeText = new(0.65f, 0.85f, 0.45f);
        static readonly Color ColDim = new(0.55f, 0.55f, 0.60f);

        [MenuItem("Tools/Wagenheimer/Cloud Save/Audit Integration", priority = 2)]
        static void Open()
        {
            var w = GetWindow<CloudSaveAudit>("Cloud Save Audit");
            w.minSize = new Vector2(500, 400);
        }

        void OnEnable() => _results = new List<AuditItem>();

        void OnGUI()
        {
            var bg = ColBg;

            EditorGUI.DrawRect(new Rect(0, 0, position.width, 54), ColAccent);
            GUILayout.Space(8);
            var bannerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 17,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("\u2699\ufe0f  Cloud Save Integration Audit", bannerStyle, GUILayout.ExpandWidth(true));
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.85f, 0.90f, 1f) },
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Scans your project for CloudSave setup status", subStyle);
            GUILayout.Space(6);

            EditorGUI.DrawRect(new Rect(0, 54, position.width, position.height - 54), bg);

            var areaStyle = new GUIStyle { padding = new RectOffset(6, 6, 4, 4) };
            EditorGUILayout.BeginVertical(areaStyle);

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("\ud83d\udd0d  Run Audit Now", GUILayout.Height(30)))
                    RunAudit();
                if (_ranAudit)
                {
                    _showDetails = GUILayout.Toggle(_showDetails, "Show File Details", GUILayout.Height(30), GUILayout.Width(130));
                }
            }

            if (_ranAudit && _results.Count > 0)
            {
                var passed = _results.Count(r => r.Status == AuditStatus.Passed);
                var failed = _results.Count(r => r.Status == AuditStatus.Failed);
                var total = _results.Count;
                var pct = total > 0 ? (float)passed / total : 0f;

                EditorGUILayout.Space(4);

                var barRect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));
                var color = pct >= 1f ? ColGreen : pct >= 0.7f ? ColOrange : ColRed;
                if (pct > 0f)
                    EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), color);
                var centerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                    fontSize = 11
                };
                EditorGUI.LabelField(barRect, $"{passed}/{total}  items OK  ({(int)(pct * 100)}%)", centerStyle);

                if (passed == total)
                    EditorGUILayout.HelpBox("All checks passed! CloudSave is fully configured.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox(
                        $"Checks: {passed} passed, {failed} failed, {total - passed - failed} optional.\nFix failures marked in red.",
                        MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_ranAudit)
            {
                for (int i = 0; i < _results.Count; i++)
                {
                    DrawAuditItem(_results[i], i, _showDetails);
                    EditorGUILayout.Space(4);
                }

                DrawManualChecks();
            }
            else
            {
                var msgStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                { normal = { textColor = ColDim }, alignment = TextAnchor.MiddleCenter };
                EditorGUILayout.Space(40);
                EditorGUILayout.LabelField("Click \"Run Audit Now\" to scan your project", msgStyle, GUILayout.ExpandWidth(true));
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(4);

            DrawFooter();

            EditorGUILayout.EndVertical();
        }

        void RunAudit()
        {
            _results.Clear();
            _ranAudit = true;

            _results.Add(CheckPackageInstalled());
            _results.Add(CheckConfigure());
            _results.Add(CheckInitAndSync());
            _results.Add(CheckSaveAsync());
            _results.Add(CheckLastSavedField());
            _results.Add(CheckSaveDataSerializable());
            _results.Add(CheckCloudSaveUICreated());
            _results.Add(CheckSyncStatusUICreated());
            _results.Add(CheckCloudAuthUICreated());
            _results.Add(CheckAuthUpgrade());
            _results.Add(CheckProjectSettings());
        }

        void DrawAuditItem(AuditItem item, int index, bool showFiles)
        {
            var status = item.Status;
            var color = status == AuditStatus.Passed ? ColGreen
                : status == AuditStatus.Failed ? ColRed : ColOrange;
            var icon = status == AuditStatus.Passed ? "\u2713"
                : status == AuditStatus.Failed ? "\u2717" : "!";

            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), color);
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            var badgeR = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20), GUILayout.Height(20));
            EditorGUI.DrawRect(badgeR, color);
            var badgeStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            EditorGUI.LabelField(badgeR, (index + 1).ToString(), badgeStyle);
            GUILayout.Space(8);

            EditorGUILayout.BeginVertical();
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 13, normal = { textColor = color } };
            EditorGUILayout.LabelField(item.Title, titleStyle);

            if (!string.IsNullOrEmpty(item.Detail))
            {
                var detailStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                { normal = { textColor = ColDim } };
                EditorGUILayout.LabelField(item.Detail, detailStyle);
            }

            if (showFiles && item.Matches.Count > 0)
            {
                EditorGUI.DrawRect(EditorGUILayout.BeginVertical(), ColCode);
                GUILayout.Space(2);
                foreach (var m in item.Matches)
                {
                    var fileStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal = { textColor = ColCodeText },
                        wordWrap = true,
                        fontSize = 10
                    };
                    EditorGUILayout.LabelField("  " + m, fileStyle);
                }
                GUILayout.Space(2);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        void DrawManualChecks()
        {
            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), ColOrange);
            GUILayout.Space(4);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 13, normal = { textColor = ColOrange } };
            EditorGUILayout.LabelField("\u26a0\ufe0f  Manual Checks (not auto-detectable)", titleStyle);

            var detailStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            { normal = { textColor = ColDim } };
            EditorGUILayout.LabelField(
                "These must be verified manually in the Unity Editor or Dashboard:",
                detailStyle);

            EditorGUILayout.LabelField("  \u2022 Verify Cloud Save is ENABLED in Unity Dashboard (dashboard.unity3d.com)",
                detailStyle);
            EditorGUILayout.LabelField("  \u2022 Verify project is LINKED via Edit \u2192 Project Settings \u2192 Services",
                detailStyle);
            EditorGUILayout.LabelField("  \u2022 Verify you are logged into Unity Account in the Editor",
                detailStyle);
            EditorGUILayout.LabelField("  \u2022 Test real sync: Build & run on device, save, reinstall, check if cloud save loads",
                detailStyle);

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();
            if (Btn("\ud83d\udcd6  Full Integration Guide", ColAccent))
                Application.OpenURL("https://github.com/wagenheimer/UnityCloudSave/blob/main/docs/INTEGRATION.md");
            if (Btn("\ud83e\udde9  Prefab Generator", ColAccent))
                EditorApplication.ExecuteMenuItem("Tools/Wagenheimer/Cloud Save/Setup UI Prefabs/All");
            if (Btn("\ud83e\uddea  Test Window", ColAccent))
                EditorApplication.ExecuteMenuItem("Tools/Wagenheimer/Cloud Save/Open Test Window");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);
            if (Btn("Copy AI Audit Prompt", ColAccent))
            {
                EditorGUIUtility.systemCopyBuffer = GetAiPrompt();
                Debug.Log("[CloudSave] AI audit prompt copied.");
            }
        }

        static bool Btn(string label, Color color)
        {
            var orig = GUI.backgroundColor;
            GUI.backgroundColor = color;
            var clicked = GUILayout.Button(label, GUILayout.Height(22));
            GUI.backgroundColor = orig;
            return clicked;
        }

        // ── Audit checks ─────────────────────────────────────────────────

        static AuditItem CheckPackageInstalled()
        {
            var matches = new List<string>();
            var manifestPath = Path.GetFullPath("Packages/manifest.json");
            if (File.Exists(manifestPath))
            {
                var text = File.ReadAllText(manifestPath);
                if (text.Contains("com.wagenheimer.cloudsave"))
                {
                    var lines = File.ReadAllLines(manifestPath);
                    for (int i = 0; i < lines.Length; i++)
                        if (lines[i].Contains("com.wagenheimer.cloudsave"))
                            matches.Add($"Packages/manifest.json:{i + 1}  " + lines[i].Trim());
                    return Pass("Package installed", "Found in manifest.json or referenced in code", matches);
                }
            }
            var packagePath = Path.GetFullPath("Packages/com.wagenheimer.cloudsave");
            if (Directory.Exists(packagePath))
                return Pass("Package installed", "Found in Packages/ directory", matches);

            var csMatches = FindInCsFiles("Wagenheimer\\.CloudSave", false);
            if (csMatches.Count > 0)
                return Info("Namespace used but where's the package?",
                    "Found code references to Wagenheimer.CloudSave. If syncing works, package is resolved.", csMatches);

            return Fail("Package NOT found",
                "Add via Window \u2192 Package Manager \u2192 + \u2192 Add package from git URL:\nhttps://github.com/wagenheimer/UnityCloudSave.git",
                matches);
        }

        static AuditItem CheckConfigure()
        {
            var matches = FindInCsFiles("CloudSync\\.Configure", true);
            if (matches.Count > 0)
            {
                var keyMatches = new List<string>();
                var regex = new Regex("CloudSync\\.Configure\\(\"([^\"]+)\"\\)");
                foreach (var path in Directory.EnumerateFiles(Path.GetFullPath("Assets"), "*.cs", SearchOption.AllDirectories))
                {
                    try
                    {
                        var text = File.ReadAllText(path);
                        var m = regex.Match(text);
                        if (m.Success)
                            keyMatches.Add($"Key: \"{m.Groups[1].Value}\"  in {Path.GetFileName(path)}");
                    }
                    catch { }
                }
                var detail = keyMatches.Count > 0
                    ? string.Join("\n", keyMatches)
                    : $"{matches.Count} reference(s) found";
                return Pass("CloudSync.Configure() called", detail, matches);
            }
            return Fail("CloudSync.Configure() NOT called",
                "Required: CloudSync.Configure(\"my_save_key\");  at game startup.", matches);
        }

        static AuditItem CheckInitAndSync()
        {
            var matches = FindInCsFiles("CloudSync\\.InitAndSyncAsync", true);
            if (matches.Count > 0)
                return Pass("CloudSync.InitAndSyncAsync() called",
                    $"Found {matches.Count} reference(s). Syncs cloud → local with conflict resolution.", matches);
            return Fail("CloudSync.InitAndSyncAsync() NOT called",
                "Required at startup after Configure():\n_ = CloudSync.InitAndSyncAsync(timestamp, OnCloudNewer);", matches);
        }

        static AuditItem CheckSaveAsync()
        {
            var matches = FindInCsFiles("CloudSync\\.SaveAsync", true);
            if (matches.Count > 0)
                return Pass("CloudSync.SaveAsync() called",
                    $"Found {matches.Count} reference(s). Uploads local data to cloud.", matches);
            return Fail("CloudSync.SaveAsync() NOT called",
                "Required after each local save:\n_ = CloudSync.SaveAsync(bytes, timestamp);", matches);
        }

        static AuditItem CheckLastSavedField()
        {
            var regex = new Regex("long\\s+LastSaved");
            var allCs = Directory.EnumerateFiles(Path.GetFullPath("Assets"), "*.cs", SearchOption.AllDirectories);
            var matches = new List<string>();
            foreach (var f in allCs)
            {
                try
                {
                    var text = File.ReadAllText(f);
                    var m = regex.Match(text);
                    if (m.Success)
                        matches.Add(Path.GetFileName(f) + "  —  " + m.Value);
                }
                catch { }
            }
            if (matches.Count > 0)
                return Pass("Field 'long LastSaved' found",
                    "Your save class tracks timestamps for conflict resolution.", matches);
            var legacy = FindInCsFiles("LastSaved", true);
            if (legacy.Count > 0)
                return Info("'LastSaved' found but may not be 'long'",
                    "Verify your save class has:  public long LastSaved;", legacy);
            return Fail("No 'long LastSaved' field found",
                "Your save class MUST have:  public long LastSaved;  (set to DateTime.UtcNow.Ticks)", matches);
        }

        static AuditItem CheckSaveDataSerializable()
        {
            var serializables = new List<string>();
            var allCs = Directory.EnumerateFiles(Path.GetFullPath("Assets"), "*.cs", SearchOption.AllDirectories);
            foreach (var f in allCs)
            {
                try
                {
                    var text = File.ReadAllText(f);
                    if (text.Contains("[System.Serializable]"))
                        serializables.Add(Path.GetFileName(f));
                }
                catch { }
            }
            var matches = FindInCsFiles("\\[System\\.Serializable\\]", true);
            if (matches.Count > 0)
                return Pass("Serializable class found",
                    $"Found {serializables.Count} [System.Serializable] class(es)", matches);
            return Info("No [System.Serializable] class found",
                "CloudSave uses JsonUtility which requires [System.Serializable] on your save class.", matches);
        }

        static AuditItem CheckCloudSaveUICreated()
        {
            var matches = FindInCsFiles("CloudSaveUI\\.Create\\s*\\(\\s*\\)", true);
            if (matches.Count > 0)
                return Pass("CloudSaveUI.Create() called", "Shows loading overlay, toasts, conflict dialog.", matches);
            return Info("CloudSaveUI.Create() NOT called",
                "Recommended: CloudSaveUI.Create();  — shows loading overlay, toasts, and conflict dialog.", matches);
        }

        static AuditItem CheckSyncStatusUICreated()
        {
            var matches = FindInCsFiles("SyncStatusUI\\.Create\\s*\\(\\s*\\)", true);
            if (matches.Count > 0)
                return Pass("SyncStatusUI.Create() called", "Persistent sync status indicator.", matches);
            return Info("SyncStatusUI.Create() NOT called",
                "Recommended: SyncStatusUI.Create();  — corner indicator (Synced/Syncing/Offline/Error)", matches);
        }

        static AuditItem CheckCloudAuthUICreated()
        {
            var matches = FindInCsFiles("CloudAuthUI\\.Create\\s*\\(\\s*\\)", true);
            if (matches.Count > 0)
            {
                var linkRefs = FindInCsFiles("OnLinkRequested", true);
                if (linkRefs.Count > 0)
                    return Pass("CloudAuthUI.Create() + OnLinkRequested wired",
                        "Auth dialog is ready with link handler.", matches.Concat(linkRefs).ToList());
                return Pass("CloudAuthUI.Create() called",
                    "Auth dialog created but OnLinkRequested not wired yet.", matches);
            }
            return Info("CloudAuthUI.Create() NOT called",
                "Optional: shows a modal to let players link their account (GPGS/Game Center).\n" +
                "Wire it: auth.OnLinkRequested += async () => await CloudAuth.LinkGooglePlayGamesAsync(code);",
                matches);
        }

        static AuditItem CheckAuthUpgrade()
        {
            var matches = FindInCsFiles("LinkGooglePlayGamesAsync|LinkAppleGameCenterAsync|LinkAppleAsync", true);
            if (matches.Count > 0)
                return Pass("Auth upgrade configured", "Player can link to GPGS / Apple Game Center.", matches);
            return Info("Auth upgrade NOT configured",
                "Optional but needed for cross-device saves.\n" +
                "Add: await CloudAuth.LinkGooglePlayGamesAsync(code);  (Android, requires GPGS plugin)\n" +
                "Add: await CloudAuth.LinkAppleGameCenterAsync(...);  (iOS, requires native bridge)",
                matches);
        }

        static AuditItem CheckProjectSettings()
        {
            var path = Path.GetFullPath("ProjectSettings/ProjectSettings.asset");
            if (!File.Exists(path))
                return Fail("ProjectSettings.asset NOT found", "Cannot verify Unity Services configuration.", new List<string>());

            var text = File.ReadAllText(path);
            var matches = new List<string>();
            if (text.Contains("CloudSave"))
                matches.Add("Cloud Save references found in ProjectSettings");
            if (text.Contains("Unity Gaming Services") || text.Contains("Unity Project ID"))
                matches.Add("Unity Gaming Services configured");

            if (matches.Count > 0)
                return Pass("Unity Services configured", "ProjectSettings indicates Services setup.", matches);
            return Info("Unity Services setup unclear",
                "Could not auto-verify. Open Edit \u2192 Project Settings \u2192 Services and confirm project is linked.",
                matches);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        static List<string> FindInCsFiles(string pattern, bool withPaths)
        {
            var results = new List<string>();
            var assetsDir = new DirectoryInfo(Path.GetFullPath("Assets"));
            if (!assetsDir.Exists) return results;

            var regex = new Regex(pattern, RegexOptions.Compiled);
            foreach (var file in assetsDir.GetFiles("*.cs", SearchOption.AllDirectories))
            {
                try
                {
                    var lines = File.ReadAllLines(file.FullName);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (regex.IsMatch(lines[i]))
                        {
                            var relPath = GetRelativePath(file.FullName);
                            results.Add(withPaths
                                ? $"{relPath}:{i + 1}  {lines[i].Trim()}"
                                : $"{relPath}:{i + 1}");
                        }
                    }
                }
                catch { }
            }

            return results;
        }

        static string GetRelativePath(string fullPath)
        {
            var assets = Path.GetFullPath("Assets");
            if (fullPath.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(assets.Length + 1);
            var project = Path.GetDirectoryName(Path.GetFullPath("Packages"));
            if (fullPath.StartsWith(project, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(project.Length + 1);
            return fullPath;
        }

        static AuditItem Pass(string title, string detail, List<string> matches) =>
            new(AuditStatus.Passed, title, detail, matches);

        static AuditItem Fail(string title, string detail, List<string> matches) =>
            new(AuditStatus.Failed, title, detail, matches);

        static AuditItem Info(string title, string detail, List<string> matches) =>
            new(AuditStatus.Info, title, detail, matches);

        static string GetAiPrompt()
        {
            return @"You are a Unity CloudSave integration auditor.
Analyze the project files and output ONLY a markdown table.
Do NOT add explanations beyond the table.

## Checks

1. **Package installed**
   - Check if ""com.wagenheimer.cloudsave"" in Packages/manifest.json
   - OR package directory exists at Packages/com.wagenheimer.cloudsave/
   - OR any .cs file references ""Wagenheimer.CloudSave""

2. **CloudSync.Configure()**
   - Search .cs files for ""CloudSync.Configure""
   - If found, note the key used (e.g. ""CloudSync.Configure(""my_key"")"")

3. **CloudSync.InitAndSyncAsync()**
   - Search .cs files for ""CloudSync.InitAndSyncAsync""

4. **CloudSync.SaveAsync()**
   - Search .cs files for ""CloudSync.SaveAsync""

5. **long LastSaved field**
   - Search .cs files for ""long LastSaved"" in [System.Serializable] classes

6. **[System.Serializable] class**
   - Search .cs files for a class with [System.Serializable] that contains save data

7. **CloudSaveUI.Create()**
   - Search .cs files for ""CloudSaveUI.Create()""

8. **SyncStatusUI.Create()**
   - Search .cs files for ""SyncStatusUI.Create()""

9. **CloudAuthUI.Create()**
   - Search .cs files for ""CloudAuthUI.Create()""
   - Also check if ""OnLinkRequested"" is wired

10. **Auth upgrade**
    - Search .cs for ""LinkGooglePlayGamesAsync"", ""LinkAppleGameCenterAsync"", ""LinkAppleAsync""

11. **Unity Services**
    - Check ProjectSettings/ProjectSettings.asset for ""CloudSave"" or ""Unity Gaming Services""

## Output format

| # | Item | Status | Files Found | Details/Action Needed |
|---|------|--------|-------------|----------------------|
| 1 | Package installed | ✅ / ❌ / ⚠️ | (file paths) | (what to do) |
| 2 | CloudSync.Configure | ✅ / ❌ | ... | ... |
| ... | ... | ... | ... | ... |";
        }
    }

    enum AuditStatus { Passed, Failed, Info }

    class AuditItem
    {
        public AuditStatus Status { get; }
        public string Title { get; }
        public string Detail { get; }
        public List<string> Matches { get; }

        public AuditItem(AuditStatus status, string title, string detail, List<string> matches)
        {
            Status = status;
            Title = title;
            Detail = detail;
            Matches = matches ?? new List<string>();
        }
    }
}
