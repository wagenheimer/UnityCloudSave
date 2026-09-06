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
            public string Type;
            public string Resource;
            public string GenerateMenu;
            public UiHook Hook;
            public string Emoji;
            public Color Accent;
            public string Description;       // rich text
            public (string label, string method, object[] args)[] States;
        }

        static readonly Color CBlue = new(0.36f, 0.62f, 1.00f);
        static readonly Color CGreen = new(0.30f, 0.78f, 0.45f);
        static readonly Color CPurple = new(0.66f, 0.51f, 0.95f);
        static readonly Color CDim = new(0.60f, 0.60f, 0.65f);

        static readonly PackageUi[] Uis =
        {
            new PackageUi
            {
                Type = "CloudSaveUI", Resource = "CloudSaveUI", Hook = UiHook.Conflict,
                GenerateMenu = "Tools/Wagenheimer/Cloud Save/Setup UI Prefabs/Cloud Save UI",
                Emoji = "🗂", Accent = CBlue,
                Description =
                    "<b>The all-in-one player-facing UI.</b>  A full-screen <color=#5C9EFF>loading overlay</color> " +
                    "during sync, a <color=#5C9EFF>toast</color> for each sync result, and the " +
                    "<color=#5C9EFF>conflict dialog</color> (local vs cloud, 30 s auto-pick). It auto-installs " +
                    "as <i>CloudSync.ConflictResolver</i>.\n" +
                    "<color=#9A9AA0>Use it unless you already have your own loading / conflict dialogs.</color>",
                States = new (string, string, object[])[]
                {
                    ("Loading ON",  "SetLoading", new object[] { true }),
                    ("Loading OFF", "SetLoading", new object[] { false }),
                    ("Toast: Synced",      "HandleSyncCompleted", new object[] { CloudSyncResult.CloudApplied }),
                    ("Toast: Local newer", "HandleSyncCompleted", new object[] { CloudSyncResult.LocalNewer }),
                    ("Toast: Offline",     "HandleSyncCompleted", new object[] { CloudSyncResult.Offline }),
                    ("Toast: Error",       "HandleSyncCompleted", new object[] { CloudSyncResult.Error }),
                },
            },
            new PackageUi
            {
                Type = "SyncStatusUI", Resource = "SyncStatusUI", Hook = UiHook.SyncStatus,
                GenerateMenu = "Tools/Wagenheimer/Cloud Save/Setup UI Prefabs/Sync Status UI",
                Emoji = "📶", Accent = CGreen,
                Description =
                    "<b>A small corner badge.</b>  Shows <color=#4CC773>Synced / Syncing / Offline / Error</color> " +
                    "and the last-sync time. Passive, always on screen, no interaction.\n" +
                    "<color=#9A9AA0>Nice-to-have polish — skip it if your HUD already surfaces sync state.</color>",
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
                Emoji = "🔑", Accent = CPurple,
                Description =
                    "<b>A modal card that asks the player to link an account</b> (Facebook / Google / Apple) " +
                    "for <color=#A882F2>cross-device saves</color>. You handle its <i>OnLinkRequested</i> event.\n" +
                    "<color=#9A9AA0>Only needed if you prompt for account linking through a dedicated dialog.</color>",
                States = new (string, string, object[])[]
                {
                    ("Show",              "Show", Array.Empty<object>()),
                    ("Hide",              "Hide", Array.Empty<object>()),
                    ("Link result: OK",   "SetLinkResult", new object[] { true }),
                    ("Link result: fail", "SetLinkResult", new object[] { false }),
                },
            },
        };

        GameObject _newPrefab;
        UiHook _newHook = UiHook.Conflict;
        string _newName = "";
        string _newNote = "";

        GUIStyle _rich;
        GUIStyle Rich => _rich ??= new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true, fontSize = 11 };

        public void Draw(CloudSaveSetupState state, SetupSnapshot snapshot, Action requestRecompute)
        {
            EditorGUILayout.LabelField("Cloud Save UIs", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                Application.isPlaying
                    ? "Play Mode — the Drive buttons run the real components (toasts animate)."
                    : "Edit Mode — spawn a UI into the open scene to style it, then Apply to prefab. Enter Play Mode to see toasts animate.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            foreach (var ui in Uis)
                DrawPackageUi(ui, state, requestRecompute);

            EditorGUILayout.Space(10);
            DrawCustomRegistration(state, requestRecompute);
        }

        void DrawPackageUi(PackageUi ui, CloudSaveSetupState state, Action requestRecompute)
        {
            var custom = state.CustomUis.FirstOrDefault(u => u.Hook == ui.Hook);
            var type = FindRuntimeType(ui.Type);
            var live = type != null ? (Component)UnityEngine.Object.FindObjectOfType(type) : null;
            var prefabPath = $"Assets/Resources/{ui.Resource}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), ui.Accent);
            GUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{ui.Emoji}  {ui.Type}",
                    new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, normal = { textColor = ui.Accent } });
                GUILayout.FlexibleSpace();
                string status = custom != null ? $"↳ your form: {custom.DisplayName}"
                    : live != null ? "● in scene"
                    : prefab != null ? "prefab ready"
                    : "no prefab yet";
                EditorGUILayout.LabelField(status,
                    new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight, normal = { textColor = custom != null ? CDim : ui.Accent } },
                    GUILayout.Width(180));
            }

            EditorGUILayout.LabelField(ui.Description, Rich);
            GUILayout.Space(3);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Prefab", CDimMini, GUILayout.Width(52));
                if (GUILayout.Button(prefab != null ? "Regenerate" : "Generate", EditorStyles.miniButton))
                    DeferGenerate(ui.GenerateMenu, requestRecompute);
                using (new EditorGUI.DisabledScope(prefab == null))
                {
                    if (GUILayout.Button("Open", EditorStyles.miniButton)) AssetDatabase.OpenAsset(prefab);
                    if (GUILayout.Button("Ping", EditorStyles.miniButton)) EditorGUIUtility.PingObject(prefab);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Preview", CDimMini, GUILayout.Width(52));
                if (live == null)
                {
                    if (GUILayout.Button("Spawn in scene", EditorStyles.miniButton)) Spawn(ui, type, prefab);
                }
                else
                {
                    if (GUILayout.Button("Apply to prefab", EditorStyles.miniButton)) ApplyToPrefab(live);
                    if (GUILayout.Button("Remove", EditorStyles.miniButton))
                    {
                        UnityEngine.Object.DestroyImmediate(live.gameObject);
                        requestRecompute();
                    }
                }
            }

            if (live != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Drive", CDimMini, GUILayout.Width(52));
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawWrappedButtons(ui, live);
                    }
                }
            }

            GUILayout.Space(2);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        static void DrawWrappedButtons(PackageUi ui, Component live)
        {
            const int perRow = 3;
            var all = ui.States
                .Select(s => (label: s.label, action: (Action)(() => Invoke(live, s.method, s.args))))
                .ToList();
            if (ui.Hook == UiHook.Conflict)
                all.Add((label: "Conflict dialog", action: (Action)(() => ShowMockConflict(live))));

            for (int i = 0; i < all.Count; i += perRow)
                using (new EditorGUILayout.HorizontalScope())
                    for (int j = i; j < Mathf.Min(i + perRow, all.Count); j++)
                        if (GUILayout.Button(all[j].label, EditorStyles.miniButton))
                            all[j].action();
        }

        void DrawCustomRegistration(CloudSaveSetupState state, Action requestRecompute)
        {
            EditorGUILayout.LabelField("Your own forms", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Point a built-in UI at your project's form. The matching step then reads " +
                "\"covered by your form\" instead of \"not configured\".", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(2);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _newPrefab = (GameObject)EditorGUILayout.ObjectField("Form prefab", _newPrefab, typeof(GameObject), false);
                _newHook = (UiHook)EditorGUILayout.EnumPopup("Covers", _newHook);
                _newName = EditorGUILayout.TextField("Display name", _newName);
                _newNote = EditorGUILayout.TextField("Note", _newNote);
                using (new EditorGUI.DisabledScope(_newPrefab == null && string.IsNullOrEmpty(_newName)))
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

            foreach (var reg in state.CustomUis.ToList())
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(reg.Hook.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(80));
                    EditorGUILayout.LabelField(reg.DisplayName, EditorStyles.boldLabel, GUILayout.Width(170));
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

        // ── helpers ───────────────────────────────────────────────────────

        static GUIStyle _cDimMini;
        static GUIStyle CDimMini => _cDimMini ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = CDim } };

        static void DeferGenerate(string menu, Action requestRecompute)
        {
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem(menu);
                requestRecompute();
            };
        }

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
                var comp = type != null ? go.AddComponent(type) : null;
                // Awake doesn't run on AddComponent in Edit Mode — build the UI explicitly.
                type?.GetMethod("BuildDefaultUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    ?.Invoke(comp, null);
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
                    "This UI was spawned from scratch. Use \"Generate\" first, then spawn that prefab.", "OK");
                return;
            }
            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
        }

        static void Invoke(object target, string method, object[] args)
        {
            var mi = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi == null) { Debug.LogWarning($"[UiTab] {target.GetType().Name}.{method}() not found — tell the maintainer."); return; }
            try { mi.Invoke(target, args); }
            catch (Exception e) { Debug.LogWarning($"[UiTab] {method} threw: {e.InnerException?.Message ?? e.Message}"); }
        }

        static void ShowMockConflict(object cloudSaveUi)
        {
            var data = new CloudConflictData(
                DateTime.UtcNow.AddDays(-1).Ticks, DateTime.UtcNow.Ticks,
                new byte[] { 1, 2, 3 }, CloudConflictReason.CloudIsNewer);
            Invoke(cloudSaveUi, "ShowConflictDialogAsync", new object[] { data });
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
