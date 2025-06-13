using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class PhoneVerificationDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();

        private string _credVerifyId;
        private string _phoneNumber;
        private string _emailAddress;
        private string _password;
        private Dictionary<string, string> _regExtra;

        private InputFieldNoctua _inputVerificationCode;
        private ButtonNoctua _buttonVerify;

        protected override void Attach() { }
        protected override void Detach() { }

        public void Show( string verificationId, string phoneNumber, string emailAddress, string password, Dictionary<string, string> regExtra)
        {
            Debug.Log("PhoneVerificationDialogPresenter.Show()");
            View.visible = true;

            _credVerifyId = verificationId;
            _phoneNumber = phoneNumber;
            _emailAddress = emailAddress;
            _password = password;
            _regExtra = regExtra;

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
            panelVE = View.Q<VisualElement>("PhoneVerificationDialog");

            _inputVerificationCode = new InputFieldNoctua(View.Q<TextField>("VerificationCode"));
            
            _inputVerificationCode.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputVerificationCode));
            _inputVerificationCode.SetFocus();

            var backButton = View.Q<Button>("BackButton");
            var resendButton = View.Q<Label>("ResendCode");
            _buttonVerify = new ButtonNoctua(View.Q<Button>("VerifyButton"));

            resendButton.RegisterCallback<ClickEvent>(OnResendButtonClick);
            backButton.RegisterCallback<ClickEvent>(OnBackButtonClick);
            _buttonVerify.button.RegisterCallback<PointerUpEvent>(OnVerifyButtonClick);
        }

        private void OnValueChanged(InputFieldNoctua input)
        {
            input.AdjustLabel();
            HideAllErrors();
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            Visible = false;
            Model.ShowEmailRegistration(false);
        }

        private async void OnResendButtonClick(ClickEvent evt)
        {
            HideAllErrors();

            _log.Debug("clicking resend button");

            _buttonVerify.ToggleLoading(true);
                        
            View.Q<Label>("ResendingCode").RemoveFromClassList("hide");

            View?.Q<Label>("ResendCode")?.AddToClassList("hide");            
            View?.Q<VisualElement>("DialogContent")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogHeader")?.AddToClassList("hide");
            
            try
            {
                var result = await Model.AuthService.RegisterWithEmailSendPhoneNumberVerificationAsync(_phoneNumber);

                _credVerifyId = result.VerificationId;

                _log.Debug($"Resent verification code with ID: {_credVerifyId}");

                _buttonVerify.ToggleLoading(false);

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
                    _buttonVerify.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);                    
                }
                else
                {
                    _buttonVerify.Error(e.Message);                    
                }

                _buttonVerify.ToggleLoading(false);

                View.Q<Label>("ResendingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");                
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
            }

        }

        private async void OnVerifyButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking verify button");

            _buttonVerify.ToggleLoading(true);

            View.Q<Label>("VerifyingCode").RemoveFromClassList("hide");
            View?.Q<Label>("ResendCode")?.AddToClassList("hide");            
            View?.Q<VisualElement>("DialogContent")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogHeader")?.AddToClassList("hide");
            
            var verificationCode = _inputVerificationCode.textField.value;

            try
            {
                _log.Debug($"Verifying phone number with code: {verificationCode} and ID: {_credVerifyId}");

                var result = await Model.AuthService.RegisterWithEmailVerifyPhoneNumberAsync(_credVerifyId, verificationCode);

                _log.Debug($"Verified phone number with ID: {_credVerifyId}");

                _buttonVerify.Clear();

                View.Q<Label>("VerifyingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");

                CredentialVerification resultCredential;

                resultCredential = await Model.AuthService.RegisterWithEmailAsync(_emailAddress, _password, _regExtra);

                Model.ShowEmailVerification(_emailAddress, _password, resultCredential.Id, _regExtra);

                Visible = false;

            }
            catch (Exception e)
            {
                _log.Debug($"Error: {e.Message}");

                if (e is NoctuaException noctuaEx)
                {
                    _buttonVerify.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);
                }
                else
                {
                    _buttonVerify.Error(e.Message);
                }

                _buttonVerify.ToggleLoading(false);

                View.Q<Label>("VerifyingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
            }
        }

        private void HideAllErrors()
        {            
            _inputVerificationCode.Reset();
            _buttonVerify.Clear();         
        }
    }
}
