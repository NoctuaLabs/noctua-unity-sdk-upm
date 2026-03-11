#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter for the Editor-only mock payment sheet that mimics Google Pay / StoreKit.
    /// Shows product info and allows the developer to confirm or cancel the mock purchase.
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

            Visible = true;

            return await _tcs.Task;
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
