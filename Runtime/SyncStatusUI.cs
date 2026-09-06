using System;
using System.Collections;
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
        [SerializeField] TextMeshProUGUI _detailText;
        [SerializeField] TextMeshProUGUI _lastSyncText;

        [Header("Layout")]
        [SerializeField] int _sortOrder = 150;

        [Header("Colors (overridden by CloudSaveUITheme if present)")]
        [SerializeField] Color _colorSynced   = new Color(0.20f, 0.80f, 0.20f);
        [SerializeField] Color _colorSyncing  = new Color(0.20f, 0.50f, 1.00f);
        [SerializeField] Color _colorOffline  = new Color(1.00f, 0.80f, 0.00f);
        [SerializeField] Color _colorError    = new Color(1.00f, 0.25f, 0.25f);

        [SerializeField] CanvasGroup _cg;

        static SyncStatusUI _instance;
        SyncStatus _status = SyncStatus.Offline;
        DateTime _lastSyncTime;
        bool _hasLastSync;
        bool _usingExternalCanvas;
        CloudSaveUITheme _theme;

        Color ColSynced  => _theme != null ? _theme.Success : _colorSynced;
        Color ColSyncing => _theme != null ? _theme.Accent  : _colorSyncing;
        Color ColOffline => _theme != null ? _theme.Warning : _colorOffline;
        Color ColError_  => _theme != null ? _theme.Error   : _colorError;
        Color ColPanel   => _theme != null ? _theme.Panel   : new Color(0.05f, 0.05f, 0.05f, 0.82f);
        Color ColText    => _theme != null ? _theme.Text     : new Color(0.92f, 0.92f, 0.95f);
        Color ColTextDim => _theme != null ? _theme.TextDim  : new Color(0.60f, 0.60f, 0.65f);
        int Radius       => _theme != null ? Mathf.Clamp(_theme.CornerRadius, 4, 40) : 20;

        public static SyncStatusUI Instance => _instance;
        public SyncStatus Status => _status;

        // ── Public API ─────────────────────────────────────────────────────

        public void SetStatus(SyncStatus status)
        {
            _status = status;
            _statusText.text = CloudSaveLocale.SyncStatusText(status);
            _icon.color = status switch
            {
                SyncStatus.Synced  => ColSynced,
                SyncStatus.Syncing => ColSyncing,
                SyncStatus.Offline => ColOffline,
                SyncStatus.Error   => ColError_,
                _ => ColSynced
            };

            if (status == SyncStatus.Synced)
            {
                _lastSyncTime = DateTime.Now;
                _hasLastSync = true;
                UpdateLastSync();
            }

            if (status == SyncStatus.Syncing)
                _lastSyncText.gameObject.SetActive(false);

            UpdateDetail();
        }

        void UpdateDetail()
        {
            if (_detailText == null) return;
            var playerId = CloudAuth.IsReady ? CloudAuth.PlayerId : "not initialized";
            var provider = CloudAuth.IsReady ? CloudAuth.Provider.ToString() : "-";
            var syncResult = CloudSync.LastResult?.ToString() ?? "never synced";
            _detailText.text = $"Player: {playerId}\nAuth: {provider} | Sync: {syncResult}";
            _detailText.gameObject.SetActive(true);
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
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _theme = CloudSaveUITheme.Current;

            if (GetComponentInParent<Canvas>() != null) _usingExternalCanvas = true;
            else if (CloudSaveUiHost.Canvas != null)
            {
                transform.SetParent(CloudSaveUiHost.Canvas.transform, false);
                _usingExternalCanvas = true;
            }

            if (_root == null)
                BuildUI();
            else
                UpdateCanvasSortOrder();

            if (!_usingExternalCanvas)
                DontDestroyOnLoad(gameObject);

            if (_cg != null && (_theme == null || _theme.EnableAnimations))
                StartCoroutine(FadeIn());

            // If sync already ran before UI was created, use the last result
            if (CloudSync.LastResult.HasValue)
            {
                OnSyncCompleted(CloudSync.LastResult.Value);
            }
            else
            {
                SetStatus(SyncStatus.Offline);
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
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

        void UpdateCanvasSortOrder()
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = _sortOrder;
        }

        void UpdateLastSync()
        {
            _lastSyncText.text = CloudSaveLocale.SyncLast(_lastSyncTime.ToString("HH:mm"));
            _lastSyncText.gameObject.SetActive(true);
        }

        // ── Factory ─────────────────────────────────────────────────────────

        public static SyncStatusUI Create()
        {
            if (_instance != null)
                return _instance;

            var prefab = Resources.Load<GameObject>("SyncStatusUI");
            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                instance.name = "SyncStatusUI";
                return instance.GetComponent<SyncStatusUI>();
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

        IEnumerator FadeIn()
        {
            _cg.alpha = 0f;
            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                _cg.alpha = Mathf.Clamp01(t / 0.25f);
                yield return null;
            }
            _cg.alpha = 1f;
        }

        void BuildUI()
        {
            var canvas = EnsureCanvas();

            _root = MakePanel(canvas.gameObject, "Root", ColPanel,
                new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero, rounded: Radius);
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.pivot = new Vector2(1f, 0f);
            rootRt.sizeDelta = new Vector2(430, 120);
            rootRt.anchoredPosition = new Vector2(-24, 24);
            _cg = _root.AddComponent<CanvasGroup>();
            _cg.blocksRaycasts = false;

            var shadow = MakePanel(canvas.gameObject, "RootShadow", new Color(0, 0, 0, 0.30f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero, rounded: 34, shadow: true);
            var shRt = shadow.GetComponent<RectTransform>();
            shRt.pivot = new Vector2(1f, 0f);
            shRt.sizeDelta = rootRt.sizeDelta + new Vector2(40, 40);
            shRt.anchoredPosition = rootRt.anchoredPosition + new Vector2(0, -8);
            shRt.SetSiblingIndex(rootRt.GetSiblingIndex());

            var row = MakePanel(_root, "Row", Color.clear,
                new Vector2(0f, 0.42f), new Vector2(1f, 1f), new Vector2(14, 0), new Vector2(-14, -4));

            _icon = MakeIcon(row);

            _statusText = MakeText(row, "StatusText", CloudSaveLocale.SyncStatusText(SyncStatus.Offline),
                ColText, 22, TextAlignmentOptions.Left,
                new Vector2(0.14f, 0f), new Vector2(0.62f, 1f), Vector2.zero, Vector2.zero);
            _statusText.fontStyle = FontStyles.Bold;

            _lastSyncText = MakeText(row, "LastSync", "",
                ColTextDim, 17, TextAlignmentOptions.Right,
                new Vector2(0.60f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _lastSyncText.gameObject.SetActive(false);

            _detailText = MakeText(_root, "Detail", "",
                ColTextDim, 15, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(1f, 0.42f), new Vector2(16, 2), new Vector2(-14, 0));
        }

        Image MakeIcon(GameObject parent)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30, 30);
            rt.anchoredPosition = new Vector2(20, 0);
            var img = go.AddComponent<Image>();
            img.color = ColSynced;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillAmount = 0.78f;
            UiGeneratedSprite.Attach(img, UiGeneratedSprite.Kind.Ring);
            return img;
        }

        Canvas EnsureCanvas()
        {
            var host = CloudSaveUiHost.Resolve(this);
            if (host != null)
            {
                _usingExternalCanvas = true;
                if (host.GetComponent<GraphicRaycaster>() == null)
                    host.gameObject.AddComponent<GraphicRaycaster>();
                return host;
            }

            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                var go = new GameObject("SyncStatusCanvas", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                canvas = go.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, _sortOrder);
            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        GameObject MakePanel(GameObject parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            int rounded = 0, bool shadow = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = color;
            if (shadow)
            {
                img.type = Image.Type.Sliced;
                img.raycastTarget = false;
                UiGeneratedSprite.Attach(img, UiGeneratedSprite.Kind.Shadow, Mathf.Max(8, rounded));
            }
            else if (rounded > 0)
            {
                img.type = Image.Type.Sliced;
                UiGeneratedSprite.Attach(img, UiGeneratedSprite.Kind.RoundedRect, rounded);
            }
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
            if (_theme != null && _theme.Font != null) txt.font = _theme.Font;
            txt.text = content;
            txt.color = color;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.raycastTarget = false;
            return txt;
        }

#if UNITY_EDITOR
        [ContextMenu("Setup References from Children")]
        void SetupReferencesFromChildren()
        {
            _root = FindChild("Root");
            _cg = FindChild("Root")?.GetComponent<CanvasGroup>();
            _icon = FindChild("Icon")?.GetComponent<Image>();
            _statusText = FindChild("StatusText")?.GetComponent<TextMeshProUGUI>();
            _detailText = FindChild("Detail")?.GetComponent<TextMeshProUGUI>();
            _lastSyncText = FindChild("LastSync")?.GetComponent<TextMeshProUGUI>();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        GameObject FindChild(string name)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        internal void BuildDefaultUI()
        {
            BuildUI();
            SetupReferencesFromChildren();
        }
#endif
    }
}
