using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class EmailVerificationDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();

        private string _email;
        private string _password;
        private int _credVerifyId;
        private string _credVerifyCode;

        private InputFieldNoctua inputVerificationCode;
        private ButtonNoctua buttonVerify;

        protected override void Attach() { }
        protected override void Detach() { }

        public void Show(string email, string password, int verificationId)
        {
            Debug.Log("EmailVerificationDialogPresenter.Show()");
            View.visible = true;

            _email = email;
            _password = password;
            _credVerifyId = verificationId;

            SetupView();
            HideAllErrors();
        }

        private void Start()
        {
            SetupView();
            HideAllErrors();
        }

        private void SetupView()
        {
            panelVE = View.Q<VisualElement>("EmailVerificationDialog");

            inputVerificationCode = new InputFieldNoctua(View.Q<TextField>("VerificationCode"));
            
            inputVerificationCode.textField.RegisterValueChangedCallback(evt => OnValueChanged(inputVerificationCode));
            inputVerificationCode.SetFocus();

            var backButton = View.Q<Button>("BackButton");
            var resendButton = View.Q<Label>("ResendCode");
            buttonVerify = new ButtonNoctua(View.Q<Button>("VerifyButton"));

            resendButton.RegisterCallback<ClickEvent>(OnResendButtonClick);
            backButton.RegisterCallback<ClickEvent>(OnBackButtonClick);
            buttonVerify.button.RegisterCallback<ClickEvent>(OnVerifyButtonClick);
        }

        private void OnValueChanged(InputFieldNoctua _input)
        {
            _input.AdjustLabel();
            HideAllErrors();
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            Visible = false;
            Model.ShowEmailRegistration(false);
        }

        private async void OnResendButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking resend button");

            buttonVerify.ToggleLoading(true);
                        
            View.Q<Label>("ResendingCode").RemoveFromClassList("hide");

            View?.Q<Label>("ResendCode")?.AddToClassList("hide");            
            View?.Q<VisualElement>("DialogContent")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogHeader")?.AddToClassList("hide");
            
            try
            {
                var result = await Model.RegisterWithEmailAsync(_email, _password);
                _log.Debug("RegisterWithPassword verification ID: " + result.Id);

                _credVerifyId = result.Id;

                buttonVerify.ToggleLoading(false);

                View.Q<Label>("ResendingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");                
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");

            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    buttonVerify.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);                    
                }
                else
                {
                    buttonVerify.Error(e.Message);                    
                }

                buttonVerify.ToggleLoading(false);

                View.Q<Label>("ResendingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");                
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
            }

        }

        private async void OnVerifyButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking verify button");

            buttonVerify.ToggleLoading(true);

            View.Q<Label>("VerifyingCode").RemoveFromClassList("hide");
            View?.Q<Label>("ResendCode")?.AddToClassList("hide");            
            View?.Q<VisualElement>("DialogContent")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogHeader")?.AddToClassList("hide");

            try
            {
                if (Model.AuthService.RecentAccount == null ||
                !(Model.AuthService.RecentAccount != null && Model.AuthService.RecentAccount.IsGuest))
                {
                    // If account container is empty or it's not guest, verify directly.
                    await Model.VerifyEmailRegistration(_credVerifyId, _credVerifyCode);
                }
                else
                {
                    // If guest, here will be a confirmation dialog between verification processes.
                    var token = await Model.AuthService.BeginVerifyEmailRegistrationAsync(_credVerifyId, _credVerifyCode);
                    Model.ShowBindConfirmation(token);
                }

                Visible = false;

                buttonVerify.Clear();

                View.Q<Label>("VerifyingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");                
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");

            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    buttonVerify.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);                    
                }
                else
                {
                    buttonVerify.Error(e.Message);                    
                }
                
                buttonVerify.ToggleLoading(false);

                View.Q<Label>("VerifyingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");                
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
            }
        }

        private void HideAllErrors()
        {
            //Normalize border
            inputVerificationCode.Reset();
            buttonVerify.Clear();

            View.Q<Label>("ErrCode").AddToClassList("hide");            
        }
    }
}
