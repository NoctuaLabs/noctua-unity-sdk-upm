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

        internal PaymentUIAdapter(UIFactory uiFactory)
        {
            _uiFactory = uiFactory;
            _completeDialog = uiFactory.Create<CustomPaymentCompleteDialogPresenter, object>(new object());
            _failedDialog = uiFactory.Create<FailedPaymentDialogPresenter, object>(new object());
        }

        public UniTask<string> ShowCustomPaymentCompleteDialog(bool nativePaymentButtonEnabled)
            => _completeDialog.Show(nativePaymentButtonEnabled);

        public UniTask<bool> ShowFailedPaymentDialog(PaymentStatus status)
            => _failedDialog.Show(status);

        public void ShowLoadingProgress(bool show)
            => _uiFactory.ShowLoadingProgress(show);

        public UniTask<bool> ShowRetryDialog(string message, string context = "general")
            => _uiFactory.ShowRetryDialog(message, context);

        public void ShowError(string message)
            => _uiFactory.ShowError(message);

        public void ShowError(LocaleTextKey textKey)
            => _uiFactory.ShowError(textKey);

        public void ShowGeneralNotification(string message, bool isSuccess = false, uint durationMs = 3000)
            => _uiFactory.ShowGeneralNotification(message, isSuccess, durationMs);
    }
}
