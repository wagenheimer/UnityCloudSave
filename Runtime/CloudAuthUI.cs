using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.CloudSave
{
    [HelpURL("https://github.com/wagenheimer/UnityCloudSave")]
    public class CloudAuthUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Image _overlay;
        [SerializeField] GameObject _cardRoot;
        [SerializeField] TextMeshProUGUI _titleText;
        [SerializeField] TextMeshProUGUI _descriptionText;
        [SerializeField] TextMeshProUGUI _statusText;
        [SerializeField] Image _providerIcon;
        [SerializeField] Button _linkButton;
        [SerializeField] TextMeshProUGUI _linkButtonText;
        [SerializeField] Button _closeButton;
        [SerializeField] TextMeshProUGUI _closeButtonText;

        [Header("Layout")]
        [SerializeField] int _sortOrder = 250;

        [Header("Colors")]
        [SerializeField] Color _overlayColor = new Color(0f, 0f, 0f, 0.70f);

        /// <summary>
        /// Fires when the player clicks the link button. Wire this to your platform-specific
        /// authentication flow (GPGS on Android, Game Center on iOS).
        /// The button is disabled until you call <see cref="SetLinkResult"/>.
        /// </summary>
        public event Action OnLinkRequested;

        /// <summary>Fires when the dialog is hidden (close button or overlay click).</summary>
        public event Action OnDismissed;

        enum AuthState { Anonymous, Linked }
        AuthState _state;

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Call this after the platform auth completes (success or failure).
        /// Re-enables the link button on failure.
        /// </summary>
        public void SetLinkResult(bool success)
        {
            _linkButton.interactable = !success;
        }

        public static CloudAuthUI Create()
        {
            var prefab = Resources.Load<GameObject>("CloudAuthUI");
            if (prefab != null)
            {
                var go = Instantiate(prefab);
                go.name = "CloudAuthUI";
                return go.GetComponent<CloudAuthUI>();
            }

#if UNITY_EDITOR
            return CreateAndSavePrefab();
#else
            var go = new GameObject("CloudAuthUI");
            return go.AddComponent<CloudAuthUI>();
#endif
        }

        public void Show()
        {
            gameObject.SetActive(true);
            RefreshUI();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            OnDismissed?.Invoke();
        }

        // ── Lifecycle ──────────────────────────────────────────────────────

        void Awake()
        {
            if (_cardRoot == null)
                BuildUI();

            _linkButton.onClick.AddListener(OnLinkClicked);
            _closeButton.onClick.AddListener(Hide);
            CloudAuth.OnLinked += OnLinked;
        }

        void OnDestroy()
        {
            CloudAuth.OnLinked -= OnLinked;
        }

        void OnLinked(CloudAuthProvider provider)
        {
            _state = AuthState.Linked;
            RefreshUI();
        }

        void OnLinkClicked()
        {
            _linkButton.interactable = false;
            OnLinkRequested?.Invoke();
        }

        void RefreshUI()
        {
            _titleText.text = CloudSaveLocale.AuthTitle();
            _descriptionText.text = CloudSaveLocale.AuthDescription();

            switch (_state)
            {
                case AuthState.Anonymous:
                    _statusText.text = CloudSaveLocale.AuthStatusAnonymous();
                    _linkButton.interactable = true;
                    _linkButtonText.text = GetPlatformButtonText();
                    break;

                case AuthState.Linked:
                    var providerName = GetCurrentProviderName();
                    _statusText.text = CloudSaveLocale.AuthStatusLinked(providerName);
                    _linkButton.interactable = false;
                    _linkButtonText.text = providerName;
                    break;
            }

            _closeButtonText.text = CloudSaveLocale.AuthBtnClose();
        }

        static string GetPlatformButtonText()
        {
#if UNITY_ANDROID
            return CloudSaveLocale.AuthBtnGoogle();
#elif UNITY_IOS
            return CloudSaveLocale.AuthBtnApple();
#else
            return CloudSaveLocale.AuthBtnSignInApple();
#endif
        }

        static string GetCurrentProviderName()
        {
#if UNITY_ANDROID
            return "Google Play Games";
#elif UNITY_IOS
            return "Game Center";
#else
            return "";
#endif
        }

        // ── Factory (Editor) ───────────────────────────────────────────────

#if UNITY_EDITOR
        static CloudAuthUI CreateAndSavePrefab()
        {
            var go = new GameObject("CloudAuthUI");
            var ui = go.AddComponent<CloudAuthUI>();
            ui.BuildUI();

            var dir = "Assets/Resources";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");

            var path = dir + "/CloudAuthUI.prefab";
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, path);
            DestroyImmediate(go);
            UnityEditor.AssetDatabase.Refresh();

            var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = Instantiate(saved);
            instance.name = "CloudAuthUI";
            return instance.GetComponent<CloudAuthUI>();
        }
#endif

        // ── Procedural UI (fallback) ────────────────────────────────────────

        void BuildUI()
        {
            var canvas = MakeCanvas("CloudAuthCanvas", _sortOrder);

            _overlay = MakePanel(canvas.gameObject, "Overlay", _overlayColor,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();

            var overlayBtn = _overlay.gameObject.AddComponent<Button>();
            overlayBtn.targetGraphic = _overlay;
            overlayBtn.onClick.AddListener(Hide);
            overlayBtn.transition = Selectable.Transition.None;

            _cardRoot = MakePanel(canvas.gameObject, "Card", new Color(0.12f, 0.12f, 0.14f),
                new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.75f), Vector2.zero, Vector2.zero);

            _titleText = MakeText(_cardRoot, "Title", CloudSaveLocale.AuthTitle(),
                Color.white, 34, TextAlignmentOptions.TopCenter,
                new Vector2(0f, 0.80f), new Vector2(1f, 1f),
                new Vector2(16, 0), new Vector2(-16, -8));
            _titleText.fontStyle = FontStyles.Bold;

            _descriptionText = MakeText(_cardRoot, "Description", CloudSaveLocale.AuthDescription(),
                new Color(0.7f, 0.7f, 0.7f), 24, TextAlignmentOptions.TopCenter,
                new Vector2(0f, 0.62f), new Vector2(1f, 0.78f),
                new Vector2(16, 0), new Vector2(-16, 0));

            var statusIcon = MakeIcon(_cardRoot, "ProviderIcon",
                new Vector2(0.42f, 0.48f), new Vector2(0.58f, 0.56f));

            _statusText = MakeText(_cardRoot, "StatusText", CloudSaveLocale.AuthStatusAnonymous(),
                Color.white, 26, TextAlignmentOptions.TopCenter,
                new Vector2(0f, 0.36f), new Vector2(1f, 0.48f),
                Vector2.zero, Vector2.zero);

            MakeButton(_cardRoot, "BtnLink", GetPlatformButtonText(),
                new Color(0.20f, 0.50f, 1.00f), Color.white,
                new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.30f),
                out _linkButton, out _linkButtonText);

            MakeButton(_cardRoot, "BtnClose", CloudSaveLocale.AuthBtnClose(),
                new Color(0.25f, 0.25f, 0.28f), new Color(0.6f, 0.6f, 0.6f),
                new Vector2(0.10f, 0.02f), new Vector2(0.90f, 0.10f),
                out _closeButton, out _closeButtonText);

            _providerIcon = statusIcon.GetComponent<Image>();
        }

        Image MakeIcon(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.5f, 0.5f, 0.5f);
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

        void MakeButton(GameObject parent, string name, string label,
            Color bgColor, Color textColor,
            Vector2 anchorMin, Vector2 anchorMax,
            out Button button, out TextMeshProUGUI buttonText)
        {
            var go = MakePanel(parent, name, bgColor, anchorMin, anchorMax,
                Vector2.zero, Vector2.zero);
            buttonText = MakeText(go, "Label", label, textColor, 26, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
            button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(() => { });
        }

#if UNITY_EDITOR
        [ContextMenu("Setup References from Children")]
        void SetupReferencesFromChildren()
        {
            _overlay = FindChild("Overlay")?.GetComponent<Image>();
            _cardRoot = FindChild("Card");
            _titleText = FindChild("Title")?.GetComponent<TextMeshProUGUI>();
            _descriptionText = FindChild("Description")?.GetComponent<TextMeshProUGUI>();
            _statusText = FindChild("StatusText")?.GetComponent<TextMeshProUGUI>();
            _providerIcon = FindChild("ProviderIcon")?.GetComponent<Image>();
            _linkButton = FindChild("BtnLink")?.GetComponent<Button>();
            _linkButtonText = FindChild("BtnLink")?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            _closeButton = FindChild("BtnClose")?.GetComponent<Button>();
            _closeButtonText = FindChild("BtnClose")?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
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
