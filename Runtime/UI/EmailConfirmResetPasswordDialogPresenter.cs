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

        private List<TextField> _textFields;
        private ButtonNoctua _submitButton;

        private InputFieldNoctua _inputVerificationCode;
        private InputFieldNoctua _inputPassword;
        private InputFieldNoctua _inputRePassword;

        private Button _showPasswordButton;
        private Button _showRePasswordButton;

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

            _inputVerificationCode = new InputFieldNoctua(View.Q<TextField>("VerificationCode"));
            _inputPassword = new InputFieldNoctua(View.Q<TextField>("PasswordTF"));
            _inputRePassword = new InputFieldNoctua(View.Q<TextField>("RePasswordTF"));
            _submitButton = new ButtonNoctua(View.Q<Button>("ContinueButton"));

            _showPasswordButton = View.Q<Button>("ShowPasswordButton");
            _showRePasswordButton = View.Q<Button>("ShowRePasswordButton");

            _inputPassword.textField.isPasswordField = true;
            _inputRePassword.textField.isPasswordField = true;

            _inputVerificationCode.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputVerificationCode));
            _inputPassword.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputPassword));
            _inputRePassword.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputRePassword));

            _inputVerificationCode.SetFocus();
            _inputPassword.SetFocus();
            _inputRePassword.SetFocus();

            _showPasswordButton.RegisterCallback<ClickEvent>(OnToggleShowPassword);
            _showRePasswordButton.RegisterCallback<ClickEvent>(OnToggleShowRePassword);

            _showPasswordButton.RemoveFromClassList("btn-password-hide");
            _showRePasswordButton.RemoveFromClassList("btn-password-hide");

            _textFields = new List<TextField>
            {
                _inputVerificationCode.textField,
                _inputPassword.textField,
                _inputRePassword.textField

            };

            Utility.UpdateButtonState(_textFields, _submitButton.button);

            _submitButton.button.RegisterCallback<ClickEvent>(OnContinueButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<ClickEvent>(OnBackButtonClick);
        }

        public void OnToggleShowPassword(ClickEvent evt)
        {
            _inputPassword.textField.Blur();
            _inputPassword.textField.isPasswordField = !_inputPassword.textField.isPasswordField;

            if (_inputPassword.textField.isPasswordField)
            {
                _showPasswordButton.RemoveFromClassList("btn-password-hide");
            }
            else
            {
                _showPasswordButton.AddToClassList("btn-password-hide");
            }

            TouchScreenKeyboard.hideInput = true;
        }

        public void OnToggleShowRePassword(ClickEvent evt)
        {
            _inputRePassword.textField.Blur();
            _inputRePassword.textField.isPasswordField = !_inputRePassword.textField.isPasswordField;

            if (_inputRePassword.textField.isPasswordField)
            {
                _showRePasswordButton.RemoveFromClassList("btn-password-hide");
            }
            else
            {
                _showRePasswordButton.AddToClassList("btn-password-hide");
            }

            TouchScreenKeyboard.hideInput = true;
        }

        private void OnValueChanged(InputFieldNoctua input)
        {
            input.AdjustLabel();
            Utility.UpdateButtonState(_textFields, _submitButton.button);
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

            _submitButton.ToggleLoading(true);

            var verificationId = _credVerifyId;
            var verificationCode = _inputVerificationCode.text;
            var password = _inputPassword.text;
            var rePassword = _inputRePassword.text;

            // Validation
            if (string.IsNullOrEmpty(verificationCode))
            {
                _submitButton.ToggleLoading(false);
                
                _inputVerificationCode.Error("Verification code should not be empty");                
                return;
            }

            if (!string.IsNullOrEmpty(Utility.ValidatePassword(password)))
            {
                _submitButton.ToggleLoading(false);                

                _inputPassword.Error(Utility.ValidatePassword(password));
                return;
            }

            if (!string.IsNullOrEmpty(Utility.ValidateReenterPassword(password, rePassword)))
            {
                _submitButton.ToggleLoading(false);                
                
                _inputRePassword.Error(Utility.ValidateReenterPassword(password, rePassword));
                return;
            }

            try
            {
                await Model.AuthService.ConfirmResetPasswordAsync(verificationId, verificationCode, password);

                EventSender?.Send("reset_password_success");

                Visible = false;

                _submitButton.Clear();

                Model.ShowInfo(Locale.GetTranslation($"{GetType().Name}.SuccessNotification"));
            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    if (noctuaEx.ErrorCode == 2022)
                    {
                        _inputVerificationCode.Error("The verification code is invalid");
                    }
                    else
                    {
                        _submitButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);                        
                    }
                }
                else
                {
                    _submitButton.Error(e.Message);                    
                }

                _submitButton.ToggleLoading(false);
            }
        }

        private void HideAllErrors()
        {
            //Normalize border
            _inputVerificationCode.Reset();
            _inputPassword.Reset();
            _inputRePassword.Reset();

            _submitButton.Clear();
        }
    }
}
