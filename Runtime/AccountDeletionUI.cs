using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Reusable UI component for In-App Account Deletion (Apple Guideline 5.1.1(v) & Google Play Compliance).
    /// Can be dropped into any Options, Settings or Account screen at Design-Time or generated as a prefab.
    /// </summary>
    [HelpURL("https://github.com/wagenheimer/UnityCloudSave")]
    public class AccountDeletionUI : MonoBehaviour
    {
        [Header("Trigger Button")]
        [SerializeField] private Button _deleteButton;
        [SerializeField] private TextMeshProUGUI _deleteButtonText;

        [Header("Confirmation Modal")]
        [SerializeField] private GameObject _modalRoot;
        [SerializeField] private Image _overlay;
        [SerializeField] private GameObject _cardRoot;
        [SerializeField] private CanvasGroup _modalCg;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _warningText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TextMeshProUGUI _confirmButtonText;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TextMeshProUGUI _cancelButtonText;

        [Header("Rules")]
        [SerializeField] private bool _mobileOnly = true;
        [SerializeField] private bool _autoHideWhenNotSignedIn = true;

        [Header("Colors & Styling")]
        [SerializeField] private Color _buttonColor = new Color(0.72f, 0.20f, 0.20f, 1f);
        [SerializeField] private Color _buttonTextColor = Color.white;
        [SerializeField] private Color _overlayColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color _modalBgColor = new Color(0.12f, 0.14f, 0.18f, 0.98f);

        public event Action OnAccountDeleted;
        public event Action OnDeletionCancelled;

        public Button DeleteButton => _deleteButton;
        public GameObject ModalRoot => _modalRoot;

        private void Awake()
        {
            if (_deleteButton != null)
                _deleteButton.onClick.AddListener(OpenConfirmationModal);

            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(OnConfirmClicked);

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(CloseConfirmationModal);

            if (_modalRoot != null)
                _modalRoot.SetActive(false);
        }

        private void OnEnable()
        {
            UpdateVisibility();
        }

        private void Update()
        {
            UpdateVisibility();
        }

        public void UpdateVisibility()
        {
            if (_deleteButton == null) return;

            bool visible = true;
            if (_mobileOnly && !Application.isMobilePlatform && !Application.isEditor)
                visible = false;

            if (_autoHideWhenNotSignedIn && !CloudAuth.IsSignedIn)
                visible = false;

            if (_deleteButton.gameObject.activeSelf != visible)
                _deleteButton.gameObject.SetActive(visible);
        }

        public void OpenConfirmationModal()
        {
            if (_modalRoot != null)
            {
                _modalRoot.SetActive(true);
                if (_modalCg != null)
                {
                    _modalCg.alpha = 1f;
                }
            }
        }

        public void CloseConfirmationModal()
        {
            if (_modalRoot != null)
                _modalRoot.SetActive(false);
            OnDeletionCancelled?.Invoke();
        }

        private async void OnConfirmClicked()
        {
            if (_confirmButton != null) _confirmButton.interactable = false;

            try
            {
                bool success = await CloudAuth.DeleteAccountAsync();
                if (success)
                {
                    CloseConfirmationModal();
                    if (_deleteButton != null) _deleteButton.gameObject.SetActive(false);
                    OnAccountDeleted?.Invoke();
                }
            }
            finally
            {
                if (_confirmButton != null) _confirmButton.interactable = true;
            }
        }

#if UNITY_EDITOR
        public void BuildDefaultUI()
        {
            BuildTriggerButton();
            BuildModal();
            SetupReferences();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void BuildTriggerButton()
        {
            var btnGo = new GameObject("DeleteAccountButton", typeof(RectTransform));
            btnGo.transform.SetParent(transform, false);
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(25f, 20f);
            rt.sizeDelta = new Vector2(170f, 40f);

            var img = btnGo.AddComponent<Image>();
            img.color = _buttonColor;
            img.type = Image.Type.Sliced;
            UiGeneratedSprite.Attach(img, UiGeneratedSprite.Kind.RoundedRect, 10);

            _deleteButton = btnGo.AddComponent<Button>();
            _deleteButton.targetGraphic = img;

            var txtGo = new GameObject("Label", typeof(RectTransform));
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(8, 4);
            txtRt.offsetMax = new Vector2(-8, -4);

            _deleteButtonText = txtGo.AddComponent<TextMeshProUGUI>();
            _deleteButtonText.text = CloudSaveLocale.BtnDeleteAccount();
            _deleteButtonText.color = _buttonTextColor;
            _deleteButtonText.fontSize = 15;
            _deleteButtonText.alignment = TextAlignmentOptions.Center;
            _deleteButtonText.fontStyle = FontStyles.Bold;
        }

        private void BuildModal()
        {
            var modal = new GameObject("DeleteAccountModal", typeof(RectTransform));
            modal.transform.SetParent(transform, false);
            var rt = modal.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _overlay = modal.AddComponent<Image>();
            _overlay.color = _overlayColor;
            UiGeneratedSprite.AddBackdrop(modal);

            _modalRoot = modal;
            _modalCg = modal.AddComponent<CanvasGroup>();

            // Center Card
            var card = new GameObject("Card", typeof(RectTransform));
            card.transform.SetParent(modal.transform, false);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(500f, 280f);
            cardRt.anchoredPosition = Vector2.zero;

            var cardImg = card.AddComponent<Image>();
            cardImg.color = _modalBgColor;
            cardImg.type = Image.Type.Sliced;
            UiGeneratedSprite.Attach(cardImg, UiGeneratedSprite.Kind.RoundedRect, 16);
            _cardRoot = card;

            // Warning Icon
            var iconRt = UiIcons.Build(card, UiIcon.Error, new Color(0.9f, 0.3f, 0.3f), 48f);
            iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.84f);
            iconRt.anchoredPosition = Vector2.zero;

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(card.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.06f, 0.62f);
            titleRt.anchorMax = new Vector2(0.94f, 0.76f);
            titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;
            _titleText = titleGo.AddComponent<TextMeshProUGUI>();
            _titleText.text = CloudSaveLocale.DeleteAccountTitle();
            _titleText.color = Color.white;
            _titleText.fontSize = 22;
            _titleText.fontStyle = FontStyles.Bold;
            _titleText.alignment = TextAlignmentOptions.Center;

            // Warning Description
            var descGo = new GameObject("Description", typeof(RectTransform));
            descGo.transform.SetParent(card.transform, false);
            var descRt = descGo.GetComponent<RectTransform>();
            descRt.anchorMin = new Vector2(0.08f, 0.34f);
            descRt.anchorMax = new Vector2(0.92f, 0.62f);
            descRt.offsetMin = descRt.offsetMax = Vector2.zero;
            _warningText = descGo.AddComponent<TextMeshProUGUI>();
            _warningText.text = CloudSaveLocale.DeleteAccountWarning();
            _warningText.color = new Color(0.75f, 0.78f, 0.85f);
            _warningText.fontSize = 14;
            _warningText.alignment = TextAlignmentOptions.Center;

            // Confirm Delete Button (Red)
            var btnConfGo = new GameObject("BtnConfirmDelete", typeof(RectTransform));
            btnConfGo.transform.SetParent(card.transform, false);
            var confRt = btnConfGo.GetComponent<RectTransform>();
            confRt.anchorMin = new Vector2(0.08f, 0.08f);
            confRt.anchorMax = new Vector2(0.48f, 0.26f);
            confRt.offsetMin = confRt.offsetMax = Vector2.zero;
            var confImg = btnConfGo.AddComponent<Image>();
            confImg.color = new Color(0.80f, 0.22f, 0.22f, 1f);
            confImg.type = Image.Type.Sliced;
            UiGeneratedSprite.Attach(confImg, UiGeneratedSprite.Kind.RoundedRect, 10);
            _confirmButton = btnConfGo.AddComponent<Button>();
            _confirmButton.targetGraphic = confImg;

            var confTxtGo = new GameObject("Label", typeof(RectTransform));
            confTxtGo.transform.SetParent(btnConfGo.transform, false);
            var confTxtRt = confTxtGo.GetComponent<RectTransform>();
            confTxtRt.anchorMin = Vector2.zero;
            confTxtRt.anchorMax = Vector2.one;
            confTxtRt.offsetMin = confTxtRt.offsetMax = Vector2.zero;
            _confirmDeleteText = confTxtGo.AddComponent<TextMeshProUGUI>();
            _confirmDeleteText.text = CloudSaveLocale.BtnDeleteAccount();
            _confirmDeleteText.color = Color.white;
            _confirmDeleteText.fontSize = 15;
            _confirmDeleteText.fontStyle = FontStyles.Bold;
            _confirmDeleteText.alignment = TextAlignmentOptions.Center;

            // Cancel Button (Dark Blue/Gray)
            var btnCancelGo = new GameObject("BtnCancel", typeof(RectTransform));
            btnCancelGo.transform.SetParent(card.transform, false);
            var cancelRt = btnCancelGo.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.52f, 0.08f);
            cancelRt.anchorMax = new Vector2(0.92f, 0.26f);
            cancelRt.offsetMin = cancelRt.offsetMax = Vector2.zero;
            var cancelImg = btnCancelGo.AddComponent<Image>();
            cancelImg.color = new Color(0.24f, 0.28f, 0.38f, 1f);
            cancelImg.type = Image.Type.Sliced;
            UiGeneratedSprite.Attach(cancelImg, UiGeneratedSprite.Kind.RoundedRect, 10);
            _cancelButton = btnCancelGo.AddComponent<Button>();
            _cancelButton.targetGraphic = cancelImg;

            var cancelTxtGo = new GameObject("Label", typeof(RectTransform));
            cancelTxtGo.transform.SetParent(btnCancelGo.transform, false);
            var cancelTxtRt = cancelTxtGo.GetComponent<RectTransform>();
            cancelTxtRt.anchorMin = Vector2.zero;
            cancelTxtRt.anchorMax = Vector2.one;
            cancelTxtRt.offsetMin = cancelTxtRt.offsetMax = Vector2.zero;
            _cancelText = cancelTxtGo.AddComponent<TextMeshProUGUI>();
            _cancelText.text = CloudSaveLocale.BtnCancel();
            _cancelText.color = Color.white;
            _cancelText.fontSize = 15;
            _cancelText.fontStyle = FontStyles.Bold;
            _cancelText.alignment = TextAlignmentOptions.Center;

            modal.SetActive(false);
        }

        [ContextMenu("Setup References from Children")]
        public void SetupReferences()
        {
            _deleteButton = transform.Find("DeleteAccountButton")?.GetComponent<Button>();
            _deleteButtonText = _deleteButton?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();

            var modal = transform.Find("DeleteAccountModal");
            if (modal != null)
            {
                _modalRoot = modal.gameObject;
                _overlay = modal.GetComponent<Image>();
                _modalCg = modal.GetComponent<CanvasGroup>();
                var card = modal.Find("Card");
                if (card != null)
                {
                    _cardRoot = card.gameObject;
                    _titleText = card.Find("Title")?.GetComponent<TextMeshProUGUI>();
                    _warningText = card.Find("Description")?.GetComponent<TextMeshProUGUI>();
                    _confirmButton = card.Find("BtnConfirmDelete")?.GetComponent<Button>();
                    _confirmDeleteText = _confirmButton?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                    _cancelButton = card.Find("BtnCancel")?.GetComponent<Button>();
                    _cancelText = _cancelButton?.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                }
            }
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}

