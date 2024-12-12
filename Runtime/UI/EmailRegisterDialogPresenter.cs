using System;
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
        private DropdownField _phoneCode;
        private TextField _phoneNumber;
        private TextField _birthDate;
        private DropdownField _gender;
        private DropdownField _country;
        private TextField _idCard;
        private TextField _placeOfIssue;
        private TextField _dateOfIssue;
        private TextField _address;
        private List<TextField> textFields;
        private Button continueButton;
        private GlobalConfig _config;
        private List<string> _phoneCodeList = new List<string>();
        private List<string> _countryList = new List<string>();

        protected override void Attach(){}
        protected override void Detach(){}

        private void Start()
        {
            SetupInputFields(true);
            HideAllErrors();
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

        public void Show(bool clearForm)
        {
            SetupInputFields(clearForm);
            HideAllErrors();

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
            var backButton = View.Q<Button>("BackButton");
            var loginLink = View.Q<Label>("LoginLink");

            //Behaviour whitelabel - VN
            _fullname = View.Q<TextField>("FullNameTF");
            _phoneCode = View.Q<DropdownField>("PhoneCodeDF");
            _phoneNumber = View.Q<TextField>("PhoneNumberTF");
            _birthDate = View.Q<TextField>("BirthdateTF");
            _gender = View.Q<DropdownField>("GenderTF");
            _country = View.Q<DropdownField>("CountryTF");
            _idCard = View.Q<TextField>("IDCardTF");
            _placeOfIssue = View.Q<TextField>("PlaceOfIssueTF");
            _dateOfIssue = View.Q<TextField>("DateOfIssueTF");
            _address = View.Q<TextField>("AddressTF");

            if(!string.IsNullOrEmpty(_config?.Noctua?.Flags))
            {
                SetupDropdown();  
                SetupDatePicker();
                SetCountries();
                ShowBehaviourWhitelabel(true);
            }

            // Visibility
            continueButton.RemoveFromClassList("hide");

            // Default values
            if (clearForm) {
                passwordField.isPasswordField = true;
                rePasswordField.isPasswordField = true;
                emailField.value = "";
                passwordField.value = "";
                rePasswordField.value = "";

                _fullname.value = "";
                _phoneNumber.value = "";
                _idCard.value = "";
                _placeOfIssue.value = "";
                _address.value = "";
            }

            if(string.IsNullOrEmpty(_config?.Noctua?.Flags))
            {
                textFields = new List<TextField>
                {
                    emailField,
                    passwordField,
                    rePasswordField

                };
            }
            else
            {
                textFields = new List<TextField>
                {
                    emailField,
                    passwordField,
                    rePasswordField,
                    _fullname,
                    _phoneNumber,
                    _birthDate,
                    _idCard,
                    _placeOfIssue,
                    _dateOfIssue,
                    _address
                };
            }

            Utility.UpdateButtonState(textFields, continueButton);

            // Callbacks
            continueButton.RegisterCallback<PointerUpEvent>(OnContinueButtonClick);
            backButton.RegisterCallback<PointerUpEvent>(OnBackButtonClick);
            emailField.RegisterValueChangedCallback(evt => OnEmailValueChanged(emailField));
            passwordField.RegisterValueChangedCallback(evt => OnPasswordValueChanged(passwordField));
            rePasswordField.RegisterValueChangedCallback(evt => OnRePasswordValueChanged(rePasswordField));

            showPasswordButton.RegisterCallback<PointerUpEvent>(OnToggleShowPassword);
            showRePasswordButton.RegisterCallback<PointerUpEvent>(OnToggleShowRePassword);

            showPasswordButton.RemoveFromClassList("btn-password-hide");
            showRePasswordButton.RemoveFromClassList("btn-password-hide");

            emailField.hideMobileInput = true;
            passwordField.hideMobileInput = true;
            rePasswordField.hideMobileInput = true;

            //Behaviour whitelabel - VN
            if (!string.IsNullOrEmpty(_config?.Noctua?.Flags))
            {
                _fullname.RegisterValueChangedCallback(evt => OnValueChanged(_fullname));
                _phoneNumber.RegisterValueChangedCallback(evt => OnValueChanged(_phoneNumber));
                _idCard.RegisterValueChangedCallback(evt => OnValueChanged(_idCard));
                _placeOfIssue.RegisterValueChangedCallback(evt => OnValueChanged(_placeOfIssue));
                _address.RegisterValueChangedCallback(evt => OnValueChanged(_address));
            }

            loginLink.RegisterCallback<PointerUpEvent>(OnLoginLinkClick);
        }

        #region Check Text Field Focus

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

            TouchScreenKeyboard.hideInput = true;
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

            TouchScreenKeyboard.hideInput = true;
        }

        #endregion

        private void SetupDropdown()
        {
             var genderChoices = new List<string> {"Male", "Female"};
            Color textColor = new Color(98f / 255f, 100f / 255f, 104f / 255f);
            
            var regionCode = _config?.Noctua?.Region ?? "";

            _gender.choices = genderChoices; 
            _gender.value = Utility.GetTranslation("Select.Gender",  Utility.LoadTranslations(regionCode));
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
            _dateOfIssue.isReadOnly = true;

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
                    textField.labelElement.style.display = DisplayStyle.None;
                    Utility.UpdateButtonState(textFields, continueButton);
                });
            }

            _birthDate.UnregisterCallback<PointerUpEvent>(evt => OpenDatePickerHandler(1, _birthDate, evt));
            _dateOfIssue.UnregisterCallback<PointerUpEvent>(evt => OpenDatePickerHandler(2, _dateOfIssue, evt));

            _birthDate.RegisterCallback<PointerUpEvent>(evt => OpenDatePickerHandler(1, _birthDate, evt));
            _dateOfIssue.RegisterCallback<PointerUpEvent>(evt => OpenDatePickerHandler(2, _dateOfIssue, evt));  
        }

        private void SetCountries()
        {
            _phoneCodeList.Clear();
            _countryList.Clear();

            List<Country> countries = CountryData.Countries;

            foreach(var country in countries)
            {
                _countryList.Add(country.Name);
                _phoneCodeList.Add(country.PhoneCode);
            }

            _phoneCode.choices = _phoneCodeList; 
            _phoneCode.value = _phoneCodeList[0];
            _phoneCode.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _phoneCode.value = evt.newValue;
                _phoneCode.labelElement.style.display = DisplayStyle.None;
            });

            Color textColor = new Color(98f / 255f, 100f / 255f, 104f / 255f);

            var regionCode = _config?.Noctua?.Region ?? "";

            _country.choices = _countryList; 
            _country.value = Utility.GetTranslation("Select.Country",  Utility.LoadTranslations(regionCode));
            _country.style.color = textColor;
            _country.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                _country.style.color = Color.white;
                _country.value = evt.newValue;
                _country.labelElement.style.display = DisplayStyle.None;
            });            
        }

        private void OnLoginLinkClick(PointerUpEvent evt)
        {
            Visible = false;
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

            var emailAddress = _email;
            var password = _password;
            var rePassword = _rePassword;

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


            if (!password.Equals(rePassword)) {
                View.Q<Label>("ErrPasswordMismatch").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }

            Dictionary<string, string> regExtra = null;
            
            if (!string.IsNullOrEmpty(_config?.Noctua?.Flags))
            {
                if(_gender.value == "Select Gender")
                {
                    View.Q<Label>("ErrEmailEmpty").text = "Please Select Gender!";
                    View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");
                    View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                    View.Q<VisualElement>("Spinner").AddToClassList("hide");
                    return;
                }

                if(_country.value == "Select Country")
                {
                    View.Q<Label>("ErrEmailEmpty").text = "Please Select Country!";
                    View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");
                    View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                    View.Q<VisualElement>("Spinner").AddToClassList("hide");
                    return;
                }

                var birthDate = DateTime
                    .ParseExact(_birthDate.value, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                    .ToUniversalTime();
                    
                if (birthDate.AddYears(18) > DateTime.UtcNow)
                {
                    View.Q<Label>("ErrUnderage").RemoveFromClassList("hide");
                    View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                    View.Q<VisualElement>("Spinner").AddToClassList("hide");
                        
                    return;
                }

                var issueDate = DateTime
                    .ParseExact(_dateOfIssue.value, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                    .ToUniversalTime();

                regExtra = new Dictionary<string, string>()
                {
                    { "fullname", _fullname.value },
                    { "phone_number", _phoneCode.value + _phoneNumber.value },
                    { "birth_date", birthDate.ToString() },
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

                if(!string.IsNullOrEmpty(_config?.Noctua?.Flags))
                {
                    _fullname.value = string.Empty;
                    _phoneCode.value = string.Empty;
                    _birthDate.value = string.Empty;
                    _idCard.value = string.Empty;
                    _placeOfIssue.value = string.Empty;
                    _dateOfIssue.value = string.Empty;
                    _address.value = string.Empty;
                }

                Model.ShowEmailVerification(emailAddress, password, result.Id);

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

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
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
            }
        }

        private void OnBackButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking back button");
            
            View.Q<VisualElement>("Spinner").AddToClassList("hide");
            View.Q<Button>("ContinueButton").RemoveFromClassList("hide");

            Visible = false;
            
            Model.NavigateBack();
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

            Utility.UpdateButtonState(textFields, continueButton);
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

            Utility.UpdateButtonState(textFields, continueButton);
        }

        private void OnRePasswordValueChanged(TextField textField)
        {
            HideAllErrors();

            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;
            }
            _rePassword = textField.value;

            Utility.UpdateButtonState(textFields, continueButton);
        }

        //Behaviour whitelabel - VN
        private void OnValueChanged(TextField textField)
        {
            if(string.IsNullOrEmpty(textField.value)) {
                textField.labelElement.style.display = DisplayStyle.Flex;
            } else {
                textField.labelElement.style.display = DisplayStyle.None;
            }

            Utility.UpdateButtonState(textFields, continueButton);           
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
            View.Q<Label>("ErrUnderage").RemoveFromClassList("hide");

            View.Q<Label>("ErrCode").AddToClassList("hide");
            View.Q<Label>("ErrEmailInvalid").AddToClassList("hide");
            View.Q<Label>("ErrEmailEmpty").AddToClassList("hide");
            View.Q<Label>("ErrPasswordTooShort").AddToClassList("hide");
            View.Q<Label>("ErrPasswordEmpty").AddToClassList("hide");
            View.Q<Label>("ErrPasswordMismatch").AddToClassList("hide");
            View.Q<Label>("ErrUnderage").AddToClassList("hide");
        }

        private void ShowBehaviourWhitelabel(bool isShow)
        {
            if(isShow)
            {
                View.Q<VisualElement>("PhoneNumberContainer").RemoveFromClassList("hide");

                _fullname.RemoveFromClassList("hide");
                _birthDate.RemoveFromClassList("hide");
                _gender.RemoveFromClassList("hide");
                _country.RemoveFromClassList("hide");
                _idCard.RemoveFromClassList("hide");
                _placeOfIssue.RemoveFromClassList("hide");
                _dateOfIssue.RemoveFromClassList("hide");
                _address.RemoveFromClassList("hide");
            }
            else
            {
                View.Q<VisualElement>("PhoneNumberContainer").AddToClassList("hide");

                _fullname.AddToClassList("hide");
                _birthDate.AddToClassList("hide");
                _gender.AddToClassList("hide");
                _country.AddToClassList("hide");
                _idCard.AddToClassList("hide");
                _placeOfIssue.AddToClassList("hide");
                _dateOfIssue.AddToClassList("hide");
                _address.AddToClassList("hide");
            }
        }
    }
}
