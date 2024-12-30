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
        
        private List<TextField> textFields;
        private ButtonNoctua submitButton;

        private InputFieldNoctua inputEmail;

        public void Show(bool clearForm)
        {
            Visible = true;

            Setup();

            if (clearForm)
            {
                inputEmail.Clear();
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
          
            inputEmail = new InputFieldNoctua(View.Q<TextField>("EmailTF"));
            submitButton = new ButtonNoctua(View.Q<Button>("ContinueButton"));

            inputEmail.textField.RegisterValueChangedCallback(evt => OnValueChanged(inputEmail));
            inputEmail.SetFocus();
            
            textFields = new List<TextField>
            {
                inputEmail.textField
            };

            Utility.UpdateButtonState(textFields, submitButton.button);

            submitButton.button.RegisterCallback<ClickEvent>(OnContinueButtonClick);
            View.Q<Button>("BackButton").RegisterCallback<ClickEvent>(OnBackButtonClick);
        }

        private void OnValueChanged(InputFieldNoctua _input)
        {
            _input.AdjustLabel();
            Utility.UpdateButtonState(textFields, submitButton.button);
        }

        private async void OnContinueButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking continue button");

            HideAllErrors();

            submitButton.ToggleLoading(true);

            var emailAddress = inputEmail.text.Replace(" ", string.Empty);

            // Validation
            if (!string.IsNullOrEmpty(Utility.ValidateEmail(emailAddress)))
            {
                submitButton.ToggleLoading(false);

                inputEmail.Error(Utility.ValidateEmail(emailAddress));
                return;
            }

            try
            {

                var credentialVerification = await Model.AuthService.RequestResetPasswordAsync(emailAddress);

                EventSender?.Send("forgot_password_success");

                Model.ShowEmailConfirmResetPassword(credentialVerification.Id);

                Visible = false;

                inputEmail.Clear();
                submitButton.Clear();

            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    submitButton.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);                    
                }
                else
                {
                    submitButton.Error(e.Message);                    
                }

                submitButton.ToggleLoading(false);
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
            //Normalize border
            inputEmail.Reset();
            submitButton.Clear();
        }
    }
}
