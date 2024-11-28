using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using com.noctuagames.sdk.Events;

namespace com.noctuagames.sdk.UI
{
    internal class EmailConfirmResetPasswordDialogPresenter : Presenter<AuthenticationModel>
    {
        public EventSender EventSender;
     
        private string _credVerifyCode;
        private string _password;
        private string _rePassword;
        private int _credVerifyId;
        private List<TextField> textFields;
        private Button submitButton;

        public void Show(int credVerifyId)
        {

            Visible = true;
            _credVerifyId = credVerifyId;

            Setup();

            EventSender?.Send("reset_password_opened");
        }

        protected override void Attach(){}
        protected override void Detach(){}

        private void Start()
        {
            Setup();
        }

        private void Setup()
        {
            var verificationCodeField = View.Q<TextField>("VerificationCode");
            var passwordField = View.Q<TextField>("PasswordTF");
            var rePasswordField = View.Q<TextField>("RePasswordTF");
            submitButton = View.Q<Button>("ContinueButton");

            verificationCodeField.value = "";
            passwordField.value = "";
            rePasswordField.value = "";

            passwordField.isPasswordField = true;
            rePasswordField.isPasswordField = true;

            verificationCodeField.RegisterValueChangedCallback(evt => OnVerificationCodeValueChanged(verificationCodeField));
            passwordField.RegisterValueChangedCallback(evt => OnPasswordValueChanged(passwordField));
            rePasswordField.RegisterValueChangedCallback(evt => OnRePasswordValueChanged(rePasswordField));
                
            textFields = new List<TextField>
            {
                verificationCodeField,
                passwordField,
                rePasswordField

            };

            Utility.UpdateButtonState(textFields, submitButton);

            View.Q<Button>("ContinueButton").RegisterCallback<ClickEvent>(OnContinueButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<ClickEvent>(OnBackButtonClick);
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

            Utility.UpdateButtonState(textFields, submitButton);
        }

        private void OnPasswordValueChanged(TextField textField)
        {
            HideAllErrors();

            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;
            }
            _password = textField.value;

            Utility.UpdateButtonState(textFields, submitButton);
        }

        private void OnRePasswordValueChanged(TextField textField)
        {
            HideAllErrors();

            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;
            }
            _rePassword = textField.value;

            Utility.UpdateButtonState(textFields, submitButton);
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            Visible = false;
            Model.ShowEmailResetPassword(false);
        }

        private async void OnContinueButtonClick(ClickEvent evt)
        {
            Debug.Log("EmailConfirmResetPasswordDialogPresenter.OnContinueButtonClick()");

            HideAllErrors();

            var spinnerInstance = new Spinner();
            View.Q<Button>("ContinueButton").AddToClassList("hide");
            View.Q<VisualElement>("Spinner").Clear();
            View.Q<VisualElement>("Spinner").Add(spinnerInstance);
            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");

            var verificationId = _credVerifyId;
            var verificationCode = _credVerifyCode;
            var password = _password;
            var rePassword = _rePassword;

            // Validation
            if (string.IsNullOrEmpty(verificationCode)) {
                View.Q<Label>("ErrVerificationCodeEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }

            if (string.IsNullOrEmpty(password)) {
                View.Q<Label>("ErrPasswordEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }

            if (password?.Length < 6) {
                View.Q<Label>("ErrPasswordTooShort").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }


            if (!password.Equals(rePassword)) {
                View.Q<Label>("ErrPasswordMismatch").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }

            try {

                await Model.AuthService.ConfirmResetPasswordAsync(verificationId, verificationCode, password);
                
                EventSender?.Send("reset_password_success");

                Visible = false;
                
                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

            } catch (Exception e) {
                if (e is NoctuaException noctuaEx)
                {
                    Debug.Log("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                    if (noctuaEx.ErrorCode == 2022) {
                        View.Q<Label>("ErrVerificationCodeInvalid").RemoveFromClassList("hide");
                    } else {
                        View.Q<Label>("ErrCode").text = noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message;
                    }
                } else {
                    Debug.Log("Exception: " + e);
                    View.Q<Label>("ErrCode").text = e.Message;
                }
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }
        }   

        private void HideAllErrors()
        {
            // To avoid duplicate classes
            View.Q<Label>("ErrCode").RemoveFromClassList("hide");
            View.Q<Label>("ErrVerificationCodeEmpty").RemoveFromClassList("hide");
            View.Q<Label>("ErrVerificationCodeInvalid").RemoveFromClassList("hide");
            View.Q<Label>("ErrPasswordTooShort").RemoveFromClassList("hide");
            View.Q<Label>("ErrPasswordEmpty").RemoveFromClassList("hide");
            View.Q<Label>("ErrPasswordMismatch").RemoveFromClassList("hide");

            View.Q<Label>("ErrCode").AddToClassList("hide");
            View.Q<Label>("ErrVerificationCodeEmpty").AddToClassList("hide");
            View.Q<Label>("ErrVerificationCodeInvalid").AddToClassList("hide");
            View.Q<Label>("ErrPasswordTooShort").AddToClassList("hide");
            View.Q<Label>("ErrPasswordEmpty").AddToClassList("hide");
            View.Q<Label>("ErrPasswordMismatch").AddToClassList("hide");
        }
    }
}
