#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter for the Editor-only mock payment sheet that mimics Google Pay / StoreKit.
    /// Shows product info and allows the developer to confirm or cancel the mock purchase.
    /// Supports both portrait and landscape orientations.
    /// </summary>
    internal class EditorPaymentSheetPresenter : Presenter<object>
    {
        private Button _payButton;
        private Button _cancelButton;
        private Button _closeButton;
        private Label _productIdLabel;
        private Label _priceLabel;

        private readonly ILogger _log = new NoctuaLogger(typeof(EditorPaymentSheetPresenter));

        private UniTaskCompletionSource<bool> _tcs;

        // Elements that receive the landscape class
        private VisualElement _sheet;
        private VisualElement _header;
        private VisualElement _divider1;
        private VisualElement _divider2;
        private VisualElement _content;
        private VisualElement _infoLeft;
        private VisualElement _infoRight;
        private VisualElement _productSection;
        private VisualElement _priceSection;
        private VisualElement _methodSection;
        private VisualElement _buttons;
        private Label _title;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _payButton = View.Q<Button>("PayButton");
            _cancelButton = View.Q<Button>("CancelButton");
            _closeButton = View.Q<Button>("CloseButton");
            _productIdLabel = View.Q<Label>("ProductId");
            _priceLabel = View.Q<Label>("Price");

            _sheet = View.Q<VisualElement>("EditorPaymentSheet");
            _header = View.Q<VisualElement>("EpsHeader");
            _title = View.Q<Label>("EpsTitle");
            _divider1 = View.Q<VisualElement>("EpsDivider1");
            _divider2 = View.Q<VisualElement>("EpsDivider2");
            _content = View.Q<VisualElement>("EpsContent");
            _infoLeft = View.Q<VisualElement>("EpsInfoLeft");
            _infoRight = View.Q<VisualElement>("EpsInfoRight");
            _productSection = View.Q<VisualElement>("EpsProductSection");
            _priceSection = View.Q<VisualElement>("EpsPriceSection");
            _methodSection = View.Q<VisualElement>("EpsMethodSection");
            _buttons = View.Q<VisualElement>("EpsButtons");

            _payButton.RegisterCallback<PointerUpEvent>(OnPay);
            _cancelButton.RegisterCallback<PointerUpEvent>(OnCancel);
            _closeButton.RegisterCallback<PointerUpEvent>(OnCancel);
        }

        /// <summary>
        /// Shows the editor mock payment sheet and waits for the user to confirm or cancel.
        /// </summary>
        /// <param name="productId">The product identifier to display.</param>
        /// <param name="price">The formatted price string.</param>
        /// <param name="currency">The currency code (e.g., "USD").</param>
        /// <returns>true if the user clicked Pay, false if canceled.</returns>
        public async UniTask<bool> Show(string productId, string price, string currency)
        {
            _tcs = new UniTaskCompletionSource<bool>();

            _productIdLabel.text = productId;
            _priceLabel.text = $"{currency} {price}";
            _payButton.text = $"Pay {currency} {price}";

            SetOrientation();

            Visible = true;

            return await _tcs.Task;
        }

        private void SetOrientation()
        {
            bool isLandscape = Screen.width > Screen.height;

            if (isLandscape)
            {
                _sheet?.AddToClassList("landscape");
                _header?.AddToClassList("landscape");
                _title?.AddToClassList("landscape");
                _divider1?.AddToClassList("landscape");
                _divider2?.AddToClassList("landscape");
                _content?.AddToClassList("landscape");
                _infoLeft?.AddToClassList("landscape");
                _infoRight?.AddToClassList("landscape");
                _productSection?.AddToClassList("landscape");
                _priceSection?.AddToClassList("landscape");
                _methodSection?.AddToClassList("landscape");
                _productIdLabel?.AddToClassList("landscape");
                _priceLabel?.AddToClassList("landscape");
                _buttons?.AddToClassList("landscape");
                _payButton?.AddToClassList("landscape");
                _cancelButton?.AddToClassList("landscape");
            }
            else
            {
                _sheet?.RemoveFromClassList("landscape");
                _header?.RemoveFromClassList("landscape");
                _title?.RemoveFromClassList("landscape");
                _divider1?.RemoveFromClassList("landscape");
                _divider2?.RemoveFromClassList("landscape");
                _content?.RemoveFromClassList("landscape");
                _infoLeft?.RemoveFromClassList("landscape");
                _infoRight?.RemoveFromClassList("landscape");
                _productSection?.RemoveFromClassList("landscape");
                _priceSection?.RemoveFromClassList("landscape");
                _methodSection?.RemoveFromClassList("landscape");
                _productIdLabel?.RemoveFromClassList("landscape");
                _priceLabel?.RemoveFromClassList("landscape");
                _buttons?.RemoveFromClassList("landscape");
                _payButton?.RemoveFromClassList("landscape");
                _cancelButton?.RemoveFromClassList("landscape");
            }
        }

        private void OnPay(PointerUpEvent evt)
        {
            _log.Debug("Editor mock payment confirmed");

            Visible = false;

            _tcs?.TrySetResult(true);
        }

        private void OnCancel(PointerUpEvent evt)
        {
            _log.Debug("Editor mock payment canceled");

            Visible = false;

            _tcs?.TrySetResult(false);
        }
    }
}
#endif
