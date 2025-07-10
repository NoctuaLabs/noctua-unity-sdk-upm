using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;

namespace com.noctuagames.sdk.UI
{
    internal class EmailRegisterDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();

        private InputFieldNoctua _inputEmail;
        private InputFieldNoctua _inputPassword;
        private InputFieldNoctua _inputRepassword;
        private List<TextField> textFields;

        private Button _showPasswordButton;
        private Button _showRePasswordButton;

        private ButtonNoctua _continueButton; // Main submit button
        private GlobalConfig _config;

        protected override void Attach() { }
        protected override void Detach() { }

        private void Start()
        {
            SetupInputFields(true);
            HideAllErrors();
        }

        public void Show(bool clearForm, bool isRegisterOnly)
        {
            SetupInputFields(clearForm);
            HideAllErrors();
            
            var additionalFooter = View.Q<VisualElement>("AdditionalFooterContent");
            if (isRegisterOnly)
            {
                additionalFooter.AddToClassList("hide");
            }
            else
            {
                additionalFooter.RemoveFromClassList("hide");
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

            Visible = true;
        }

        public void SetBehaviourWhitelabel(GlobalConfig config)
        {
            _config = config;

            _log.Debug("behaviour Whitelabel: " + JsonConvert.SerializeObject(_config?.Noctua?.RemoteFeatureFlags));
        }

        private void SetupInputFields(bool clearForm)
        {
            panelVE = View.Q<VisualElement>("NoctuaRegisterBox");
            _inputEmail = new InputFieldNoctua(View.Q<TextField>("EmailTF"));
            _inputPassword = new InputFieldNoctua(View.Q<TextField>("PasswordTF"));
            _inputRepassword = new InputFieldNoctua(View.Q<TextField>("RePasswordTF"));

            _showPasswordButton = View.Q<Button>("ShowPasswordButton");
            _showRePasswordButton = View.Q<Button>("ShowRePasswordButton");

            _continueButton = new ButtonNoctua(View.Q<Button>("ContinueButton"));
            var backButton = View.Q<Button>("BackButton");
            var loginLink = View.Q<Label>("LoginLink");

            // Default values
            if (clearForm)
            {
                _inputPassword.textField.isPasswordField = true;
                _inputRepassword.textField.isPasswordField = true;
            }

            textFields = new List<TextField>
            {
                _inputEmail.textField,
                _inputPassword.textField,
                _inputRepassword.textField

            };

            Utility.UpdateButtonState(textFields, _continueButton.button);
            // Callbacks
            _continueButton.button.RegisterCallback<PointerUpEvent>(OnContinueButtonClick);

            loginLink.RegisterCallback<ClickEvent>(OnLoginLinkClick);
            backButton.RegisterCallback<ClickEvent>(OnBackButtonClick);

            _showPasswordButton.RegisterCallback<ClickEvent>(OnToggleShowPassword);
            _showRePasswordButton.RegisterCallback<ClickEvent>(OnToggleShowRePassword);

            _showPasswordButton.RemoveFromClassList("btn-password-hide");
            _showRePasswordButton.RemoveFromClassList("btn-password-hide");

            _inputEmail.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputEmail));
            _inputPassword.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputPassword));
            _inputRepassword.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputRepassword));

            _inputEmail.SetFocus();
            _inputPassword.SetFocus();
            _inputRepassword.SetFocus();
          
            // Show the footer content
            View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
            View.Q<VisualElement>("footerContent").RemoveFromClassList("hide");
            View.Q<VisualElement>("footerContent").AddToClassList("generic-register-footer");                
           
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
        }

        public void OnToggleShowRePassword(ClickEvent evt)
        {
            _inputRepassword.textField.Blur();
            _inputRepassword.textField.isPasswordField = !_inputRepassword.textField.isPasswordField;

            if (_inputRepassword.textField.isPasswordField)
            {
                _showRePasswordButton.RemoveFromClassList("btn-password-hide");
            }
            else
            {
                _showRePasswordButton.AddToClassList("btn-password-hide");
            }
        }

        private void OnLoginLinkClick(ClickEvent evt)
        {
            Visible = false;

            Model.ShowEmailLogin();
        }


        private bool validateForm()
        {
            HideAllErrors();

            _continueButton.ToggleLoading(true);

            var emailAddress = _inputEmail.text.Replace(" ", string.Empty);
            var password = _inputPassword.text;
            var rePassword = _inputRepassword.text;

            // Validation
            if (!string.IsNullOrEmpty(Utility.ValidateEmail(emailAddress)))
            {
                _continueButton.ToggleLoading(false);
                _inputEmail.Error(Utility.ValidateEmail(emailAddress));
                return false;
            }

            if (!string.IsNullOrEmpty(Utility.ValidatePassword(password)))
            {
                _continueButton.ToggleLoading(false);
                _inputPassword.Error(Utility.ValidatePassword(password));
                return false;
            }

            if (!string.IsNullOrEmpty(Utility.ValidateReenterPassword(password, rePassword)))
            {
                _continueButton.ToggleLoading(false);
                _inputRepassword.Error(Utility.ValidateReenterPassword(password, rePassword));
                return false;
            }
            return true;
        }

        private async void OnContinueButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking continue button");

            if (!validateForm()) {
                return;
            }

            var emailAddress = _inputEmail.text.Replace(" ", string.Empty);
            var password = _inputPassword.text;
            var rePassword = _inputRepassword.text;

            Dictionary<string, string> regExtra = null;

            try
            {
                CredentialVerification result;

                switch (Model.AuthIntention)
                {
                    case AuthIntention.None:
                    case AuthIntention.Switch:
                        result = await Model.AuthService.RegisterWithEmailAsync(emailAddress, password, regExtra);
                        break;
                    case AuthIntention.Link:
                        result = await Model.AuthService.LinkWithEmailAsync(emailAddress, password);
                        break;
                    default:
                        throw new NoctuaException(NoctuaErrorCode.Authentication, $"Invalid AuthIntention {Model.AuthIntention}");
                }

                Debug.Log("RegisterWithPassword verification ID: " + result.Id);

                Visible = false;

                _inputEmail.Clear();
                _inputPassword.Clear();
                _inputRepassword.Clear();

                Model.ShowEmailVerification(emailAddress, password, result.Id, regExtra);

                _continueButton.Clear();
            }
            catch (Exception e)
            {
                _log.Info("Error during registration: " + e.Message);

                if (e is NoctuaException noctuaEx)
                {
                    _continueButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);
                }
                else
                {
                    _continueButton.Error(e.Message);
                }

                _continueButton.ToggleLoading(false);
                
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
            }
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking back button");   

            _continueButton.Clear();

            Visible = false;

            Model.NavigateBack();
        }

        private void OnValueChanged(InputFieldNoctua input)
        {
            input.AdjustLabel();
            Utility.UpdateButtonState(textFields, _continueButton.button);
        }

        private void HideAllErrors()
        {
            //Normalize border
            _inputEmail.Reset();
            _inputPassword.Reset();
            _inputRepassword.Reset();

            _continueButton.Clear();
        }
    }
}
