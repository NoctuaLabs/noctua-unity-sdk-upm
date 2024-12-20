﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.UI;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;
using Newtonsoft.Json;

namespace com.noctuagames.sdk.UI
{
    internal class EmailRegisterDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();
        
        private string _email;
        private string _password;
        private string _rePassword;
        private TextField emailField;
        private TextField passwordField;
        private TextField rePasswordField;

        private Button showPasswordButton;
        private Button showRePasswordButton;

        private VisualElement panelVE;

        //Behaviour whitelabel - VN
        private TextField _fullname;
        private TextField _phoneNumber;
        private TextField _birthDate;
        private DropdownField _gender;
        private TextField _country;
        private TextField _idCard;
        private TextField _placeOfIssue;
        private Button _dateOfIssue;
        private TextField _address;
        private List<TextField> textFields;
        private Button continueButton;
        private Button wizardContinueButton;
        private GlobalConfig _config;

        private int _wizardPage = 0;

        protected override void Attach(){}
        protected override void Detach(){}

        private void Start()
        {
            SetupInputFields(true);
            HideAllErrors();
        }

        private async void Update()
        {
            if (panelVE == null) return;

            if (TouchScreenKeyboard.visible && !panelVE.ClassListContains("dialog-box-keyboard-shown"))
            {
                // Hide the button to avoid double tap
                View.Q<Button>("WizardNextTo2Button").AddToClassList("hide");
                View.Q<Button>("WizardNextTo3Button").AddToClassList("hide");
                View.Q<Button>("WizardNextTo4Button").AddToClassList("hide");
                View.Q<Button>("WizardPrevTo1Button").AddToClassList("hide");
                View.Q<Button>("WizardPrevTo2Button").AddToClassList("hide");
                View.Q<Button>("WizardPrevTo3Button").AddToClassList("hide");
                View.Q<Button>("ContinueButton").AddToClassList("hide");
                View.Q<Button>("WizardContinueButton").AddToClassList("hide");
                await Task.Delay(100);
                panelVE.AddToClassList("dialog-box-keyboard-shown");
                await Task.Delay(100);
                // Show it again.
                View.Q<Button>("WizardNextTo2Button").RemoveFromClassList("hide");
                View.Q<Button>("WizardNextTo3Button").RemoveFromClassList("hide");
                View.Q<Button>("WizardNextTo4Button").RemoveFromClassList("hide");
                View.Q<Button>("WizardPrevTo1Button").RemoveFromClassList("hide");
                View.Q<Button>("WizardPrevTo2Button").RemoveFromClassList("hide");
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<Button>("WizardContinueButton").RemoveFromClassList("hide");
            }

            if (!TouchScreenKeyboard.visible && panelVE.ClassListContains("dialog-box-keyboard-shown"))
            {
                panelVE.RemoveFromClassList("dialog-box-keyboard-shown");
            }
        }

        public void Show(bool clearForm, bool isRegisterOnly)
        {
            SetupInputFields(clearForm);
            HideAllErrors();

            View.Q<VisualElement>("WizardSpinner").AddToClassList("hide");

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
                var logo = Utility.GetCoPublisherLogo(_config.CoPublisher.CompanyName);
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
            
            _log.Debug("behaviour Whitelabel: " + JsonConvert.SerializeObject(_config?.Noctua?.Flags));
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

        private void SetupInputFields(bool clearForm)
        {
            panelVE = View.Q<VisualElement>("NoctuaRegisterBox");
            emailField = View.Q<TextField>("EmailTF");
            passwordField = View.Q<TextField>("PasswordTF");
            rePasswordField = View.Q<TextField>("RePasswordTF");

            showPasswordButton = View.Q<Button>("ShowPasswordButton");
            showRePasswordButton = View.Q<Button>("ShowRePasswordButton");

            continueButton = View.Q<Button>("ContinueButton");
            wizardContinueButton = View.Q<Button>("WizardContinueButton");
            var backButton = View.Q<Button>("BackButton");
            var loginLink = View.Q<Label>("LoginLink");

            //Behaviour whitelabel - VN
            _fullname = View.Q<TextField>("FullNameTF");
            _phoneNumber = View.Q<TextField>("PhoneNumberTF");
            _birthDate = View.Q<TextField>("BirthdateTF");
            _gender = View.Q<DropdownField>("GenderTF");
            _country = View.Q<TextField>("CountryTF");
            _idCard = View.Q<TextField>("IDCardTF");
            _placeOfIssue = View.Q<TextField>("PlaceOfIssueTF");
            _dateOfIssue = View.Q<Button>("DateOfIssueTF");
            _address = View.Q<TextField>("AddressTF");

            if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
            {
                SetupDropdown();  
                SetupDatePicker();
                // Disable country dropdown as Vietnam copublisher is asking for raw text input for country.
                // SetCountries();
            }

            // Visibility
            continueButton.RemoveFromClassList("hide");
            wizardContinueButton.RemoveFromClassList("hide");

            // Default values
            if (clearForm) {
                passwordField.isPasswordField = true;
                rePasswordField.isPasswordField = true;
                emailField.value = "";
                passwordField.value = "";
                rePasswordField.value = "";

                _fullname.value = "";
                _phoneNumber.value = "";
                _country.value = "";
                _idCard.value = "";
                _placeOfIssue.value = "";
                _address.value = "";
            }

            if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
            {
                textFields = new List<TextField>
                {
                    emailField,
                    passwordField,
                    rePasswordField,
                    _fullname,
                    _phoneNumber,
                    _birthDate,
                    _country,
                    _idCard,
                    _placeOfIssue,
                    _address
                };
            }
            else
            {
                textFields = new List<TextField>
                {
                    emailField,
                    passwordField,
                    rePasswordField

                };
            }

            Utility.UpdateButtonState(textFields, continueButton);
            Utility.UpdateButtonState(textFields, wizardContinueButton);

            // Callbacks
            continueButton.RegisterCallback<PointerUpEvent>(OnContinueButtonClick);
            wizardContinueButton.RegisterCallback<PointerUpEvent>(OnContinueButtonClick);
            backButton.RegisterCallback<PointerUpEvent>(OnBackButtonClick);
            emailField.RegisterValueChangedCallback(evt => OnEmailValueChanged(emailField));
            passwordField.RegisterValueChangedCallback(evt => OnPasswordValueChanged(passwordField));
            rePasswordField.RegisterValueChangedCallback(evt => OnRePasswordValueChanged(rePasswordField));

            emailField.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);
            passwordField.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);
            rePasswordField.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);
            emailField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);
            passwordField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);
            rePasswordField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);

            showPasswordButton.RegisterCallback<PointerUpEvent>(OnToggleShowPassword);
            showRePasswordButton.RegisterCallback<PointerUpEvent>(OnToggleShowRePassword);

            showPasswordButton.RemoveFromClassList("btn-password-hide");
            showRePasswordButton.RemoveFromClassList("btn-password-hide");

            emailField.hideMobileInput = true;
            passwordField.hideMobileInput = true;
            rePasswordField.hideMobileInput = true;

            //Behaviour whitelabel - VN
            if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
            {
                _fullname.RegisterValueChangedCallback(evt => OnValueChanged(_fullname));
                _phoneNumber.RegisterValueChangedCallback(evt => OnValueChanged(_phoneNumber));
                _country.RegisterValueChangedCallback(evt => OnValueChanged(_country));
                _idCard.RegisterValueChangedCallback(evt => OnValueChanged(_idCard));
                _placeOfIssue.RegisterValueChangedCallback(evt => OnValueChanged(_placeOfIssue));
                _address.RegisterValueChangedCallback(evt => OnValueChanged(_address));
            
                // Wizard navigation
                View.Q<Button>("WizardNextTo2Button").RegisterCallback<PointerUpEvent>(NavigateToWizard2);
                View.Q<Button>("WizardNextTo3Button").RegisterCallback<PointerUpEvent>(NavigateToWizard3);
                View.Q<Button>("WizardNextTo4Button").RegisterCallback<PointerUpEvent>(NavigateToWizard4);
                View.Q<Button>("WizardPrevTo1Button").RegisterCallback<PointerUpEvent>(NavigateToWizard1);
                View.Q<Button>("WizardPrevTo2Button").RegisterCallback<PointerUpEvent>(NavigateToWizard2);
                View.Q<Button>("WizardPrevTo3Button").RegisterCallback<PointerUpEvent>(NavigateToWizard3);
                // Hide the footer content
                View.Q<VisualElement>("footerContent").AddToClassList("hide");
                // Navigate to the first wizard
                View.Q<VisualElement>("RegisterWizard1NextButton").RemoveFromClassList("hide");
                NavigateToWizard1();
            } else {
                // Show the footer content
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("footerContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("footerContent").AddToClassList("generic-register-footer");
                View.Q<VisualElement>("ErrBox").AddToClassList("generic-register-errbox");
            }

            loginLink.RegisterCallback<PointerUpEvent>(OnLoginLinkClick);



        }

        public async void NavigateToWizard1(PointerUpEvent evt = null)
        {
            _wizardPage = 1;
            View.Q<VisualElement>("RegisterWizard1").RemoveFromClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");
            View.Q<VisualElement>("ErrBox").AddToClassList("hide");
            View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
            View.Q<VisualElement>("footerContent").AddToClassList("wizard-register-footer");
        }

        public async void NavigateToWizard2(PointerUpEvent evt = null)
        {
            _wizardPage = 2;
            View.Q<VisualElement>("footerContent").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").RemoveFromClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");
            View.Q<VisualElement>("ErrBox").AddToClassList("hide");
            View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
        }

        public async void NavigateToWizard3(PointerUpEvent evt = null) {
            HideAllErrors();
            _wizardPage = 3;
            View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").RemoveFromClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");
            View.Q<VisualElement>("ErrBox").AddToClassList("hide");
            View.Q<VisualElement>("footerContent").AddToClassList("hide");
            View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
        }

        public async void NavigateToWizard4(PointerUpEvent evt = null) {
            _wizardPage = 4;
            View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").RemoveFromClassList("hide");
            View.Q<VisualElement>("ErrBox").RemoveFromClassList("hide");
            View.Q<VisualElement>("ErrBox").AddToClassList("wizard-register-errbox");
            View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
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
        }

        #endregion

        private void SetupDropdown()
        {
             var genderChoices = new List<string> {Locale.GetTranslation("Select.Gender.Male"), Locale.GetTranslation("Select.Gender.Female")};
            Color textColor = new Color(98f / 255f, 100f / 255f, 104f / 255f);
            
            var regionCode = _config?.Noctua?.Region ?? "";

            _gender.choices = genderChoices; 
            _gender.value = Locale.GetTranslation("Select.Gender");

            _gender.style.color = textColor;

            _gender.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _gender.style.color = Color.white;
                _gender.value = evt.newValue;
                _gender.labelElement.style.display = DisplayStyle.None;
            });
        }

        private void SetupDatePicker()
        {
            _birthDate.isReadOnly = true;

            string startDate = "01/01/2000";
            DateTime parsedDate = DateTime.ParseExact(startDate, "dd/MM/yyyy", null);

            void OpenDatePickerHandler(int pickerId, TextField textField, PointerUpEvent upEvent)
            {
                //handle open twice date picker
                upEvent.StopImmediatePropagation();

                Noctua.OpenDatePicker(parsedDate.Year, parsedDate.Month, parsedDate.Day, pickerId,
                (DateTime _date) =>
                {
                    Debug.Log("Date Selected :" + _date.ToString("dd/MM/yyyy"));
                },
                (DateTime _date) =>
                {
                    Debug.Log("Date Picked :" + _date.ToString("dd/MM/yyyy"));

                    textField.value = _date.ToString("dd/MM/yyyy");
                });
            }

            View.Q<VisualElement>("BirthdateContainer").UnregisterCallback<PointerUpEvent>(evt => {});

            View.Q<VisualElement>("BirthdateContainer").RegisterCallback<ClickEvent>(upEvent =>
            {

                upEvent.StopImmediatePropagation();

                Noctua.OpenDatePicker(parsedDate.Year, parsedDate.Month, parsedDate.Day, 1,
                (DateTime _date) =>
                {
                    _log.Debug($"picked date '{_date:O}'");
                },
                (DateTime _date) =>
                {
                    _birthDate.value = _date.ToString("dd/MM/yyyy");
                    _birthDate.labelElement.style.display = DisplayStyle.None;
                    Utility.UpdateButtonState(textFields, continueButton);
                    Utility.UpdateButtonState(textFields, wizardContinueButton);

                });
            });

            View.Q<VisualElement>("DateOfIssueContainer").UnregisterCallback<PointerUpEvent>(evt => {});
            View.Q<VisualElement>("DateOfIssueContainer").RegisterCallback<ClickEvent>(upEvent =>
            {

                upEvent.StopImmediatePropagation();

                Noctua.OpenDatePicker(parsedDate.Year, parsedDate.Month, parsedDate.Day, 1,
                (DateTime _date) =>
                {
                    _log.Debug($"picked date '{_date:O}'");
                },
                (DateTime _date) =>
                {
                    _dateOfIssue.text = _date.ToString("dd/MM/yyyy");
                    _dateOfIssue.RemoveFromClassList("grey-text");
                    _dateOfIssue.AddToClassList("white-text");
                    Utility.UpdateButtonState(textFields, continueButton);
                    Utility.UpdateButtonState(textFields, wizardContinueButton);

                });
            });
        }

        // Unused, leave it here for future use.
        /*
        private void SetCountries()
        {
            _countryList.Clear();

            List<Country> countries = CountryData.Countries;

            foreach(var country in countries)
            {
                _countryList.Add(country.Name);
            }

            Color textColor = new Color(98f / 255f, 100f / 255f, 104f / 255f);

            var regionCode = _config?.Noctua?.Region ?? "";

            _country.choices = _countryList; 
            _country.value = Locale.GetTranslation("Select.Country");
            _country.style.color = textColor;
            _country.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _country.style.color = Color.white;
                _country.value = evt.newValue;
                _country.labelElement.style.display = DisplayStyle.None;
            });            
        }
        */

        private void OnLoginLinkClick(PointerUpEvent evt)
        {
            Visible = false;
            
            Model.ClearNavigation();
            Model.PushNavigation(() => Model.ShowEmailRegistration(false));
            Model.ShowEmailLogin();
        }

        private async void OnContinueButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking continue button");
            
            HideAllErrors();

            if (View.Q<VisualElement>("Spinner").childCount == 0)
            {             
                View.Q<VisualElement>("Spinner").Add(new Spinner(30, 30));                
            }

            View.Q<Button>("ContinueButton").AddToClassList("hide");            
            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");
            // Wizard
            View.Q<Button>("WizardContinueButton").AddToClassList("hide");            
            View.Q<Button>("WizardPrevTo3Button").AddToClassList("hide");
            View.Q<VisualElement>("WizardSpinner").RemoveFromClassList("hide");

            var emailAddress = _email;
            var password = _password;
            var rePassword = _rePassword;

            // Validation
            if (string.IsNullOrEmpty(emailAddress)) {
                _log.Debug("form validation: email is empty");
                //View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                // Wizard
                View.Q<Button>("WizardContinueButton").RemoveFromClassList("hide");            
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<VisualElement>("WizardSpinner").AddToClassList("hide");

                emailField.ElementAt(1).AddToClassList("noctua-text-input-error");
                emailField.Q<Label>("error").RemoveFromClassList("hide");
                emailField.Q<Label>("error").text = "Email address should not be empty";
                emailField.Q<VisualElement>("title").style.color = ColorModule.redError;

                // Show the error at the end of the wizard as well
                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    Model.ShowGeneralNotification(emailField.Q<Label>("error").text, false);
                }

                return;
            }

            if (!IsValidEmail(emailAddress)) {
                _log.Debug("form validation: email is not valid");
                //View.Q<Label>("ErrEmailInvalid").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                // Wizard
                View.Q<Button>("WizardContinueButton").RemoveFromClassList("hide");            
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<VisualElement>("WizardSpinner").AddToClassList("hide");

                emailField.ElementAt(1).AddToClassList("noctua-text-input-error");
                emailField.Q<Label>("error").RemoveFromClassList("hide");
                emailField.Q<Label>("error").text = "Email address is not valid";
                emailField.Q<VisualElement>("title").style.color = ColorModule.redError;

                // Show the error at the end of the wizard as well
                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    Model.ShowGeneralNotification(emailField.Q<Label>("error").text, false);
                }

                return;
            }

            if (string.IsNullOrEmpty(password)) {
                _log.Debug("form validation: password is empty");
                //View.Q<Label>("ErrPasswordEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                // Wizard
                View.Q<Button>("WizardContinueButton").RemoveFromClassList("hide");            
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<VisualElement>("WizardSpinner").AddToClassList("hide");

                passwordField.ElementAt(1).AddToClassList("noctua-text-input-error");
                passwordField.Q<Label>("error").RemoveFromClassList("hide");
                passwordField.Q<Label>("error").text = "Password should not be empty";
                passwordField.Q<VisualElement>("title").style.color = ColorModule.redError;

                // Show the error at the end of the wizard as well
                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    Model.ShowGeneralNotification(passwordField.Q<Label>("error").text, false);
                }
                return;
            }

            if (password?.Length < 6) {
                _log.Debug("form validation: password is not valid");
                //View.Q<Label>("ErrPasswordTooShort").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                // Wizard
                View.Q<Button>("WizardContinueButton").RemoveFromClassList("hide");            
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<VisualElement>("WizardSpinner").AddToClassList("hide");

                passwordField.ElementAt(1).AddToClassList("noctua-text-input-error");
                passwordField.Q<Label>("error").RemoveFromClassList("hide");
                passwordField.Q<Label>("error").text = "Password is too short. Minimum 6 character";
                passwordField.Q<VisualElement>("title").style.color = ColorModule.redError;

                // Show the error at the end of the wizard as well
                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    Model.ShowGeneralNotification(passwordField.Q<Label>("error").text, false);
                }
                return;
            }

            if (!password.Equals(rePassword)) {
                _log.Debug("form validation: mismatched repeated password");
                //View.Q<Label>("ErrPasswordMismatch").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                // Wizard
                View.Q<Button>("WizardContinueButton").RemoveFromClassList("hide");            
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<VisualElement>("WizardSpinner").AddToClassList("hide");

                rePasswordField.ElementAt(1).AddToClassList("noctua-text-input-error");
                rePasswordField.Q<Label>("error").RemoveFromClassList("hide");
                rePasswordField.Q<Label>("error").text = "Password is not matched with repeated password";
                rePasswordField.Q<VisualElement>("title").style.color = ColorModule.redError;

                // Show the error at the end of the wizard as well
                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    Model.ShowGeneralNotification(rePasswordField.Q<Label>("error").text, false);
                }
                return;
            }

            Dictionary<string, string> regExtra = null;
            
            if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
            {
                if(_gender.value == "Select Gender")
                {
                    _log.Debug("form validation: gender is empty");
                    //View.Q<Label>("ErrEmailEmpty").text = "Please Select Gender!";
                    //View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");
                    View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                    View.Q<VisualElement>("Spinner").AddToClassList("hide");
                    // Wizard
                    View.Q<Button>("WizardContinueButton").RemoveFromClassList("hide");            
                    View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                    View.Q<VisualElement>("WizardSpinner").AddToClassList("hide");

                    _gender.ElementAt(1).AddToClassList("noctua-text-input-error");
                    _gender.Q<Label>("error").RemoveFromClassList("hide");
                    _gender.Q<Label>("error").text = "Please Select Gender!";
                    _gender.Q<VisualElement>("title").style.color = ColorModule.redError;

                    // Show the error at the end of the wizard as well
                    if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                    {
                        Model.ShowGeneralNotification(_gender.Q<Label>("error").text, false);
                    }

                    return;
                }

                var birthDate = DateTime
                    .ParseExact(_birthDate.value, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                    .ToUniversalTime();
                    
                if (birthDate.AddYears(18) > DateTime.UtcNow)
                {
                    _log.Debug("form validation: birthdate under 18");
                    //View.Q<Label>("ErrUnderage").RemoveFromClassList("hide");
                    //View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                    View.Q<VisualElement>("Spinner").AddToClassList("hide");
                    // Wizard
                    View.Q<Button>("WizardContinueButton").RemoveFromClassList("hide");            
                    View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                    View.Q<VisualElement>("WizardSpinner").AddToClassList("hide");

                    _birthDate.ElementAt(1).AddToClassList("noctua-text-input-error");
                    _birthDate.Q<Label>("error").RemoveFromClassList("hide");
                    _birthDate.Q<Label>("error").text = "Minimum age is 18 years old";
                    _birthDate.Q<VisualElement>("title").style.color = ColorModule.redError;

                    // Show the error at the end of the wizard as well
                    if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                    {
                        Model.ShowGeneralNotification(_birthDate.Q<Label>("error").text, false);
                    }

                    return;
                }

                if (_dateOfIssue.text == "")
                {
                    Model.ShowGeneralNotification("Date of issue should not be empty.", false);
                    return;
                }

                var issueDate = DateTime
                    .ParseExact(_dateOfIssue.text, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                    .ToUniversalTime();

                regExtra = new Dictionary<string, string>()
                {
                    { "fullname", _fullname.value },
                    { "phone_number", _phoneNumber.value },
                    { "birth_date", birthDate.ToString() },
                    { "country", _country.value },
                    { "id_card", _idCard.value },
                    { "place_of_issue", _placeOfIssue.value },
                    { "date_of_issue", issueDate.ToString() },
                    { "address", _address.value }
                };
            }

            try {
                var result = await Model.RegisterWithEmailAsync(emailAddress, password, regExtra);
                Debug.Log("RegisterWithPassword verification ID: " + result.Id);

                Visible = false;

                View.Q<TextField>("EmailTF").value = string.Empty;
                View.Q<TextField>("PasswordTF").value = string.Empty;
                View.Q<TextField>("RePasswordTF").value = string.Empty;

                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    _fullname.value = string.Empty;
                    _birthDate.value = string.Empty;
                    _country.value = string.Empty;
                    _idCard.value = string.Empty;
                    _placeOfIssue.value = string.Empty;
                    _dateOfIssue.text = string.Empty;
                    _address.value = string.Empty;
                }

                Model.ShowEmailVerification(emailAddress, password, result.Id);

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                // Wizard
                View.Q<Button>("WizardContinueButton").RemoveFromClassList("hide");            
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<VisualElement>("WizardSpinner").AddToClassList("hide");

            } 
            catch (Exception e) {
                _log.Warning($"{e.Message}\n{e.StackTrace}");
                
                if (e is NoctuaException noctuaEx)
                {
                    View.Q<Label>("ErrCode").text = noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message;
                } else {
                    View.Q<Label>("ErrCode").text = e.Message;
                }

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                // Wizard
                View.Q<Button>("WizardContinueButton").RemoveFromClassList("hide");            
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<VisualElement>("WizardSpinner").AddToClassList("hide");
                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    Model.ShowGeneralNotification(View.Q<Label>("ErrCode").text, false);
                } else {
                    View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                }
            }
        }

        private void OnBackButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking back button");
            View.Q<VisualElement>("WizardSpinner").RemoveFromClassList("hide");

            if (_wizardPage == 4) {
                NavigateToWizard3();
                return;
            } else if (_wizardPage == 3) {
                NavigateToWizard2();
                return;
            } else if (_wizardPage == 2) {
                NavigateToWizard1();
                return;
            }

            View.Q<VisualElement>("Spinner").AddToClassList("hide");
            View.Q<Button>("ContinueButton").RemoveFromClassList("hide");

            Visible = false;
            
            Model.NavigateBack();
        }

        private void OnEmailValueChanged(TextField textField)
        {
            HideAllErrors();

            _email = textField.value;

            AdjustHideLabelElement(textField);
            Utility.UpdateButtonState(textFields, continueButton);
            Utility.UpdateButtonState(textFields, wizardContinueButton);
        }

        private void OnPasswordValueChanged(TextField textField)
        {
            HideAllErrors();

            _password = textField.value;

            AdjustHideLabelElement(textField);
            Utility.UpdateButtonState(textFields, continueButton);
            Utility.UpdateButtonState(textFields, wizardContinueButton);
        }

        private void OnRePasswordValueChanged(TextField textField)
        {
            HideAllErrors();

            _rePassword = textField.value;

            AdjustHideLabelElement(textField);
            Utility.UpdateButtonState(textFields, continueButton);
            Utility.UpdateButtonState(textFields, wizardContinueButton);
        }

        //Behaviour whitelabel - VN
        private void OnValueChanged(TextField textField)
        {
            AdjustHideLabelElement(textField);
            Utility.UpdateButtonState(textFields, continueButton);           
            Utility.UpdateButtonState(textFields, wizardContinueButton);
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

        private void HideAllErrors()
        {
            //Normalize border
            emailField.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");
            passwordField.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");
            rePasswordField.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");
            _gender.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");
            _country.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");
            _birthDate.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");

            emailField.Q<Label>("error").AddToClassList("hide");
            passwordField.Q<Label>("error").AddToClassList("hide");
            rePasswordField.Q<Label>("error").AddToClassList("hide");
            _gender.Q<Label>("error").AddToClassList("hide");
            _country.Q<Label>("error").AddToClassList("hide");
            _birthDate.Q<Label>("error").AddToClassList("hide");

            View.Q<Label>("ErrCode").AddToClassList("hide");
            View.Q<Label>("ErrEmailInvalid").AddToClassList("hide");
            View.Q<Label>("ErrEmailEmpty").AddToClassList("hide");
            View.Q<Label>("ErrPasswordTooShort").AddToClassList("hide");
            View.Q<Label>("ErrPasswordEmpty").AddToClassList("hide");
            View.Q<Label>("ErrPasswordMismatch").AddToClassList("hide");
            View.Q<Label>("ErrUnderage").AddToClassList("hide");
        }
    }
}
