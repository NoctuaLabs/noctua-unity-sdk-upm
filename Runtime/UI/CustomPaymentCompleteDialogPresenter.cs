using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.UI
{
    internal class CustomPaymentCompleteDialogPresenter : Presenter<object>
    {
        private Button _btnComplete;
        private Button _btnCustomerService;
        private Button _btnClose;
        private Label _message;

        private readonly ILogger _log = new NoctuaLogger(typeof(CustomPaymentCompleteDialogPresenter));

        private UniTaskCompletionSource<bool> _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _btnComplete = View.Q<Button>("CustomPaymentCompleteButton");
            _btnClose = View.Q<Button>("CustomPaymentExitButton");
            _btnCustomerService = View.Q<Button>("CustomPaymentCSButton");
            _message = View.Q<Label>("Info");

            _btnComplete.RegisterCallback<PointerUpEvent>(CustomPaymentCompleteDialog);
            _btnClose.RegisterCallback<PointerUpEvent>(CloseDialog);
            _btnCustomerService.RegisterCallback<PointerUpEvent>(OpenCS);
        }

        public async UniTask<bool> Show()
        {            
            _tcs = new UniTaskCompletionSource<bool>();

            Visible = true;

            return await _tcs.Task;
        }

        private void CustomPaymentCompleteDialog(PointerUpEvent evt)
        {            
            Visible = false;

            _tcs?.TrySetResult(true);
        }

        private async void OpenCS(PointerUpEvent evt)
        {
            _log.Debug("clicking customer Service button");
            
            Visible = false;

            try
            {
                await Noctua.Platform.Content.ShowCustomerService();
            } 
            catch (Exception e) {
                _tcs?.TrySetResult(false);

                if (e is NoctuaException noctuaEx)
                {
                    _log.Error("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                } else {
                    _log.Error("Exception: " + e);
                }
            }

            Visible = true;
        }

        private void CloseDialog(PointerUpEvent evt)
        { 
            _log.Debug("On close dialog");

            Visible = false;

            _tcs?.TrySetResult(false);
        }
    }
}
