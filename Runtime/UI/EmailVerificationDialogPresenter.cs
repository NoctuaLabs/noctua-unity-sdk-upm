using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    public class EmailVerificationDialogPresenter : Presenter<NoctuaBehaviour>
    {
        private UIDocument _uiDoc;

        private string _email;
        private string _password;
        private int _verificationId;

        protected override void Attach(){}
        protected override void Detach(){}

        public void Show(string email, string password, int verificationId)
        {
            Debug.Log("EmailVerificationDialogPresenter.Show()");
            View.visible = true;

            _email = email;
            _password = password;
            _verificationId = verificationId;

            SetupView();
            HideAllErrors();
        }

        private void Awake()
        {
            LoadView();
            SetupView();
            HideAllErrors();
        }

        private void SetupView()
        {
            var verificationCode = View.Q<TextField>("VerificationCode");
            verificationCode.value = string.Empty;
            verificationCode.Focus();

            var backButton = View.Q<Button>("BackButton");
            var resendButton = View.Q<Label>("ResendCode");
            var verifyButton = View.Q<Button>("VerifyButton");

            resendButton.RegisterCallback<ClickEvent>(OnResendButtonClick);
            backButton.RegisterCallback<ClickEvent>(OnBackButtonClick);
            verifyButton.RegisterCallback<ClickEvent>(OnVerifyButtonClick);
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            Visible = false;
            Model.ShowEmailRegisterDialogUI(false);
        }

        private async void OnResendButtonClick(ClickEvent evt)
        {

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
                var result = await Model.AuthService.RegisterWithPassword(_email, _password);
                Debug.Log("RegisterWithPassword verification ID: " + result.Id);

                _verificationId = result.Id;

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

                View?.Q<VisualElement>("Spinner")?.AddToClassList("hide");
                View.Q<Label>("ResendingCode").AddToClassList("hide");

                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<Button>("VerifyButton")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
                return;
            }
            
        }

        private async void OnVerifyButtonClick(ClickEvent evt)
        {
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
                var userBundle = await Model.AuthService.VerifyCredential(_verificationId, View.Q<TextField>("VerificationCode").value);

                Visible = false;

                Model.ShowWelcomeToast(userBundle);

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

                View?.Q<VisualElement>("Spinner")?.AddToClassList("hide");
                View.Q<Label>("VerifyingCode").AddToClassList("hide");

                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<Button>("VerifyButton")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
                return;
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
