using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.UI;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;

namespace com.noctuagames.sdk.UI
{
    public class LoginResult
    {
        public bool Success { get; set; }
        public UserBundle User { get; set; }
        public Exception Error { get; set; }
    }
    
    internal class EmailLoginDialogPresenter : Presenter<AuthenticationModel>
    {
        private string _email;
        private string _password;
        private VisualElement _spinner;
        
        private Action<UserBundle> _onLoginSuccess;

        public void Show(Action<UserBundle> onLoginSuccess)
        {
            Setup();

            _onLoginSuccess = onLoginSuccess;
            Visible = true;
        }
        
        
        protected override void Attach(){}
        protected override void Detach(){}

        private void Start()
        {
            Setup();
        }

        private void Setup()
        {
            var emailField = View.Q<TextField>("EmailTF");
            var passwordField = View.Q<TextField>("PasswordTF");
            var submitButton = View.Q<Button>("ContinueButton");

            passwordField.isPasswordField = true;

            emailField.RegisterValueChangedCallback(evt => OnEmailValueChanged(emailField));
            passwordField.RegisterValueChangedCallback(evt => OnPasswordValueChanged(passwordField));

            List<TextField> textFields = new List<TextField>
            {
                emailField,
                passwordField
            };

            Utility.ValidateFormFields(textFields, submitButton);

            View.Q<Label>("ForgotPassword").RegisterCallback<PointerUpEvent>(OnForgotPasswordButtonClick);
            View.Q<Label>("Register").RegisterCallback<PointerUpEvent>(OnRegisterButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<PointerUpEvent>(OnBackButtonClick);
            View.Q<Button>("ContinueButton").RegisterCallback<PointerUpEvent>(OnContinueButtonClick);
            
            _spinner = new Spinner();
            View.Q<VisualElement>("Spinner").Clear();
            View.Q<VisualElement>("Spinner").Add(_spinner);
            View.Q<VisualElement>("Spinner").AddToClassList("hide");
        }

        private void OnEmailValueChanged(TextField textField)
        {
            HideAllErrors();

            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;
            }
            _email = textField.value;
            AdjustHideLabelElement(textField);
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

            AdjustHideLabelElement(textField);
        }

        private void AdjustHideLabelElement(TextField textField) {
            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;
            }
        }

        private void OnBackButtonClick(PointerUpEvent evt)
        {
            Visible = false;
            
            Model.NavigateBack();
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                // Regular expression pattern to validate email address
                string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

                // Use IdnMapping class to convert Unicode domain names, if applicable
                email = Regex.Replace(email, @"(@)(.+)$", match =>
                {
                    var idn = new IdnMapping();
                    string domainName = idn.GetAscii(match.Groups[2].Value);
                    return match.Groups[1].Value + domainName;
                }, RegexOptions.None, TimeSpan.FromMilliseconds(200));

                // Return true if the email matches the pattern
                return Regex.IsMatch(email, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch
            {
                return false;
            }
        }

        private void OnForgotPasswordButtonClick(PointerUpEvent evt)
        {
            Visible = false;
            // Show with empty form
            Model.PushNavigation(() => Model.ShowEmailLogin());
            Model.ShowEmailResetPassword(true);
        }

        private void OnRegisterButtonClick(PointerUpEvent evt)
        {
            Visible = false;
            
            Model.PushNavigation(() => Model.ShowEmailLogin());
            Model.ShowEmailRegistration(true);
        }

        private async void OnContinueButtonClick(PointerUpEvent evt)
        {
            Debug.Log("EmailLoginDialogPresenter.OnContinueButtonClick()");

            HideAllErrors();

            var spinnerInstance = new Spinner();
            View.Q<Button>("ContinueButton").AddToClassList("hide");
            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");

            var emailAddress = _email.Replace(" ", string.Empty);
            var password = _password;

            // Validation
            if (string.IsNullOrEmpty(emailAddress)) {
                View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
            
                return;
            }

            if (!IsValidEmail(emailAddress)) {
                View.Q<Label>("ErrEmailInvalid").RemoveFromClassList("hide");
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

            try {
                var userBundle = await Model.AuthService.LoginWithEmailAsync(_email, _password);
                
                _onLoginSuccess?.Invoke(userBundle);

                View.visible = false;

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
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
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
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
