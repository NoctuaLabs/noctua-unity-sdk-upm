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
        private string _credVerifyCode;
        private string _password;
        private string _rePassword;
        private int _credVerifyId;
        private List<TextField> textFields;
        private Button submitButton;

        private TextField verificationCodeField;
        private TextField passwordField;
        private TextField rePasswordField;

        private Button showPasswordButton;
        private Button showRePasswordButton;

        private VisualElement panelVE;

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

        private void Update()
        {
            if (panelVE == null) return;

            if (TouchScreenKeyboard.visible && !panelVE.ClassListContains("dialog-box-keyboard-shown"))
            {
                panelVE.AddToClassList("dialog-box-keyboard-shown");
            }

            if (!TouchScreenKeyboard.visible && panelVE.ClassListContains("dialog-box-keyboard-shown"))
            {
                panelVE.RemoveFromClassList("dialog-box-keyboard-shown");
            }
        }

        private void Setup()
        {
            panelVE = View.Q<VisualElement>("Panel");
            verificationCodeField = View.Q<TextField>("VerificationCode");
            passwordField = View.Q<TextField>("PasswordTF");
            rePasswordField = View.Q<TextField>("RePasswordTF");
            submitButton = View.Q<Button>("ContinueButton");

            verificationCodeField.value = "";
            passwordField.value = "";
            rePasswordField.value = "";

            showPasswordButton = View.Q<Button>("ShowPasswordButton");
            showRePasswordButton = View.Q<Button>("ShowRePasswordButton");

            passwordField.isPasswordField = true;
            rePasswordField.isPasswordField = true;

            verificationCodeField.RegisterValueChangedCallback(evt => OnVerificationCodeValueChanged(verificationCodeField));
            passwordField.RegisterValueChangedCallback(evt => OnPasswordValueChanged(passwordField));
            rePasswordField.RegisterValueChangedCallback(evt => OnRePasswordValueChanged(rePasswordField));

            verificationCodeField.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);
            passwordField.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);
            rePasswordField.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);
            verificationCodeField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);
            passwordField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);
            rePasswordField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);

            showPasswordButton.RegisterCallback<PointerUpEvent>(OnToggleShowPassword);
            showRePasswordButton.RegisterCallback<PointerUpEvent>(OnToggleShowRePassword);

            passwordField.hideMobileInput = true;
            rePasswordField.hideMobileInput = true;
            verificationCodeField.hideMobileInput = true;            
            
            showPasswordButton.RemoveFromClassList("btn-password-hide");
            showRePasswordButton.RemoveFromClassList("btn-password-hide");

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

        #region Check Text Field Focus

        public void OnTextFieldFocusChange(FocusInEvent _event)
        {
            HideAllErrors();
            (_event.target as VisualElement).Children().ElementAt(1).AddToClassList("noctua-text-input-focus");
            (_event.target as VisualElement).Q<VisualElement>("title").style.color = ColorModule.white;
        }

        public void OnTextFieldFocusChange(FocusOutEvent _event)
        {
            (_event.target as VisualElement).Children().ElementAt(1).RemoveFromClassList("noctua-text-input-focus");
            (_event.target as VisualElement).Q<VisualElement>("title").style.color = ColorModule.greyInactive;
        }

        public void OnToggleShowPassword(PointerUpEvent _event)
        {
            passwordField.Blur();
            passwordField.isPasswordField = !passwordField.isPasswordField;

            if (passwordField.isPasswordField)
            {
                showPasswordButton.RemoveFromClassList("btn-password-hide");
            }
            else
            {
                showPasswordButton.AddToClassList("btn-password-hide");
            }

            TouchScreenKeyboard.hideInput = true;
        }

        public void OnToggleShowRePassword(PointerUpEvent _event)
        {
            rePasswordField.Blur();
            rePasswordField.isPasswordField = !rePasswordField.isPasswordField;

            if (rePasswordField.isPasswordField)
            {
                showRePasswordButton.RemoveFromClassList("btn-password-hide");
            }
            else
            {
                showRePasswordButton.AddToClassList("btn-password-hide");
            }

            TouchScreenKeyboard.hideInput = true;
        }

        #endregion

        private void OnVerificationCodeValueChanged(TextField textField)
        {
            HideAllErrors();

            _credVerifyCode = textField.value;

            AdjustHideLabelElement(textField);
            Utility.UpdateButtonState(textFields, submitButton);
        }

        private void OnPasswordValueChanged(TextField textField)
        {
            HideAllErrors();
            
            _password = textField.value;

            AdjustHideLabelElement(textField);
            Utility.UpdateButtonState(textFields, submitButton);
        }

        private void OnRePasswordValueChanged(TextField textField)
        {
            HideAllErrors();

            _rePassword = textField.value;

            AdjustHideLabelElement(textField);
            Utility.UpdateButtonState(textFields, submitButton);
        }

        private void AdjustHideLabelElement(TextField textField)
        {
            if (string.IsNullOrEmpty(textField.value))
            {
                textField.labelElement.style.display = DisplayStyle.Flex;
                textField.Q<VisualElement>("title").AddToClassList("hide");
            }
            else
            {
                textField.labelElement.style.display = DisplayStyle.None;
                textField.Q<VisualElement>("title").RemoveFromClassList("hide");
            }
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

            if (View.Q<VisualElement>("Spinner").childCount == 0)
            {                
                View.Q<VisualElement>("Spinner").Add(new Spinner(30, 30));
            }
            
            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");
            View.Q<Button>("ContinueButton").AddToClassList("hide");

            var verificationId = _credVerifyId;
            var verificationCode = _credVerifyCode;
            var password = _password;
            var rePassword = _rePassword;

            // Validation
            if (string.IsNullOrEmpty(verificationCode)) {
                //View.Q<Label>("ErrVerificationCodeEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                verificationCodeField.ElementAt(1).AddToClassList("noctua-text-input-error");
                verificationCodeField.Q<Label>("error").RemoveFromClassList("hide");
                verificationCodeField.Q<Label>("error").text = "Verification code should not be empty";
                verificationCodeField.Q<VisualElement>("title").style.color = ColorModule.redError;
                return;
            }

            if (string.IsNullOrEmpty(password)) {
                //View.Q<Label>("ErrPasswordEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                passwordField.ElementAt(1).AddToClassList("noctua-text-input-error");
                passwordField.Q<Label>("error").RemoveFromClassList("hide");
                passwordField.Q<Label>("error").text = "Password should not be empty";
                passwordField.Q<VisualElement>("title").style.color = ColorModule.redError;
                return;
            }

            if (password?.Length < 6) {
                //View.Q<Label>("ErrPasswordTooShort").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                passwordField.ElementAt(1).AddToClassList("noctua-text-input-error");
                passwordField.Q<Label>("error").RemoveFromClassList("hide");
                passwordField.Q<Label>("error").text = "Password is too short. Minimum 6 character";
                passwordField.Q<VisualElement>("title").style.color = ColorModule.redError;
                return;
            }


            if (!password.Equals(rePassword)) {
                //View.Q<Label>("ErrPasswordMismatch").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                rePasswordField.ElementAt(1).AddToClassList("noctua-text-input-error");
                rePasswordField.Q<Label>("error").RemoveFromClassList("hide");
                rePasswordField.Q<Label>("error").text = "Password is not matched with repeated password";
                rePasswordField.Q<VisualElement>("title").style.color = ColorModule.redError;
                return;
            }

            try {
                await Model.AuthService.ConfirmResetPasswordAsync(verificationId, verificationCode, password);
                
                EventSender?.Send("reset_password_success");

                Visible = false;
                
                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                
                Model.ShowInfo(Locale.GetTranslation($"{GetType().Name}.SuccessNotification"));
            } catch (Exception e) {
                _log.Warning($"{e.Message}\n{e.StackTrace}");
                    
                if (e is NoctuaException noctuaEx)
                {
                    if (noctuaEx.ErrorCode == 2022) 
                    {
                        //View.Q<Label>("ErrVerificationCodeInvalid").RemoveFromClassList("hide");
                        verificationCodeField.ElementAt(1).AddToClassList("noctua-text-input-error");
                        verificationCodeField.Q<Label>("error").RemoveFromClassList("hide");
                        verificationCodeField.Q<Label>("error").text = "The verification code is invalid";
                        verificationCodeField.Q<VisualElement>("title").style.color = ColorModule.redError;
                    } else 
                    {
                        View.Q<Label>("ErrCode").text = noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message;
                    }
                } else {
                    View.Q<Label>("ErrCode").text = e.Message;
                }
                
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
            }
        }   

        private void HideAllErrors()
        {
            //Normalize border
            verificationCodeField.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");
            passwordField.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");
            rePasswordField.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");

            verificationCodeField.Q<Label>("error").AddToClassList("hide");
            passwordField.Q<Label>("error").AddToClassList("hide");
            rePasswordField.Q<Label>("error").AddToClassList("hide");

            View.Q<Label>("ErrCode").AddToClassList("hide");
            View.Q<Label>("ErrVerificationCodeEmpty").AddToClassList("hide");
            View.Q<Label>("ErrVerificationCodeInvalid").AddToClassList("hide");
            View.Q<Label>("ErrPasswordTooShort").AddToClassList("hide");
            View.Q<Label>("ErrPasswordEmpty").AddToClassList("hide");
            View.Q<Label>("ErrPasswordMismatch").AddToClassList("hide");
        }
    }
}
