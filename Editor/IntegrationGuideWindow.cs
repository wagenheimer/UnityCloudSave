using UnityEditor;
using UnityEngine;

namespace Wagenheimer.CloudSave.Editor
{
    internal class IntegrationGuideWindow : EditorWindow
    {
        Vector2 _scroll;

        static Color ColBg => EditorGUIUtility.isProSkin
            ? new Color(0.16f, 0.16f, 0.18f) : new Color(0.82f, 0.82f, 0.84f);
        static Color ColCard => EditorGUIUtility.isProSkin
            ? new Color(0.20f, 0.20f, 0.22f) : new Color(0.90f, 0.90f, 0.92f);
        static Color ColCode => EditorGUIUtility.isProSkin
            ? new Color(0.12f, 0.12f, 0.14f) : new Color(0.95f, 0.95f, 0.97f);
        static Color ColText => EditorGUIUtility.isProSkin
            ? new Color(0.92f, 0.92f, 0.95f) : Color.black;
        static Color ColDim => EditorGUIUtility.isProSkin
            ? new Color(0.55f, 0.55f, 0.60f) : new Color(0.30f, 0.30f, 0.33f);
        static readonly Color ColAccent = new(0.22f, 0.60f, 1.00f);
        static readonly Color ColCodeText = new(0.65f, 0.85f, 0.45f);

        [MenuItem("Tools/Wagenheimer/Cloud Save/Integration Guide", priority = 0)]
        static void Open()
        {
            var w = GetWindow<IntegrationGuideWindow>("Cloud Save \u2014 Integration Guide");
            w.minSize = new Vector2(480, 400);
        }

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
            EditorGUILayout.LabelField("\u2601\ufe0f  Cloud Save Setup Guide", bannerStyle, GUILayout.ExpandWidth(true));
            var subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.85f, 0.90f, 1f) },
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Complete checklist to activate cloud saves in your project", subStyle);
            GUILayout.Space(6);

            EditorGUI.DrawRect(new Rect(0, 54, position.width, position.height - 54), ColBg);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawCard(1, "Install Package", "Window \u2192 Package Manager \u2192 + \u2192 Add package from git URL",
                code: "https://github.com/wagenheimer/UnityCloudSave.git",
                link: "Open Dashboard", url: "https://dashboard.unity3d.com");

            DrawCard(2, "Enable Cloud Save", "Go to dashboard.unity3d.com \u2192 your project \u2192 Cloud Save \u2192 Enable");

            DrawCard(3, "Link Unity Services", "Edit \u2192 Project Settings \u2192 Services \u2192 Log in & link project");

            DrawCard(4, "Startup Code",
                "Add to GameManager.Awake() or Start():",
                code: "CloudSync.Configure(\"meu_jogo\");\nCloudSaveUI.Create();\nSyncStatusUI.Create();\n_ = CloudSync.InitAndSyncAsync(\n    saveData.LastSaved,\n    AplicarCloudSave);");

            DrawCard(5, "Sync Callback",
                "Called automatically when cloud save is newer:",
                code: "private void AplicarCloudSave(byte[] cloudBytes)\n{\n    var json = Encoding.UTF8.GetString(cloudBytes);\n    saveData = JsonUtility.FromJson<MeuSaveData>(json);\n}");

            DrawCard(6, "Save to Cloud",
                "Call after every local save:",
                code: "saveData.LastSaved = DateTime.UtcNow.Ticks;\nvar json = JsonUtility.ToJson(saveData);\nvar bytes = Encoding.UTF8.GetBytes(json);\nFile.WriteAllBytes(localPath, bytes);\n_ = CloudSync.SaveAsync(bytes, saveData.LastSaved);");

            DrawCard(7, "SaveData Class",
                "Your data class MUST include a timestamp:",
                code: "[System.Serializable]\npublic class MeuSaveData\n{\n    public long LastSaved;  // REQUIRED\n    public int Moedas;\n    public int Fase;\n}");

            DrawCard(8, "Dashboard: Configure Sign-In Methods",
                "PR\u00c9-REQUISITO para auth upgrade (Android/iOS).\n\nDashboard \u2192 Authentication \u2192 Sign-In Methods:\n  \u2022 Anonymous: enabled by default \u2713\n  \u2022 Google Play Games: paste Web client ID from Google Play Console\n  \u2022 Apple Game Center: just enable (no extra fields)\n  \u2022 Apple (Sign in with Apple): fill Service ID + Redirect URL\n\nWithout this, LinkGooglePlayGamesAsync / LinkApple* will fail.",
                code: "Dashboard URL (click to open):\nhttps://dashboard.unity3d.com/",
                link: "\ud83d\udd17 Open Dashboard \u2192 Authentication", url: "https://dashboard.unity3d.com/");

            DrawCard(9, "Auth \u2014 Facebook (optional)",
                "PR\u00c9-REQUISITOS:\n  1. Meta for Developers: create App, get App ID and App Secret\n  2. Dashboard \u2192 Auth \u2192 Sign-In Methods: enable Facebook + paste App ID & Secret\n  3. Install Facebook SDK for Unity (Facebook.Unity)\n\nCODE:\nFB.LogInWithReadPermissions(perms, result => {\n    var token = AccessToken.CurrentAccessToken.TokenString;\n    _ = CloudAuth.LinkFacebookAsync(token);\n});",
                code: "// 1 \u2014 Login Facebook\nFB.LogInWithReadPermissions(new List<string> { \"public_profile\", \"email\" }, result =>\n{\n    if (FB.IsLoggedIn)\n    {\n        // 2 \u2014 Vincular ao UGS\n        _ = CloudAuth.LinkFacebookAsync(AccessToken.CurrentAccessToken.TokenString);\n    }\n});");

            DrawCard(10, "Auth \u2014 Android (optional)",
                "PR\u00c9-REQUISITOS:\n  1. Google Play Console: enable Play Games Services + get OAuth 2.0 Web client ID\n  2. Dashboard \u2192 Auth \u2192 Sign-In Methods: activate GPGS + paste client ID\n  3. Install GPGS plugin: com.google.play.games\n\nCODE (3 steps):\nSTEP 1: PlayGamesPlatform.Authenticate\nSTEP 2: RequestServerSideAccess(false, code =>)\nSTEP 3: CloudAuth.LinkGooglePlayGamesAsync(code)",
                code: "// 1 \u2014 Autenticar no Google Play Games\nPlayGamesPlatform.Instance.Authenticate(status =>\n{\n    if (status != SignInStatus.Success) return;\n\n    // 2 \u2014 Solicitar server auth code\n    PlayGamesPlatform.Instance.RequestServerSideAccess(\n        false, serverAuthCode =>\n    {\n        // 3 \u2014 Vincular ao UGS\n        _ = CloudAuth.LinkGooglePlayGamesAsync(serverAuthCode);\n    });\n});");

            DrawCard(11, "Auth \u2014 iOS (optional)",
                "PR\u00c9-REQUISITOS:\n  1. Apple Developer: enable Game Center or Sign in with Apple on App ID\n  2. Dashboard \u2192 Auth \u2192 Sign-In Methods: enable Apple / Game Center\n  3. Install Apple.GameKit / AppleSignInUnity\n\nCODE:\n_ = CloudAuth.LinkAppleAsync(identityToken);\n// ou via Game Center signature:\n_ = CloudAuth.LinkAppleGameCenterAsync(...);");

            DrawCard(12, "Test Without UGS",
                "Tools \u2192 Wagenheimer \u2192 Cloud Save \u2192 Open Test Window\nSimulate sync, toasts, conflicts and events \u2014 no internet needed.");

            DrawCard(13, "Audit Integration",
                "Run a full project scan: Tools \u2192 Wagenheimer \u2192 Cloud Save \u2192 Audit Integration\nValidates package, code, UIs, auth calls, platform plugins, AND store compliance.");

            DrawCard(14, "Store Compliance: Account Deletion (MANDATORY)",
                "Apple Guideline 5.1.1(v) & Google Play MANDATE an in-app Account Deletion flow if you offer logins.\n\n" +
                "1. Provide an in-game button calling CloudAuth.DeleteAccountAsync()\n" +
                "2. Google Play: register your public Account Deletion web URL in Play Console Data Safety form\n" +
                "3. Meta: register your Data Deletion URL in Facebook Developer App Settings",
                code: "// In your Account / Settings dialog:\npublic async void OnClickDeleteAccount()\n{\n    bool success = await CloudAuth.DeleteAccountAsync();\n    if (success)\n    {\n        ClearLocalGameData();\n        ReturnToTitleScreen();\n    }\n}");

            DrawCard(15, "Reset Progress vs. Delete Account",
                "Reset Progress wipes gameplay data (levels, coins) while KEEPING the account linked.\n" +
                "Delete Account destroys the cloud account and identity permanently.",
                code: "// Reset Save Game (keeps login, updates cloud):\nawait CloudSync.ResetProgressAsync(\n    onClearLocalSave: () => MySave.ResetProgress(),\n    getCleanSaveBytes: () => MySave.ToBytes());");

            DrawCard(16, "Legacy Migration (PlayFab / Firebase)",
                "Migrate players from legacy cloud saves to UGS seamlessly with 1 line of code:",
                code: "await CloudMigration.TryMigrateAsync(\n    fetchLegacySaveAsync: async () => await FetchPlayFabSaveAsync(),\n    onApplyLegacyLocally: bytes => ApplySave(bytes));");

            DrawCard(17, "AI & Unity CLI Automation (Verify via AI)",
                "You or your AI Agent can run a 100% automated project audit via Unity CLI or Batchmode:\n\n" +
                "1. If Unity Editor is running:\n" +
                "   unity command menu --path \"Tools/Wagenheimer/Cloud Save/Run Audit (CLI Report)\"\n\n" +
                "2. The audit runs all 19 checks and saves the report to:\n" +
                "   Library/CloudSaveAuditReport.txt (and .md)\n\n" +
                "Pass the command or prompt below to Antigravity, Claude, ChatGPT, Cursor, Copilot, etc.",
                code: "unity command menu --path \"Tools/Wagenheimer/Cloud Save/Run Audit (CLI Report)\"");

            GUILayout.Space(10);

            using (new GUILayout.HorizontalScope())
            {
                if (Btn("\u26a1 Copy Unity CLI Command", ColAccent))
                {
                    EditorGUIUtility.systemCopyBuffer = "unity command menu --path \"Tools/Wagenheimer/Cloud Save/Run Audit (CLI Report)\"";
                    Debug.Log("[CloudSave] Unity CLI command copied.");
                }
                if (Btn("\ud83d\udcbb Copy Batchmode Command", ColAccent))
                {
                    string pDir = System.IO.Path.GetFullPath(".");
                    EditorGUIUtility.systemCopyBuffer = $"& \"$env:ProgramFiles\\Unity\\Hub\\Editor\\6000.5.8f1\\Editor\\Unity.exe\" -batchmode -quit -projectPath \"{pDir}\" -executeMethod Wagenheimer.CloudSave.Editor.CloudSaveAudit.RunAuditFromCli -logFile -";
                    Debug.Log("[CloudSave] Batchmode command copied.");
                }
                if (Btn("\ud83e\udd16 Copy AI Prompt", ColAccent))
                {
                    EditorGUIUtility.systemCopyBuffer =
                        "Please run the Unity Cloud Save audit:\n" +
                        "1. Execute: unity command menu --path \"Tools/Wagenheimer/Cloud Save/Run Audit (CLI Report)\"\n" +
                        "2. Read: Library/CloudSaveAuditReport.txt\n" +
                        "3. Review any failed checks or store compliance requirements and propose fixes.";
                    Debug.Log("[CloudSave] AI prompt copied.");
                }
            }

            GUILayout.Space(6);

            using (new GUILayout.HorizontalScope())
            {
                if (Btn(" Full Guide (docs/INTEGRATION.md)", ColAccent))
                    Application.OpenURL("https://github.com/wagenheimer/UnityCloudSave/blob/main/docs/INTEGRATION.md");
                if (Btn(" Open Audit Window", ColAccent))
                    EditorApplication.ExecuteMenuItem("Tools/Wagenheimer/Cloud Save/Audit Integration");
                if (Btn(" Open Test Window", ColAccent))
                    EditorApplication.ExecuteMenuItem("Tools/Wagenheimer/Cloud Save/Open Test Window");
            }
            GUILayout.Space(6);
            EditorGUILayout.EndScrollView();
        }

        void DrawCard(int step, string title, string description, string code = null,
            string link = null, string url = null)
        {
            var r = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), ColCard);
            EditorGUI.DrawRect(new Rect(r.x - 2, r.y - 2, 3, r.height + 4), ColAccent);
            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            var badgeR = GUILayoutUtility.GetRect(22, 22, GUILayout.Width(22), GUILayout.Height(22));
            EditorGUI.DrawRect(badgeR, ColAccent);
            var badgeStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            EditorGUI.LabelField(badgeR, step.ToString(), badgeStyle);
            GUILayout.Space(8);

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 13, normal = { textColor = ColAccent } });
            if (!string.IsNullOrEmpty(description))
                EditorGUILayout.LabelField(description, new GUIStyle(EditorStyles.wordWrappedMiniLabel)
                { normal = { textColor = ColDim } });
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (!string.IsNullOrEmpty(code))
            {
                EditorGUI.DrawRect(EditorGUILayout.BeginVertical(), ColCode);
                GUILayout.Space(4);
                var codeStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    normal = { textColor = ColCodeText },
                    wordWrap = true
                };
                EditorGUILayout.LabelField(code, codeStyle);
                GUILayout.Space(2);
                if (Btn(" Copy Code", ColAccent))
                {
                    EditorGUIUtility.systemCopyBuffer = code;
                    Debug.Log("[CloudSave] Code copied.");
                }
                EditorGUILayout.EndVertical();
            }

            if (!string.IsNullOrEmpty(link) && !string.IsNullOrEmpty(url))
            {
                if (Btn(link, ColAccent))
                    Application.OpenURL(url);
            }

            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        static bool Btn(string label, Color color)
        {
            var orig = GUI.backgroundColor;
            GUI.backgroundColor = color;
            var clicked = GUILayout.Button(label, GUILayout.Height(22));
            GUI.backgroundColor = orig;
            return clicked;
        }
    }
}
