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
       
        private List<TextField> _textFields;
        private InputFieldNoctua _inputEmail;
        private InputFieldNoctua _inputPassword;

        private ButtonNoctua _submitButton;
        private Button _showPasswordButton;

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
            _showPasswordButton = View.Q<Button>("ShowPasswordButton");

            _submitButton = new ButtonNoctua(View.Q<Button>("ContinueButton"));
            _inputEmail = new InputFieldNoctua(View.Q<TextField>("EmailTF"));
            _inputPassword = new InputFieldNoctua(View.Q<TextField>("PasswordTF"));

            _inputPassword.textField.isPasswordField = true;

            _inputEmail.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputEmail));
            _inputPassword.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputPassword));

            _inputEmail.SetFocus();
            _inputPassword.SetFocus();

            _textFields = new List<TextField>
            {
                _inputEmail.textField,
                _inputPassword.textField
            };

            Utility.UpdateButtonState(_textFields, _submitButton.button);

           // _submitButton.button.RegisterCallback<ClickEvent>(OnContinueButtonClick);
            _submitButton.button.RegisterCallback<PointerUpEvent>(evt =>
            {
                OnContinueButtonClick(evt);
            });
            View.Q<Label>("ForgotPassword").RegisterCallback<ClickEvent>(OnForgotPasswordButtonClick);
            View.Q<Label>("Register").RegisterCallback<ClickEvent>(OnRegisterButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<ClickEvent>(OnBackButtonClick);            

            _showPasswordButton.RegisterCallback<ClickEvent>(OnToggleShowPassword);

            _showPasswordButton.RemoveFromClassList("btn-password-hide");

            if (View.Q<VisualElement>("Spinner").childCount == 0)
            {
                View.Q<VisualElement>("Spinner").Add(new Spinner(30, 30));
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
            }

            // Show copublisher logo
            if (!string.IsNullOrEmpty(_config?.CoPublisher?.CompanyName))
            {
                var logo = Utility.GetCoPublisherLogo(_config?.CoPublisher?.CompanyName);
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

        public void OnToggleShowPassword(ClickEvent evt)
        {
            _inputPassword.textField.isPasswordField = !_inputPassword.textField.isPasswordField;

            if (_inputPassword.textField.isPasswordField)
            {
                _showPasswordButton.RemoveFromClassList("btn-password-hide");
            }
            else
            {
                _showPasswordButton.AddToClassList("btn-password-hide");
            }
        }

        public void SetBehaviourWhitelabel(GlobalConfig config)
        {
            _config = config;
        }

        private void OnValueChanged(InputFieldNoctua input)
        {
            input.AdjustLabel();
            Utility.UpdateButtonState(_textFields, _submitButton.button);
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

            Model.ShowEmailRegistration(true);
        }

        private async void OnContinueButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking continue button");

            HideAllErrors();

            _submitButton.ToggleLoading(true);

            var emailAddress = _inputEmail.text.Replace(" ", string.Empty);
            var password = _inputPassword.text;

            // Validation
            if (!string.IsNullOrEmpty(Utility.ValidateEmail(emailAddress)))
            {
                _submitButton.ToggleLoading(false);

                _inputEmail.Error(Utility.ValidateEmail(emailAddress));
                return;
            }

            if (!string.IsNullOrEmpty(Utility.ValidatePassword(password)))
            {
                _submitButton.ToggleLoading(false);

                _inputPassword.Error(Utility.ValidatePassword(password));
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
                        Model.ShowBindConflictDialog(playerToken);
                    }
                }

                _inputEmail.Clear();
                _inputPassword.Clear();
                _submitButton.Clear();

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

                    _submitButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);               
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
            _inputEmail.Reset();
            _inputPassword.Reset();
            _submitButton.Clear();     
        }
    }
}
