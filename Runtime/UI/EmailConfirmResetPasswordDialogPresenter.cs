using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using com.noctuagames.sdk.Events;

namespace com.noctuagames.sdk.UI
{
    internal class EmailConfirmResetPasswordDialogPresenter : Presenter<AuthenticationModel>
    {
        public EventSender EventSender;

        private readonly ILogger _log = new NoctuaLogger();

        private int _credVerifyId;

        private List<TextField> textFields;
        private ButtonNoctua submitButton;

        private InputFieldNoctua inputVerificationCode;
        private InputFieldNoctua inputPassword;
        private InputFieldNoctua inputRePassword;

        private Button showPasswordButton;
        private Button showRePasswordButton;

        public void Show(int credVerifyId)
        {

            Visible = true;
            _credVerifyId = credVerifyId;

            Setup();

            EventSender?.Send("reset_password_opened");
        }

        protected override void Attach() { }
        protected override void Detach() { }

        private void Start()
        {
            Setup();
        }

        private void Setup()
        {
            panelVE = View.Q<VisualElement>("Panel");

            inputVerificationCode = new InputFieldNoctua(View.Q<TextField>("VerificationCode"));
            inputPassword = new InputFieldNoctua(View.Q<TextField>("PasswordTF"));
            inputRePassword = new InputFieldNoctua(View.Q<TextField>("RePasswordTF"));
            submitButton = new ButtonNoctua(View.Q<Button>("ContinueButton"));

            showPasswordButton = View.Q<Button>("ShowPasswordButton");
            showRePasswordButton = View.Q<Button>("ShowRePasswordButton");

            inputPassword.textField.isPasswordField = true;
            inputRePassword.textField.isPasswordField = true;

            inputVerificationCode.textField.RegisterValueChangedCallback(evt => OnValueChanged(inputVerificationCode));
            inputPassword.textField.RegisterValueChangedCallback(evt => OnValueChanged(inputPassword));
            inputRePassword.textField.RegisterValueChangedCallback(evt => OnValueChanged(inputRePassword));

            inputVerificationCode.SetFocus();
            inputPassword.SetFocus();
            inputRePassword.SetFocus();

            showPasswordButton.RegisterCallback<ClickEvent>(OnToggleShowPassword);
            showRePasswordButton.RegisterCallback<ClickEvent>(OnToggleShowRePassword);

            showPasswordButton.RemoveFromClassList("btn-password-hide");
            showRePasswordButton.RemoveFromClassList("btn-password-hide");

            textFields = new List<TextField>
            {
                inputVerificationCode.textField,
                inputPassword.textField,
                inputRePassword.textField

            };

            Utility.UpdateButtonState(textFields, submitButton.button);

            submitButton.button.RegisterCallback<ClickEvent>(OnContinueButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<ClickEvent>(OnBackButtonClick);
        }

        public void OnToggleShowPassword(ClickEvent _event)
        {
            inputPassword.textField.Blur();
            inputPassword.textField.isPasswordField = !inputPassword.textField.isPasswordField;

            if (inputPassword.textField.isPasswordField)
            {
                showPasswordButton.RemoveFromClassList("btn-password-hide");
            }
            else
            {
                showPasswordButton.AddToClassList("btn-password-hide");
            }

            TouchScreenKeyboard.hideInput = true;
        }

        public void OnToggleShowRePassword(ClickEvent _event)
        {
            inputRePassword.textField.Blur();
            inputRePassword.textField.isPasswordField = !inputRePassword.textField.isPasswordField;

            if (inputRePassword.textField.isPasswordField)
            {
                showRePasswordButton.RemoveFromClassList("btn-password-hide");
            }
            else
            {
                showRePasswordButton.AddToClassList("btn-password-hide");
            }

            TouchScreenKeyboard.hideInput = true;
        }

        private void OnValueChanged(InputFieldNoctua _input)
        {
            _input.AdjustLabel();
            Utility.UpdateButtonState(textFields, submitButton.button);
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking back button");

            Visible = false;

            Model.ShowEmailResetPassword(false);
        }

        private async void OnContinueButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking continue button");

            HideAllErrors();

            submitButton.ToggleLoading(true);

            var verificationId = _credVerifyId;
            var verificationCode = inputVerificationCode.text;
            var password = inputPassword.text;
            var rePassword = inputRePassword.text;

            // Validation
            if (string.IsNullOrEmpty(verificationCode))
            {
                submitButton.ToggleLoading(false);
                
                inputVerificationCode.Error("Verification code should not be empty");                
                return;
            }

            if (!string.IsNullOrEmpty(Utility.ValidatePassword(password)))
            {
                submitButton.ToggleLoading(false);                

                inputPassword.Error(Utility.ValidatePassword(password));
                return;
            }

            if (!string.IsNullOrEmpty(Utility.ValidateReenterPassword(password, rePassword)))
            {
                submitButton.ToggleLoading(false);                
                
                inputRePassword.Error(Utility.ValidateReenterPassword(password, rePassword));
                return;
            }

            try
            {
                await Model.AuthService.ConfirmResetPasswordAsync(verificationId, verificationCode, password);

                EventSender?.Send("reset_password_success");

                Visible = false;

                submitButton.Clear();

                Model.ShowInfo(Locale.GetTranslation($"{GetType().Name}.SuccessNotification"));
            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    if (noctuaEx.ErrorCode == 2022)
                    {
                        inputVerificationCode.Error("The verification code is invalid");
                    }
                    else
                    {
                        submitButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);                        
                    }
                }
                else
                {
                    submitButton.Error(e.Message);                    
                }

                submitButton.ToggleLoading(false);
            }
        }

        private void HideAllErrors()
        {
            //Normalize border
            inputVerificationCode.Reset();
            inputPassword.Reset();
            inputRePassword.Reset();

            submitButton.Clear();
        }
    }
}
