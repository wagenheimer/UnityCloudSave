using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.CloudSave
{
    [HelpURL("https://github.com/wagenheimer/UnityCloudSave")]
    public class CloudSaveUI : MonoBehaviour
    {
        [Header("Loading Overlay")]
        [SerializeField] private GameObject _loadingRoot;
        [SerializeField] private TextMeshProUGUI _loadingText;

        [Header("Toast")]
        [SerializeField] private GameObject _toastRoot;
        [SerializeField] private Image _toastBg;
        [SerializeField] private TextMeshProUGUI _toastText;

        [Header("Conflict Dialog")]
        [SerializeField] private GameObject _conflictRoot;
        [SerializeField] private TextMeshProUGUI _conflictTitle;
        [SerializeField] private TextMeshProUGUI _localInfoText;
        [SerializeField] private TextMeshProUGUI _cloudInfoText;

        float _loadingDots;
        Coroutine _toastRoutine;
        TaskCompletionSource<CloudConflictChoice> _conflictTcs;

        static readonly Color ColOverlay   = new Color(0f,    0f,    0f,    0.72f);
        static readonly Color ColPanel     = new Color(0.12f, 0.12f, 0.14f, 0.97f);
        static readonly Color ColAccent    = new Color(0.22f, 0.60f, 1f,    1f);
        static readonly Color ColSuccess   = new Color(0.20f, 0.75f, 0.35f, 1f);
        static readonly Color ColWarning   = new Color(1f,    0.75f, 0.10f, 1f);
        static readonly Color ColError     = new Color(0.85f, 0.25f, 0.20f, 1f);
        static readonly Color ColText      = new Color(0.92f, 0.92f, 0.95f, 1f);
        static readonly Color ColTextDim   = new Color(0.65f, 0.65f, 0.70f, 1f);
        static readonly Color ColLocalCard = new Color(0.18f, 0.18f, 0.22f, 1f);
        static readonly Color ColCloudCard = new Color(0.10f, 0.22f, 0.38f, 1f);

        void Awake()
        {
            if (_loadingRoot == null)
                BuildUI();

            DontDestroyOnLoad(gameObject);

            CloudSync.OnSyncStarted     += HandleSyncStarted;
            CloudSync.OnSyncCompleted   += HandleSyncCompleted;
            CloudAuth.OnLinked          += HandleLinked;
            CloudAuth.OnAccountSwitched += HandleAccountSwitched;
            CloudSync.ConflictResolver   = ShowConflictDialogAsync;
        }

        void OnDestroy()
        {
            CloudSync.OnSyncStarted     -= HandleSyncStarted;
            CloudSync.OnSyncCompleted   -= HandleSyncCompleted;
            CloudAuth.OnLinked          -= HandleLinked;
            CloudAuth.OnAccountSwitched -= HandleAccountSwitched;
            if (CloudSync.ConflictResolver == (Func<CloudConflictData, Task<CloudConflictChoice>>)ShowConflictDialogAsync)
                CloudSync.ConflictResolver = null;
        }

        void Update()
        {
            if (!_loadingRoot.activeSelf) return;
            _loadingDots += Time.unscaledDeltaTime * 2f;
            int dots = (int)(_loadingDots % 4);
            _loadingText.text = CloudSaveLocale.Loading() + new string('.', dots);
        }

        void HandleSyncStarted() => SetLoading(true);

        void HandleSyncCompleted(CloudSyncResult result)
        {
            SetLoading(false);
            switch (result)
            {
                case CloudSyncResult.CloudApplied:
                    ShowToast(CloudSaveLocale.Synced(), ColSuccess);
                    break;
                case CloudSyncResult.LocalNewer:
                    ShowToast(CloudSaveLocale.LocalNewer(), ColAccent);
                    break;
                case CloudSyncResult.UserChoseLocal:
                    ShowToast(CloudSaveLocale.LocalKept(), ColAccent);
                    break;
                case CloudSyncResult.NoCloudSave:
                    break;
                case CloudSyncResult.Offline:
                    ShowToast(CloudSaveLocale.Offline(), ColWarning);
                    break;
                case CloudSyncResult.Error:
                    ShowToast(CloudSaveLocale.Error(), ColError);
                    break;
            }
        }

        void HandleLinked(CloudAuthProvider provider)
        {
            var name = provider == CloudAuthProvider.GooglePlayGames ? "Google Play Games"
                     : provider == CloudAuthProvider.Apple            ? "Apple"
                     : provider == CloudAuthProvider.AppleGameCenter  ? "Game Center"
                     : "conta";
            ShowToast(CloudSaveLocale.AccountLinked(name), ColAccent);
        }

        void HandleAccountSwitched(CloudAuthProvider provider) =>
            ShowToast(CloudSaveLocale.AccountSwitched(), ColWarning);

        void SetLoading(bool visible)
        {
            _loadingRoot.SetActive(visible);
            if (visible) _loadingDots = 0f;
        }

        void ShowToast(string message, Color color)
        {
            if (_toastRoutine != null)
                StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(message, color));
        }

        IEnumerator ToastRoutine(string message, Color color)
        {
            _toastText.text = message;
            _toastBg.color  = color;
            var cg = _toastRoot.GetComponent<CanvasGroup>();
            _toastRoot.SetActive(true);

            for (float t = 0; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                cg.alpha = t / 0.25f;
                yield return null;
            }
            cg.alpha = 1f;

            yield return new WaitForSecondsRealtime(2.5f);

            for (float t = 0; t < 0.35f; t += Time.unscaledDeltaTime)
            {
                cg.alpha = 1f - (t / 0.35f);
                yield return null;
            }
            cg.alpha = 0f;
            _toastRoot.SetActive(false);
        }

        async Task<CloudConflictChoice> ShowConflictDialogAsync(CloudConflictData data)
        {
            _conflictTcs = new TaskCompletionSource<CloudConflictChoice>();
            _conflictTitle.text = data.Reason == CloudConflictReason.AccountSwitched
                ? CloudSaveLocale.ConflictTitleAccount()
                : CloudSaveLocale.ConflictTitleCloud();
            _localInfoText.text = FormatTimestamp(CloudSaveLocale.ConflictLocal(),   data.LocalTimestamp);
            _cloudInfoText.text = FormatTimestamp(CloudSaveLocale.ConflictCloud(), data.CloudTimestamp);
            _conflictRoot.SetActive(true);
            return await _conflictTcs.Task;
        }

        void ResolveConflict(CloudConflictChoice choice)
        {
            _conflictRoot.SetActive(false);
            _conflictTcs?.TrySetResult(choice);
        }

        static string FormatTimestamp(string label, long ticks)
        {
            if (ticks <= 0) return $"{label}\n{CloudSaveLocale.ConflictNone()}";
            var dt = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
            return $"{label}\n{dt:dd/MM/yyyy  HH:mm}";
        }

        // ── Static factory ─────────────────────────────────────────────────

        /// <summary>
        /// Creates a CloudSaveUI instance. Uses a custom prefab from
        /// Resources/CloudSaveUI.prefab if it exists; otherwise builds
        /// the UI procedurally. In the Editor, the prefab is auto-generated
        /// on first call so you can customize it.
        /// </summary>
        public static CloudSaveUI Create()
        {
            var prefab = Resources.Load<GameObject>("CloudSaveUI");
            if (prefab != null)
            {
                var go = Instantiate(prefab);
                go.name = "CloudSaveUI";
                return go.GetComponent<CloudSaveUI>();
            }

#if UNITY_EDITOR
            return CreateAndSavePrefab();
#else
            var go = new GameObject("CloudSaveUI");
            return go.AddComponent<CloudSaveUI>();
#endif
        }

#if UNITY_EDITOR
        static CloudSaveUI CreateAndSavePrefab()
        {
            var go = new GameObject("CloudSaveUI");
            var ui = go.AddComponent<CloudSaveUI>();
            ui.BuildUI();

            var dir = "Assets/Resources";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");

            var path = dir + "/CloudSaveUI.prefab";
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            UnityEditor.AssetDatabase.Refresh();

            var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = Instantiate(saved);
            instance.name = "CloudSaveUI";
            return instance.GetComponent<CloudSaveUI>();
        }
#endif

#if UNITY_EDITOR
        [ContextMenu("Setup References from Children")]
        void SetupReferencesFromChildren()
        {
            _loadingRoot  = FindChild("Loading");
            _loadingText  = FindChild("LoadingText")?.GetComponent<TextMeshProUGUI>();
            _toastRoot    = FindChild("Toast");
            _toastBg      = FindChild("Toast")?.GetComponent<Image>();
            _toastText    = FindChild("ToastText")?.GetComponent<TextMeshProUGUI>();
            _conflictRoot = FindChild("Conflict");
            _conflictTitle = FindChild("Title")?.GetComponent<TextMeshProUGUI>();
            _localInfoText = FindChild("LocalInfo")?.GetComponent<TextMeshProUGUI>();
            _cloudInfoText = FindChild("CloudInfo")?.GetComponent<TextMeshProUGUI>();
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

        // ── UI construction (fallback) ──────────────────────────────────────

        void BuildUI()
        {
            var canvas = MakeCanvas("CloudSaveCanvas", 200);
            BuildLoadingOverlay(canvas);
            BuildToast(canvas);
            BuildConflictDialog(canvas);
        }

        Canvas MakeCanvas(string goName, int sortOrder)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        void BuildLoadingOverlay(Canvas canvas)
        {
            _loadingRoot = MakePanel(canvas.gameObject, "Loading", ColOverlay,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var inner = MakePanel(_loadingRoot, "LoadingInner", ColPanel,
                new Vector2(0.2f, 0.44f), new Vector2(0.8f, 0.58f), Vector2.zero, Vector2.zero);
            _loadingText = MakeText(inner, "LoadingText", CloudSaveLocale.Loading(),
                ColText, 28, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(-20, -20), new Vector2(20, 20));
            _loadingRoot.SetActive(false);
        }

        void BuildToast(Canvas canvas)
        {
            _toastRoot = new GameObject("Toast", typeof(RectTransform));
            _toastRoot.transform.SetParent(canvas.transform, false);
            var cg = _toastRoot.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            var rt = _toastRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.05f);
            rt.anchorMax = new Vector2(0.9f, 0.10f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _toastBg = _toastRoot.AddComponent<Image>();
            _toastBg.color = ColSuccess;
            _toastText = MakeText(_toastRoot, "ToastText", "",
                Color.white, 26, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(-16, -8), new Vector2(16, 8));
            _toastRoot.SetActive(false);
        }

        void BuildConflictDialog(Canvas canvas)
        {
            _conflictRoot = MakePanel(canvas.gameObject, "Conflict", ColOverlay,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var card = MakePanel(_conflictRoot, "ConflictCard", ColPanel,
                new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.72f), Vector2.zero, Vector2.zero);

            _conflictTitle = MakeText(card, "Title", CloudSaveLocale.ConflictTitleCloud(),
                ColText, 34, TextAlignmentOptions.TopCenter,
                new Vector2(0f, 0.75f), new Vector2(1f, 1f),
                new Vector2(16, 0), new Vector2(-16, -8));
            _conflictTitle.fontStyle = FontStyles.Bold;

            MakeText(card, "Subtitle", CloudSaveLocale.ConflictChoose(),
                ColTextDim, 24, TextAlignmentOptions.TopCenter,
                new Vector2(0f, 0.62f), new Vector2(1f, 0.76f),
                new Vector2(16, 0), new Vector2(-16, 0));

            var localCard = MakePanel(card, "LocalCard", ColLocalCard,
                new Vector2(0.03f, 0.28f), new Vector2(0.48f, 0.62f), Vector2.zero, Vector2.zero);
            _localInfoText = MakeText(localCard, "LocalInfo", "",
                ColText, 24, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));

            var cloudCard = MakePanel(card, "CloudCard", ColCloudCard,
                new Vector2(0.52f, 0.28f), new Vector2(0.97f, 0.62f), Vector2.zero, Vector2.zero);
            _cloudInfoText = MakeText(cloudCard, "CloudInfo", "",
                ColText, 24, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));

            MakeButton(card, "BtnLocal", CloudSaveLocale.BtnKeepLocal(), ColLocalCard, ColTextDim,
                new Vector2(0.03f, 0.05f), new Vector2(0.48f, 0.26f),
                () => ResolveConflict(CloudConflictChoice.UseLocal));

            MakeButton(card, "BtnCloud", CloudSaveLocale.BtnUseCloud(), ColAccent, Color.white,
                new Vector2(0.52f, 0.05f), new Vector2(0.97f, 0.26f),
                () => ResolveConflict(CloudConflictChoice.UseCloud));

            _conflictRoot.SetActive(false);
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
            txt.text       = content;
            txt.color      = color;
            txt.fontSize   = fontSize;
            txt.alignment  = alignment;
            return txt;
        }

        void MakeButton(GameObject parent, string name, string label,
            Color bgColor, Color textColor,
            Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            var go = MakePanel(parent, name, bgColor, anchorMin, anchorMax,
                Vector2.zero, Vector2.zero);
            MakeText(go, "Label", label, textColor, 28, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick());
        }
    }
}
