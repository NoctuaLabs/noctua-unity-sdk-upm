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
        private string _email;
        private string _password;
        private string _rePassword;

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

        public void Show(bool clearForm)
        {
            SetupInputFields(clearForm);
            HideAllErrors();

            Visible = true;
        }

        public void SetBehaviourWhitelabel(GlobalConfig config)
        {
            _config = config;
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
            var emailField = View.Q<TextField>("EmailTF");
            var passwordField = View.Q<TextField>("PasswordTF");
            var rePasswordField = View.Q<TextField>("RePasswordTF");
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
            
            //Behaviour whitelabel - VN
            if(!string.IsNullOrEmpty(_config?.Noctua?.Flags))
            {
                _fullname.RegisterValueChangedCallback(evt => OnValueChanged(_fullname));
                _phoneNumber.RegisterValueChangedCallback(evt => OnValueChanged(_phoneNumber));
                _idCard.RegisterValueChangedCallback(evt => OnValueChanged(_idCard));
                _placeOfIssue.RegisterValueChangedCallback(evt => OnValueChanged(_placeOfIssue));
                _address.RegisterValueChangedCallback(evt => OnValueChanged(_address));
            }

            loginLink.RegisterCallback<PointerUpEvent>(OnLoginLinkClick);
        }

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
            HideAllErrors();

            var spinnerInstance = new Spinner();
            View.Q<Button>("ContinueButton").AddToClassList("hide");
            View.Q<VisualElement>("Spinner").Clear();
            View.Q<VisualElement>("Spinner").Add(spinnerInstance);
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
            
            if(!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _gender.value == "Select Gender")
            {
                View.Q<Label>("ErrEmailEmpty").text = "Please Select Gender!";
                View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }

            if(!string.IsNullOrEmpty(_config?.Noctua?.Flags) && _country.value == "Select Country")
            {
                View.Q<Label>("ErrEmailEmpty").text = "Please Select Country!";
                View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }

            try {
                 Dictionary<string, string> regExtra = new();

                if(!string.IsNullOrEmpty(_config?.Noctua?.Flags))
                {
                    var dob = _birthDate.value;
                    string format = "dd/MM/yyyy";
                    DateTime dateTime = DateTime.ParseExact(dob, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                    dateTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, 0, 0, 0, DateTimeKind.Utc);

                    var doi = _dateOfIssue.value;
                    DateTime dateTimeDoi = DateTime.ParseExact(doi, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                    dateTimeDoi = new DateTime(dateTimeDoi.Year, dateTimeDoi.Month, dateTimeDoi.Day, 0, 0, 0, DateTimeKind.Utc);

                    Dictionary<string, string> regExtraDic = new Dictionary<string, string>()
                    {
                        { "fullname", _fullname.value },
                        { "phone_number", _phoneCode.value + _phoneNumber.value },
                        { "birth_date", dateTime.ToString() },
                        { "id_card", _idCard.value },
                        { "place_of_issue", _placeOfIssue.value },
                        { "date_of_issue", dateTimeDoi.ToString() },
                        { "address", _address.value }
                    };

                    regExtra = regExtraDic;
                }

                var result = await Model.RegisterWithEmailAsync(emailAddress, password, regExtra == null ? null : regExtra);
                Debug.Log("RegisterWithPassword verification ID: " + result.Id);

                View.visible = false;

                Model.ShowEmailVerification(emailAddress, password, result.Id);

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

            } 
            catch (Exception e) {
                if (e is NoctuaException noctuaEx)
                {
                    Debug.Log("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                    View.Q<Label>("ErrCode").text = noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message;
                } else {
                    Debug.Log("Exception: " + e);
                    View.Q<Label>("ErrCode").text = e.Message;
                }
                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("AdditionalFooterContent").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }
        }

        private void OnBackButtonClick(PointerUpEvent evt)
        {
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

            View.Q<Label>("ErrCode").AddToClassList("hide");
            View.Q<Label>("ErrEmailInvalid").AddToClassList("hide");
            View.Q<Label>("ErrEmailEmpty").AddToClassList("hide");
            View.Q<Label>("ErrPasswordTooShort").AddToClassList("hide");
            View.Q<Label>("ErrPasswordEmpty").AddToClassList("hide");
            View.Q<Label>("ErrPasswordMismatch").AddToClassList("hide");
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
