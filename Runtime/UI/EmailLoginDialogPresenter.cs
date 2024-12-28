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

        private VisualElement panelVE;
        private List<TextField> textFields;
        private InputFieldNoctua inputEmail;
        private InputFieldNoctua inputPassword;

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
            submitButton = View.Q<Button>("ContinueButton");
            showPasswordButton = View.Q<Button>("ShowPasswordButton");

            inputEmail = new InputFieldNoctua(View.Q<TextField>("EmailTF"));
            inputPassword = new InputFieldNoctua(View.Q<TextField>("PasswordTF"));

            inputPassword.textField.isPasswordField = true;

            inputEmail.textField.RegisterValueChangedCallback(evt => OnValueChanged(inputEmail));
            inputPassword.textField.RegisterValueChangedCallback(evt => OnValueChanged(inputPassword));

            inputEmail.SetFocus();
            inputPassword.SetFocus();

            textFields = new List<TextField>
            {
                inputEmail.textField,
                inputPassword.textField
            };

            Utility.UpdateButtonState(textFields, submitButton);            

            View.Q<Label>("ForgotPassword").RegisterCallback<ClickEvent>(OnForgotPasswordButtonClick);
            View.Q<Label>("Register").RegisterCallback<ClickEvent>(OnRegisterButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<ClickEvent>(OnBackButtonClick);
            View.Q<Button>("ContinueButton").RegisterCallback<ClickEvent>(OnContinueButtonClick);

            showPasswordButton.RegisterCallback<PointerUpEvent>(OnToggleShowPassword);

            showPasswordButton.RemoveFromClassList("btn-password-hide");

            if (View.Q<VisualElement>("Spinner").childCount == 0)
            {
                View.Q<VisualElement>("Spinner").Add(new Spinner(30, 30));
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
            }

            // Show copublisher logo
            if (!string.IsNullOrEmpty(_config?.CoPublisher?.CompanyName))
            {
                var logo = Utility.GetCoPublisherLogo(_config.CoPublisher.CompanyName);
                var defaultLogo = Resources.Load<Texture2D>(logo);
                View.Q<VisualElement>("NoctuaLogoWithText").style.backgroundImage = new StyleBackground(defaultLogo);
                View.Q<VisualElement>("NoctuaLogoWithText").RemoveFromClassList("hide");
            }
            else
            {
                View.Q<VisualElement>("NoctuaLogoWithText").AddToClassList("hide");
            }

            HideAllErrors();
        }

        public void OnToggleShowPassword(PointerUpEvent _event)
        {
            inputPassword.textField.isPasswordField = !inputPassword.textField.isPasswordField;

            if (inputPassword.textField.isPasswordField)
            {
                showPasswordButton.RemoveFromClassList("btn-password-hide");
            }
            else
            {
                showPasswordButton.AddToClassList("btn-password-hide");
            }
        }

        public void SetBehaviourWhitelabel(GlobalConfig config)
        {
            _config = config;
        }

        private void OnValueChanged(InputFieldNoctua _input)
        {
            _input.AdjustLabel();
            Utility.UpdateButtonState(textFields, submitButton);
        }

        private void OnBackButtonClick(ClickEvent evt)
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

        private void OnForgotPasswordButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking forgot password button");

            Visible = false;
            // Show with empty form
            Model.PushNavigation(() => Model.ShowEmailLogin());
            Model.ShowEmailResetPassword(true);
        }

        private void OnRegisterButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking register button");

            Visible = false;

            Model.ClearNavigation();
            Model.PushNavigation(() => Model.ShowEmailLogin());
            Model.ShowEmailRegistration(true);
        }

        private async void OnContinueButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking continue button");

            HideAllErrors();

            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");
            View.Q<Button>("ContinueButton").AddToClassList("hide");

            var emailAddress = inputEmail.text.Replace(" ", string.Empty);
            var password = inputPassword.text;

            // Validation
            if (string.IsNullOrEmpty(emailAddress))
            {                
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                inputEmail.Error("Email address should not be empty");
                return;
            }

            if (!IsValidEmail(emailAddress))
            {
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                inputEmail.Error("Email address is not valid");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {                
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                inputPassword.Error("Password should not be empty");
                return;
            }

            if (password?.Length < 6)
            {                
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

                inputPassword.Error("Password is too short. Minimum 6 character");
                return;
            }

            try
            {
                if (Model.AuthService.RecentAccount == null ||
                !(Model.AuthService.RecentAccount != null && Model.AuthService.RecentAccount.IsGuest))
                {
                    // If account container is empty or it's not guest, login directly.
                    var userBundle = await Model.AuthService.LoginWithEmailAsync(emailAddress, password);

                    _onLoginSuccess?.Invoke(userBundle);
                }
                else
                {
                    // If guest, show bind confirmation dialog for guest.
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

                inputEmail.Clear();
                inputPassword.Clear();

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");                
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
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
            }
        }

        private void HideAllErrors()
        {
            //Normalize border
            inputEmail.Reset();
            inputPassword.Reset();

            View.Q<Label>("ErrCode").AddToClassList("hide");            
        }
    }
}
