using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.UI
{
    internal class RetryDialogPresenter : Presenter<object>
    {
        private Button _btnRetry;
        private Button _btnClose;
        private Label _message;
        private Label _csInfo;

        private readonly ILogger _log = new NoctuaLogger(typeof(RetryDialogPresenter));

        private UniTaskCompletionSource<bool> _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _btnRetry = View.Q<Button>("RetryButton");
            _btnClose = View.Q<Button>("ExitButton");
            _message = View.Q<Label>("Info");
            _csInfo = View.Q<Label>("CSInfo");

            _btnRetry.RegisterCallback<PointerUpEvent>(RetryDialog);
            _csInfo.RegisterCallback<PointerUpEvent>(OpenCS);
            _btnClose.RegisterCallback<PointerUpEvent>(CloseDialog);
        }

        public async UniTask<bool> Show(string message)
        {            
            _tcs = new UniTaskCompletionSource<bool>();

            Visible = true;

            _message.text = message;

            return await _tcs.Task;
        }

        private void RetryDialog(PointerUpEvent evt)
        {            
            Visible = false;

            _tcs?.TrySetResult(true);
        }

        private async void OpenCS(PointerUpEvent evt)
        {
            _log.Debug("clicking customer Service button");
            
            try
            {
                Visible = false;
                _tcs?.TrySetResult(false);

                await Noctua.Platform.Content.ShowCustomerService();
            } 
            catch (Exception e) {

                Visible = false;
                _tcs?.TrySetResult(false);

                if (e is NoctuaException noctuaEx)
                {
                    _log.Error("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                } else {
                    _log.Error("Exception: " + e);
                }
            }
        }

        private void CloseDialog(PointerUpEvent evt)
        { 
            _log.Debug("On close dialog");

            Visible = false;

            _tcs?.TrySetResult(false);
        }
    }
}
