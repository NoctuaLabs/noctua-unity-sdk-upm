using com.noctuagames.sdk.UI;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Adapts the concrete UI presenters and UIFactory into the <see cref="IPaymentUI"/> interface
    /// for consumption by NoctuaIAPService, decoupling the presenter layer from direct UI creation.
    /// </summary>
    internal class PaymentUIAdapter : IPaymentUI
    {
        private readonly CustomPaymentCompleteDialogPresenter _completeDialog;
        private readonly FailedPaymentDialogPresenter _failedDialog;
        private readonly UIFactory _uiFactory;
#if UNITY_EDITOR
        private readonly EditorPaymentSheetPresenter _editorPaymentSheet;
#endif

        /// <summary>Creates the adapter, pre-building payment dialog presenters.</summary>
        internal PaymentUIAdapter(UIFactory uiFactory)
        {
            _uiFactory = uiFactory;
            _completeDialog = uiFactory.Create<CustomPaymentCompleteDialogPresenter, object>(new object());
            _failedDialog = uiFactory.Create<FailedPaymentDialogPresenter, object>(new object());
#if UNITY_EDITOR
            _editorPaymentSheet = uiFactory.Create<EditorPaymentSheetPresenter, object>(new object());
#endif
            // Payment dialogs answer the player's Buy tap, which may come from an open campaign
            // popup — keep them above it.
            SetModal(_completeDialog);
            SetModal(_failedDialog);
#if UNITY_EDITOR
            SetModal(_editorPaymentSheet);
#endif
        }

        private static void SetModal(UnityEngine.Component presenter)
        {
            var doc = presenter == null ? null : presenter.GetComponent<UnityEngine.UIElements.UIDocument>();
            if (doc != null) doc.sortingOrder = UIUtility.ModalSortingOrder;
        }

        /// <inheritdoc />
        public UniTask<string> ShowCustomPaymentCompleteDialog(bool nativePaymentButtonEnabled)
            => _completeDialog.Show(nativePaymentButtonEnabled);

        /// <inheritdoc />
        public UniTask<bool> ShowFailedPaymentDialog(PaymentStatus status)
            => _failedDialog.Show(status);

        /// <inheritdoc />
        public void ShowLoadingProgress(bool show)
            => _uiFactory.ShowLoadingProgress(show);

        /// <inheritdoc />
        public UniTask<bool> ShowRetryDialog(string message, string context = "general")
            => _uiFactory.ShowRetryDialog(message, context);

        /// <inheritdoc />
        public void ShowError(string message)
            => _uiFactory.ShowError(message);

        /// <inheritdoc />
        public void ShowError(LocaleTextKey textKey)
            => _uiFactory.ShowError(textKey);

        /// <inheritdoc />
        public void ShowGeneralNotification(string message, bool isSuccess = false, uint durationMs = 3000)
            => _uiFactory.ShowGeneralNotification(message, isSuccess, durationMs);

#if UNITY_EDITOR
        /// <inheritdoc />
        public UniTask<bool> ShowEditorPaymentSheet(string productId, string price, string currency)
            => _editorPaymentSheet.Show(productId, price, currency);
#endif
    }
}
