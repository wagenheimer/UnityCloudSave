using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
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

        [Header("Polish (auto-wired)")]
        [SerializeField] private RectTransform _spinner;
        [SerializeField] private CanvasGroup _loadingCg;
        [SerializeField] private CanvasGroup _conflictCg;
        [SerializeField] private RectTransform _conflictCard;
        [SerializeField] private RectTransform _toastIcon;
        [SerializeField] private RectTransform _localCardRt;
        [SerializeField] private RectTransform _cloudCardRt;

        [Header("Layout")]
        [SerializeField] private int _sortOrder = 200;

        static CloudSaveUI _instance;
        Coroutine _toastRoutine;
        readonly Queue<(string message, Color color)> _toastQueue = new();
        bool _toastPumping;
        TaskCompletionSource<CloudConflictChoice> _conflictTcs;
        CancellationTokenSource _conflictCts;
        CloudSaveUITheme _theme;

        /// <summary>
        /// Optional hook for a human-readable summary of each side of a conflict ("Level 42 · 1200 coins").
        /// Return (localSummary, cloudSummary); either may be null to fall back to the timestamp.
        /// <see cref="CloudSaveController"/> wires this from <c>CloudSaveOptions.DescribeSave</c>.
        /// </summary>
        public static Func<CloudConflictData, (string local, string cloud)> ConflictSummaryProvider;

        Color ColOverlay   => _theme != null ? _theme.Overlay   : new Color(0f, 0f, 0f, 0.72f);
        Color ColPanel     => _theme != null ? _theme.Panel     : new Color(0.12f, 0.12f, 0.14f, 0.97f);
        Color ColAccent    => _theme != null ? _theme.Accent    : new Color(0.22f, 0.60f, 1f, 1f);
        Color ColSuccess   => _theme != null ? _theme.Success   : new Color(0.20f, 0.75f, 0.35f, 1f);
        Color ColWarning   => _theme != null ? _theme.Warning   : new Color(1f, 0.75f, 0.10f, 1f);
        Color ColError     => _theme != null ? _theme.Error     : new Color(0.85f, 0.25f, 0.20f, 1f);
        Color ColText      => _theme != null ? _theme.Text      : new Color(0.92f, 0.92f, 0.95f, 1f);
        Color ColTextDim   => _theme != null ? _theme.TextDim   : new Color(0.65f, 0.65f, 0.70f, 1f);
        Color ColLocalCard => _theme != null ? _theme.LocalCard : new Color(0.18f, 0.18f, 0.22f, 1f);
        Color ColCloudCard => _theme != null ? _theme.CloudCard : new Color(0.10f, 0.22f, 0.38f, 1f);
        int Radius         => _theme != null ? Mathf.Clamp(_theme.CornerRadius, 4, 64) : 28;
        bool Anim          => _theme == null || _theme.EnableAnimations;
        float ToastSeconds => _theme != null && _theme.ToastSeconds > 0f ? _theme.ToastSeconds : 2.6f;
        TMP_FontAsset ThemeFont => _theme != null ? _theme.Font : null;

        public static CloudSaveUI Instance => _instance;

        void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _theme = CloudSaveUITheme.Current;
            MaybeAttachToHost();

            if (_loadingRoot == null)
                BuildUI();
            else
                UpdateCanvasSortOrder();

            if (!_usingExternalCanvas)
                DontDestroyOnLoad(gameObject);

            CloudSync.OnSyncStarted     += HandleSyncStarted;
            CloudSync.OnSyncCompleted   += HandleSyncCompleted;
            CloudAuth.OnLinked          += HandleLinked;
            CloudAuth.OnAccountSwitched += HandleAccountSwitched;
            CloudSync.ConflictResolver   = ShowConflictDialogAsync;
        }

        void OnDestroy()
        {
            if (_instance != this) return;

            _instance = null;
            CloudSync.OnSyncStarted     -= HandleSyncStarted;
            CloudSync.OnSyncCompleted   -= HandleSyncCompleted;
            CloudAuth.OnLinked          -= HandleLinked;
            CloudAuth.OnAccountSwitched -= HandleAccountSwitched;
            if (CloudSync.ConflictResolver == (Func<CloudConflictData, Task<CloudConflictChoice>>)ShowConflictDialogAsync)
                CloudSync.ConflictResolver = null;

            _conflictCts?.Cancel();
            _conflictCts?.Dispose();
        }

        void Update()
        {
            if (!Anim || _loadingRoot == null || !_loadingRoot.activeSelf) return;
            if (_spinner != null)
                _spinner.localRotation = Quaternion.Euler(0, 0, _spinner.localEulerAngles.z - Time.unscaledDeltaTime * 320f);
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
            if (_loadingRoot == null) return;
            if (visible)
            {
                _loadingRoot.SetActive(true);
                if (_spinner != null) _spinner.localRotation = Quaternion.identity;
                if (_loadingCg != null)
                {
                    if (Anim) StartCoroutine(FadeScale(_loadingCg, null, true, 0.18f));
                    else _loadingCg.alpha = 1f;
                }
            }
            else if (_loadingRoot.activeSelf && _loadingCg != null && Anim)
            {
                StartCoroutine(FadeScale(_loadingCg, null, false, 0.15f, () => _loadingRoot.SetActive(false)));
            }
            else
            {
                _loadingRoot.SetActive(false);
            }
        }

        void ShowToast(string message, Color color)
        {
            _toastQueue.Enqueue((message, color));
            if (!_toastPumping) StartCoroutine(PumpToasts());
        }

        IEnumerator PumpToasts()
        {
            _toastPumping = true;
            while (_toastQueue.Count > 0)
            {
                var (message, color) = _toastQueue.Dequeue();
                yield return _toastRoutine = StartCoroutine(ToastRoutine(message, color));
            }
            _toastPumping = false;
        }

        UiIcon ToastIconKind(Color color) =>
            color == ColSuccess ? UiIcon.Check :
            color == ColError   ? UiIcon.Cross :
            color == ColWarning ? UiIcon.Warn  : UiIcon.Cloud;

        void SetToastIcon(Color color)
        {
            if (_toastIcon == null) return;
            for (int i = _toastIcon.childCount - 1; i >= 0; i--) Destroy(_toastIcon.GetChild(i).gameObject);
            UiIcons.Build(_toastIcon.gameObject, ToastIconKind(color), Color.white, 44f);
        }

        IEnumerator ToastRoutine(string message, Color color)
        {
            _toastText.text = message;
            SetToastIcon(color);
            _toastBg.color = color;

            var cg = _toastRoot.GetComponent<CanvasGroup>();
            var rt = (RectTransform)_toastRoot.transform;
            _toastRoot.SetActive(true);

            float baseY = rt.anchoredPosition.y;
            const float rise = 28f;

            if (Anim)
            {
                for (float t = 0; t < 0.22f; t += Time.unscaledDeltaTime)
                {
                    float k = EaseOut(t / 0.22f);
                    cg.alpha = k;
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY - rise * (1f - k));
                    yield return null;
                }
            }
            cg.alpha = 1f;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY);

            yield return new WaitForSecondsRealtime(ToastSeconds);

            if (Anim)
            {
                for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
                {
                    float k = t / 0.3f;
                    cg.alpha = 1f - k;
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY + rise * k);
                    yield return null;
                }
            }
            cg.alpha = 0f;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, baseY);
            _toastRoot.SetActive(false);
        }

        static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }

        IEnumerator FadeScale(CanvasGroup cg, RectTransform scaleTarget, bool show, float dur, Action done = null)
        {
            float from = show ? 0f : 1f, to = show ? 1f : 0f;
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = show ? EaseOut(t / dur) : t / dur;
                float a = Mathf.Lerp(from, to, k);
                cg.alpha = a;
                if (scaleTarget != null) scaleTarget.localScale = Vector3.one * Mathf.Lerp(show ? 0.92f : 1f, show ? 1f : 0.96f, k);
                yield return null;
            }
            cg.alpha = to;
            if (scaleTarget != null) scaleTarget.localScale = Vector3.one;
            done?.Invoke();
        }

        async Task<CloudConflictChoice> ShowConflictDialogAsync(CloudConflictData data)
        {
            _conflictCts?.Cancel();
            _conflictCts?.Dispose();
            _conflictCts = new CancellationTokenSource();
            _conflictTcs = new TaskCompletionSource<CloudConflictChoice>();

            _conflictTitle.text = data.Reason == CloudConflictReason.AccountSwitched
                ? CloudSaveLocale.ConflictTitleAccount()
                : CloudSaveLocale.ConflictTitleCloud();

            (string local, string cloud) summary = default;
            try { summary = ConflictSummaryProvider?.Invoke(data) ?? default; }
            catch (Exception e) { Debug.LogWarning("[CloudSaveUI] ConflictSummaryProvider threw: " + e.Message); }

            _localInfoText.text = string.IsNullOrEmpty(summary.local)
                ? FormatTimestamp(CloudSaveLocale.ConflictLocal(), data.LocalTimestamp)
                : $"{summary.local}\n<size=80%>{Relative(data.LocalTimestamp)}</size>";
            _cloudInfoText.text = string.IsNullOrEmpty(summary.cloud)
                ? FormatTimestamp(CloudSaveLocale.ConflictCloud(), data.CloudTimestamp)
                : $"{summary.cloud}\n<size=80%>{Relative(data.CloudTimestamp)}</size>";

            HighlightNewer(data.LocalTimestamp, data.CloudTimestamp);
            _conflictRoot.SetActive(true);
            if (_conflictCg != null)
            {
                if (Anim) StartCoroutine(FadeScale(_conflictCg, _conflictCard, true, 0.20f));
                else { _conflictCg.alpha = 1f; if (_conflictCard != null) _conflictCard.localScale = Vector3.one; }
            }

            var timeout = Task.Delay(30000, _conflictCts.Token);
            var choice  = _conflictTcs.Task;

            var done = await Task.WhenAny(choice, timeout);
            if (done != choice)
                ResolveConflict(CloudConflictChoice.UseCloud);

            return await _conflictTcs.Task;
        }

        void ResolveConflict(CloudConflictChoice choice)
        {
            _conflictTcs?.TrySetResult(choice);
            if (_conflictCg != null && isActiveAndEnabled && Anim)
                StartCoroutine(FadeScale(_conflictCg, _conflictCard, false, 0.14f, () => _conflictRoot.SetActive(false)));
            else
                _conflictRoot.SetActive(false);
        }

        void HighlightNewer(long localTicks, long cloudTicks)
        {
            if (_localCardRt == null || _cloudCardRt == null) return;
            bool cloudNewer = cloudTicks > localTicks;
            SetCardHighlight(_localCardRt, !cloudNewer);
            SetCardHighlight(_cloudCardRt, cloudNewer);
        }

        void SetCardHighlight(RectTransform card, bool on)
        {
            var outline = card.GetComponent<Outline>();
            if (outline == null) outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = on ? ColAccent : new Color(0, 0, 0, 0);
            outline.effectDistance = new Vector2(3, -3);
        }

        void UpdateCanvasSortOrder()
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = _sortOrder;
        }

        static string FormatTimestamp(string label, long ticks)
        {
            if (ticks <= 0) return $"{label}\n{CloudSaveLocale.ConflictNone()}";
            var dt = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
            return $"{label}\n{dt.ToString("g", CultureInfo.CurrentCulture)}\n<size=80%>{Relative(ticks)}</size>";
        }

        static string Relative(long ticks)
        {
            if (ticks <= 0) return "";
            var span = DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
            if (span.TotalSeconds < 0) return "";
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
            return $"{(int)(span.TotalDays / 30)}mo ago";
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
            if (_instance != null)
                return _instance;

            var prefab = Resources.Load<GameObject>("CloudSaveUI");
            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                instance.name = "CloudSaveUI";
                return instance.GetComponent<CloudSaveUI>();
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
            ui._sortOrder = ui._sortOrder;

            var dir = "Assets/Resources";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");

            var path = dir + "/CloudSaveUI.prefab";
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEditor.AssetDatabase.Refresh();

            var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = Instantiate(saved);
            instance.name = "CloudSaveUI";
            return instance.GetComponent<CloudSaveUI>();
        }

        [ContextMenu("Setup References from Children")]
        void SetupReferencesFromChildren()
        {
            _loadingRoot  = FindChild("Loading");
            _loadingText  = FindChild("LoadingText")?.GetComponent<TextMeshProUGUI>();
            _toastRoot    = FindChild("Toast");
            _toastBg      = FindChild("Toast")?.GetComponent<Image>();
            _toastText    = FindChild("ToastText")?.GetComponent<TextMeshProUGUI>();
            _toastIcon    = FindChild("ToastIcon")?.GetComponent<RectTransform>();
            _conflictRoot = FindChild("Conflict");
            _conflictTitle = FindChild("Title")?.GetComponent<TextMeshProUGUI>();
            _localInfoText = FindChild("LocalInfo")?.GetComponent<TextMeshProUGUI>();
            _cloudInfoText = FindChild("CloudInfo")?.GetComponent<TextMeshProUGUI>();
            _spinner       = FindChild("Spinner")?.GetComponent<RectTransform>();
            _loadingCg     = FindChild("Loading")?.GetComponent<CanvasGroup>();
            _conflictCg    = FindChild("Conflict")?.GetComponent<CanvasGroup>();
            _conflictCard  = FindChild("ConflictCard")?.GetComponent<RectTransform>();
            _localCardRt   = FindChild("LocalCard")?.GetComponent<RectTransform>();
            _cloudCardRt   = FindChild("CloudCard")?.GetComponent<RectTransform>();
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

        // ── UI construction (procedural fallback / prefab generator) ────────

        void BuildUI()
        {
            var canvas = EnsureCanvas();
            BuildLoadingOverlay(canvas);
            BuildToast(canvas);
            BuildConflictDialog(canvas);
        }

        bool _usingExternalCanvas;

        /// <summary>
        /// If this UI sits under a Canvas already (dropped into a scene) or a host Canvas was set via
        /// <see cref="CloudSaveUiHost"/>, adopt it and skip DontDestroyOnLoad. Otherwise nothing here.
        /// </summary>
        void MaybeAttachToHost()
        {
            if (GetComponentInParent<Canvas>() != null) { _usingExternalCanvas = true; return; }
            if (CloudSaveUiHost.Canvas != null)
            {
                transform.SetParent(CloudSaveUiHost.Canvas.transform, false);
                _usingExternalCanvas = true;
            }
        }

        /// <summary>
        /// The Canvas to build into: an existing host/ancestor Canvas (left untouched except for a
        /// GraphicRaycaster), a Canvas already under this UI (customised prefab), or a fresh
        /// ScreenSpaceOverlay one for the zero-config case.
        /// </summary>
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
                var go = new GameObject("CloudSaveCanvas", typeof(RectTransform));
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

        void BuildLoadingOverlay(Canvas canvas)
        {
            var overlay = MakeImage(canvas.gameObject, "Loading", ColOverlay);
            Stretch(overlay);
            _loadingRoot = overlay.gameObject;
            _loadingCg = _loadingRoot.AddComponent<CanvasGroup>();
            UiGeneratedSprite.AddBackdrop(_loadingRoot);

            var card = MakeRounded(_loadingRoot, "LoadingCard", ColPanel, 28);
            Center(card, 460, 300);
            AddShadow(card);

            var track = MakeImage(card.gameObject, "SpinnerTrack", new Color(1, 1, 1, 0.08f));
            UiGeneratedSprite.Attach(track, UiGeneratedSprite.Kind.Ring);
            CenterInParent(track.rectTransform, new Vector2(0.5f, 0.62f), 78);

            var arc = MakeImage(card.gameObject, "Spinner", ColAccent);
            UiGeneratedSprite.Attach(arc, UiGeneratedSprite.Kind.Ring);
            arc.type = Image.Type.Filled;
            arc.fillMethod = Image.FillMethod.Radial360;
            arc.fillClockwise = true;
            arc.fillAmount = 0.28f;
            _spinner = arc.rectTransform;
            CenterInParent(_spinner, new Vector2(0.5f, 0.62f), 78);

            _loadingText = MakeText(card.gameObject, "LoadingText", CloudSaveLocale.Loading(),
                ColText, 30, TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.40f), Vector2.zero, Vector2.zero);
            _loadingText.fontStyle = FontStyles.Bold;

            _loadingRoot.SetActive(false);
        }

        static void CenterInParent(RectTransform rt, Vector2 anchor, float size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = Vector2.zero;
        }

        void BuildToast(Canvas canvas)
        {
            var safeGo = new GameObject("ToastSafeArea", typeof(RectTransform));
            safeGo.transform.SetParent(canvas.transform, false);
            var safe = Stretch((RectTransform)safeGo.transform);
            safe.gameObject.AddComponent<UiSafeArea>();

            var pill = MakeRounded(safe.gameObject, "Toast", ColSuccess, Mathf.Max(18, Radius));
            _toastRoot = pill.gameObject;
            _toastBg = pill.GetComponent<Image>();
            pill.anchorMin = pill.anchorMax = new Vector2(0.5f, 0f);
            pill.pivot = new Vector2(0.5f, 0f);
            pill.sizeDelta = new Vector2(760, 96);
            pill.anchoredPosition = new Vector2(0, 40);

            var cg = _toastRoot.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var iconGo = new GameObject("ToastIcon", typeof(RectTransform));
            iconGo.transform.SetParent(_toastRoot.transform, false);
            _toastIcon = (RectTransform)iconGo.transform;
            _toastIcon.anchorMin = _toastIcon.anchorMax = new Vector2(0f, 0.5f);
            _toastIcon.pivot = new Vector2(0.5f, 0.5f);
            _toastIcon.sizeDelta = new Vector2(48, 48);
            _toastIcon.anchoredPosition = new Vector2(50, 0);

            _toastText = MakeText(_toastRoot, "ToastText", "", Color.white, 27, TextAlignmentOptions.Left,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(90, 6), new Vector2(-20, -6));
            _toastText.fontStyle = FontStyles.Bold;
            _toastText.enableWordWrapping = false;
            _toastText.overflowMode = TextOverflowModes.Ellipsis;

            _toastRoot.SetActive(false);
        }

        void BuildConflictDialog(Canvas canvas)
        {
            var overlay = MakeImage(canvas.gameObject, "Conflict", ColOverlay);
            Stretch(overlay);
            _conflictRoot = overlay.gameObject;
            _conflictCg = _conflictRoot.AddComponent<CanvasGroup>();
            UiGeneratedSprite.AddBackdrop(_conflictRoot);

            _conflictCard = MakeRounded(_conflictRoot, "ConflictCard", ColPanel, Radius);
            Center(_conflictCard, 900, 760);
            AddShadow(_conflictCard);
            var card = _conflictCard.gameObject;

            var badge = UiIcons.Build(card, UiIcon.Cloud, ColAccent, 80f);
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.92f);
            badge.anchoredPosition = Vector2.zero;

            _conflictTitle = MakeText(card, "Title", CloudSaveLocale.ConflictTitleCloud(),
                ColText, 38, TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.88f), Vector2.zero, Vector2.zero);
            _conflictTitle.fontStyle = FontStyles.Bold;

            MakeText(card, "Subtitle", CloudSaveLocale.ConflictChoose(),
                ColTextDim, 25, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.77f), Vector2.zero, Vector2.zero);

            int infoRadius = Mathf.Max(12, Radius - 8);
            _localCardRt = MakeRounded(card, "LocalCard", ColLocalCard, infoRadius);
            _localCardRt.anchorMin = new Vector2(0.05f, 0.30f);
            _localCardRt.anchorMax = new Vector2(0.485f, 0.63f);
            _localCardRt.offsetMin = _localCardRt.offsetMax = Vector2.zero;
            var li = UiIcons.Build(_localCardRt.gameObject, UiIcon.Device, ColText, 46f);
            li.anchorMin = li.anchorMax = new Vector2(0.5f, 0.80f); li.anchoredPosition = Vector2.zero;
            MakeText(_localCardRt.gameObject, "LocalCaption", CloudSaveLocale.ConflictLocal(), ColTextDim, 20,
                TextAlignmentOptions.Center, new Vector2(0, 0.44f), new Vector2(1, 0.62f), Vector2.zero, Vector2.zero);
            _localInfoText = MakeText(_localCardRt.gameObject, "LocalInfo", "", ColText, 24, TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.44f), Vector2.zero, Vector2.zero);

            _cloudCardRt = MakeRounded(card, "CloudCard", ColCloudCard, infoRadius);
            _cloudCardRt.anchorMin = new Vector2(0.515f, 0.30f);
            _cloudCardRt.anchorMax = new Vector2(0.95f, 0.63f);
            _cloudCardRt.offsetMin = _cloudCardRt.offsetMax = Vector2.zero;
            var ci = UiIcons.Build(_cloudCardRt.gameObject, UiIcon.Cloud, ColText, 46f);
            ci.anchorMin = ci.anchorMax = new Vector2(0.5f, 0.80f); ci.anchoredPosition = Vector2.zero;
            MakeText(_cloudCardRt.gameObject, "CloudCaption", CloudSaveLocale.ConflictCloud(), ColTextDim, 20,
                TextAlignmentOptions.Center, new Vector2(0, 0.44f), new Vector2(1, 0.62f), Vector2.zero, Vector2.zero);
            _cloudInfoText = MakeText(_cloudCardRt.gameObject, "CloudInfo", "", ColText, 24, TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.44f), Vector2.zero, Vector2.zero);

            MakeButton(card, "BtnLocal", CloudSaveLocale.BtnKeepLocal(), ColLocalCard, ColText, false,
                new Vector2(0.05f, 0.06f), new Vector2(0.485f, 0.24f),
                () => ResolveConflict(CloudConflictChoice.UseLocal));
            MakeButton(card, "BtnCloud", CloudSaveLocale.BtnUseCloud(), ColAccent, Color.white, true,
                new Vector2(0.515f, 0.06f), new Vector2(0.95f, 0.24f),
                () => ResolveConflict(CloudConflictChoice.UseCloud));

            _conflictRoot.SetActive(false);
        }

        // ── low-level builders ────────────────────────────────────────────

        static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }
        static RectTransform Stretch(Image img) => Stretch(img.rectTransform);

        static void Center(RectTransform rt, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
        }

        Image MakeImage(GameObject parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        RectTransform MakeRounded(GameObject parent, string name, Color color, int radius)
        {
            var img = MakeImage(parent, name, color);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            UiGeneratedSprite.Attach(img, UiGeneratedSprite.Kind.RoundedRect, radius);
            return img.rectTransform;
        }

        void AddShadow(RectTransform card)
        {
            var img = MakeImage(card.parent.gameObject, card.name + "Shadow", new Color(0, 0, 0, 0.35f));
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            UiGeneratedSprite.Attach(img, UiGeneratedSprite.Kind.Shadow, 34);
            var rt = img.rectTransform;
            rt.anchorMin = card.anchorMin; rt.anchorMax = card.anchorMax; rt.pivot = card.pivot;
            rt.sizeDelta = card.sizeDelta + new Vector2(46, 46);
            rt.anchoredPosition = card.anchoredPosition + new Vector2(0, -10);
            rt.SetSiblingIndex(card.GetSiblingIndex());
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
            if (ThemeFont != null) txt.font = ThemeFont;
            txt.text = content;
            txt.color = color;
            txt.fontSize = fontSize;
            txt.alignment = alignment;
            txt.raycastTarget = false;
            return txt;
        }

        void MakeButton(GameObject parent, string name, string label, Color bgColor, Color textColor,
            bool filled, Vector2 anchorMin, Vector2 anchorMax, Action onClick)
        {
            var rt = MakeRounded(parent, name, filled ? bgColor : new Color(1, 1, 1, 0.06f), 22);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = rt.GetComponent<Image>();

            if (!filled)
            {
                var outline = rt.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1, 1, 1, 0.20f);
                outline.effectDistance = new Vector2(2, -2);
            }

            var lbl = MakeText(rt.gameObject, "Label", label, textColor, 28, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
            lbl.fontStyle = FontStyles.Bold;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = filled ? new Color(1.1f, 1.1f, 1.1f, 1f) : new Color(1f, 1f, 1f, 2.6f),
                pressedColor = filled ? new Color(0.88f, 0.88f, 0.88f, 1f) : new Color(1f, 1f, 1f, 4f),
                selectedColor = Color.white,
                disabledColor = new Color(1, 1, 1, 0.4f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f,
            };
            btn.onClick.AddListener(() => onClick());
        }
    }
}
