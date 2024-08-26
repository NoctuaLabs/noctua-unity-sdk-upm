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
    public class RegisterDialogPresenter : Presenter<NoctuaAuthService>
    {
        private UIDocument _uiDoc;

        private string _email;
        private string _password;
        private string _rePassword;

        protected override void Attach()
        {
            Model.OnAuthenticated += RefreshItems;

            HideAllErrors();
        }

        protected override void Detach()
        {
            Model.OnAuthenticated -= RefreshItems;
            HideAllErrors();
        }

        private void Awake()
        {
            //var visualTree = Resources.Load<VisualTreeAsset>("RegisterDialog");
            //var styleSheet = Resources.Load<StyleSheet>("Noctua");
            
            //_uiDoc = gameObject.AddComponent<UIDocument>();
            //_uiDoc.visualTreeAsset = visualTree;
            //_uiDoc.rootVisualElement.styleSheets.Add(styleSheet);

            LoadView();
            SetupInputFields();
            HideAllErrors();
        }

        public void Show()
        {

            View.visible = true;
            SetupInputFields();
            HideAllErrors();
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


        private void SetupInputFields()
        {
            var emailField = View.Q<TextField>("EmailTF");
            var passwordField = View.Q<TextField>("PasswordTF");
            var rePasswordField = View.Q<TextField>("RePasswordTF");
            var continueButton = View.Q<Button>("ContinueButton");
            var backButton = View.Q<Button>("BackButton");

            // Visibility
            View.Q<VisualElement>("Spinner").AddToClassList("hide");
            continueButton.RemoveFromClassList("hide");

            // Default values
            passwordField.isPasswordField = true;
            rePasswordField.isPasswordField = true;
            emailField.value = "";
            passwordField.value = "";
            rePasswordField.value = "";

            // Callbacks
            continueButton.RegisterCallback<ClickEvent>(OnContinueButtonClick);
            backButton.RegisterCallback<ClickEvent>(OnBackButtonClick);
            emailField.RegisterValueChangedCallback(evt => OnEmailValueChanged(emailField));
            passwordField.RegisterValueChangedCallback(evt => OnPasswordValueChanged(passwordField));
            rePasswordField.RegisterValueChangedCallback(evt => OnRePasswordValueChanged(rePasswordField));


        }

        private void RefreshItems(UserBundle obj)
        {
        }

        private async void OnContinueButtonClick(ClickEvent evt)
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

            try {
                var result = await Model.RegisterWithPassword(emailAddress, password);
                Debug.Log("RegisterWithPassword verification ID: " + result.Id);

                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

            } catch (Exception e) {
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
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            View.visible = false;
            View.Q<VisualElement>("Spinner").AddToClassList("hide");
            View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
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
    }
}
