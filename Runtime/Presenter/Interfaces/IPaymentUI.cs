using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Abstraction for payment-related UI dialogs and notifications.
    /// Implemented by the View/UI layer, consumed by the IAP presenter.
    /// </summary>
    public interface IPaymentUI
    {
        /// <summary>
        /// Show the custom payment completion dialog.
        /// </summary>
        /// <param name="nativePaymentButtonEnabled">Whether to show native payment fallback button.</param>
        /// <returns>"continue_verify", "cancel", "native_payment", or "error"</returns>
        UniTask<string> ShowCustomPaymentCompleteDialog(bool nativePaymentButtonEnabled);

        /// <summary>
        /// Show the failed payment dialog.
        /// </summary>
        /// <param name="status">The payment failure status to display.</param>
        /// <returns>true if user wants to retry, false otherwise.</returns>
        UniTask<bool> ShowFailedPaymentDialog(PaymentStatus status);

        /// <summary>
        /// Show or hide a loading progress indicator.
        /// </summary>
        void ShowLoadingProgress(bool show);

        /// <summary>
        /// Show a retry dialog with a message. Returns true if user clicks retry.
        /// </summary>
        UniTask<bool> ShowRetryDialog(string message, string context = "general");

        /// <summary>
        /// Show an error notification with a string message.
        /// </summary>
        void ShowError(string message);

        /// <summary>
        /// Show an error notification with a locale text key.
        /// </summary>
        void ShowError(LocaleTextKey textKey);

        /// <summary>
        /// Show a general notification (success or failure).
        /// </summary>
        void ShowGeneralNotification(string message, bool isSuccess = false, uint durationMs = 3000);
    }
}
