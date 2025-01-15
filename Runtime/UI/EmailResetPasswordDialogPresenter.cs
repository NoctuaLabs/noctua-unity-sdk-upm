using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using com.noctuagames.sdk.UI;
using com.noctuagames.sdk.Events;

namespace com.noctuagames.sdk.UI
{
    internal class EmailResetPasswordDialogPresenter : Presenter<AuthenticationModel>
    {
        public EventSender EventSender;

        private readonly ILogger _log = new NoctuaLogger();
        
        private List<TextField> _textFields;
        private ButtonNoctua _submitButton;

        private InputFieldNoctua _inputEmail;

        public void Show(bool clearForm)
        {
            Visible = true;

            Setup();

            if (clearForm)
            {
                _inputEmail.Clear();
            }

            EventSender?.Send("forgot_password_opened");
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
          
            _inputEmail = new InputFieldNoctua(View.Q<TextField>("EmailTF"));
            _submitButton = new ButtonNoctua(View.Q<Button>("ContinueButton"));

            _inputEmail.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputEmail));
            _inputEmail.SetFocus();
            
            _textFields = new List<TextField>
            {
                _inputEmail.textField
            };

            Utility.UpdateButtonState(_textFields, _submitButton.button);

            _submitButton.button.RegisterCallback<PointerUpEvent>(OnContinueButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<ClickEvent>(OnBackButtonClick);
        }

        private void OnValueChanged(InputFieldNoctua input)
        {
            input.AdjustLabel();
            Utility.UpdateButtonState(_textFields, _submitButton.button);
        }

        private async void OnContinueButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking continue button");

            HideAllErrors();

            _submitButton.ToggleLoading(true);

            var emailAddress = _inputEmail.text.Replace(" ", string.Empty);

            // Validation
            if (!string.IsNullOrEmpty(Utility.ValidateEmail(emailAddress)))
            {
                _submitButton.ToggleLoading(false);

                _inputEmail.Error(Utility.ValidateEmail(emailAddress));
                return;
            }

            try
            {

                var credentialVerification = await Model.AuthService.RequestResetPasswordAsync(emailAddress);

                EventSender?.Send("forgot_password_success");

                Model.ShowEmailConfirmResetPassword(credentialVerification.Id);

                Visible = false;

                _inputEmail.Clear();
                _submitButton.Clear();

            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    _submitButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);                    
                }
                else
                {
                    _submitButton.Error(e.Message);                    
                }

                _submitButton.ToggleLoading(false);
            }
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking back button");

            Visible = false;

            Model.ShowEmailLogin();
        }

        private void HideAllErrors()
        {            
            _inputEmail.Reset();
            _submitButton.Clear();
        }
    }
}
