using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

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
       
        private List<TextField> textFields;
        private InputFieldNoctua inputEmail;
        private InputFieldNoctua inputPassword;

        private ButtonNoctua submitButton;
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

        private void Setup()
        {            
            panelVE = View.Q<VisualElement>("Panel");            
            showPasswordButton = View.Q<Button>("ShowPasswordButton");

            submitButton = new ButtonNoctua(View.Q<Button>("ContinueButton"));
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

            Utility.UpdateButtonState(textFields, submitButton.button);

            submitButton.button.RegisterCallback<ClickEvent>(OnContinueButtonClick);
            View.Q<Label>("ForgotPassword").RegisterCallback<ClickEvent>(OnForgotPasswordButtonClick);
            View.Q<Label>("Register").RegisterCallback<ClickEvent>(OnRegisterButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<ClickEvent>(OnBackButtonClick);            

            showPasswordButton.RegisterCallback<ClickEvent>(OnToggleShowPassword);

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

        public void OnToggleShowPassword(ClickEvent _event)
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
            Utility.UpdateButtonState(textFields, submitButton.button);
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            Visible = false;
            Model.NavigateBack();
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

            submitButton.ToggleLoading(true);

            var emailAddress = inputEmail.text.Replace(" ", string.Empty);
            var password = inputPassword.text;

            // Validation
            if (!string.IsNullOrEmpty(Utility.ValidateEmail(emailAddress)))
            {
                submitButton.ToggleLoading(false);

                inputEmail.Error(Utility.ValidateEmail(emailAddress));
                return;
            }

            if (!string.IsNullOrEmpty(Utility.ValidatePassword(password)))
            {
                submitButton.ToggleLoading(false);

                inputPassword.Error(Utility.ValidatePassword(password));
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
                submitButton.Clear();

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

                    submitButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);               
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
            inputEmail.Reset();
            inputPassword.Reset();
            submitButton.Clear();     
        }
    }
}
