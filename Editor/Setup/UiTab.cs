using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Wagenheimer.CloudSave;

namespace Wagenheimer.CloudSave.Editor.Setup
{
    /// <summary>
    /// The Hub's "UI" tab: see, generate, preview and drive every Cloud Save UI in one place, and
    /// register a project-owned form as the replacement for a built-in one.
    /// Absorbs the standalone CloudSaveUIPrefabGenerator + the UI half of CloudSaveTester.
    /// </summary>
    public sealed class UiTab
    {
        struct PackageUi
        {
            public string Type;          // runtime type name in Wagenheimer.CloudSave
            public string Resource;      // Resources/<Resource>.prefab
            public string GenerateMenu;  // Tools menu item that (re)builds the prefab
            public UiHook Hook;
            public (string label, string method, object[] args)[] States;
        }

        static readonly PackageUi[] Uis =
        {
            new PackageUi
            {
                Type = "CloudSaveUI", Resource = "CloudSaveUI", Hook = UiHook.Conflict,
                GenerateMenu = "Tools/Wagenheimer/Cloud Save/Setup UI Prefabs/Cloud Save UI",
                States = new (string, string, object[])[]
                {
                    ("Loading ON",  "SetLoading", new object[] { true }),
                    ("Loading OFF", "SetLoading", new object[] { false }),
                    ("Toast: Synced",       "HandleSyncCompleted", new object[] { CloudSyncResult.CloudApplied }),
                    ("Toast: Local newer",  "HandleSyncCompleted", new object[] { CloudSyncResult.LocalNewer }),
                    ("Toast: Offline",      "HandleSyncCompleted", new object[] { CloudSyncResult.Offline }),
                    ("Toast: Error",        "HandleSyncCompleted", new object[] { CloudSyncResult.Error }),
                },
            },
            new PackageUi
            {
                Type = "SyncStatusUI", Resource = "SyncStatusUI", Hook = UiHook.SyncStatus,
                GenerateMenu = "Tools/Wagenheimer/Cloud Save/Setup UI Prefabs/Sync Status UI",
                States = new (string, string, object[])[]
                {
                    ("Synced",  "SetStatus", new object[] { SyncStatus.Synced }),
                    ("Syncing", "SetStatus", new object[] { SyncStatus.Syncing }),
                    ("Offline", "SetStatus", new object[] { SyncStatus.Offline }),
                    ("Error",   "SetStatus", new object[] { SyncStatus.Error }),
                },
            },
            new PackageUi
            {
                Type = "CloudAuthUI", Resource = "CloudAuthUI", Hook = UiHook.Auth,
                GenerateMenu = "Tools/Wagenheimer/Cloud Save/Setup UI Prefabs/Cloud Auth UI",
                States = new (string, string, object[])[]
                {
                    ("Show",            "Show", Array.Empty<object>()),
                    ("Hide",            "Hide", Array.Empty<object>()),
                    ("Link result: OK", "SetLinkResult", new object[] { true }),
                    ("Link result: fail", "SetLinkResult", new object[] { false }),
                },
            },
        };

        // custom-form registration form state
        GameObject _newPrefab;
        UiHook _newHook = UiHook.Conflict;
        string _newName = "";
        string _newNote = "";

        public void Draw(CloudSaveSetupState state, SetupSnapshot snapshot, Action requestRecompute)
        {
            EditorGUILayout.HelpBox(
                Application.isPlaying
                    ? "Play Mode: state buttons drive the real components (toasts animate)."
                    : "Edit Mode: spawn a UI into the open scene, style it, then Apply to prefab. Toasts don't animate until Play Mode.",
                MessageType.Info);

            foreach (var ui in Uis)
                DrawPackageUi(ui, state, snapshot, requestRecompute);

            EditorGUILayout.Space(8);
            DrawCustomRegistration(state, requestRecompute);
        }

        void DrawPackageUi(PackageUi ui, CloudSaveSetupState state, SetupSnapshot snapshot, Action requestRecompute)
        {
            var custom = state.CustomUis.FirstOrDefault(u => u.Hook == ui.Hook);
            var type = FindRuntimeType(ui.Type);
            var live = type != null ? UnityEngine.Object.FindObjectOfType(type) : null;
            var prefabPath = $"Assets/Resources/{ui.Resource}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(ui.Type, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    string status = custom != null ? $"replaced by: {custom.DisplayName}"
                        : live != null ? "in scene"
                        : prefab != null ? "prefab ready"
                        : "no prefab";
                    EditorGUILayout.LabelField(status, EditorStyles.miniLabel, GUILayout.Width(160));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(prefab != null ? "Regenerate prefab" : "Generate prefab", EditorStyles.miniButton))
                    {
                        EditorApplication.ExecuteMenuItem(ui.GenerateMenu);
                        requestRecompute();
                    }
                    using (new EditorGUI.DisabledScope(prefab == null))
                    {
                        if (GUILayout.Button("Open prefab", EditorStyles.miniButton)) AssetDatabase.OpenAsset(prefab);
                        if (GUILayout.Button("Ping", EditorStyles.miniButton)) EditorGUIUtility.PingObject(prefab);
                    }
                    if (live == null && GUILayout.Button("Spawn in scene", EditorStyles.miniButton))
                        Spawn(ui, type, prefab);
                    if (live != null && GUILayout.Button("Apply to prefab", EditorStyles.miniButton))
                        ApplyToPrefab((Component)live);
                    if (live != null && GUILayout.Button("Remove", EditorStyles.miniButton))
                    {
                        UnityEngine.Object.DestroyImmediate(((Component)live).gameObject);
                        requestRecompute();
                    }
                }

                if (live != null)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Drive:", GUILayout.Width(40));
                        foreach (var s in ui.States)
                            if (GUILayout.Button(s.label, EditorStyles.miniButton))
                                Invoke(live, s.method, s.args);
                        if (ui.Hook == UiHook.Conflict && GUILayout.Button("Conflict dialog", EditorStyles.miniButton))
                            ShowMockConflict(live);
                    }
                }
            }
        }

        void DrawCustomRegistration(CloudSaveSetupState state, Action requestRecompute)
        {
            EditorGUILayout.LabelField("Your own forms", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Register a project form as the replacement for a built-in UI. The matching step then reads " +
                "\"covered by your form\" instead of \"not configured\".", EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _newPrefab = (GameObject)EditorGUILayout.ObjectField("Form prefab", _newPrefab, typeof(GameObject), false);
                _newHook = (UiHook)EditorGUILayout.EnumPopup("Covers", _newHook);
                _newName = EditorGUILayout.TextField("Display name", _newName);
                _newNote = EditorGUILayout.TextField("Note", _newNote);
                using (new EditorGUI.DisabledScope(_newPrefab == null && string.IsNullOrEmpty(_newName)))
                {
                    if (GUILayout.Button("Register / update"))
                    {
                        var path = _newPrefab != null ? AssetDatabase.GetAssetPath(_newPrefab) : "";
                        var name = !string.IsNullOrEmpty(_newName) ? _newName
                            : _newPrefab != null ? _newPrefab.name : _newHook.ToString();
                        state.AddOrUpdateCustomUi(new CustomUiRegistration
                        {
                            Id = _newHook + ":" + name,
                            DisplayName = name, Hook = _newHook, PrefabPath = path, Note = _newNote,
                        });
                        _newPrefab = null; _newName = ""; _newNote = "";
                        requestRecompute();
                    }
                }
            }

            foreach (var reg in state.CustomUis.ToList())
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{reg.Hook}", GUILayout.Width(90));
                    EditorGUILayout.LabelField(reg.DisplayName, EditorStyles.boldLabel, GUILayout.Width(180));
                    EditorGUILayout.LabelField(reg.PrefabPath, EditorStyles.miniLabel);
                    var asset = string.IsNullOrEmpty(reg.PrefabPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(reg.PrefabPath);
                    using (new EditorGUI.DisabledScope(asset == null))
                        if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(44)))
                            EditorGUIUtility.PingObject(asset);
                    if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        state.RemoveCustomUi(reg.Id);
                        requestRecompute();
                        break;
                    }
                }
            }
        }

        // ── spawn / apply / invoke helpers ─────────────────────────────────

        static void Spawn(PackageUi ui, Type type, GameObject prefab)
        {
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                go = new GameObject(ui.Type);
                if (type != null) go.AddComponent(type);
            }
            Undo.RegisterCreatedObjectUndo(go, "Spawn " + ui.Type);
            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        static void ApplyToPrefab(Component live)
        {
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(live.gameObject);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Not a prefab instance",
                    "This UI was spawned from scratch (no prefab). Use \"Generate prefab\" first, then spawn that.", "OK");
                return;
            }
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
        }

        static void Invoke(object target, string method, object[] args)
        {
            var mi = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) { Debug.LogWarning($"[UiTab] {target.GetType().Name}.{method}() not found."); return; }
            try { mi.Invoke(target, args); }
            catch (Exception e) { Debug.LogWarning($"[UiTab] {method} threw: {e.InnerException?.Message ?? e.Message}"); }
        }

        static void ShowMockConflict(object cloudSaveUi)
        {
            var data = new CloudConflictData(
                DateTime.UtcNow.AddDays(-1).Ticks, DateTime.UtcNow.Ticks,
                new byte[] { 1, 2, 3 }, CloudConflictReason.CloudIsNewer);
            var mi = cloudSaveUi.GetType().GetMethod("ShowConflictDialogAsync",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) { Debug.LogWarning("[UiTab] ShowConflictDialogAsync not found."); return; }
            try { mi.Invoke(cloudSaveUi, new object[] { data }); }
            catch (Exception e) { Debug.LogWarning("[UiTab] conflict dialog: " + (e.InnerException?.Message ?? e.Message)); }
        }

        static Type FindRuntimeType(string shortName)
        {
            var full = "Wagenheimer.CloudSave." + shortName;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full);
                if (t != null) return t;
            }
            return null;
        }
    }
}
