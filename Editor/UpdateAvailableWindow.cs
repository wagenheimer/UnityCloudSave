using UnityEditor;
using UnityEngine;

namespace Wagenheimer.CloudSave.Editor
{
    internal class UpdateAvailableWindow : EditorWindow
    {
        string _packageDisplayName;
        string _currentVersion;
        string _latestVersion;
        string _repoUrl;
        string _skipPrefKey;

        public static void Show(string packageDisplayName, string currentVersion, string latestVersion,
            string repoUrl, string skipPrefKey)
        {
            var window = CreateInstance<UpdateAvailableWindow>();
            window.titleContent = new GUIContent($"{packageDisplayName} — Update Available");
            window._packageDisplayName = packageDisplayName;
            window._currentVersion = currentVersion;
            window._latestVersion = latestVersion;
            window._repoUrl = repoUrl;
            window._skipPrefKey = skipPrefKey;

            var size = new Vector2(380, 180);
            window.minSize = size;
            window.maxSize = size;
            window.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField($"Nova versão do {_packageDisplayName} disponível!", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Instalada: {_currentVersion}");
            EditorGUILayout.LabelField($"Disponível: {_latestVersion}");
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Pacote referenciado via Git URL — o Package Manager não detecta updates automaticamente. " +
                "Atualize manualmente em Window > Package Manager (botão Update) ou remova a entrada em " +
                "Library/PackageCache e reabra o projeto.",
                MessageType.Info);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ver no GitHub"))
                    Application.OpenURL($"{_repoUrl}/releases/latest");

                if (GUILayout.Button("Lembrar depois"))
                    Close();

                if (GUILayout.Button("Ignorar esta versão"))
                {
                    EditorPrefs.SetString(_skipPrefKey, _latestVersion);
                    Close();
                }
            }
        }
    }
}
