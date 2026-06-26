using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.CloudSave
{
    [HelpURL("https://github.com/wagenheimer/UnityCloudSave")]
    public class SyncStatusUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameObject _root;
        [SerializeField] Image _icon;
        [SerializeField] TextMeshProUGUI _statusText;
        [SerializeField] TextMeshProUGUI _lastSyncText;

        [Header("Colors")]
        [SerializeField] Color _colorSynced   = new Color(0.20f, 0.80f, 0.20f);
        [SerializeField] Color _colorSyncing  = new Color(0.20f, 0.50f, 1.00f);
        [SerializeField] Color _colorOffline  = new Color(1.00f, 0.80f, 0.00f);
        [SerializeField] Color _colorError    = new Color(1.00f, 0.25f, 0.25f);

        DateTime _lastSyncTime;
        bool _hasLastSync;

        // ── Public API ─────────────────────────────────────────────────────

        public void SetStatus(SyncStatus status)
        {
            _statusText.text = CloudSaveLocale.SyncStatus(status);
            _icon.color = status switch
            {
                SyncStatus.Synced  => _colorSynced,
                SyncStatus.Syncing => _colorSyncing,
                SyncStatus.Offline => _colorOffline,
                SyncStatus.Error   => _colorError,
                _ => _colorSynced
            };

            if (status == SyncStatus.Synced)
            {
                _lastSyncTime = DateTime.Now;
                _hasLastSync = true;
                UpdateLastSync();
            }

            if (status == SyncStatus.Syncing)
                _lastSyncText.gameObject.SetActive(false);
        }

        public void SetLastSync(DateTime time)
        {
            _lastSyncTime = time;
            _hasLastSync = true;
            UpdateLastSync();
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        // ── Lifecycle ──────────────────────────────────────────────────────

        void Awake()
        {
            if (_root == null)
                BuildUI();
        }

        void OnEnable()
        {
            CloudSync.OnSyncStarted   += OnSyncStarted;
            CloudSync.OnSyncCompleted += OnSyncCompleted;
        }

        void OnDisable()
        {
            CloudSync.OnSyncStarted   -= OnSyncStarted;
            CloudSync.OnSyncCompleted -= OnSyncCompleted;
        }

        void OnSyncStarted()
        {
            SetStatus(SyncStatus.Syncing);
        }

        void OnSyncCompleted(CloudSyncResult result)
        {
            var status = result switch
            {
                CloudSyncResult.CloudApplied    => SyncStatus.Synced,
                CloudSyncResult.LocalNewer      => SyncStatus.Synced,
                CloudSyncResult.UserChoseLocal  => SyncStatus.Synced,
                CloudSyncResult.NoCloudSave     => SyncStatus.Synced,
                CloudSyncResult.Offline         => SyncStatus.Offline,
                CloudSyncResult.Error           => SyncStatus.Error,
                _                               => SyncStatus.Error
            };
            SetStatus(status);
        }

        // ── Helpers ────────────────────────────────────────────────────────

        void UpdateLastSync()
        {
            _lastSyncText.text = CloudSaveLocale.SyncLast(_lastSyncTime.ToString("HH:mm"));
            _lastSyncText.gameObject.SetActive(true);
        }

        // ── Factory ─────────────────────────────────────────────────────────

        public static SyncStatusUI Create()
        {
            var prefab = Resources.Load<GameObject>("SyncStatusUI");
            if (prefab != null)
            {
                var go = Instantiate(prefab);
                go.name = "SyncStatusUI";
                return go.GetComponent<SyncStatusUI>();
            }

#if UNITY_EDITOR
            return CreateAndSavePrefab();
#else
            var go = new GameObject("SyncStatusUI");
            return go.AddComponent<SyncStatusUI>();
#endif
        }

#if UNITY_EDITOR
        static SyncStatusUI CreateAndSavePrefab()
        {
            var go = new GameObject("SyncStatusUI");
            var ui = go.AddComponent<SyncStatusUI>();
            ui.BuildUI();

            var dir = "Assets/Resources";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");

            var path = dir + "/SyncStatusUI.prefab";
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            DestroyImmediate(go);
            UnityEditor.AssetDatabase.Refresh();

            var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = Instantiate(saved);
            instance.name = "SyncStatusUI";
            return instance.GetComponent<SyncStatusUI>();
        }
#endif

        // ── Procedural UI (fallback) ────────────────────────────────────────

        void BuildUI()
        {
            var canvas = MakeCanvas("SyncStatusCanvas", 150);
            _root = MakePanel(canvas.gameObject, "Root",
                new Color(0.05f, 0.05f, 0.05f, 0.70f),
                new Vector2(0.80f, 0.02f), new Vector2(0.98f, 0.07f),
                Vector2.zero, Vector2.zero);

            var container = MakePanel(_root, "Container", Color.clear,
                Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));

            _icon = MakeIcon(container);

            _statusText = MakeText(container, "StatusText", CloudSaveLocale.SyncStatus(SyncStatus.Synced),
                _colorSynced, 22, TextAlignmentOptions.Left,
                new Vector2(0.08f, 0f), new Vector2(0.65f, 1f),
                Vector2.zero, Vector2.zero);

            _lastSyncText = MakeText(container, "LastSync", "",
                new Color(0.60f, 0.60f, 0.60f), 18, TextAlignmentOptions.Right,
                new Vector2(0.65f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero);
            _lastSyncText.gameObject.SetActive(false);
        }

        Image MakeIcon(GameObject parent)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.2f);
            rt.anchorMax = new Vector2(0f, 0.8f);
            rt.sizeDelta = new Vector2(16, 16);
            var img = go.AddComponent<Image>();
            img.color = _colorSynced;
            return img;
        }

        Canvas MakeCanvas(string goName, int sortOrder)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        GameObject MakePanel(GameObject parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            go.AddComponent<Image>().color = color;
            return go;
        }

        TextMeshProUGUI MakeText(GameObject parent, string name, string content, Color color,
            int fontSize, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text = content;
            txt.color = color;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            return txt;
        }

#if UNITY_EDITOR
        [ContextMenu("Setup References from Children")]
        void SetupReferencesFromChildren()
        {
            _root = FindChild("Root");
            _icon = FindChild("Icon")?.GetComponent<Image>();
            _statusText = FindChild("StatusText")?.GetComponent<TextMeshProUGUI>();
            _lastSyncText = FindChild("LastSync")?.GetComponent<TextMeshProUGUI>();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        GameObject FindChild(string name)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }
#endif
    }
}
