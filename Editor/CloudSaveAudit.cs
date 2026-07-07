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

        [MenuItem("Tools/Wagenheimer/Cloud Save/Audit Integration", priority = 2)]
        static void Open() => GetWindow<CloudSaveAudit>("Cloud Save Audit");

        void OnEnable() => _results = new List<AuditItem>();

        void OnGUI()
        {
            GUILayout.Label("CloudSave Integration Audit", EditorStyles.boldLabel);
            GUILayout.Space(4);

            if (GUILayout.Button("Run Audit Now", GUILayout.Height(30)))
                RunAudit();

            EditorGUILayout.Space(4);

            if (_ranAudit)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                foreach (var item in _results)
                {
                    DrawAuditItem(item);
                    EditorGUILayout.Space(2);
                }

                var passed = _results.Count(r => r.Status == AuditStatus.Passed);
                var total = _results.Count;
                EditorGUILayout.Space(8);

                if (passed == total)
                    EditorGUILayout.HelpBox("Tudo configurado! O CloudSave est\u00e1 pronto.", MessageType.Info);
                else
                    EditorGUILayout.HelpBox(
                        "Itens pendentes precisam de a\u00e7\u00e3o. Veja o Integration Guide (Tools \u2192 Wagenheimer \u2192 Cloud Save \u2192 Integration Guide).",
                        MessageType.Warning);

                EditorGUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy AI Prompt"))
            {
                EditorGUIUtility.systemCopyBuffer = GetAiPrompt();
                Debug.Log("[CloudSave] AI audit prompt copied to clipboard.");
            }
            if (GUILayout.Button("Integration Guide"))
                Application.OpenURL("https://github.com/wagenheimer/UnityCloudSave/blob/main/docs/INTEGRATION.md");
            EditorGUILayout.EndHorizontal();
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
            _results.Add(CheckCloudSaveUICreated());
            _results.Add(CheckSyncStatusUICreated());
            _results.Add(CheckCloudAuthUICreated());
            _results.Add(CheckAuthUpgrade());
            _results.Add(CheckProjectSettings());
        }

        static void DrawAuditItem(AuditItem item)
        {
            var color = item.Status == AuditStatus.Passed ? Color.green : Color.red;
            var icon = item.Status == AuditStatus.Passed ? "\u2713" : "\u2717";
            var bg = EditorGUIUtility.isProSkin
                ? new Color(0.15f, 0.15f, 0.15f, 0.5f)
                : new Color(0.85f, 0.85f, 0.85f, 0.5f);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            var labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = color;
            GUILayout.Label($" {icon}  {item.Label}", labelStyle);

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(item.Detail))
            {
                var detailStyle = new GUIStyle(EditorStyles.miniLabel);
                detailStyle.wordWrap = true;
                GUILayout.Label(item.Detail, detailStyle);
            }

            EditorGUILayout.EndVertical();
        }

        // ── Individual checks ─────────────────────────────────────────────

        static AuditItem CheckPackageInstalled()
        {
            var manifestPath = Path.GetFullPath("Packages/manifest.json");
            if (File.Exists(manifestPath))
            {
                var text = File.ReadAllText(manifestPath);
                if (text.Contains("com.wagenheimer.cloudsave"))
                    return Pass("Package instalado via manifest.json");
            }

            var packagePath = Path.GetFullPath("Packages/com.wagenheimer.cloudsave");
            if (Directory.Exists(packagePath))
                return Pass("Package embarcado em Packages/");

            var anyCs = FindInCsFiles("Wagenheimer.CloudSave");
            if (anyCs.Count > 0)
                return Pass("Namespace Wagenheimer.CloudSave encontrado");

            return Fail("Package n\u00e3o encontrado. Instale via Package Manager.");
        }

        static AuditItem CheckConfigure()
        {
            var matches = FindInCsFiles("CloudSync\\.Configure");
            if (matches.Count > 0)
                return Pass("CloudSync.Configure chamado no projeto");

            return Fail("CloudSync.Configure nunca chamado. Chamar no startup.");
        }

        static AuditItem CheckInitAndSync()
        {
            var matches = FindInCsFiles("CloudSync\\.InitAndSyncAsync");
            if (matches.Count > 0)
                return Pass(matches.Count + " refer\u00eancia(s) encontrada(s)");

            return Fail("CloudSync.InitAndSyncAsync nunca chamado. Chamar no startup ap\u00f3s Configure().");
        }

        static AuditItem CheckSaveAsync()
        {
            var matches = FindInCsFiles("CloudSync\\.SaveAsync");
            if (matches.Count > 0)
                return Pass(matches.Count + " refer\u00eancia(s) encontrada(s)");

            return Fail("CloudSync.SaveAsync nunca chamado. Chamar ap\u00f3s cada save local.");
        }

        static AuditItem CheckLastSavedField()
        {
            var matches = FindInCsFiles("long\\s+LastSaved");
            if (matches.Count > 0)
                return Pass("Campo 'long LastSaved' encontrado");

            var legacy = FindInCsFiles("LastSaved");
            if (legacy.Count > 0)
                return Pass("'LastSaved' referenciado (verificar se \u00e9 long)");

            return Fail("Nenhum campo 'long LastSaved' encontrado.");
        }

        static AuditItem CheckCloudSaveUICreated()
        {
            var matches = FindInCsFiles("CloudSaveUI\\.Create\\s*\\(\\s*\\)");
            if (matches.Count > 0)
                return Pass("CloudSaveUI.Create() chamado");

            return Info("CloudSaveUI.Create() n\u00e3o encontrado (opcional, mas recomendado)");
        }

        static AuditItem CheckSyncStatusUICreated()
        {
            var matches = FindInCsFiles("SyncStatusUI\\.Create\\s*\\(\\s*\\)");
            if (matches.Count > 0)
                return Pass("SyncStatusUI.Create() chamado");

            return Info("SyncStatusUI.Create() n\u00e3o encontrado (opcional, mas recomendado)");
        }

        static AuditItem CheckCloudAuthUICreated()
        {
            var matches = FindInCsFiles("CloudAuthUI\\.Create\\s*\\(\\s*\\)");
            if (matches.Count > 0)
                return Pass("CloudAuthUI.Create() chamado");

            return Info("CloudAuthUI.Create() n\u00e3o encontrado (opcional)");
        }

        static AuditItem CheckAuthUpgrade()
        {
            var matches = FindInCsFiles("LinkGooglePlayGamesAsync|LinkAppleGameCenterAsync|LinkAppleAsync");
            if (matches.Count > 0)
                return Pass("Auth upgrade configurado");

            return Info("Auth upgrade n\u00e3o encontrado (opcional — fallback anonymous)");
        }

        static AuditItem CheckProjectSettings()
        {
            var path = Path.GetFullPath("ProjectSettings/ProjectSettings.asset");
            if (!File.Exists(path))
                return Fail("ProjectSettings.asset n\u00e3o encontrado");

            var text = File.ReadAllText(path);
            if (text.Contains("CloudSave") || text.Contains("Unity Gaming Services"))
                return Pass("Unity Services parece configurado");

            return Info("N\u00e3o foi poss\u00edvel confirmar Services no ProjectSettings (verificar manualmente: Edit \u2192 Project Settings \u2192 Services)");
        }

        // ── Helpers ────────────────────────────────────────────────────────

        static List<string> FindInCsFiles(string pattern)
        {
            var results = new List<string>();
            var assetsDir = new DirectoryInfo(Path.GetFullPath("Assets"));
            if (!assetsDir.Exists) return results;

            var csFiles = assetsDir.GetFiles("*.cs", SearchOption.AllDirectories);
            var regex = new Regex(pattern, RegexOptions.Compiled);

            foreach (var file in csFiles)
            {
                try
                {
                    var text = File.ReadAllText(file.FullName);
                    if (regex.IsMatch(text))
                    {
                        var match = regex.Match(text);
                        results.Add(file.Name + ": " + (match.Groups.Count > 1 ? match.Groups[1].Value : ""));
                    }
                }
                catch { }
            }

            return results;
        }

        static AuditItem Pass(string detail) => new(AuditStatus.Passed, detail);
        static AuditItem Fail(string detail) => new(AuditStatus.Failed, detail);
        static AuditItem Info(string detail) => new(AuditStatus.Info, detail);

        // ── AI Prompt ──────────────────────────────────────────────────────

        static string GetAiPrompt()
        {
            return @"Voc\u00ea \u00e9 um auditor de integra\u00e7\u00e3o Unity Cloud Save.
Analise os arquivos do projeto e responda APENAS com uma tabela
marcando ✅ ou ❌ para cada item abaixo.
N\u00e3o explique nada al\u00e9m da tabela.

## Itens a verificar

1. Package instalado:
   - Verificar se ""com.wagenheimer.cloudsave"" aparece em
     Packages/manifest.json ou se a pasta Packages/com.wagenheimer.cloudsave existe.
   - Procurar refer\u00eancia ao namespace ""Wagenheimer.CloudSave"" em
     qualquer arquivo .cs.

2. CloudSync.Configure:
   - Procurar chamadas a ""CloudSync.Configure"" em arquivos .cs.

3. CloudSync.InitAndSyncAsync:
   - Procurar chamadas a ""CloudSync.InitAndSyncAsync"" em .cs.

4. CloudSync.SaveAsync:
   - Procurar chamadas a ""CloudSync.SaveAsync"" em .cs.

5. Campo LastSaved:
   - Procurar ""long LastSaved"" em classes serializ\u00e1veis em .cs.

6. CloudSaveUI.Create:
   - Procurar ""CloudSaveUI.Create"" em .cs.

7. SyncStatusUI.Create:
   - Procurar ""SyncStatusUI.Create"" em .cs.

8. CloudAuthUI.Create:
   - Procurar ""CloudAuthUI.Create"" em .cs.

9. Auth upgrade:
   - Procurar ""LinkGooglePlayGamesAsync"", ""LinkAppleGameCenterAsync"",
     ""LinkAppleAsync"" em .cs.

10. Projeto dashboard:
    - Procurar ProjectSettings.asset e verificar Cloud Save/Unity Services.
    - Se n\u00e3o conseguir confirmar, marcar como ""⚠️ (manual)"".

## Formato de sa\u00edda

| # | Item | Status | Detalhes |
|---|------|--------|----------|
| 1 | Package instalado | ✅ ou ❌ | (info extra) |
| ... | ... | ... | ... |";
        }
    }

    enum AuditStatus { Passed, Failed, Info }

    class AuditItem
    {
        public AuditStatus Status { get; }
        public string Label => Status switch
        {
            AuditStatus.Passed => "Pass",
            AuditStatus.Failed => "Fail",
            _ => "Info"
        };
        public string Detail { get; }

        public AuditItem(AuditStatus status, string detail)
        {
            Status = status;
            Detail = detail;
        }
    }
}
