using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.UI;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;
using com.noctuagames.sdk.Events;

namespace com.noctuagames.sdk.UI
{
    internal class EmailResetPasswordDialogPresenter : Presenter<AuthenticationModel>
    {
        public EventSender EventSender;

        private readonly ILogger _log = new NoctuaLogger();
        private string _email;
        private List<TextField> textFields;
        private Button submitButton;

        private TextField emailField;
        private VisualElement panelVE;

        public void Show(bool clearForm)
        {
            Visible = true;

            Setup();

            if (clearForm)
            {
                View.Q<TextField>("EmailTF").value = "";
            }

            EventSender?.Send("forgot_password_opened");
        }

        protected override void Attach() { }
        protected override void Detach() { }

        private void Start()
        {
            Setup();
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

        private void Setup()
        {
            panelVE = View.Q<VisualElement>("Panel");
            emailField = View.Q<TextField>("EmailTF");
            submitButton = View.Q<Button>("ContinueButton");

            emailField.RegisterValueChangedCallback(evt => OnEmailValueChanged(emailField));
            emailField.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);            
            emailField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);            

            textFields = new List<TextField>
            {
                emailField
            };

            Utility.UpdateButtonState(textFields, submitButton);

            View.Q<Button>("ContinueButton").RegisterCallback<PointerUpEvent>(OnContinueButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<PointerUpEvent>(OnBackButtonClick);
        }

        public void OnTextFieldFocusChange(FocusInEvent _event)
        {
            (_event.target as VisualElement).Children().ElementAt(1).AddToClassList("noctua-text-input-focus");
            (_event.target as VisualElement).Q<VisualElement>("title").style.color = Color.white;
        }

        public void OnTextFieldFocusChange(FocusOutEvent _event)
        {
            (_event.target as VisualElement).Children().ElementAt(1).RemoveFromClassList("noctua-text-input-focus");
            (_event.target as VisualElement).Q<VisualElement>("title").style.color = new Color(0.4862745f, 0.4941176f, 0.5058824f, 1.0f);
        }

        private void OnEmailValueChanged(TextField textField)
        {
            HideAllErrors();
          
            _email = textField.value;

            AdjustHideLabelElement(textField);
            Utility.UpdateButtonState(textFields, submitButton);
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

            var emailAddress = _email.Replace(" ", string.Empty);

            // Validation
            if (string.IsNullOrEmpty(emailAddress))
            {
                View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }

            if (!IsValidEmail(emailAddress))
            {
                View.Q<Label>("ErrEmailInvalid").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
                return;
            }

            try
            {

                var credentialVerification = await Model.AuthService.RequestResetPasswordAsync(emailAddress);

                EventSender?.Send("forgot_password_success");

                Model.ShowEmailConfirmResetPassword(credentialVerification.Id);

                Visible = false;

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");

            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    View.Q<Label>("ErrCode").text = noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message;
                }
                else
                {
                    View.Q<Label>("ErrCode").text = e.Message;
                }

                View.Q<Label>("ErrCode").RemoveFromClassList("hide");
                View.Q<Button>("ContinueButton").RemoveFromClassList("hide");
                View.Q<VisualElement>("Spinner").AddToClassList("hide");
            }
        }

        private void OnBackButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking back button");

            Visible = false;

            Model.ShowEmailLogin();
        }

        private void HideAllErrors()
        {
            // To avoid duplicate classes
            View.Q<Label>("ErrCode").RemoveFromClassList("hide");
            View.Q<Label>("ErrEmailInvalid").RemoveFromClassList("hide");
            View.Q<Label>("ErrEmailEmpty").RemoveFromClassList("hide");

            View.Q<Label>("ErrCode").AddToClassList("hide");
            View.Q<Label>("ErrEmailInvalid").AddToClassList("hide");
            View.Q<Label>("ErrEmailEmpty").AddToClassList("hide");
        }
    }
}
