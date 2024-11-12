using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class EmailVerificationDialogPresenter : Presenter<AuthenticationModel>
    {
        private string _email;
        private string _password;
        private int _credVerifyId;
        private string _credVerifyCode;

        protected override void Attach(){}
        protected override void Detach(){}

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
            var verificationCode = View.Q<TextField>("VerificationCode");
            verificationCode.value = string.Empty;
            verificationCode.Focus();
            verificationCode.RegisterValueChangedCallback(evt => OnVerificationCodeValueChanged(verificationCode));

            var backButton = View.Q<Button>("BackButton");
            var resendButton = View.Q<Label>("ResendCode");
            var verifyButton = View.Q<Button>("VerifyButton");

            resendButton.RegisterCallback<ClickEvent>(OnResendButtonClick);
            backButton.RegisterCallback<ClickEvent>(OnBackButtonClick);
            verifyButton.RegisterCallback<ClickEvent>(OnVerifyButtonClick);
        }

        private void OnVerificationCodeValueChanged(TextField textField)
        {
            HideAllErrors();

            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;
            }
            _credVerifyCode = textField.value;
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            Noctua.CloseKeyboardiOS();
            Visible = false;
            Model.ShowEmailRegistration(false);
        }

        private async void OnResendButtonClick(ClickEvent evt)
        {
            Noctua.CloseKeyboardiOS();

            var spinnerInstance = new Spinner();
            View.Q<VisualElement>("Spinner").Clear();
            View.Q<VisualElement>("Spinner").Add(spinnerInstance);
            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");
            View.Q<Label>("ResendingCode").RemoveFromClassList("hide");

            View?.Q<Label>("ResendCode")?.AddToClassList("hide");
            View?.Q<Button>("VerifyButton")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogContent")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogHeader")?.AddToClassList("hide");
            try {
                var result = await Model.RegisterWithEmailAsync(_email, _password);
                Debug.Log("RegisterWithPassword verification ID: " + result.Id);

                _credVerifyId = result.Id;

                View?.Q<VisualElement>("Spinner")?.AddToClassList("hide");
                View.Q<Label>("ResendingCode").AddToClassList("hide");

                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<Button>("VerifyButton")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");

            } catch (Exception e) {
                if (e is NoctuaException noctuaEx)
                {
                    Debug.Log("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                    View.Q<Label>("ErrCode").text = noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message;
                } else {
                    Debug.Log("Exception: " + e);
                    View.Q<Label>("ErrCode").text = e.Message;
                }

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");

                View?.Q<VisualElement>("Spinner")?.AddToClassList("hide");
                View.Q<Label>("ResendingCode").AddToClassList("hide");

                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<Button>("VerifyButton")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
            }
            
        }

        private async void OnVerifyButtonClick(ClickEvent evt)
        {
            Noctua.CloseKeyboardiOS();
            
            var spinnerInstance = new Spinner();
            View.Q<VisualElement>("Spinner").Clear();
            View.Q<VisualElement>("Spinner").Add(spinnerInstance);
            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");
            View.Q<Label>("VerifyingCode").RemoveFromClassList("hide");

            View?.Q<Label>("ResendCode")?.AddToClassList("hide");
            View?.Q<Button>("VerifyButton")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogContent")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogHeader")?.AddToClassList("hide");
            try {
                await Model.VerifyEmailRegistration(_credVerifyId, _credVerifyCode);

                Visible = false;

                View?.Q<VisualElement>("Spinner")?.AddToClassList("hide");
                View.Q<Label>("VerifyingCode").AddToClassList("hide");

                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<Button>("VerifyButton")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");

            } catch (Exception e) {
                if (e is NoctuaException noctuaEx)
                {
                    Debug.Log("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                    View.Q<Label>("ErrCode").text = noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message;
                } else {
                    Debug.Log("Exception: " + e);
                    View.Q<Label>("ErrCode").text = e.Message;
                }

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");

                View?.Q<VisualElement>("Spinner")?.AddToClassList("hide");
                View.Q<Label>("VerifyingCode").AddToClassList("hide");

                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<Button>("VerifyButton")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
            }
        }

        private void HideAllErrors()
        {
            // To avoid duplicate classes
            View.Q<Label>("ErrCode").RemoveFromClassList("hide");
            View.Q<Label>("ErrEmailInvalid").RemoveFromClassList("hide");
            View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");
            View.Q<Label>("ErrPasswordTooShort").RemoveFromClassList("hide");
            View.Q<Label>("ErrPasswordEmpty").RemoveFromClassList("hide");
            View.Q<Label>("ErrPasswordMismatch").RemoveFromClassList("hide");

            View.Q<Label>("ErrCode").AddToClassList("hide");
            View.Q<Label>("ErrEmailInvalid").AddToClassList("hide");
            View.Q<Label>("ErrEmailEmpty").AddToClassList("hide");
            View.Q<Label>("ErrPasswordTooShort").AddToClassList("hide");
            View.Q<Label>("ErrPasswordEmpty").AddToClassList("hide");
            View.Q<Label>("ErrPasswordMismatch").AddToClassList("hide");
        }
    }
}
