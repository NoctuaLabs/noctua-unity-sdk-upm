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
        private readonly ILogger _log = new NoctuaLogger();

        private string _email;
        private string _password;
        private VisualElement panelVE;
        private List<TextField> textFields;
        private TextField emailField;
        private TextField passwordField;
        private Button submitButton;
        private Button showPasswordButton;

        private Action<UserBundle> _onLoginSuccess;
        private GlobalConfig _config;

        public void Show(Action<UserBundle> onLoginSuccess)
        {
            Setup();

            _onLoginSuccess = onLoginSuccess;
            Visible = true;
        }

        protected override void Attach() { }
        protected override void Detach() { }

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
            emailField = View.Q<TextField>("EmailTF");
            passwordField = View.Q<TextField>("PasswordTF");
            submitButton = View.Q<Button>("ContinueButton");
            showPasswordButton = View.Q<Button>("ShowPasswordButton");

            passwordField.isPasswordField = true;

            emailField.RegisterValueChangedCallback(evt => OnEmailValueChanged(emailField));
            passwordField.RegisterValueChangedCallback(evt => OnPasswordValueChanged(passwordField));            

            emailField.hideMobileInput = true;
            passwordField.hideMobileInput = true;

            textFields = new List<TextField>
            {
                emailField,
                passwordField
            };

            Utility.UpdateButtonState(textFields, submitButton);

            View.Q<Label>("ForgotPassword").RegisterCallback<PointerUpEvent>(OnForgotPasswordButtonClick);
            View.Q<Label>("Register").RegisterCallback<PointerUpEvent>(OnRegisterButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<PointerUpEvent>(OnBackButtonClick);
            View.Q<Button>("ContinueButton").RegisterCallback<PointerUpEvent>(OnContinueButtonClick);

            emailField.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);
            passwordField.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);
            emailField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);
            passwordField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);

            showPasswordButton.RegisterCallback<PointerUpEvent>(OnToggleShowPassword);

            showPasswordButton.RemoveFromClassList("btn-password-hide");

            if (View.Q<VisualElement>("Spinner").childCount == 0)
            {
                View.Q<VisualElement>("Spinner").Add(new Spinner(30, 30));
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
            }
        }

        public void OnToggleShowPassword(PointerUpEvent _event)
        {
            passwordField.isPasswordField = !passwordField.isPasswordField;

            if (passwordField.isPasswordField)
            {
                showPasswordButton.RemoveFromClassList("btn-password-hide");
            }
            else
            {
                showPasswordButton.AddToClassList("btn-password-hide");
            }
        }

        public void OnTextFieldFocusChange(FocusInEvent _event)
        {
            (_event.target as VisualElement).Children().ElementAt(1).AddToClassList("noctua-text-input-focus");
            (_event.target as VisualElement).Q<VisualElement>("Tittle").style.color = Color.white;
        }

        public void OnTextFieldFocusChange(FocusOutEvent _event)
        {
            (_event.target as VisualElement).Children().ElementAt(1).RemoveFromClassList("noctua-text-input-focus");
            (_event.target as VisualElement).Q<VisualElement>("Tittle").style.color = new Color(0.4862745f, 0.4941176f, 0.5058824f, 1.0f);
        }

        public void SetBehaviourWhitelabel(GlobalConfig config)
        {
            _config = config;
        }

        private void OnEmailValueChanged(TextField textField)
        {
            HideAllErrors();

            _email = textField.value;
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

        private void AdjustHideLabelElement(TextField textField)
        {
            if (string.IsNullOrEmpty(textField.value))
            {
                textField.labelElement.style.display = DisplayStyle.Flex;
                textField.Q<VisualElement>("Tittle").AddToClassList("hide");
            }
            else
            {
                textField.labelElement.style.display = DisplayStyle.None;
                textField.Q<VisualElement>("Tittle").RemoveFromClassList("hide");
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
            _log.Debug("clicking forgot password button");

            Visible = false;
            // Show with empty form
            Model.PushNavigation(() => Model.ShowEmailLogin());
            Model.ShowEmailResetPassword(true);
        }

        private void OnRegisterButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking register button");

            Visible = false;

            Model.PushNavigation(() => Model.ShowEmailLogin());
            Model.ShowEmailRegistration(true);
        }

        private async void OnContinueButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking continue button");

            HideAllErrors();

            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");
            View.Q<Button>("ContinueButton").AddToClassList("hide");

            var emailAddress = _email.Replace(" ", string.Empty);
            var password = _password;

            // Validation
            if (string.IsNullOrEmpty(emailAddress))
            {
                View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                return;
            }

            if (!IsValidEmail(emailAddress))
            {
                View.Q<Label>("ErrEmailInvalid").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                View.Q<Label>("ErrPasswordEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                return;
            }

            if (password?.Length < 6)
            {
                View.Q<Label>("ErrPasswordTooShort").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                return;
            }

            try
            {
                if (Model.AuthService.RecentAccount.IsGuest)
                {
                    var playerToken = await Model.AuthService.GetEmailLoginTokenAsync(emailAddress, password);

                    if (playerToken.Player == null)
                    {
                        Model.ShowBindConfirmation(playerToken);
                    }
                    else
                    {
                        Model.ShowConnectConflict(playerToken);
                    }
                }
                else
                {
                    var userBundle = await Model.AuthService.LoginWithEmailAsync(emailAddress, password);

                    _onLoginSuccess?.Invoke(userBundle);
                }

                View.Q<TextField>("EmailTF").value = string.Empty;
                View.Q<TextField>("PasswordTF").value = string.Empty;

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                Visible = false;
            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    if (noctuaEx.ErrorCode == (int)NoctuaErrorCode.UserBanned)
                    {
                        bool confirmed = await Model.ShowBannedConfirmationDialog();

                        if (confirmed)
                        {
                            throw;
                        }

                        throw new OperationCanceledException("Action canceled.");
                    }

                    View.Q<Label>("ErrCode").text = noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message;
                }
                else
                {
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
