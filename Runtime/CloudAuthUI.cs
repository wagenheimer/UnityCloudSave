using System;
using System.Collections;
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
        [SerializeField] CanvasGroup _cg;

        CloudSaveUITheme _theme;
        Color ColOverlay => _theme != null ? _theme.Overlay  : _overlayColor;
        Color ColPanel   => _theme != null ? _theme.Panel    : new Color(0.12f, 0.12f, 0.14f, 0.98f);
        Color ColAccent  => _theme != null ? _theme.Accent   : new Color(0.20f, 0.50f, 1.00f, 1f);
        Color ColText    => _theme != null ? _theme.Text      : Color.white;
        Color ColTextDim => _theme != null ? _theme.TextDim   : new Color(0.68f, 0.68f, 0.72f);
        int Radius       => _theme != null ? Mathf.Clamp(_theme.CornerRadius, 4, 48) : 26;
        bool Anim        => _theme == null || _theme.EnableAnimations;

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
                var instance = Instantiate(prefab);
                instance.name = "CloudAuthUI";
                return instance.GetComponent<CloudAuthUI>();
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
            if (_cg != null && Anim) StartCoroutine(FadeCard());
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            OnDismissed?.Invoke();
        }

        IEnumerator FadeCard()
        {
            _cg.alpha = 0f;
            if (_cardRoot != null) _cardRoot.transform.localScale = Vector3.one * 0.94f;
            for (float t = 0; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.18f);
                k = 1f - (1f - k) * (1f - k);
                _cg.alpha = k;
                if (_cardRoot != null) _cardRoot.transform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, k);
                yield return null;
            }
            _cg.alpha = 1f;
            if (_cardRoot != null) _cardRoot.transform.localScale = Vector3.one;
        }

        // ── Lifecycle ──────────────────────────────────────────────────────

        void Awake()
        {
            _theme = CloudSaveUITheme.Current;

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
            return CloudAuth.Provider switch
            {
                CloudAuthProvider.Facebook => "Facebook",
                CloudAuthProvider.GooglePlayGames => "Google Play Games",
                CloudAuthProvider.Google => "Google",
                CloudAuthProvider.Apple => "Apple",
                CloudAuthProvider.AppleGameCenter => "Game Center",
                _ => ""
            };
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
            var canvas = EnsureCanvas();

            _overlay = MakePanel(canvas.gameObject, "Overlay", ColOverlay,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
            var overlayBtn = _overlay.gameObject.AddComponent<Button>();
            overlayBtn.targetGraphic = _overlay;
            overlayBtn.onClick.AddListener(Hide);
            overlayBtn.transition = Selectable.Transition.None;

            var shadow = MakePanel(canvas.gameObject, "CardShadow", new Color(0, 0, 0, 0.35f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, shadow: 36);
            var shRt = shadow.GetComponent<RectTransform>();
            shRt.pivot = new Vector2(0.5f, 0.5f);
            shRt.sizeDelta = new Vector2(940, 1020);
            shRt.anchoredPosition = new Vector2(0, -12);

            _cardRoot = MakePanel(canvas.gameObject, "Card", ColPanel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, rounded: Radius);
            var cardRt = _cardRoot.GetComponent<RectTransform>();
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(900, 980);
            cardRt.anchoredPosition = Vector2.zero;
            _cg = _cardRoot.AddComponent<CanvasGroup>();

            var badge = UiIcons.Build(_cardRoot, UiIcon.Cloud, ColAccent, 90f);
            badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.90f);
            badge.anchoredPosition = Vector2.zero;

            _titleText = MakeText(_cardRoot, "Title", CloudSaveLocale.AuthTitle(),
                ColText, 36, TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.90f), Vector2.zero, Vector2.zero);
            _titleText.fontStyle = FontStyles.Bold;

            _descriptionText = MakeText(_cardRoot, "Description", CloudSaveLocale.AuthDescription(),
                ColTextDim, 24, TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.78f), Vector2.zero, Vector2.zero);

            var iconRt = UiIcons.Build(_cardRoot, UiIcon.Sync, ColAccent, 90f);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.50f);
            iconRt.anchoredPosition = Vector2.zero;
            _providerIcon = iconRt.GetComponentInChildren<Image>();

            _statusText = MakeText(_cardRoot, "StatusText", CloudSaveLocale.AuthStatusAnonymous(),
                ColText, 26, TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.44f), Vector2.zero, Vector2.zero);

            MakeButton(_cardRoot, "BtnLink", GetPlatformButtonText(), ColAccent, Color.white, true,
                new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.29f), out _linkButton, out _linkButtonText);
            MakeButton(_cardRoot, "BtnClose", CloudSaveLocale.AuthBtnClose(), ColPanel, ColTextDim, false,
                new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.15f), out _closeButton, out _closeButtonText);
        }

        Canvas EnsureCanvas()
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                var go = new GameObject("CloudAuthCanvas", typeof(RectTransform));
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
            int rounded = 0, int shadow = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var img = go.AddComponent<Image>();
            img.color = color;
            if (shadow > 0)
            {
                img.type = Image.Type.Sliced;
                img.raycastTarget = false;
                UiGeneratedSprite.Attach(img, UiGeneratedSprite.Kind.Shadow, shadow);
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

        void MakeButton(GameObject parent, string name, string label, Color bgColor, Color textColor,
            bool filled, Vector2 anchorMin, Vector2 anchorMax,
            out Button button, out TextMeshProUGUI buttonText)
        {
            var go = MakePanel(parent, name, filled ? bgColor : new Color(1, 1, 1, 0.06f),
                anchorMin, anchorMax, Vector2.zero, Vector2.zero, rounded: Mathf.Max(12, Radius - 6));
            if (!filled)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = new Color(1, 1, 1, 0.20f);
                o.effectDistance = new Vector2(2, -2);
            }
            buttonText = MakeText(go, "Label", label, textColor, 27, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6));
            buttonText.fontStyle = FontStyles.Bold;
            button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = filled ? new Color(1.1f, 1.1f, 1.1f, 1f) : new Color(1f, 1f, 1f, 2.6f),
                pressedColor = filled ? new Color(0.88f, 0.88f, 0.88f, 1f) : new Color(1f, 1f, 1f, 4f),
                selectedColor = Color.white,
                disabledColor = new Color(1, 1, 1, 0.4f),
                colorMultiplier = 1f,
                fadeDuration = 0.1f,
            };
            button.onClick.AddListener(() => { });
        }

#if UNITY_EDITOR
        [ContextMenu("Setup References from Children")]
        void SetupReferencesFromChildren()
        {
            _overlay = FindChild("Overlay")?.GetComponent<Image>();
            _cardRoot = FindChild("Card");
            _cg = FindChild("Card")?.GetComponent<CanvasGroup>();
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
