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

            EditorGUI.DrawRect(new Rect(0, 54, position.width, position.height - 54), ColBg);

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
            _results.Add(CheckFacebookAuth());
            _results.Add(CheckAndroidAuth());
            _results.Add(CheckiOSAuth());
            _results.Add(CheckProjectSettings());
            _results.Add(CheckAccountDeletionCompliance());
            _results.Add(CheckAccountDeletionUI());
            _results.Add(CheckPrivacyAndDataDeletionUrls());
            _results.Add(CheckSaveResetSupport());
            _results.Add(CheckLegacyMigration());
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
            EditorGUILayout.LabelField("  \u2022 Verify Sign-In Methods are ENABLED in Dashboard \u2192 Authentication \u2192 Sign-In Methods",
                detailStyle);
            EditorGUILayout.LabelField("    Anonymous: ON (default) | GPGS: ON + Web client ID | Apple Game Center: ON",
                detailStyle);
            EditorGUILayout.LabelField("  \u2022 Google Play Data Safety Form: Register your public Account Deletion URL in Play Console",
                detailStyle);
            EditorGUILayout.LabelField("  \u2022 Meta Developer Portal: Register Data Deletion Request/Instructions URL under App Settings > Basic",
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
                    $"Found {matches.Count} reference(s). Syncs cloud \u2192 local with conflict resolution.", matches);
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
            var regex = new Regex("long\\s+(LastSaved|SaveDateTime|LastSaveTime)");
            var allCs = Directory.EnumerateFiles(Path.GetFullPath("Assets"), "*.cs", SearchOption.AllDirectories);
            var matches = new List<string>();
            foreach (var f in allCs)
            {
                try
                {
                    var text = File.ReadAllText(f);
                    var m = regex.Match(text);
                    if (m.Success)
                        matches.Add(Path.GetFileName(f) + "  \u2014  " + m.Value);
                }
                catch { }
            }
            if (matches.Count > 0)
                return Pass("Field 'long LastSaved / SaveDateTime' found",
                    "Your save class tracks timestamps for conflict resolution.", matches);
            var legacy = FindInCsFiles("LastSaved|SaveDateTime", true);
            if (legacy.Count > 0)
                return Info("Timestamp field found but may not be 'long'",
                    "Verify your save class has:  public long LastSaved; (or SaveDateTime)", legacy);
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
                    if (text.Contains("[System.Serializable]") || text.Contains("[Serializable]"))
                        serializables.Add(Path.GetFileName(f));
                }
                catch { }
            }
            var matches = FindInCsFiles("\\[(System\\.)?Serializable\\]", true);
            if (matches.Count > 0)
                return Pass("Serializable class found",
                    $"Found {serializables.Count} [Serializable] class(es)", matches);
            return Info("No [Serializable] class found",
                "CloudSave uses JsonUtility which requires [Serializable] on your save class.", matches);
        }

        static AuditItem CheckCloudSaveUICreated()
        {
            var matches = FindInCsFiles("CloudSaveUI\\.Create\\s*\\(\\s*\\)|formCloudSave", true);
            if (matches.Count > 0)
                return Pass("Conflict / Save UI found", "Shows loading overlay, toasts, or custom conflict dialog.", matches);
            return Info("CloudSaveUI.Create() NOT called",
                "Recommended: CloudSaveUI.Create();  \u2014 shows loading overlay, toasts, and conflict dialog.", matches);
        }

        static AuditItem CheckSyncStatusUICreated()
        {
            var matches = FindInCsFiles("SyncStatusUI\\.Create\\s*\\(\\s*\\)|btCloudSave", true);
            if (matches.Count > 0)
                return Pass("Sync status indicator found", "Persistent sync status indicator in UI.", matches);
            return Info("SyncStatusUI.Create() NOT called",
                "Recommended: SyncStatusUI.Create();  \u2014 corner indicator (Synced/Syncing/Offline/Error)", matches);
        }

        static AuditItem CheckCloudAuthUICreated()
        {
            var matches = FindInCsFiles("CloudAuthUI\\.Create\\s*\\(\\s*\\)|formSaveProgress|formAccount", true);
            if (matches.Count > 0)
            {
                return Pass("Auth / Account UI found",
                    "Auth dialog / account management is ready with login handlers.", matches);
            }
            return Info("CloudAuthUI.Create() NOT called",
                "Optional: shows a modal to let players link their account (Facebook/GPGS/Apple/Game Center).\n" +
                "Wire it: auth.OnLinkRequested += async () => await CloudAuth.LinkFacebookAsync(token);",
                matches);
        }

        static AuditItem CheckAuthUpgrade()
        {
            var matches = FindInCsFiles("LinkGooglePlayGamesAsync|LinkAppleGameCenterAsync|LinkAppleAsync|LinkFacebookAsync|LinkGoogleAsync", true);
            if (matches.Count > 0)
            {
                var hasFacebook = false;
                var hasAndroid = false;
                var hasIOS = false;
                foreach (var m in matches)
                {
                    if (m.Contains("LinkFacebook")) hasFacebook = true;
                    if (m.Contains("LinkGooglePlayGames") || m.Contains("LinkGoogle")) hasAndroid = true;
                    if (m.Contains("LinkApple")) hasIOS = true;
                }
                var detail = "";
                if (hasFacebook) detail += "Facebook configured. ";
                if (hasAndroid) detail += "Android (Google) configured. ";
                if (hasIOS) detail += "iOS (Apple) configured. ";
                return Pass("Auth upgrade code found", detail.Trim(), matches);
            }
            return Info("Auth upgrade NOT configured",
                "Optional but needed for cross-device saves.\n" +
                "Facebook: FB.LogInWithReadPermissions \u2192 LinkFacebookAsync(token)\n" +
                "Android: PlayGamesPlatform.Authenticate() \u2192 RequestServerSideAccess() \u2192 LinkGooglePlayGamesAsync(code)\n" +
                "iOS: AppleAuthManager \u2192 LinkAppleAsync(token) / GKLocalPlayer \u2192 LinkAppleGameCenterAsync(...)",
                matches);
        }

        static AuditItem CheckFacebookAuth()
        {
            var matches = FindInCsFiles("FB\\.Init|LinkFacebookAsync|FacebookSDK|AccessToken\\.CurrentAccessToken", true);
            if (matches.Count > 0)
                return Pass("Facebook auth setup detected", "Facebook SDK and linking code found.", matches);

            return Info("Facebook auth NOT detected",
                "Optional. To enable Facebook login:\n" +
                "  1. Install Facebook SDK for Unity\n" +
                "  2. Dashboard \u2192 Authentication \u2192 Sign-In Methods: enable Facebook + paste App ID / Secret\n" +
                "  3. FB.LogInWithReadPermissions(...) \u2192 CloudAuth.LinkFacebookAsync(accessToken)",
                matches);
        }

        /// Checks for Android GPGS plugin (com.google.play.games) and PlayGamesPlatform references.
        static AuditItem CheckAndroidAuth()
        {
            var matches = new List<string>();

            var manifestPath = Path.GetFullPath("Packages/manifest.json");
            if (File.Exists(manifestPath))
            {
                var text = File.ReadAllText(manifestPath);
                if (text.Contains("com.google.play.games"))
                    matches.Add("Found: com.google.play.games in Packages/manifest.json");
            }

            var pluginDir = new DirectoryInfo(Path.GetFullPath("Assets"));
            if (pluginDir.Exists)
            {
                var gpgFiles = pluginDir.GetFiles("*GooglePlayGames*", SearchOption.AllDirectories);
                foreach (var f in gpgFiles.Take(3))
                    matches.Add("Found: " + GetRelativePath(f.FullName));
            }

            var csMatches = FindInCsFiles("PlayGamesPlatform|GooglePlayGames", true);
            matches.AddRange(csMatches.Take(3));

            if (matches.Count > 0)
                return Pass("Android (GPGS) auth setup detected",
                    "Google Play Games plugin found.", matches);

            return Info("Android GPGS auth NOT detected",
                "Not required. Without GPGS: saves stay on device (anonymous).\n" +
                "To enable cross-device saves:\n" +
                "  1. Google Play Console: enable Play Games, get OAuth 2.0 Web client ID\n" +
                "  2. Dashboard \u2192 Authentication \u2192 Sign-In Methods: activate GPGS + paste client ID\n" +
                "  3. Install GPGS plugin (com.google.play.games)\n" +
                "  4. PlayGamesPlatform.Instance.Authenticate()\n" +
                "  5. RequestServerSideAccess(false, code => ...)\n" +
                "  6. CloudAuth.LinkGooglePlayGamesAsync(code)",
                matches);
        }

        /// Checks for Apple.GameKit references (official Unity package, only supported option).
        static AuditItem CheckiOSAuth()
        {
            var matches = new List<string>();

            var csMatches = FindInCsFiles("Apple\\.GameKit|GKLocalPlayer|FetchItemsForIdentityVerification", true);
            matches.AddRange(csMatches.Take(3));

            var appleLink = FindInCsFiles("LinkAppleGameCenterAsync|LinkAppleAsync", true);
            matches.AddRange(appleLink.Take(2));

            if (matches.Count > 0)
                return Pass("iOS (Game Center / Apple) auth setup detected",
                    "Apple.GameKit reference found.", matches);

            return Info("iOS Game Center auth NOT detected",
                "Not required. Without Game Center: saves stay on device (anonymous).\n" +
                "To enable cross-device saves:\n" +
                "  1. Apple Developer: enable Game Center on App ID\n" +
                "  2. Dashboard \u2192 Authentication \u2192 Sign-In Methods: enable Apple Game Center\n" +
                "  3. Install Apple.GameKit (official Unity package)\n" +
                "  4. Authenticate with Game Center (GKLocalPlayer)\n" +
                "  5. Get identity verification signature\n" +
                "  6. CloudAuth.LinkAppleGameCenterAsync(..., signature, ...)",
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

        static AuditItem CheckAccountDeletionCompliance()
        {
            var matches = FindInCsFiles("CloudAuth\\.DeleteAccountAsync|DeleteAccountAsync", true);
            if (matches.Count > 0)
            {
                return Pass("Account Deletion (Apple 5.1.1(v) & Google Play)",
                    "Found DeleteAccountAsync call. Complies with store account deletion requirements.", matches);
            }

            var authMatches = FindInCsFiles("LinkGooglePlayGamesAsync|LinkApple|LinkFacebook|FB\\.LogIn", false);
            if (authMatches.Count > 0)
            {
                return Fail("MANDATORY: Account Deletion NOT implemented",
                    "Apple Guideline 5.1.1(v) and Google Play MANDATE an in-app account deletion mechanism " +
                    "for any game supporting player sign-in or social linking.\n" +
                    "Fix: Provide an in-game option calling await CloudAuth.DeleteAccountAsync();",
                    authMatches);
            }

            return Info("Account Deletion not detected",
                "Recommended: Call CloudAuth.DeleteAccountAsync(). Required by Apple & Google Play if your game supports accounts or social logins.",
                matches);
        }

        static AuditItem CheckAccountDeletionUI()
        {
            var matches = FindInCsFiles("DeleteConfirmation|btRemoveAccount|DeleteAccount|accountremoved|ExcluirConta|DeleteAccountTitle", true);
            if (matches.Count > 0)
            {
                return Pass("Account Deletion UI / Confirmation found",
                    "In-app UI with confirmation safeguards for account deletion detected.", matches);
            }

            return Info("Account Deletion UI confirmation not detected",
                "Ensure your settings/account screen provides a clear 'Delete Account' button with confirmation dialog (e.g. typing DELETE).",
                matches);
        }

        static AuditItem CheckPrivacyAndDataDeletionUrls()
        {
            var matches = FindInCsFiles("Application\\.OpenURL.*(policy|privacy|terms|termos|privacidade|delete)", true);
            if (matches.Count > 0)
            {
                return Pass("Privacy Policy / Terms URL referenced",
                    "Found privacy policy or deletion instructions link in code.", matches);
            }

            return Info("Privacy Policy / Deletion URL not detected in code",
                "Google Play (Data Safety Form) and Meta Developer Portal require public HTTPS URLs " +
                "for Privacy Policy and User Data Deletion Instructions.", matches);
        }

        static AuditItem CheckSaveResetSupport()
        {
            var matches = FindInCsFiles("ResetProgressAsync|DeleteCloudSaveAsync|ResetGameProgress|ResetProgress", true);
            if (matches.Count > 0)
            {
                return Pass("Save Game Reset support detected",
                    "Game implements progress wipe / reset functionality without destroying account identity.", matches);
            }

            return Info("Save Game Reset not detected",
                "Optional but recommended for player UX: CloudSync.ResetProgressAsync(onClearLocalSave);",
                matches);
        }

        static AuditItem CheckLegacyMigration()
        {
            var migrationMatches = FindInCsFiles("CloudMigration\\.TryMigrateAsync", true);
            if (migrationMatches.Count > 0)
            {
                return Pass("Legacy Cloud Migration configured",
                    "Using CloudMigration.TryMigrateAsync to seamlessly import legacy saves into UGS.", migrationMatches);
            }

            var legacyMatches = FindInCsFiles("PlayFabClientAPI|PlayFab|Firebase", true);
            if (legacyMatches.Count > 0)
            {
                return Info("Legacy backend detected without CloudMigration",
                    "Found PlayFab / Firebase references. Use CloudMigration.TryMigrateAsync to migrate existing players to UGS automatically.",
                    legacyMatches);
            }

            return Pass("Pure Unity Cloud Save architecture",
                "No legacy backends detected. Game operates cleanly on UGS.", new List<string>());
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
   - If found, note the key used

3. **CloudSync.InitAndSyncAsync()**
   - Search .cs files for ""CloudSync.InitAndSyncAsync""

4. **CloudSync.SaveAsync()**
   - Search .cs files for ""CloudSync.SaveAsync""

5. **long LastSaved field**
   - Search .cs files for ""long LastSaved"" in [System.Serializable] classes

6. **[System.Serializable] class**
   - Search .cs files for a [System.Serializable] class for save data

7. **CloudSaveUI.Create()** — Search .cs for ""CloudSaveUI.Create()""

8. **SyncStatusUI.Create()** — Search .cs for ""SyncStatusUI.Create()""

9. **CloudAuthUI.Create()** — Search .cs for ""CloudAuthUI.Create()""
   - Also check if ""OnLinkRequested"" is wired

10. **Auth upgrade (UGS link calls)**
    - Search .cs for ""LinkGooglePlayGamesAsync"", ""LinkAppleGameCenterAsync"", ""LinkAppleAsync""
    - Note which platform(s) are configured

11. **Android GPGS plugin**
    - Check Packages/manifest.json for ""com.google.play.games""
    - Search Assets/ for GooglePlayGames DLLs or .cs references
    - Search .cs for ""PlayGamesPlatform"" or ""GooglePlayGames""

12. **iOS (Apple.GameKit)**
    - Search .cs for ""Apple.GameKit"", ""GKLocalPlayer"", ""FetchItemsForIdentityVerification""
    - Search for ""LinkAppleGameCenterAsync"" or ""LinkAppleAsync""
    - (Note: Apple.GameKit is the official Unity package — this is the only supported option)

13. **Unity Services**
    - Check ProjectSettings/ProjectSettings.asset for ""CloudSave"" or ""Unity Gaming Services""

14. **Dashboard Sign-In Methods (manual check)**
    - Go to dashboard.unity3d.com \u2192 Authentication \u2192 Sign-In Methods
    - Verify Anonymous is enabled
    - If Android: verify Google Play Games is ON and has a Web client ID
    - If iOS: verify Apple Game Center (and optionally Apple) is ON

15. **Google Play Console (Android, manual check)**
    - In play.google.com/console, verify Play Games Services is enabled
    - Verify OAuth 2.0 Web client ID exists and matches Dashboard

16. **Apple Developer (iOS, manual check)**
    - Verify Game Center capability is enabled on the App ID
    - If using Sign in with Apple: verify Service ID + Redirect URL

## Output format

| # | Item | Status | Files Found | Details/Action Needed |
|---|------|--------|-------------|----------------------|
| 1 | Package installed | ✅ / ❌ / ⚠️ | (paths) | (what to do) |
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
