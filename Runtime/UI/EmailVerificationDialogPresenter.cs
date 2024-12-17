using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class EmailVerificationDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();

        private string _email;
        private string _password;
        private int _credVerifyId;
        private string _credVerifyCode;

        private VisualElement panelVE;
        private TextField verificationCode;

        protected override void Attach() { }
        protected override void Detach() { }

        public void Show(string email, string password, int verificationId)
        {
            Debug.Log("EmailVerificationDialogPresenter.Show()");
            View.visible = true;

            _email = email;
            _password = password;
            _credVerifyId = verificationId;

            SetupView();
            HideAllErrors();
        }

        private void Start()
        {
            SetupView();
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

        private void SetupView()
        {
            panelVE = View.Q<VisualElement>("EmailVerificationDialog");
            verificationCode = View.Q<TextField>("VerificationCode");
            verificationCode.value = string.Empty;
            verificationCode.RegisterValueChangedCallback(evt => OnVerificationCodeValueChanged(verificationCode));

            verificationCode.RegisterCallback<FocusInEvent>(OnTextFieldFocusChange);            
            verificationCode.RegisterCallback<FocusOutEvent>(OnTextFieldFocusChange);            

            var backButton = View.Q<Button>("BackButton");
            var resendButton = View.Q<Label>("ResendCode");
            var verifyButton = View.Q<Button>("VerifyButton");

            resendButton.RegisterCallback<ClickEvent>(OnResendButtonClick);
            backButton.RegisterCallback<ClickEvent>(OnBackButtonClick);
            verifyButton.RegisterCallback<ClickEvent>(OnVerifyButtonClick);
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

        private void OnVerificationCodeValueChanged(TextField textField)
        {
            HideAllErrors();
            
            _credVerifyCode = textField.value;
            AdjustHideLabelElement(textField);
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

        private void OnBackButtonClick(ClickEvent evt)
        {
            Visible = false;
            Model.ShowEmailRegistration(false);
        }

        private async void OnResendButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking resend button");

            if (View.Q<VisualElement>("Spinner").childCount == 0)
            {                
                View.Q<VisualElement>("Spinner").Add(new Spinner(30, 30));
            }
            
            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");
            View.Q<Label>("ResendingCode").RemoveFromClassList("hide");

            View?.Q<Label>("ResendCode")?.AddToClassList("hide");
            View?.Q<Button>("VerifyButton")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogContent")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogHeader")?.AddToClassList("hide");
            try
            {
                var result = await Model.RegisterWithEmailAsync(_email, _password);
                _log.Debug("RegisterWithPassword verification ID: " + result.Id);

                _credVerifyId = result.Id;

                View?.Q<VisualElement>("Spinner")?.AddToClassList("hide");
                View.Q<Label>("ResendingCode").AddToClassList("hide");

                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<Button>("VerifyButton")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");

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

                View?.Q<VisualElement>("Spinner")?.AddToClassList("hide");
                View.Q<Label>("ResendingCode").AddToClassList("hide");

                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<Button>("VerifyButton")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
            }

        }

        private async void OnVerifyButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking verify button");

            var spinnerInstance = new Spinner();
            View.Q<VisualElement>("Spinner").Clear();
            View.Q<VisualElement>("Spinner").Add(spinnerInstance);
            View.Q<VisualElement>("Spinner").RemoveFromClassList("hide");
            View.Q<Label>("VerifyingCode").RemoveFromClassList("hide");

            View?.Q<Label>("ResendCode")?.AddToClassList("hide");
            View?.Q<Button>("VerifyButton")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogContent")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogHeader")?.AddToClassList("hide");
            try
            {
                if (Model.AuthService.RecentAccount.IsGuest)
                {
                    var token = await Model.AuthService.BeginVerifyEmailRegistrationAsync(_credVerifyId, _credVerifyCode);
                    Model.ShowBindConfirmation(token);
                }
                else
                {
                    await Model.VerifyEmailRegistration(_credVerifyId, _credVerifyCode);
                }

                Visible = false;

                View?.Q<VisualElement>("Spinner")?.AddToClassList("hide");
                View.Q<Label>("VerifyingCode").AddToClassList("hide");

                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<Button>("VerifyButton")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");

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

                View?.Q<VisualElement>("Spinner")?.AddToClassList("hide");
                View.Q<Label>("VerifyingCode").AddToClassList("hide");

                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");
                View?.Q<Button>("VerifyButton")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
            }
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
