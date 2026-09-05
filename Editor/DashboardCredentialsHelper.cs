using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Wagenheimer.CloudSave.Editor
{
    public class DashboardCredentialsHelper : EditorWindow
    {
        private static CredentialsData _cachedData;
        private string _serviceAccountKey = "";
        private string _apiStatus = "";

        [System.Serializable]
        public class CredentialsData
        {
            public string OrganizationId;
            public string CloudProjectId;
            public string GoogleWebClientId;
            public string GoogleAndroidClientId;
            public string FacebookAppId;
            public string DashboardUrl;
        }

        [MenuItem("Tools/Wagenheimer/Cloud Save/Dashboard Credentials Helper", priority = 10)]
        public static void Open()
        {
            var window = GetWindow<DashboardCredentialsHelper>("UGS Credentials");
            window.minSize = new Vector2(520, 480);
            _cachedData = DetectCredentials();
        }

        [MenuItem("Tools/Wagenheimer/Cloud Save/Export Credentials (CLI)", priority = 11)]
        public static void ExportCredentialsMenuItem()
        {
            ExportCredentialsCli();
        }

        /// <summary>
        /// Entry point for AI and CLI to detect project credentials and export to JSON.
        /// </summary>
        public static string ExportCredentialsCli()
        {
            var data = DetectCredentials();
            var json = JsonUtility.ToJson(data, true);

            Debug.Log("[UGS Credentials Helper]\n" + json);
            Console.WriteLine("[UGS Credentials Helper]\n" + json);

            try
            {
                var dir = Path.GetFullPath("Library");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "UgsCredentials.json"), json);
            }
            catch { }

            return json;
        }

        public static CredentialsData DetectCredentials()
        {
            var data = new CredentialsData();

            // 1. Cloud Project ID and Organization ID
            var projSettings = Path.GetFullPath("ProjectSettings/ProjectSettings.asset");
            if (File.Exists(projSettings))
            {
                var text = File.ReadAllText(projSettings);
                var match = Regex.Match(text, @"cloudProjectId:\s*([0-9a-fA-F-]+)");
                if (match.Success)
                    data.CloudProjectId = match.Groups[1].Value.Trim();

                var orgMatch = Regex.Match(text, @"organizationId:\s*([^\r\n]+)");
                if (orgMatch.Success)
                    data.OrganizationId = orgMatch.Groups[1].Value.Trim();
            }

            if (!string.IsNullOrEmpty(data.CloudProjectId))
            {
                if (!string.IsNullOrEmpty(data.OrganizationId))
                    data.DashboardUrl = $"https://cloud.unity.com/organizations/{data.OrganizationId}/projects/{data.CloudProjectId}/player-authentication/identity-providers";
                else
                    data.DashboardUrl = $"https://cloud.unity.com/projects/{data.CloudProjectId}/player-authentication/identity-providers";
            }
            else
            {
                data.DashboardUrl = "https://cloud.unity.com";
            }

            // 2. Google Web Client ID (from google-services.json)
            string[] googleJsonPaths = {
                Path.GetFullPath("google-services.json"),
                Path.GetFullPath("Assets/google-services.json"),
                Path.GetFullPath("Assets/StreamingAssets/google-services-desktop.json")
            };

            foreach (var p in googleJsonPaths)
            {
                if (File.Exists(p))
                {
                    var text = File.ReadAllText(p);

                    // Web client ID is client_type 3
                    var webMatch = Regex.Match(text, @"client_id""\s*:\s*""([^""]+)""[^}]*client_type""\s*:\s*3", RegexOptions.Singleline);
                    if (!webMatch.Success)
                    {
                        webMatch = Regex.Match(text, @"client_type""\s*:\s*3[^}]*client_id""\s*:\s*""([^""]+)""", RegexOptions.Singleline);
                    }

                    if (webMatch.Success)
                    {
                        data.GoogleWebClientId = webMatch.Groups[1].Value;
                    }

                    // Android client ID is client_type 1
                    var androidMatch = Regex.Match(text, @"client_id""\s*:\s*""([^""]+)""[^}]*client_type""\s*:\s*1", RegexOptions.Singleline);
                    if (androidMatch.Success)
                    {
                        data.GoogleAndroidClientId = androidMatch.Groups[1].Value;
                    }

                    if (!string.IsNullOrEmpty(data.GoogleWebClientId)) break;
                }
            }

            // Fallback for iOS plist
            if (string.IsNullOrEmpty(data.GoogleWebClientId))
            {
                string[] plistPaths = {
                    Path.GetFullPath("GoogleService-Info.plist"),
                    Path.GetFullPath("Assets/GoogleService-Info.plist")
                };

                foreach (var p in plistPaths)
                {
                    if (File.Exists(p))
                    {
                        var text = File.ReadAllText(p);
                        var match = Regex.Match(text, @"<string>([0-9]+-[a-zA-Z0-9_-]+\.apps\.googleusercontent\.com)</string>");
                        if (match.Success)
                        {
                            data.GoogleWebClientId = match.Groups[1].Value;
                            break;
                        }
                    }
                }
            }

            // 3. Facebook App ID
            var fbSettings = Path.GetFullPath("Assets/FacebookSDK/SDK/Resources/FacebookSettings.asset");
            if (File.Exists(fbSettings))
            {
                var text = File.ReadAllText(fbSettings);
                var match = Regex.Match(text, @"appIds:\s*\n\s*-\s*([0-9]+)");
                if (match.Success)
                    data.FacebookAppId = match.Groups[1].Value.Trim();
            }

            return data;
        }

        private void OnEnable()
        {
            _cachedData = DetectCredentials();
        }

        private void OnGUI()
        {
            if (_cachedData == null) _cachedData = DetectCredentials();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("UGS Identity Providers Auto-Config", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Detected credentials from project files to configure Unity Dashboard.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);

            // Google Card
            DrawSection("Google / Google Play Games (Web Client ID)", () =>
            {
                if (!string.IsNullOrEmpty(_cachedData.GoogleWebClientId))
                {
                    EditorGUILayout.SelectableLabel(_cachedData.GoogleWebClientId, EditorStyles.textField, GUILayout.Height(20));
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Copy Google Web Client ID", GUILayout.Height(24)))
                    {
                        EditorGUIUtility.systemCopyBuffer = _cachedData.GoogleWebClientId;
                        Debug.Log("[UGS] Copied Google Web Client ID to clipboard.");
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.HelpBox("google-services.json not found or does not contain a Web Client ID (client_type 3).", MessageType.Warning);
                }
            });

            EditorGUILayout.Space(8);

            // Facebook Card
            DrawSection("Facebook (App ID)", () =>
            {
                if (!string.IsNullOrEmpty(_cachedData.FacebookAppId))
                {
                    EditorGUILayout.SelectableLabel(_cachedData.FacebookAppId, EditorStyles.textField, GUILayout.Height(20));
                    if (GUILayout.Button("Copy Facebook App ID", GUILayout.Height(24)))
                    {
                        EditorGUIUtility.systemCopyBuffer = _cachedData.FacebookAppId;
                        Debug.Log("[UGS] Copied Facebook App ID to clipboard.");
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("FacebookSettings.asset not found in Assets/FacebookSDK/SDK/Resources.", MessageType.Info);
                }
            });

            EditorGUILayout.Space(8);

            // Dashboard Direct Link Card
            DrawSection("Unity Dashboard Direct Link", () =>
            {
                EditorGUILayout.LabelField($"Project ID: {_cachedData.CloudProjectId ?? "Unlinked"}");
                if (GUILayout.Button("Open Sign-In Methods in Dashboard", GUILayout.Height(28)))
                {
                    Application.OpenURL(_cachedData.DashboardUrl);
                }
            });

            EditorGUILayout.Space(8);

            // Automated API Section
            DrawSection("100% Automated Setup via Unity Management API", () =>
            {
                EditorGUILayout.LabelField("To let AI/Editor configure UGS without opening the browser:", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("1. Dashboard -> Project Settings -> Service Accounts -> Create Service Account (Admin role)");
                EditorGUILayout.LabelField("2. Paste Key ID:Secret Key below:");

                _serviceAccountKey = EditorGUILayout.PasswordField("Service Account (Key:Secret)", _serviceAccountKey);

                GUI.enabled = !string.IsNullOrEmpty(_serviceAccountKey) && !string.IsNullOrEmpty(_cachedData.CloudProjectId);
                if (GUILayout.Button("Configure Google & Facebook in UGS Now (via API)", GUILayout.Height(28)))
                {
                    ConfigureViaApi(_serviceAccountKey, _cachedData);
                }
                GUI.enabled = true;

                if (!string.IsNullOrEmpty(_apiStatus))
                {
                    EditorGUILayout.HelpBox(_apiStatus, MessageType.Info);
                }
            });
        }

        private void DrawSection(string title, Action drawContent)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
            drawContent?.Invoke();
            EditorGUILayout.EndVertical();
        }

        private void ConfigureViaApi(string authKey, CredentialsData data)
        {
            _apiStatus = "Sending configuration to Unity Services API...";

            // Basic Auth header: Base64(KeyId:SecretKey)
            string encodedAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(authKey.Trim()));

            // 1. Configure Google Play Games
            if (!string.IsNullOrEmpty(data.GoogleWebClientId))
            {
                string gpgUrl = $"https://services.unity.com/api/auth/v1/projects/{data.CloudProjectId}/idps/google-play-games";
                string jsonBody = $"{{\"clientId\":\"{data.GoogleWebClientId}\",\"disabled\":false}}";

                var req = new UnityWebRequest(gpgUrl, "PUT");
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonBody));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("Authorization", "Basic " + encodedAuth);

                var op = req.SendWebRequest();
                op.completed += _ =>
                {
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        _apiStatus = "Successfully configured Google Play Games in UGS!";
                        Debug.Log("[UGS Auto-Config] " + _apiStatus);
                    }
                    else
                    {
                        _apiStatus = $"Error configuring Google: {req.error}\n{req.downloadHandler?.text}";
                        Debug.LogWarning("[UGS Auto-Config] " + _apiStatus);
                    }
                    req.Dispose();
                    Repaint();
                };
            }
        }
    }
}
