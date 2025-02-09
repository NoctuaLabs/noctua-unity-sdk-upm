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
        private Button _btnNativePayment;
        private Label _message;

        private readonly ILogger _log = new NoctuaLogger(typeof(CustomPaymentCompleteDialogPresenter));

        private UniTaskCompletionSource<string> _tcs;

        protected override void Attach()
        {}

        protected override void Detach()
        {}

        private void Start()
        {
            _btnComplete = View.Q<Button>("CustomPaymentCompleteButton");
            _btnClose = View.Q<Button>("CustomPaymentExitButton");
            _btnNativePayment = View.Q<Button>("PayWithNativePaymentButton");
            _btnCustomerService = View.Q<Button>("CustomPaymentCSButton");
            _message = View.Q<Label>("Info");

            _btnComplete.RegisterCallback<PointerUpEvent>(CustomPaymentCompleteDialog);
            _btnClose.RegisterCallback<PointerUpEvent>(CloseDialog);
            _btnNativePayment.RegisterCallback<PointerUpEvent>(PurchaseUsingNativePayment);
            _btnCustomerService.RegisterCallback<PointerUpEvent>(OpenCS);
        }

        public async UniTask<string> Show(bool nativePaymentButtonEnabled)
        {            
            _tcs = new UniTaskCompletionSource<string>();

            Visible = true;
            if (nativePaymentButtonEnabled)
            {
                View.Q<VisualElement>("Separator").RemoveFromClassList("hide");
                _btnNativePayment.RemoveFromClassList("hide");
#if UNITY_ANDROID
                _btnNativePayment.text = Locale.GetTranslation("CustomPaymentCompleteDialogPresenter.PayWithPlaystore");
#elif UNITY_IOS
                _btnNativePayment.text = Locale.GetTranslation("CustomPaymentCompleteDialogPresenter.PayWithAppstore");
#endif
            } else {
                View.Q<VisualElement>("Separator").AddToClassList("hide");
                _btnNativePayment.AddToClassList("hide");
                View.Q<VisualElement>("Separator").AddToClassList("hide");
                _btnNativePayment.AddToClassList("hide");
            }

            return await _tcs.Task;
        }

        private void CustomPaymentCompleteDialog(PointerUpEvent evt)
        {            
            Visible = false;

            _tcs?.TrySetResult("continue_verify");
        }

        private async void OpenCS(PointerUpEvent evt)
        {
            _log.Debug("clicking customer Service button");
            
            Visible = false;

            try
            {
                await Noctua.Platform.Content.ShowCustomerService("custom_payment");
            } 
            catch (Exception e) {
                _tcs?.TrySetResult("error");

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

            // We don't want to retry the payment at close dialog
            _tcs?.TrySetResult("cancel");
        }

        private void PurchaseUsingNativePayment(PointerUpEvent evt)
        { 
            _log.Debug("On purchase with native payment");

            Visible = false;

            // We don't want to retry the payment at close dialog
            _tcs?.TrySetResult("native_payment");
        }
    }
}
