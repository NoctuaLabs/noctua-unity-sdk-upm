using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using System.Threading.Tasks;
using System.Globalization;
using Newtonsoft.Json;

namespace com.noctuagames.sdk.UI
{
    internal class EmailRegisterDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();

        private InputFieldNoctua _inputEmail;
        private InputFieldNoctua _inputPassword;
        private InputFieldNoctua _inputRepassword;

        private Button _showPasswordButton;
        private Button _showRePasswordButton;

        //Behaviour whitelabel - VN
        private InputFieldNoctua _fullname;
        private InputFieldNoctua _phoneNumber;
        private InputFieldNoctua _birthDate;
        private DropdownField _gender;
        private InputFieldNoctua _country;
        private InputFieldNoctua _idCard;
        private InputFieldNoctua _placeOfIssue;
        private Button _dateOfIssue;
        private InputFieldNoctua _address;
        private List<TextField> textFields;

        private ButtonNoctua _continueButton;
        private ButtonNoctua _wizardContinueButton;
        private GlobalConfig _config;

        private int _wizardPage = 0;

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

        private void SetupInputFields(bool clearForm)
        {
            panelVE = View.Q<VisualElement>("NoctuaRegisterBox");
            _inputEmail = new InputFieldNoctua(View.Q<TextField>("EmailTF"));
            _inputPassword = new InputFieldNoctua(View.Q<TextField>("PasswordTF"));
            _inputRepassword = new InputFieldNoctua(View.Q<TextField>("RePasswordTF"));

            _showPasswordButton = View.Q<Button>("ShowPasswordButton");
            _showRePasswordButton = View.Q<Button>("ShowRePasswordButton");

            _continueButton = new ButtonNoctua(View.Q<Button>("ContinueButton"));
            _wizardContinueButton = new ButtonNoctua(View.Q<Button>("WizardContinueButton"));
            var backButton = View.Q<Button>("BackButton");
            var loginLink = View.Q<Label>("LoginLink");

            //Behaviour whitelabel - VN
            _fullname = new InputFieldNoctua(View.Q<TextField>("FullNameTF"));
            _phoneNumber = new InputFieldNoctua(View.Q<TextField>("PhoneNumberTF"));
            _birthDate = new InputFieldNoctua(View.Q<TextField>("BirthdateTF"));
            _gender = View.Q<DropdownField>("GenderTF");
            _country = new InputFieldNoctua(View.Q<TextField>("CountryTF"));
            _idCard = new InputFieldNoctua(View.Q<TextField>("IDCardTF"));
            _placeOfIssue = new InputFieldNoctua(View.Q<TextField>("PlaceOfIssueTF"));
            _dateOfIssue = View.Q<Button>("DateOfIssueTF");
            _address = new InputFieldNoctua(View.Q<TextField>("AddressTF"));

            if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
            {
                SetupDropdown();
                SetupDatePicker();
                // Disable country dropdown as Vietnam copublisher is asking for raw text input for country.
                // SetCountries();
            }

            // Default values
            if (clearForm)
            {
                _inputPassword.textField.isPasswordField = true;
                _inputRepassword.textField.isPasswordField = true;
            }

            if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
            {
                textFields = new List<TextField>
                {
                    _inputEmail.textField,
                    _inputPassword.textField,
                    _inputRepassword.textField,
                    _fullname.textField,
                    _phoneNumber.textField,
                    _birthDate.textField,
                    _country.textField,
                    _idCard.textField,
                    _placeOfIssue.textField,
                    _address.textField
                };
            }
            else
            {
                textFields = new List<TextField>
                {
                    _inputEmail.textField,
                    _inputPassword.textField,
                    _inputRepassword.textField

                };
            }

            Utility.UpdateButtonState(textFields, _continueButton.button);
            Utility.UpdateButtonState(textFields, _wizardContinueButton.button);

            // Callbacks
            _continueButton.button.RegisterCallback<ClickEvent>(OnContinueButtonClick);
            _wizardContinueButton.button.RegisterCallback<ClickEvent>(OnContinueButtonClick);
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
            _inputEmail.SetFocus();
            _inputPassword.SetFocus();
            _inputRepassword.SetFocus();

            #region Behaviour whitelabel - VN
            if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
            {
                _fullname.textField.RegisterValueChangedCallback(evt => OnValueChanged(_fullname));
                _phoneNumber.textField.RegisterValueChangedCallback(evt => OnValueChanged(_phoneNumber));
                _country.textField.RegisterValueChangedCallback(evt => OnValueChanged(_country));
                _idCard.textField.RegisterValueChangedCallback(evt => OnValueChanged(_idCard));
                _placeOfIssue.textField.RegisterValueChangedCallback(evt => OnValueChanged(_placeOfIssue));
                _address.textField.RegisterValueChangedCallback(evt => OnValueChanged(_address));

                _fullname.SetFocus();
                _phoneNumber.SetFocus();
                _country.SetFocus();
                _idCard.SetFocus();
                _placeOfIssue.SetFocus();
                _address.SetFocus();

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
            }
            else
            {
                // Show the footer content
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("footerContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("footerContent").AddToClassList("generic-register-footer");                
            }
            #endregion
        }

        #region Navigate Wizard
        public async void NavigateToWizard1(PointerUpEvent evt = null)
        {
            _wizardPage = 1;
            View.Q<VisualElement>("RegisterWizard1").RemoveFromClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");            
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
            View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
        }

        public async void NavigateToWizard3(PointerUpEvent evt = null)
        {
            HideAllErrors();
            _wizardPage = 3;
            View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").RemoveFromClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");            
            View.Q<VisualElement>("footerContent").AddToClassList("hide");
            View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
        }

        public async void NavigateToWizard4(PointerUpEvent evt = null)
        {
            _wizardPage = 4;
            View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").RemoveFromClassList("hide");            
            View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
        }
        #endregion

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

        private void SetupDropdown()
        {
            var genderChoices = new List<string> { Locale.GetTranslation("Select.Gender.Male"), Locale.GetTranslation("Select.Gender.Female") };
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
            _birthDate.textField.isReadOnly = true;

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

            View.Q<VisualElement>("BirthdateContainer").UnregisterCallback<PointerUpEvent>(evt => { });

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
                    _birthDate.textField.value = _date.ToString("dd/MM/yyyy");
                    _birthDate.textField.labelElement.style.display = DisplayStyle.None;
                    Utility.UpdateButtonState(textFields, _continueButton.button);
                    Utility.UpdateButtonState(textFields, _wizardContinueButton.button);

                });
            });

            View.Q<VisualElement>("DateOfIssueContainer").UnregisterCallback<PointerUpEvent>(evt => { });
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
                    Utility.UpdateButtonState(textFields, _continueButton.button);
                    Utility.UpdateButtonState(textFields, _wizardContinueButton.button);

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

        private void OnLoginLinkClick(ClickEvent evt)
        {
            Visible = false;

            Model.ClearNavigation();
            Model.PushNavigation(() => Model.ShowEmailRegistration(false));
            Model.ShowEmailLogin();
        }

        private async void OnContinueButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking continue button");

            HideAllErrors();

            _continueButton.ToggleLoading(true);
            _wizardContinueButton.ToggleLoading(true);

            // Wizard            
            View.Q<Button>("WizardPrevTo3Button").AddToClassList("hide");

            var emailAddress = _inputEmail.text.Replace(" ", string.Empty);
            var password = _inputPassword.text;
            var rePassword = _inputRepassword.text;

            // Validation
            if (!string.IsNullOrEmpty(Utility.ValidateEmail(emailAddress)))
            {
                _continueButton.ToggleLoading(false);
                _wizardContinueButton.ToggleLoading(false);

                //Wizard
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                _inputEmail.Error(Utility.ValidateEmail(emailAddress));

                // Show the error at the end of the wizard as well
                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    Model.ShowGeneralNotification(_inputEmail.labelError.text, false);
                }

                return;
            }

            if (!string.IsNullOrEmpty(Utility.ValidatePassword(password)))
            {
                _continueButton.ToggleLoading(false);
                _wizardContinueButton.ToggleLoading(false);

                //Wizard
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                _inputPassword.Error(Utility.ValidatePassword(password));

                // Show the error at the end of the wizard as well
                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    Model.ShowGeneralNotification(_inputPassword.labelError.text, false);
                }

                return;
            }

            if (!string.IsNullOrEmpty(Utility.ValidateReenterPassword(password, rePassword)))
            {
                _continueButton.ToggleLoading(false);
                _wizardContinueButton.ToggleLoading(false);

                //Wizard
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                _inputRepassword.Error(Utility.ValidateReenterPassword(password, rePassword));

                // Show the error at the end of the wizard as well
                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    Model.ShowGeneralNotification(_inputRepassword.labelError.text, false);
                }

                return;
            }

            Dictionary<string, string> regExtra = null;

            if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
            {
                if (_gender.value == "Select Gender")
                {
                    _log.Debug("form validation: gender is empty");

                    _continueButton.ToggleLoading(false);
                    _wizardContinueButton.ToggleLoading(false);

                    //Wizard
                    View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

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
                    .ParseExact(_birthDate.text, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                    .ToUniversalTime();

                if (birthDate.AddYears(18) > DateTime.UtcNow)
                {
                    _log.Debug("form validation: birthdate under 18");

                    _continueButton.ToggleLoading(false);
                    _wizardContinueButton.ToggleLoading(false);

                    //Wizard
                    View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                    _birthDate.Error("Minimum age is 18 years old");

                    // Show the error at the end of the wizard as well
                    if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                    {
                        Model.ShowGeneralNotification(_birthDate.labelError.text, false);
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
                    { "fullname", _fullname.text },
                    { "phone_number", _phoneNumber.text},
                    { "birth_date", birthDate.ToString() },
                    { "country", _country.text},
                    { "id_card", _idCard.text},
                    { "place_of_issue", _placeOfIssue.text},
                    { "date_of_issue", issueDate.ToString() },
                    { "address", _address.text}
                };
            }

            try {
                CredentialVerification result;
                
                switch (Model.AuthIntention)
                {
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

                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    _fullname.Clear();
                    _birthDate.Clear();
                    _country.Clear();
                    _idCard.Clear();
                    _placeOfIssue.Clear();
                    _dateOfIssue.Clear();
                    _address.Clear();
                }

                Model.ShowEmailVerification(emailAddress, password, result.Id, regExtra);

                _continueButton.Clear();
                _wizardContinueButton.Clear();
                // Wizard                
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");                

            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    _continueButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);
                    _wizardContinueButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);
                }
                else
                {
                    _continueButton.Error(e.Message);
                    _wizardContinueButton.Error(e.Message);                    
                }

                _continueButton.ToggleLoading(false);
                _wizardContinueButton.ToggleLoading(false);
                // Wizard                
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                
                if (!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _config!.Noctua!.Flags!.Contains("VNLegalPurpose"))
                {
                    Model.ShowGeneralNotification(_continueButton.labelError.text, false);
                }
                else
                {
                    View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                }
            }
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking back button");            

            if (_wizardPage == 4)
            {
                NavigateToWizard3();
                return;
            }
            else if (_wizardPage == 3)
            {
                NavigateToWizard2();
                return;
            }
            else if (_wizardPage == 2)
            {
                NavigateToWizard1();
                return;
            }

            _continueButton.Clear();
            _wizardContinueButton.Clear();

            Visible = false;

            Model.NavigateBack();
        }

        private void OnValueChanged(InputFieldNoctua input)
        {
            input.AdjustLabel();
            Utility.UpdateButtonState(textFields, _continueButton.button);
            Utility.UpdateButtonState(textFields, _wizardContinueButton.button);
        }

        private void HideAllErrors()
        {
            //Normalize border
            _inputEmail.Reset();
            _inputPassword.Reset();
            _inputRepassword.Reset();
            _country.Reset();
            _birthDate.Reset();

            _gender.Children().ElementAt(1).RemoveFromClassList("noctua-text-input-error");
            _gender.Q<Label>("error").AddToClassList("hide");

            _continueButton.Clear();
            _wizardContinueButton.Clear();
        }
    }
}
