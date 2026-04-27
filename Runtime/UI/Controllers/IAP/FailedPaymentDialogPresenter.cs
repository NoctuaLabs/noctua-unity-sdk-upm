using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.UI
{
    /// <summary>
    /// Presenter for the failed payment dialog, showing an error message with options to retry, contact customer service, or exit.
    /// </summary>
    internal class FailedPaymentDialogPresenter : Presenter<object>
    {
        private Button _okButton;
        private Button _csButton;
        private Button _exitButton;
        private Label _messageLabel;

        private readonly ILogger _log = new NoctuaLogger();

        private UniTaskCompletionSource<bool> _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _okButton = View.Q<Button>("OKButton");
            _exitButton = View.Q<Button>("ExitButton");
            _csButton = View.Q<Button>("CSButton");
            _messageLabel = View.Q<Label>("MessageLabel");

            _okButton.RegisterCallback<PointerUpEvent>(OnOKButton);
            _exitButton.RegisterCallback<PointerUpEvent>(OnExitButton);
            _csButton.RegisterCallback<PointerUpEvent>(OnCSButton);
        }

        /// <summary>
        /// Displays the failed payment dialog with a localized message based on the payment status.
        /// </summary>
        /// <param name="status">The payment status that determines the error message displayed.</param>
        /// <returns><c>true</c> if the user chose to retry; <c>false</c> if they exited.</returns>
        public async UniTask<bool> Show(PaymentStatus status)
        {            
            _tcs = new UniTaskCompletionSource<bool>();
            _messageLabel.text = GetTranslation(status);

            Visible = true;

            return await _tcs.Task;
        }

        private void OnOKButton(PointerUpEvent evt)
        {            
            _log.Debug("clicking OK button");

            Visible = false;

            _tcs?.TrySetResult(true);
        }

        private async void OnCSButton(PointerUpEvent evt)
        {
            _log.Debug("clicking CS button");
            
            Visible = false;

            try
            {
                await Noctua.Platform.Content.ShowCustomerService("payment");
            } 
            catch (Exception e) {
                _tcs?.TrySetResult(false);
                _log.Exception(e);
            }

            Visible = true;
        }

        private void OnExitButton(PointerUpEvent evt)
        { 
            _log.Debug("clicking Exit button");

            Visible = false;

            _tcs?.TrySetResult(false);
        }
        
        private string GetTranslation(PaymentStatus status)
        {
            return Locale.GetTranslation($"{GetType().Name}.{status.ToString()}");
        }
    }
}
