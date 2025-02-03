using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.UI
{
    internal class StartGameErrorDialogPresenter : Presenter<object>
    {
        private Button _quitButton;
        private Button _csButton;
        private Label _errorLabel;
        private Label _messageLabel;

        private readonly ILogger _log = new NoctuaLogger();

        private UniTaskCompletionSource _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _quitButton = View.Q<Button>("QuitButton");
            _csButton = View.Q<Button>("CSButton");
            _errorLabel = View.Q<Label>("ErrorLabel");
            _messageLabel = View.Q<Label>("MessageLabel");

            _quitButton.RegisterCallback<PointerUpEvent>(OnQuitButton);
            _csButton.RegisterCallback<PointerUpEvent>(OnCSButton);
        }

        public async UniTask Show(string errorMessage)
        {
            _tcs = new UniTaskCompletionSource();
            _errorLabel.text = errorMessage;

            if (errorMessage.Contains("Networking"))
            {
                _messageLabel.text = "Unable to reach the server. Please check your internet connection and try again. If the issue continues, please switch to a different network and try again.";
            }

            Visible = true;

            await _tcs.Task;
            
            Application.Quit();
        }

        private void OnQuitButton(PointerUpEvent evt)
        {            
            _log.Debug("clicking quit button");

            Visible = false;

            _tcs?.TrySetResult();
        }

        private async void OnCSButton(PointerUpEvent evt)
        {
            _log.Debug("clicking CS button");
            
            Visible = false;

            try
            {
                await Noctua.Platform.Content.ShowCustomerService();
            } 
            catch (Exception e)
            {
                _tcs?.TrySetResult();
                _log.Exception(e);
            }

            Visible = true;
        }
    }
}
