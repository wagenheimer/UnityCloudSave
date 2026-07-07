using UnityEditor;
using UnityEngine;

namespace Wagenheimer.CloudSave.Editor
{
    internal class IntegrationGuideWindow : EditorWindow
    {
        Vector2 _scroll;

        [MenuItem("Tools/Wagenheimer/Cloud Save/Integration Guide", priority = 0)]
        static void Open() => GetWindow<IntegrationGuideWindow>("Cloud Save - Integration Guide");

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawSection("1. Package instalado",
                "Window \u2192 Package Manager \u2192 + \u2192 Add package from git URL...\n" +
                "https://github.com/wagenheimer/UnityCloudSave.git\n\n" +
                "Depend\u00eancias resolvem automaticamente: core, authentication, cloudSave, TextMeshPro.");

            DrawSection("2. Dashboard",
                "1. Acessar dashboard.unity3d.com\n" +
                "2. Selecionar o projeto\n" +
                "3. Cloud Save \u2192 Enable");

            DrawSection("3. Project Settings (Services)",
                "1. Edit \u2192 Project Settings \u2192 Services\n" +
                "2. Fazer login e vincular o projeto");

            DrawSection("4. C\u00f3digo — Startup",
                "No Awake/Start do GameManager:\n\n" +
                "CloudSync.Configure(\"meu_jogo\");                    // define chave\n" +
                "CloudSaveUI.Create();                                // UI de loading/toast\n" +
                "SyncStatusUI.Create();                               // indicador de status\n" +
                "_ = CloudSync.InitAndSyncAsync(                      // inicia sync\n" +
                "    saveData.LastSaved, AplicarCloudSave);");

            DrawSection("5. C\u00f3digo — Callback",
                "private void AplicarCloudSave(byte[] cloudBytes)\n" +
                "{\n" +
                "    var json = Encoding.UTF8.GetString(cloudBytes);\n" +
                "    saveData = JsonUtility.FromJson<MeuSaveData>(json);\n" +
                "}");

            DrawSection("6. C\u00f3digo — Salvar",
                "saveData.LastSaved = DateTime.UtcNow.Ticks;\n" +
                "byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(saveData));\n" +
                "File.WriteAllBytes(localPath, bytes);\n" +
                "_ = CloudSync.SaveAsync(bytes, saveData.LastSaved);");

            DrawSection("7. SaveData",
                "[System.Serializable]\n" +
                "public class MeuSaveData\n" +
                "{\n" +
                "    public long LastSaved;   // OBRIGAT\u00d3RIO\n" +
                "    // ... seus campos\n" +
                "}");

            DrawSection("8. Auth (Android - opcional)",
                "Ap\u00f3s autenticar no GPGS:\n" +
                "PlayGamesPlatform.Instance.RequestServerSideAccess(false, code =>\n" +
                "{\n" +
                "    _ = CloudAuth.LinkGooglePlayGamesAsync(code);\n" +
                "});");

            DrawSection("9. Auth (iOS - opcional)",
                "Via Apple.GameKit:\n" +
                "await CloudAuth.LinkAppleGameCenterAsync(pubKey, sig, salt, ts, teamPlayerId);\n\n" +
                "Via Sign in with Apple:\n" +
                "await CloudAuth.LinkAppleAsync(identityToken);");

            DrawSection("10. Testar",
                "Tools \u2192 Wagenheimer \u2192 Cloud Save \u2192 Open Test Window\n" +
                "Testa UIs, toasts, conflito e eventos sem UGS.");

            DrawSection("11. Verificar integra\u00e7\u00e3o",
                "Tools \u2192 Wagenheimer \u2192 Cloud Save \u2192 Audit Integration\n" +
                "Escaneia o projeto e mostra o que j\u00e1 foi feito e o que falta.");

            EditorGUILayout.EndScrollView();
        }

        static void DrawSection(string title, string body)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(body, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(2);
        }
    }
}
