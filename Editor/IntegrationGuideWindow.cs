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

            DrawCard(8, "Auth \u2014 Android (optional)",
                "After GPGS sign-in, link anonymous account:",
                code: "PlayGamesPlatform.Instance.RequestServerSideAccess(\n    false, code =>\n{\n    _ = CloudAuth.LinkGooglePlayGamesAsync(code);\n});");

            DrawCard(9, "Auth \u2014 iOS (optional)",
                "Via Apple.GameKit or Sign in with Apple.\nSee docs/INTEGRATION.md for details.");

            DrawCard(10, "Test Without UGS",
                "Tools \u2192 Wagenheimer \u2192 Cloud Save \u2192 Open Test Window\nSimulate sync, toasts, conflicts and events \u2014 no internet needed.");

            DrawCard(11, "Audit Integration",
                "Run a full project scan: Tools \u2192 Wagenheimer \u2192 Cloud Save \u2192 Audit Integration");

            GUILayout.Space(10);

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
