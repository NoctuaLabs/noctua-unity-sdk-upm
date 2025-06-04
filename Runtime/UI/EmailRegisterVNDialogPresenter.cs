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
    internal class EmailRegisterVNDialogPresenter : Presenter<AuthenticationModel>
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
        private Label _birthDateLabel;
        private DropdownField _gender;
        private InputFieldNoctua _country;
        private InputFieldNoctua _idCard;
        private InputFieldNoctua _placeOfIssue;
        private InputFieldNoctua _dateOfIssue;
        private InputFieldNoctua _address;
        private List<TextField> textFields;

        // OEG OTP
        private string _verificationId;
        private InputFieldNoctua _phoneNumberVerificationCode;

        private ButtonNoctua _continueButton; // Main submit button
        private ButtonNoctua _wizardContinueButton; // Submit button on full KYC
        private ButtonNoctua _wizard5ContinueButton; // Submit button on phone number verification
        private GlobalConfig _config;

        private int _wizardPage = 0;
        private bool _isDatePickerOpen = false;

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
            _wizardContinueButton = new ButtonNoctua(View.Q<Button>("WizardContinueButton"));
            _wizard5ContinueButton = new ButtonNoctua(View.Q<Button>("Wizard5ContinueButton"));
            var backButton = View.Q<Button>("BackButton");
            var loginLink = View.Q<Label>("LoginLink");

            //Behaviour whitelabel - VN
            _fullname = new InputFieldNoctua(View.Q<TextField>("FullNameTF"));
            _phoneNumber = new InputFieldNoctua(View.Q<TextField>("PhoneNumberTF"));
            _birthDate = new InputFieldNoctua(View.Q<TextField>("BirthdateTF"));
            _birthDateLabel = View.Q<Label>("BirthdateTFLabel");
            _gender = View.Q<DropdownField>("GenderTF");
            _country = new InputFieldNoctua(View.Q<TextField>("CountryTF"));
            _idCard = new InputFieldNoctua(View.Q<TextField>("IDCardTF"));
            _placeOfIssue = new InputFieldNoctua(View.Q<TextField>("PlaceOfIssueTF"));
            _dateOfIssue = new InputFieldNoctua(View.Q<TextField>("DateOfIssueTF"));
            _address = new InputFieldNoctua(View.Q<TextField>("AddressTF"));
            _phoneNumberVerificationCode = new InputFieldNoctua(View.Q<TextField>("PhoneNumberVerificationCodeTF"));

            if (IsVNLegalPurposeEnabled())
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

            if (IsVNLegalPurposeEnabled())
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
            Utility.UpdateButtonState(textFields, _wizard5ContinueButton.button);

            // Callbacks
            _continueButton.button.RegisterCallback<PointerUpEvent>(OnContinueButtonClick);
            _wizardContinueButton.button.RegisterCallback<PointerUpEvent>(OnWizardContinueButtonClick);
            _wizard5ContinueButton.button.RegisterCallback<PointerUpEvent>(OnWizard5ContinueButtonClick);
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
            if (IsVNLegalPurposeEnabled())
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
                View.Q<Button>("WizardPrevTo4Button").RegisterCallback<PointerUpEvent>(NavigateToWizard4);
                // Hide the footer content
                View.Q<VisualElement>("footerContent").AddToClassList("hide");
                // Navigate to the first wizard
                View.Q<VisualElement>("RegisterWizard1NextButton").RemoveFromClassList("hide");

                View.Q<Button>("ContinueButton").AddToClassList("hide");

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
        public void NavigateToWizard1(PointerUpEvent evt = null)
        {
            _isDatePickerOpen = false;

            _wizardPage = 1;
            View.Q<VisualElement>("RegisterWizard1").RemoveFromClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard5").AddToClassList("hide");
            View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
            View.Q<VisualElement>("footerContent").AddToClassList("wizard-register-footer");
            View.Q<VisualElement>("Loading").AddToClassList("hide");

            _log.Debug("Navigating to wizard 1");
        }

        public void NavigateToWizard2(PointerUpEvent evt = null)
        {
            _wizardPage = 2;
            View.Q<VisualElement>("footerContent").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").RemoveFromClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard5").AddToClassList("hide");
            View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
            View.Q<VisualElement>("Loading").AddToClassList("hide");

            _log.Debug("Navigating to wizard 2");
        }

        public void NavigateToWizard3(PointerUpEvent evt = null)
        {

            if (IsVNLegalPurposeFullKYCEnabled())
            {
                HideAllErrors();
                _wizardPage = 3;
                View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
                View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
                View.Q<VisualElement>("RegisterWizard3").RemoveFromClassList("hide");
                View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");
                View.Q<VisualElement>("RegisterWizard5").AddToClassList("hide");
                View.Q<VisualElement>("footerContent").AddToClassList("hide");
                View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
                View.Q<VisualElement>("Loading").AddToClassList("hide");
            }
            else
            {
                if (IsVNLegalPurposePhoneNumberVerificationEnabled())
                {
                    // Show phone number verification wizard
                    _log.Debug("Navigating to wizard 5 for phone number verification");
                    // OnWizardContinueButtonClick(evt);
                }
                else
                {
                    // Immediately submit register
                    OnContinueButtonClick(evt);
                }
            }

            _log.Debug("Navigating to wizard 3");
        }

        public void NavigateToWizard4(PointerUpEvent evt = null)
        {
            _wizardPage = 4;
            View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").RemoveFromClassList("hide");
            View.Q<VisualElement>("RegisterWizard5").AddToClassList("hide");
            View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
            View.Q<VisualElement>("Loading").AddToClassList("hide");

            _log.Debug("Navigating to wizard 4");
        }

        // Wizard 5 is for phone number verification
        public void NavigateToWizard5(PointerUpEvent evt = null)
        {
            _wizardPage = 5;
            View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard5").RemoveFromClassList("hide");
            View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
            View.Q<VisualElement>("Loading").AddToClassList("hide");
            _wizard5ContinueButton.ToggleLoading(false);
            View.Q<Button>("WizardPrevTo4Button").RemoveFromClassList("hide");

            _log.Debug("Navigating to wizard 5");
        }
        public void NavigateToLoading(PointerUpEvent evt = null)
        {
            View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard5").AddToClassList("hide");
            View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
            View.Q<VisualElement>("Loading").RemoveFromClassList("hide");

            _log.Debug("Navigating to loading screen");
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

            #if UNITY_EDITOR
            // We are altering the behaviour of the form/wizard
            // for in editor for easier development
            #else
            string startDate = "01/01/2000";
            DateTime parsedDate = DateTime.ParseExact(startDate, "dd/MM/yyyy", null);

            void OpenDatePickerHandler(int pickerId, TextField textField, PointerUpEvent upEvent)
            {
                //handle open twice date picker
                upEvent.StopImmediatePropagation();
                
                if(_isDatePickerOpen)
                {
                    return;
                }

                _isDatePickerOpen = true;

                Noctua.OpenDatePicker(parsedDate.Year, parsedDate.Month, parsedDate.Day, pickerId,
                (DateTime _date) =>
                {
                    Debug.Log("Date Selected :" + _date.ToString("dd/MM/yyyy"));
                },
                (DateTime _date) =>
                {
                    Debug.Log("Date Picked :" + _date.ToString("dd/MM/yyyy"));

                    textField.value = _date.ToString("dd/MM/yyyy");
                    _isDatePickerOpen = false;
                });
            }

            View.Q<VisualElement>("BirthdateContainer").UnregisterCallback<PointerUpEvent>(evt => { });

            View.Q<VisualElement>("BirthdateContainer").RegisterCallback<ClickEvent>(upEvent =>
            {
                _log.Debug("BirthdateContainer clicked : " + _isDatePickerOpen);

                upEvent.StopImmediatePropagation();

                if(_isDatePickerOpen)
                {
                    return;
                }

                _isDatePickerOpen = true;

                Noctua.OpenDatePicker(parsedDate.Year, parsedDate.Month, parsedDate.Day, 1,
                (DateTime _date) =>
                {
                    _log.Debug($"picked date '{_date:O}'");

                    _isDatePickerOpen = false;
                },
                (DateTime _date) =>
                {
                    _birthDate.textField.value = _date.ToString("dd/MM/yyyy");
                    _birthDate.textField.labelElement.style.display = DisplayStyle.None;

                    Utility.UpdateButtonState(textFields, _continueButton.button);
                    Utility.UpdateButtonState(textFields, _wizardContinueButton.button);
                    Utility.UpdateButtonState(textFields, _wizard5ContinueButton.button);

                    _isDatePickerOpen = false;

                });
            });

            View.Q<VisualElement>("DateOfIssueContainer").UnregisterCallback<PointerUpEvent>(evt => { });
            View.Q<VisualElement>("DateOfIssueContainer").RegisterCallback<ClickEvent>(upEvent =>
            {

                upEvent.StopImmediatePropagation();

                if(_isDatePickerOpen)
                {
                    return;
                }

                _isDatePickerOpen = true;

                Noctua.OpenDatePicker(parsedDate.Year, parsedDate.Month, parsedDate.Day, 1,
                (DateTime _date) =>
                {
                    _log.Debug($"picked date '{_date:O}'");

                    _isDatePickerOpen = false;
                },
                (DateTime _date) =>
                {
                    _dateOfIssue.textField.value = _date.ToString("dd/MM/yyyy");
                    _dateOfIssue.textField.labelElement.style.display = DisplayStyle.None;

                    Utility.UpdateButtonState(textFields, _continueButton.button);
                    Utility.UpdateButtonState(textFields, _wizardContinueButton.button);
                    Utility.UpdateButtonState(textFields, _wizard5ContinueButton.button);

                    _isDatePickerOpen = false;

                });
            });
            #endif

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

            Model.ShowEmailLogin();
        }

        // Register button from full KCY wizard
        private async void OnWizardContinueButtonClick(PointerUpEvent evt)
        {

            if (IsVNLegalPurposePhoneNumberVerificationEnabled())
            {
                _isDatePickerOpen = false;

                if (!validateForm())
                {
                    _log.Debug("OEG validation failed");
                    return;
                }

                // Send phone number verification code
                try
                {
                    _log.Debug("OEG sending phone number verification code");

                    var result = await Model.AuthService.RegisterWithEmailSendPhoneNumberVerificationAsync(_phoneNumber.text);

                    _verificationId = result.VerificationId;
                    _log.Debug("OEG verificationId: " + _verificationId);

                    _wizardContinueButton.ToggleLoading(false);
                    View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                    Visible = false;

                    _isDatePickerOpen = false;
                
                    var birthDate = DateTime
                        .ParseExact(_birthDate.text, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                        .ToUniversalTime();

                    var issueDate = DateTime
                        .ParseExact(_dateOfIssue.text, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                        .ToUniversalTime();

                    var regExtra = new Dictionary<string, string>()
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

                    Model.ShowPhoneVerification
                    (
                        verificationId: _verificationId,
                        phoneNumber: _phoneNumber.text.Replace(" ", string.Empty),
                        emailAddress: _inputEmail.text.Replace(" ", string.Empty),
                        password: _inputPassword.text,
                        regExtra: regExtra
                        
                    );

                    // Deprecated 
                    // NavigateToWizard5();
                }
                catch (Exception e)
                {

                    _wizardContinueButton.ToggleLoading(false);
                    View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                    _log.Debug("OEG send verification code failed: " + e.Message);
                    if (e is NoctuaException noctuaEx)
                    {
                        switch (noctuaEx.ErrorCode)
                        {
                            case 2061:
                                _log.Debug("OEG send verification code failed: " + noctuaEx.Message);
                                Model.ShowGeneralNotification(noctuaEx.Message, true);
                                NavigateToWizard5();
                                break;
                            default:
                                _log.Debug("OEG send verification code failed: " + noctuaEx.Message);
                                Model.ShowGeneralNotification(noctuaEx.Message, false);
                                NavigateToWizard4();
                                break;
                        }
                    }
                }
            } else {
                _log.Debug("Continuing to registration");

                OnContinueButtonClick(evt);
            }
        }

        // Register button from phone number verification wizard
        private async void OnWizard5ContinueButtonClick(PointerUpEvent evt)
        {
            View.Q<Button>("WizardPrevTo4Button").AddToClassList("hide");
            View.Q<Button>("WizardPrevTo3Button").AddToClassList("hide");

            // Performs verification code / OTP check
            try
            {
                var result = await Model.AuthService.RegisterWithEmailVerifyPhoneNumberAsync(_verificationId, _phoneNumberVerificationCode.text);

                _wizard5ContinueButton.ToggleLoading(false);
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<Button>("WizardPrevTo4Button").RemoveFromClassList("hide");
                
                // If it passed, continue to the email registration
                OnContinueButtonClick(evt);
            }
            catch (Exception e)
            {

                _wizard5ContinueButton.ToggleLoading(false);
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<Button>("WizardPrevTo4Button").RemoveFromClassList("hide");

                _log.Debug("OEG verification failed: " + e.Message);
                if (e is NoctuaException noctuaEx)
                {
                    switch (noctuaEx.ErrorCode)
                    {
                        default:
                            _log.Debug("OEG verification failed: " + noctuaEx.Message);
                            Model.ShowGeneralNotification(noctuaEx.Message, false);
                            NavigateToWizard5();
                            break;
                    }
                }
            }

        }

        private bool validateForm()
        {
            HideAllErrors();

            _continueButton.ToggleLoading(true);
            _wizardContinueButton.ToggleLoading(true);
            _wizard5ContinueButton.ToggleLoading(true);

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
                _wizard5ContinueButton.ToggleLoading(false);

                //Wizard
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                _inputEmail.Error(Utility.ValidateEmail(emailAddress));

                // Show the error at the end of the wizard as well
                if (IsVNLegalPurposeEnabled())
                {
                    Model.ShowGeneralNotification(_inputEmail.labelError.text, false);
                }

                return false;
            }

            if (!string.IsNullOrEmpty(Utility.ValidatePassword(password)))
            {
                _continueButton.ToggleLoading(false);
                _wizardContinueButton.ToggleLoading(false);
                _wizard5ContinueButton.ToggleLoading(false);

                //Wizard
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                _inputPassword.Error(Utility.ValidatePassword(password));

                // Show the error at the end of the wizard as well
                if (IsVNLegalPurposeEnabled())
                {
                    Model.ShowGeneralNotification(_inputPassword.labelError.text, false);
                }

                return false;
            }

            if (!string.IsNullOrEmpty(Utility.ValidateReenterPassword(password, rePassword)))
            {
                _continueButton.ToggleLoading(false);
                _wizardContinueButton.ToggleLoading(false);
                _wizard5ContinueButton.ToggleLoading(false);

                //Wizard
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                _inputRepassword.Error(Utility.ValidateReenterPassword(password, rePassword));

                // Show the error at the end of the wizard as well
                if (IsVNLegalPurposeEnabled())
                {
                    Model.ShowGeneralNotification(_inputRepassword.labelError.text, false);
                }

                return false;
            }

            if (IsVNLegalPurposeEnabled())
            {
                if (_gender.value == "Select Gender")
                {
                    _log.Debug("form validation: gender is empty");

                    _continueButton.ToggleLoading(false);
                    _wizardContinueButton.ToggleLoading(false);
                    _wizard5ContinueButton.ToggleLoading(false);

                    //Wizard
                    View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                    _gender.ElementAt(1).AddToClassList("noctua-text-input-error");
                    _gender.Q<Label>("error").RemoveFromClassList("hide");
                    _gender.Q<Label>("error").text = "Please Select Gender!";
                    _gender.Q<VisualElement>("title").style.color = ColorModule.redError;

                    // Show the error at the end of the wizard as well
                    if (IsVNLegalPurposeEnabled())
                    {
                        Model.ShowGeneralNotification(_gender.Q<Label>("error").text, false);
                    }

                    return false;
                }

                var birthDate = DateTime
                    .ParseExact(_birthDate.text, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                    .ToUniversalTime();

                if (birthDate.AddYears(18) > DateTime.UtcNow)
                {
                    _log.Debug("form validation: birthdate under 18");

                    _continueButton.ToggleLoading(false);
                    _wizardContinueButton.ToggleLoading(false);
                    _wizard5ContinueButton.ToggleLoading(false);

                    //Wizard
                    View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");

                    _birthDate.Error("Minimum age is 18 years old");

                    // Show the error at the end of the wizard as well
                    if (IsVNLegalPurposeEnabled())
                    {
                        Model.ShowGeneralNotification(_birthDate.labelError.text, false);
                    }

                    return false;
                }

                if (_dateOfIssue.text == "")
                {
                    Model.ShowGeneralNotification("Date of issue should not be empty.", false);
                    return false;
                }
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

            if (IsVNLegalPurposeEnabled())
            {
                _isDatePickerOpen = false;
                
                var birthDate = DateTime
                    .ParseExact(_birthDate.text, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                    .ToUniversalTime();

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

                _log.Debug("Register extra: " + JsonConvert.SerializeObject(regExtra));
            }

            try {
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

                if (IsVNLegalPurposeEnabled())
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
                _wizard5ContinueButton.Clear();
                // Wizard                
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");                
                View.Q<Button>("WizardPrevTo4Button").RemoveFromClassList("hide");

            }
            catch (Exception e)
            {

                _wizard5ContinueButton.ToggleLoading(false);
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                View.Q<Button>("WizardPrevTo4Button").RemoveFromClassList("hide");

                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    _continueButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);
                    _wizardContinueButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);
                    _wizard5ContinueButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);
                }
                else
                {
                    _continueButton.Error(e.Message);
                    _wizardContinueButton.Error(e.Message);                    
                    _wizard5ContinueButton.Error(e.Message);                    
                }

                _continueButton.ToggleLoading(false);
                _wizardContinueButton.ToggleLoading(false);
                _wizard5ContinueButton.ToggleLoading(false);
                // Wizard                
                View.Q<Button>("WizardPrevTo3Button").RemoveFromClassList("hide");
                
                if (IsVNLegalPurposeEnabled())
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

            _isDatePickerOpen = false;         

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
            _wizard5ContinueButton.Clear();

            Visible = false;

            Model.NavigateBack();
        }

        private void OnValueChanged(InputFieldNoctua input)
        {
            input.AdjustLabel();
            Utility.UpdateButtonState(textFields, _continueButton.button);
            Utility.UpdateButtonState(textFields, _wizardContinueButton.button);
            Utility.UpdateButtonState(textFields, _wizard5ContinueButton.button);
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
            _wizard5ContinueButton.Clear();
        }

        private bool IsVNLegalPurposeEnabled()
        {
            return _config.Noctua.RemoteFeatureFlags.ContainsKey("vnLegalPurposeEnabled") == true && _config.Noctua.RemoteFeatureFlags["vnLegalPurposeEnabled"] == true;
        }

        private bool IsVNLegalPurposeFullKYCEnabled()
        {
            return _config.Noctua.RemoteFeatureFlags.ContainsKey("vnLegalPurposeFullKYCEnabled") == true && _config.Noctua.RemoteFeatureFlags["vnLegalPurposeFullKYCEnabled"] == true;
        }

        private bool IsVNLegalPurposePhoneNumberVerificationEnabled()
        {
            return _config.Noctua.RemoteFeatureFlags.ContainsKey("vnLegalPurposePhoneNumberVerificationEnabled") == true && _config.Noctua.RemoteFeatureFlags["vnLegalPurposePhoneNumberVerificationEnabled"] == true;
        }
    }
}
