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

        // Behaviour whitelabel - VN (Vietnam specific fields)
        private InputFieldNoctua _fullname;
        private InputFieldNoctua _phoneNumber;
        private InputFieldNoctua _birthDate;
        // private Label _birthDateLabel; // Initialized but seems unused actively later, can be removed if it doesn't affect UI
        private DropdownField _gender;
        private InputFieldNoctua _country;
        private InputFieldNoctua _idCard;
        private InputFieldNoctua _placeOfIssue;
        private InputFieldNoctua _dateOfIssue;
        private InputFieldNoctua _address;
        private List<TextField> textFields;

        // OEG OTP (One-Time Password for VN flow)
        private string _verificationId;
        private InputFieldNoctua _phoneNumberVerificationCode;

        private ButtonNoctua _continueButton; // Main submit button, hidden in VN wizard flow but used for simpler VN paths
        private ButtonNoctua _wizardContinueButton; // Submit button on full KYC wizard page
        private ButtonNoctua _wizard5ContinueButton; // Submit button on phone number verification wizard page
        private GlobalConfig _config;

        private int _wizardPage = 0; // Current page in the VN wizard
        private bool _isDatePickerOpen = false; // Flag to manage date picker state

        protected override void Attach() { }
        protected override void Detach() { }

        private void Start()
        {
            // Assume IsVNLegalPurposeEnabled() is always true for this component if it's VN-only
            SetupInputFields(true);
            HideAllErrors();
        }

        public void Show(bool clearForm, bool isRegisterOnly)
        {
            // Assume IsVNLegalPurposeEnabled() is always true for this component
            SetupInputFields(clearForm);
            HideAllErrors();
            
            // VN flow manages its own footer display through wizard navigation
            var additionalFooter = View.Q<VisualElement>("AdditionalFooterContent");
            additionalFooter.AddToClassList("hide"); // Wizard will control this

            // Show co-publisher logo (general, can be kept)
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
            // This log line can be kept for debugging VN configurations
            _log.Debug("behaviour Whitelabel (VN Flow): " + JsonConvert.SerializeObject(_config?.Noctua?.RemoteFeatureFlags));
        }

        private void SetupInputFields(bool clearForm)
        {
            _inputEmail = new InputFieldNoctua(View.Q<TextField>("EmailTF"));
            _inputPassword = new InputFieldNoctua(View.Q<TextField>("PasswordTF"));
            _inputRepassword = new InputFieldNoctua(View.Q<TextField>("RePasswordTF"));

            _showPasswordButton = View.Q<Button>("ShowPasswordButton");
            _showRePasswordButton = View.Q<Button>("ShowRePasswordButton");

            _continueButton = new ButtonNoctua(View.Q<Button>("ContinueButton")); // Needed for non-wizard/fallback VN paths
            _wizardContinueButton = new ButtonNoctua(View.Q<Button>("WizardContinueButton"));
            _wizard5ContinueButton = new ButtonNoctua(View.Q<Button>("Wizard5ContinueButton"));
            var backButton = View.Q<Button>("BackButton");
            var loginLink = View.Q<Label>("LoginLink");

            // Initialize VN-specific fields
            _fullname = new InputFieldNoctua(View.Q<TextField>("FullNameTF"));
            _phoneNumber = new InputFieldNoctua(View.Q<TextField>("PhoneNumberTF"));

            _birthDate = new InputFieldNoctua(View.Q<TextField>("BirthdateTF"));
            _birthDate.textField.isReadOnly = true; // Make birthdate field read-only, handled by date picker
            
            // _birthDateLabel = View.Q<Label>("BirthdateTFLabel"); // If unused, remove
            _gender = View.Q<DropdownField>("GenderTF");
            _country = new InputFieldNoctua(View.Q<TextField>("CountryTF"));
            _idCard = new InputFieldNoctua(View.Q<TextField>("IDCardTF"));
            _placeOfIssue = new InputFieldNoctua(View.Q<TextField>("PlaceOfIssueTF"));

            _dateOfIssue = new InputFieldNoctua(View.Q<TextField>("DateOfIssueTF"));
            _dateOfIssue.textField.isReadOnly = true; // Make date of issue field read-only, handled by date picker

            _address = new InputFieldNoctua(View.Q<TextField>("AddressTF"));
            _phoneNumberVerificationCode = new InputFieldNoctua(View.Q<TextField>("PhoneNumberVerificationCodeTF"));

            SetupDropdown();
            SetupDatePicker();

            if (clearForm)
            {
                _inputPassword.textField.isPasswordField = true;
                _inputRepassword.textField.isPasswordField = true;
            }

            // textFields always include VN fields
            textFields = new List<TextField>
            {
                _inputEmail.textField, _inputPassword.textField, _inputRepassword.textField,
                _fullname.textField, _phoneNumber.textField, _birthDate.textField,
                _country.textField, _idCard.textField, _placeOfIssue.textField, 
                _dateOfIssue.textField, // Added for consistency
                _address.textField
            };
            
            Utility.UpdateButtonState(textFields, _continueButton.button); // For non-wizard VN paths
            Utility.UpdateButtonState(textFields, _wizardContinueButton.button);
            Utility.UpdateButtonState(textFields, _wizard5ContinueButton.button);

            // Callbacks
            // If ContinueButton has a role in simpler VN flows (without full wizard), this callback is kept.
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
            
            // Callbacks for VN fields
            _fullname.textField.RegisterValueChangedCallback(evt => OnValueChanged(_fullname));
            _phoneNumber.textField.RegisterValueChangedCallback(evt => OnValueChanged(_phoneNumber));
            _birthDate.textField.RegisterValueChangedCallback(evt => OnValueChanged(_birthDate)); // Added if InputFieldNoctua
            _country.textField.RegisterValueChangedCallback(evt => OnValueChanged(_country));
            _idCard.textField.RegisterValueChangedCallback(evt => OnValueChanged(_idCard));
            _placeOfIssue.textField.RegisterValueChangedCallback(evt => OnValueChanged(_placeOfIssue));
            _dateOfIssue.textField.RegisterValueChangedCallback(evt => OnValueChanged(_dateOfIssue)); // Added
            _address.textField.RegisterValueChangedCallback(evt => OnValueChanged(_address));

            _inputEmail.SetFocus(); _inputPassword.SetFocus(); _inputRepassword.SetFocus();
            _fullname.SetFocus(); _phoneNumber.SetFocus(); _birthDate.SetFocus(); // Added if InputFieldNoctua
            _country.SetFocus(); _idCard.SetFocus(); _placeOfIssue.SetFocus();
            _dateOfIssue.SetFocus(); // Added
            _address.SetFocus();

            // VN Wizard specific UI logic
            View.Q<Button>("WizardNextTo2Button").RegisterCallback<PointerUpEvent>(NavigateToWizard2);
            View.Q<Button>("WizardNextTo3Button").RegisterCallback<PointerUpEvent>(NavigateToWizard3);
            View.Q<Button>("WizardNextTo4Button").RegisterCallback<PointerUpEvent>(NavigateToWizard4);
            View.Q<Button>("WizardPrevTo1Button").RegisterCallback<PointerUpEvent>(NavigateToWizard1);
            View.Q<Button>("WizardPrevTo2Button").RegisterCallback<PointerUpEvent>(NavigateToWizard2);
            View.Q<Button>("WizardPrevTo3Button").RegisterCallback<PointerUpEvent>(NavigateToWizard3);
            View.Q<Button>("WizardPrevTo4Button").RegisterCallback<PointerUpEvent>(NavigateToWizard4);
            
            View.Q<VisualElement>("footerContent").AddToClassList("hide"); // Default footer hidden, wizard controls it
            View.Q<VisualElement>("RegisterWizard1NextButton").RemoveFromClassList("hide");
            View.Q<Button>("ContinueButton").AddToClassList("hide"); // Main continue button hidden for wizard flow
            
            NavigateToWizard1(); // Always start from the VN wizard
        }

        #region Navigate Wizard
        // All wizard navigation methods (NavigateToWizard1 to NavigateToWizard5, NavigateToLoading) are kept as they are core to the VN flow.
        public void NavigateToWizard1(PointerUpEvent evt = null)
        {
            _isDatePickerOpen = false;
            _wizardPage = 1;
            View.Q<VisualElement>("RegisterWizard1").RemoveFromClassList("hide");
            View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");
            View.Q<VisualElement>("RegisterWizard5").AddToClassList("hide");
            View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide"); // Wizard 1 might show an additional footer
            View.Q<VisualElement>("footerContent").AddToClassList("wizard-register-footer"); // Use wizard footer style
            View.Q<VisualElement>("Loading").AddToClassList("hide");
            _log.Debug("Navigating to wizard 1");
        }

        public void NavigateToWizard2(PointerUpEvent evt = null)
        {
            // Validate all fields first, including email, password, and VN fields
            if (!ValidateForm1())
            {
                _log.Debug("VN wizard validation failed");
                _wizardContinueButton.ToggleLoading(false); // Ensure loading is reset if validation fails
                return;
            }
            _wizardPage = 2;
            View.Q<VisualElement>("footerContent").AddToClassList("hide"); // Hide general footer
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
            // This logic is part of the VN flow which might have branches (with/without full KYC, with/without OTP)
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
            else // VN, but not Full KYC
            {
                if (IsVNLegalPurposePhoneNumberVerificationEnabled())
                {
                    // Call OnWizardContinueButtonClick to trigger OTP sending if form validation passes.
                    // This will lead to PhoneVerificationDialog (or NavigateToWizard5 if handled here) via OnWizardContinueButtonClick.
                     OnWizardContinueButtonClick(evt); 
                }
                else
                {

                    // Directly submit registration (simpler VN flow without full KYC & without OTP)
                    OnContinueButtonClick(evt);
                }
            }
            _log.Debug("Navigating to wizard 3 logic");
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
        
        public void NavigateToWizard5(PointerUpEvent evt = null) // Called from OnWizardContinueButtonClick logic if it decides to show a local wizard 5
        {
            // This method might not be directly called by a UI button if Model.ShowPhoneVerification handles its own UI.
            // However, if "RegisterWizard5" is a UI element controlled by this presenter, then this is relevant.
            _log.Debug("NavigateToWizard5 was called. If phone verification UI is separate, this might be for a specific local UI.");
            // If "RegisterWizard5" is part of this presenter:
            // _wizardPage = 5;
            // View.Q<VisualElement>("RegisterWizard1").AddToClassList("hide");
            // View.Q<VisualElement>("RegisterWizard2").AddToClassList("hide");
            // View.Q<VisualElement>("RegisterWizard3").AddToClassList("hide");
            // View.Q<VisualElement>("RegisterWizard4").AddToClassList("hide");
            // View.Q<VisualElement>("RegisterWizard5").RemoveFromClassList("hide"); // If this UI element exists
            // View.Q<VisualElement>("AdditionalFooterContent").AddToClassList("hide");
            // View.Q<VisualElement>("Loading").AddToClassList("hide");
            // _wizard5ContinueButton.ToggleLoading(false);
            // View.Q<Button>("WizardPrevTo4Button")?.RemoveFromClassList("hide"); // Or the appropriate back button
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
            _showPasswordButton.EnableInClassList("btn-password-hide", !_inputPassword.textField.isPasswordField);
        }

        public void OnToggleShowRePassword(ClickEvent evt)
        {
            _inputRepassword.textField.Blur();
            _inputRepassword.textField.isPasswordField = !_inputRepassword.textField.isPasswordField;
            _showRePasswordButton.EnableInClassList("btn-password-hide", !_inputRepassword.textField.isPasswordField);
        }

        private void SetupDropdown() // VN-specific
        {
            var genderChoices = new List<string> { Locale.GetTranslation("Select.Gender.Male"), Locale.GetTranslation("Select.Gender.Female") };
            Color textColor = new Color(98f / 255f, 100f / 255f, 104f / 255f);
            _gender.choices = genderChoices;
            _gender.value = Locale.GetTranslation("Select.Gender"); // Placeholder
            _gender.style.color = textColor;
            _gender.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _gender.style.color = Color.white;
                // _gender.value = evt.newValue; // Not necessary, automatically handled
                _gender.labelElement.style.display = DisplayStyle.None;
            });
        }

        private void SetupDatePicker() // VN-specific
        {
            #if UNITY_EDITOR
            // Editor behavior for easier development
            _birthDate.textField.value = "01/01/2000"; // Example
            _dateOfIssue.textField.value = "01/01/2020"; // Example
            #else
            string startDateStr = "01/01/2000"; // Default date for date picker
            DateTime parsedStartDate = DateTime.ParseExact(startDateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);

            View.Q<VisualElement>("BirthdateContainer").RegisterCallback<ClickEvent>(upEvent =>
            {
                upEvent.StopImmediatePropagation();
                if(_isDatePickerOpen) return;
                _isDatePickerOpen = true;
                Noctua.OpenDatePicker(parsedStartDate.Year, parsedStartDate.Month, parsedStartDate.Day, 1, // pickerId 1
                (DateTime _date) => { _log.Debug($"Date selection cancelled/changed: '{_date:O}'"); _isDatePickerOpen = false; },
                (DateTime _date) => {
                    _birthDate.textField.value = _date.ToString("dd/MM/yyyy");
                    _birthDate.textField.labelElement.style.display = DisplayStyle.None; // Hide label on value set
                    OnValueChanged(_birthDate); // Call OnValueChanged to update button states
                    _isDatePickerOpen = false;
                });
            });

            View.Q<VisualElement>("DateOfIssueContainer").RegisterCallback<ClickEvent>(upEvent =>
            {
                upEvent.StopImmediatePropagation();
                if(_isDatePickerOpen) return;
                _isDatePickerOpen = true;
                Noctua.OpenDatePicker(parsedStartDate.Year, parsedStartDate.Month, parsedStartDate.Day, 2, // pickerId 2 (different from birthdate)
                (DateTime _date) => { _log.Debug($"Date selection cancelled/changed: '{_date:O}'"); _isDatePickerOpen = false; },
                (DateTime _date) => {
                    _dateOfIssue.textField.value = _date.ToString("dd/MM/yyyy");
                    _dateOfIssue.textField.labelElement.style.display = DisplayStyle.None; // Hide label on value set
                    OnValueChanged(_dateOfIssue); // Call OnValueChanged
                    _isDatePickerOpen = false;
                });
            });
            #endif
        }

        private void OnLoginLinkClick(ClickEvent evt)
        {
            Visible = false;
            Model.ShowEmailLogin();
        }

        // Submit button from full KYC wizard (or VN flow requiring OTP)
        private async void OnWizardContinueButtonClick(PointerUpEvent evt)
        {
            if (!validateForm()) // Validate all fields first, including email, password, and VN fields
            {
                _log.Debug("VN wizard validation failed");
                _wizardContinueButton.ToggleLoading(false); // Ensure loading is reset if validation fails
                return;
            }

            if (IsVNLegalPurposePhoneNumberVerificationEnabled())
            {
                _wizardContinueButton.ToggleLoading(true); // Enable loading before async operation
                View.Q<Button>("WizardPrevTo3Button")?.AddToClassList("hide"); // Hide back button during loading

                try
                {
                    _log.Debug("OEG sending phone number verification code for VN");
                    var result = await Model.AuthService.RegisterWithEmailSendPhoneNumberVerificationAsync(_phoneNumber.text);
                    _verificationId = result.VerificationId;
                    _log.Debug("OEG verificationId (VN): " + _verificationId);

                    _wizardContinueButton.ToggleLoading(false);
                    View.Q<Button>("WizardPrevTo3Button")?.RemoveFromClassList("hide");

                    var birthDateVal = DateTime.ParseExact(_birthDate.text, "dd/MM/yyyy", CultureInfo.InvariantCulture).ToUniversalTime();

                    var regExtraVN = new Dictionary<string, string>
                    {
                        { "fullname", _fullname.text },
                        { "phone_number", _phoneNumber.text},
                        { "birth_date", birthDateVal.ToString() }
                    };

                    if (IsVNLegalPurposeFullKYCEnabled())
                    {
                        var issueDateVal = DateTime.ParseExact(_dateOfIssue.text, "dd/MM/yyyy", CultureInfo.InvariantCulture).ToUniversalTime();

                        regExtraVN.Add("country", _country.text);
                        regExtraVN.Add("id_card", _idCard.text);
                        regExtraVN.Add("place_of_issue", _placeOfIssue.text);
                        regExtraVN.Add("date_of_issue", issueDateVal.ToString());
                        regExtraVN.Add("address", _address.text);
                    }

                    _log.Debug("OEG regExtraVN (VN): " + JsonConvert.SerializeObject(regExtraVN));

                    Model.ShowPhoneVerification( // Navigate to phone verification dialog
                        verificationId: _verificationId,
                        phoneNumber: _phoneNumber.text.Replace(" ", string.Empty),
                        emailAddress: _inputEmail.text.Replace(" ", string.Empty),
                        password: _inputPassword.text,
                        regExtra: regExtraVN
                    );
                    
                    Visible = false; // Hide current dialog

                }
                catch (Exception e)
                {
                    _wizardContinueButton.ToggleLoading(false);
                    View.Q<Button>("WizardPrevTo3Button")?.RemoveFromClassList("hide");
                    _log.Debug("OEG send verification code failed (VN): " + e.Message);
                    
                    if (e is NoctuaException noctuaEx) {
                         Model.ShowGeneralNotification(noctuaEx.Message, false); // Default error notification
                         // If ErrorCode 2061 (e.g., number already registered but unverified),
                         // might want to proceed to OTP verification dialog or show a specific message.
                         // For now, show general error and let user stay on KYC wizard.
                         // NavigateToWizard4(); // Or an error wizard if available
                    } else {
                        Model.ShowGeneralNotification(e.Message, false);
                    }
                }
            } else { // VN, but no OTP verification needed -> proceed to OnContinueButtonClick
                _log.Debug("Continuing to final registration (VN without OTP)");
                OnContinueButtonClick(evt); // Send all collected VN data
            }
        }

        // Submit button from phone number verification wizard (if this wizard UI is part of this presenter)
        // However, Model.ShowPhoneVerification typically handles its own UI.
        // If this presenter *also* handles OTP UI (e.g., RegisterWizard5), then this is relevant.
        private async void OnWizard5ContinueButtonClick(PointerUpEvent evt)
        {
            // This method might be unused if Model.ShowPhoneVerification is a separate dialog.
            // If RegisterWizard5 is part of this presenter:
            _wizard5ContinueButton.ToggleLoading(true);
            View.Q<Button>("WizardPrevTo4Button")?.AddToClassList("hide"); // Or relevant back button
            View.Q<Button>("WizardPrevTo3Button")?.AddToClassList("hide");

            try
            {
                // Assume _verificationId is already available from the previous step
                // and _phoneNumberVerificationCode.text is the OTP entered by the user
                var result = await Model.AuthService.RegisterWithEmailVerifyPhoneNumberAsync(_verificationId, _phoneNumberVerificationCode.text);
                _wizard5ContinueButton.ToggleLoading(false);
                // Show back buttons again if needed
                // View.Q<Button>("WizardPrevTo...").RemoveFromClassList("hide"); 
                
                // If OTP is correct, proceed to final email registration with all data
                OnContinueButtonClick(evt); // This will send all data (including VN KYC)
            }
            catch (Exception e)
            {
                _wizard5ContinueButton.ToggleLoading(false);
                // Show back buttons again
                _log.Debug("OEG OTP verification failed (VN): " + e.Message);
                 if (e is NoctuaException noctuaEx) {
                    Model.ShowGeneralNotification(noctuaEx.Message, false);
                 } else {
                    Model.ShowGeneralNotification(e.Message, false);
                 }
                // Keep the user on the OTP wizard to try again or go back
                // NavigateToWizard5(); // Might not be needed if already there
            }
        }

        private bool ValidateForm1()
        {
            HideAllErrors(); // Clear previous errors

            // Toggle loading for all submit buttons that might be active
            _continueButton.ToggleLoading(true);
            _wizardContinueButton.ToggleLoading(true);
            _wizard5ContinueButton.ToggleLoading(true);
            View.Q<Button>("WizardPrevTo3Button")?.AddToClassList("hide"); // Hide back button during validation

            bool isValid = true;
            string firstErrorMessage = null; // To show the first encountered error

            var emailAddress = _inputEmail.text.Replace(" ", string.Empty);
            var password = _inputPassword.text;
            var rePassword = _inputRepassword.text;

            string emailError = Utility.ValidateEmail(emailAddress);
            if (!string.IsNullOrEmpty(emailError))
            {
                _inputEmail.Error(emailError);
                if (firstErrorMessage == null) firstErrorMessage = emailError;
                isValid = false;
            }
            string passwordError = Utility.ValidatePassword(password);
            if (!string.IsNullOrEmpty(passwordError))
            {
                _inputPassword.Error(passwordError);
                if (firstErrorMessage == null) firstErrorMessage = passwordError;
                isValid = false;
            }
            string rePasswordError = Utility.ValidateReenterPassword(password, rePassword);
            if (!string.IsNullOrEmpty(rePasswordError))
            {
                _inputRepassword.Error(rePasswordError);
                if (firstErrorMessage == null) firstErrorMessage = rePasswordError;
                isValid = false;
            }

            if (!isValid)
            {
                _continueButton.ToggleLoading(false);
                _wizardContinueButton.ToggleLoading(false);
                _wizard5ContinueButton.ToggleLoading(false);
                View.Q<Button>("WizardPrevTo3Button")?.RemoveFromClassList("hide");
                if (firstErrorMessage != null) Model.ShowGeneralNotification(firstErrorMessage, false);
                return false;
            }

            return true;
        }

        private bool validateForm() // Validation for VN flow
        {
            HideAllErrors(); // Clear previous errors

            // Toggle loading for all submit buttons that might be active
            _continueButton.ToggleLoading(true);
            _wizardContinueButton.ToggleLoading(true);
            _wizard5ContinueButton.ToggleLoading(true);
            View.Q<Button>("WizardPrevTo3Button")?.AddToClassList("hide"); // Hide back button during validation

            bool isValid = true;
            string firstErrorMessage = null; // To show the first encountered error

            var emailAddress = _inputEmail.text.Replace(" ", string.Empty);
            var password = _inputPassword.text;
            var rePassword = _inputRepassword.text;

            string emailError = Utility.ValidateEmail(emailAddress);
            if (!string.IsNullOrEmpty(emailError))
            {
                _inputEmail.Error(emailError);
                if (firstErrorMessage == null) firstErrorMessage = emailError;
                isValid = false;
            }
            string passwordError = Utility.ValidatePassword(password);
            if (!string.IsNullOrEmpty(passwordError))
            {
                _inputPassword.Error(passwordError);
                if (firstErrorMessage == null) firstErrorMessage = passwordError;
                isValid = false;
            }
            string rePasswordError = Utility.ValidateReenterPassword(password, rePassword);
            if (!string.IsNullOrEmpty(rePasswordError))
            {
                _inputRepassword.Error(rePasswordError);
                if (firstErrorMessage == null) firstErrorMessage = rePasswordError;
                isValid = false;
            }

            if (IsVNLegalPurposeFullKYCEnabled())
            {
                if (_gender.value == Locale.GetTranslation("EmailRegisterVNDialogPresenter.GenderTF.DropdownField.label")) // Assuming placeholder
                {
                    _gender.ElementAt(1).AddToClassList("noctua-text-input-error");
                    var genderErrorLabel = _gender.Q<Label>("error");
                    genderErrorLabel.text = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrGenderEmpty.Label.text"); // This key needs to exist in translations
                    genderErrorLabel.RemoveFromClassList("hide");
                    _gender.Q<VisualElement>("title").style.color = ColorModule.redError;
                    if (firstErrorMessage == null) firstErrorMessage = genderErrorLabel.text;
                    isValid = false;
                }
            }

            if (string.IsNullOrWhiteSpace(_birthDate.text))
            {
                _birthDate.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrBirthdateEmpty.Label.text")); // Add this key to match system
                if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrBirthdateEmpty.Label.text");
                isValid = false;
            }
            else
            {
                try
                {
                    var birthDateVal = DateTime.ParseExact(_birthDate.text, "dd/MM/yyyy", CultureInfo.InvariantCulture).ToUniversalTime();
                    if (birthDateVal.AddYears(18) > DateTime.UtcNow) // Age check
                    {
                        _birthDate.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrUnderage.Label.text"));
                        if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrUnderage.Label.text");
                        isValid = false;
                    }
                }
                catch (FormatException)
                {
                    _birthDate.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrInvalidDateFormat.Label.text")); // Ensure this exists
                    if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrInvalidDateFormat.Label.text");
                    isValid = false;
                }
            }

            if (IsVNLegalPurposeFullKYCEnabled())
            {
                if (string.IsNullOrWhiteSpace(_dateOfIssue.text))
                {
                    _dateOfIssue.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrDateOfIssueEmpty.Label.text")); // Ensure this key exists
                    if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrDateOfIssueEmpty.Label.text");
                    isValid = false;
                }
                else
                {
                    try
                    {
                        DateTime.ParseExact(_dateOfIssue.text, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                    }
                    catch (FormatException)
                    {
                        _dateOfIssue.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrInvalidDateFormat.Label.text"));
                        if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrInvalidDateFormat.Label.text");
                        isValid = false;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(_fullname.text))
            {
                _fullname.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrFullnameEmpty.Label.text")); // Make sure this exists
                if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrFullnameEmpty.Label.text");
                isValid = false;
            }
            if (string.IsNullOrWhiteSpace(_phoneNumber.text))
            {
                _phoneNumber.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrPhoneNumberEmpty.Label.text")); // Make sure this exists
                if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrPhoneNumberEmpty.Label.text");
                isValid = false;
            }

            if (IsVNLegalPurposeFullKYCEnabled())
            {
                if (string.IsNullOrWhiteSpace(_country.text))
                {
                    _country.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrCountryEmpty.Label.text")); // Make sure this exists
                    if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrCountryEmpty.Label.text");
                    isValid = false;
                }
                if (string.IsNullOrWhiteSpace(_idCard.text))
                {
                    _idCard.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrIDCardEmpty.Label.text")); // Make sure this exists
                    if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrIDCardEmpty.Label.text");
                    isValid = false;
                }
                if (string.IsNullOrWhiteSpace(_placeOfIssue.text))
                {
                    _placeOfIssue.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrPlaceOfIssueEmpty.Label.text")); // Make sure this exists
                    if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrPlaceOfIssueEmpty.Label.text");
                    isValid = false;
                }
                if (string.IsNullOrWhiteSpace(_address.text))
                {
                    _address.Error(Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrAddressEmpty.Label.text")); // Make sure this exists
                    if (firstErrorMessage == null) firstErrorMessage = Locale.GetTranslation("EmailRegisterVNDialogPresenter.ErrAddressEmpty.Label.text");
                    isValid = false;
                }
            }

            if (!isValid)
            {
                _continueButton.ToggleLoading(false);
                _wizardContinueButton.ToggleLoading(false);
                _wizard5ContinueButton.ToggleLoading(false);
                View.Q<Button>("WizardPrevTo3Button")?.RemoveFromClassList("hide");
                if (firstErrorMessage != null) Model.ShowGeneralNotification(firstErrorMessage, false);
                return false;
            }

            return true;
        }


        private async void OnContinueButtonClick(PointerUpEvent evt) // Final submit method for all VN flows
        {
            // No need to validate form again if called from OnWizard...ButtonClick which already validated.
            // But if called directly (e.g., VN without OTP), validation is needed.
            // For consistency, validate here or ensure all callers have validated.
            // If validateForm() is called here, ensure no conflicting loading states.
            // Assume evt from wizard has passed validation. If evt is null (direct call), validate.
            if (evt == null) { // If called not from wizard (e.g., fallback simple VN path)
                 _log.Debug("Direct OnContinueButtonClick validation failed");
                return;
            }

            if (!validateForm()) // Validate all fields first, including email, password, and VN fields
            {
                _log.Debug("VN wizard validation failed");
                _wizardContinueButton.ToggleLoading(false); // Ensure loading is reset if validation fails
                return;
            }

            _log.Debug("OnContinueButtonClick executing final registration for VN");

            _continueButton.ToggleLoading(true); // This button might not be visible, but loading state can be useful
            _wizardContinueButton.ToggleLoading(true); // If this is submit from KYC wizard
            _wizard5ContinueButton.ToggleLoading(true); // If this is submit from OTP wizard

            var emailAddress = _inputEmail.text.Replace(" ", string.Empty);
            var password = _inputPassword.text;
            
            // regExtra is always populated with VN data
            _isDatePickerOpen = false; 
            var birthDateVal = DateTime.ParseExact(_birthDate.text, "dd/MM/yyyy", CultureInfo.InvariantCulture).ToUniversalTime();

             var regExtra = new Dictionary<string, string>() {
                { "fullname", _fullname.text },
                { "phone_number", _phoneNumber.text},
                { "birth_date", birthDateVal.ToString()}
            };

            if (IsVNLegalPurposeFullKYCEnabled())
            {
                var issueDateVal = DateTime.ParseExact(_dateOfIssue.text, "dd/MM/yyyy", CultureInfo.InvariantCulture).ToUniversalTime();

                regExtra.Add("country", _country.text);
                regExtra.Add("id_card", _idCard.text);
                regExtra.Add("place_of_issue", _placeOfIssue.text);
                regExtra.Add("date_of_issue", issueDateVal.ToString());
                regExtra.Add("address", _address.text);
            }
           
            _log.Debug("Final Register extra (VN): " + JsonConvert.SerializeObject(regExtra));

            try {
                CredentialVerification result;
                switch (Model.AuthIntention)
                {
                    case AuthIntention.None: case AuthIntention.Switch:
                        result = await Model.AuthService.RegisterWithEmailAsync(emailAddress, password, regExtra);
                        break;
                    case AuthIntention.Link: // Link with email, regExtra might not be used by Link service
                        result = await Model.AuthService.LinkWithEmailAsync(emailAddress, password);
                        break;
                    default:
                        throw new NoctuaException(NoctuaErrorCode.Authentication, $"Invalid AuthIntention {Model.AuthIntention}");
                }
                
                Debug.Log("RegisterWithPassword/LinkWithEmail verification ID (VN): " + result.Id);
                Visible = false; // Hide registration dialog

                // Clear all fields after success
                _inputEmail.Clear(); _inputPassword.Clear(); _inputRepassword.Clear();
                _fullname.Clear(); _phoneNumber.Clear(); _birthDate.Clear();_gender.value = Locale.GetTranslation("Select.Gender"); // Reset dropdown
                _country.Clear(); _idCard.Clear(); _placeOfIssue.Clear(); _dateOfIssue.Clear(); _address.Clear();
                _phoneNumberVerificationCode.Clear();

                Model.ShowEmailVerification(emailAddress, password, result.Id, regExtra); // Navigate to email verification

                _continueButton.Clear(); _wizardContinueButton.Clear(); _wizard5ContinueButton.Clear();
                View.Q<Button>("WizardPrevTo3Button")?.RemoveFromClassList("hide");             
                View.Q<Button>("WizardPrevTo4Button")?.RemoveFromClassList("hide");
            }
            catch (Exception e)
            {
                _continueButton.ToggleLoading(false); 
                _wizardContinueButton.ToggleLoading(false); 
                _wizard5ContinueButton.ToggleLoading(false);
                View.Q<Button>("WizardPrevTo3Button")?.RemoveFromClassList("hide");
                View.Q<Button>("WizardPrevTo4Button")?.RemoveFromClassList("hide");

                _log.Warning($"VN Registration Final Step Error: {e.Message}\n{e.StackTrace}");
                string errorMessageToShow = e.Message;
                if (e is NoctuaException noctuaEx) {
                    errorMessageToShow = noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message;
                }
                // Set error on the button that might be visible or relevant
                 _wizardContinueButton.Error(errorMessageToShow); // If submitted from KYC wizard
                // _wizard5ContinueButton.Error(errorMessageToShow); // If submitted from OTP wizard
                // _continueButton.Error(errorMessageToShow); // If submitted from simple VN flow
                Model.ShowGeneralNotification(errorMessageToShow, false); // Show general notification
            }
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking back button (VN wizard context)");    
            _isDatePickerOpen = false;      
            // Navigate back within VN wizard
            if (_wizardPage == 5) { NavigateToWizard4(); return; } // Or relevant wizard before OTP
            if (_wizardPage == 4) { NavigateToWizard3(null); return; } // Pass null or a new PointerUpEvent if method requires it
            if (_wizardPage == 3) { NavigateToWizard2(null); return; }
            if (_wizardPage == 2) { NavigateToWizard1(null); return; }

            // If on the first wizard page or not in wizard (simple VN flow)
            _continueButton.Clear(); _wizardContinueButton.Clear(); _wizard5ContinueButton.Clear();
            Visible = false;
            Model.NavigateBack(); // Navigate to previous screen (e.g., login selection)
        }

        private void OnValueChanged(InputFieldNoctua input)
        {
            input.AdjustLabel();
            _isDatePickerOpen = false;
            // Update state for all buttons that might be active in various VN scenarios
            Utility.UpdateButtonState(textFields, _continueButton.button); 
            Utility.UpdateButtonState(textFields, _wizardContinueButton.button);
            Utility.UpdateButtonState(textFields, _wizard5ContinueButton.button);
        }

        private void HideAllErrors() // Clear errors for all VN fields
        {
            _inputEmail.Reset(); _inputPassword.Reset(); _inputRepassword.Reset();
            if (_fullname != null) _fullname.Reset(); if (_phoneNumber != null) _phoneNumber.Reset();
            if (_birthDate != null) _birthDate.Reset();
            if (_gender != null) {
                _gender.ElementAt(1)?.RemoveFromClassList("noctua-text-input-error"); // Safely access element
                _gender.Q<Label>("error")?.AddToClassList("hide"); // Safely access error label
                var titleElement = _gender.Q<VisualElement>("title");
                if (titleElement != null) titleElement.style.color = Color.white; // Reset title color safely
            }
            if (_country != null) _country.Reset(); if (_idCard != null) _idCard.Reset();
            if (_placeOfIssue != null) _placeOfIssue.Reset(); if (_dateOfIssue != null) _dateOfIssue.Reset();
            if (_address != null) _address.Reset();
            if (_phoneNumberVerificationCode != null) _phoneNumberVerificationCode.Reset();

            _continueButton.Clear(); _wizardContinueButton.Clear(); _wizard5ContinueButton.Clear();
        }

        // Feature flag methods are kept as they define variations within the VN flow itself
        private bool IsVNLegalPurposeEnabled() { return GetFeatureFlag("vnLegalPurposeEnabled"); }
        private bool IsVNLegalPurposeFullKYCEnabled() { return GetFeatureFlag("vnLegalPurposeFullKYCEnabled"); }
        private bool IsVNLegalPurposePhoneNumberVerificationEnabled() { return GetFeatureFlag("vnLegalPurposePhoneNumberVerificationEnabled"); }
        private bool GetFeatureFlag(string key)
        {
            return _config?.Noctua?.RemoteFeatureFlags != null &&
                   _config.Noctua.RemoteFeatureFlags.TryGetValue(key, out var value) &&
                   value == true;
        }
    }
}