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
        private Dictionary<string,string> _extraData;
        private string _credVerifyCode;

        private InputFieldNoctua _inputVerificationCode;
        private ButtonNoctua _buttonVerify;

        protected override void Attach() { }
        protected override void Detach() { }

        public void Show(string email, string password, int verificationId, Dictionary<string, string> extraData)
        {
            Debug.Log("EmailVerificationDialogPresenter.Show()");
            View.visible = true;

            _email = email;
            _password = password;
            _credVerifyId = verificationId;
            _extraData = extraData;

            SetupView();
            HideAllErrors();
        }

        private void Start()
        {
            SetupView();
            HideAllErrors();
        }

        private void SetupView()
        {
            panelVE = View.Q<VisualElement>("EmailVerificationDialog");

            _inputVerificationCode = new InputFieldNoctua(View.Q<TextField>("VerificationCode"));
            
            _inputVerificationCode.textField.RegisterValueChangedCallback(evt => OnValueChanged(_inputVerificationCode));
            _inputVerificationCode.SetFocus();

            var backButton = View.Q<Button>("BackButton");
            var resendButton = View.Q<Label>("ResendCode");
            _buttonVerify = new ButtonNoctua(View.Q<Button>("VerifyButton"));

            resendButton.RegisterCallback<ClickEvent>(OnResendButtonClick);
            backButton.RegisterCallback<ClickEvent>(OnBackButtonClick);
            _buttonVerify.button.RegisterCallback<PointerUpEvent>(OnVerifyButtonClick);
        }

        private void OnValueChanged(InputFieldNoctua input)
        {
            input.AdjustLabel();
            HideAllErrors();
        }

        private void OnBackButtonClick(ClickEvent evt)
        {
            Visible = false;
            Model.ShowEmailRegistration(false);
        }

        private async void OnResendButtonClick(ClickEvent evt)
        {
            _log.Debug("clicking resend button");

            _buttonVerify.ToggleLoading(true);
                        
            View.Q<Label>("ResendingCode").RemoveFromClassList("hide");

            View?.Q<Label>("ResendCode")?.AddToClassList("hide");            
            View?.Q<VisualElement>("DialogContent")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogHeader")?.AddToClassList("hide");
            
            try
            {
                CredentialVerification result;
                
                switch (Model.AuthIntention)
                {
                    case AuthIntention.None:
                    case AuthIntention.Switch:
                        result = await Model.AuthService.RegisterWithEmailAsync(_email, _password, _extraData);
                        break;
                    case AuthIntention.Link:
                        result = await Model.AuthService.LinkWithEmailAsync(_email, _password);
                        break;
                    default:
                        throw new NoctuaException(NoctuaErrorCode.Authentication, $"Invalid AuthIntention {Model.AuthIntention}");
                }

                _log.Debug("RegisterWithPassword verification ID: " + result.Id);

                _credVerifyId = result.Id;

                _buttonVerify.ToggleLoading(false);

                View.Q<Label>("ResendingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");                
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");

            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    _buttonVerify.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);                    
                }
                else
                {
                    _buttonVerify.Error(e.Message);                    
                }

                _buttonVerify.ToggleLoading(false);

                View.Q<Label>("ResendingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");                
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
            }

        }

        private async void OnVerifyButtonClick(PointerUpEvent evt)
        {
            _log.Debug("clicking verify button");

            _buttonVerify.ToggleLoading(true);

            View.Q<Label>("VerifyingCode").RemoveFromClassList("hide");
            View?.Q<Label>("ResendCode")?.AddToClassList("hide");            
            View?.Q<VisualElement>("DialogContent")?.AddToClassList("hide");
            View?.Q<VisualElement>("DialogHeader")?.AddToClassList("hide");
            
            var verificationCode = _inputVerificationCode.textField.value;

            try
            {
                switch (Model.AuthIntention)
                {
                    case AuthIntention.Switch when Model.AuthService.RecentAccount is { IsGuest: true }:
                        var token = await Model.AuthService.BeginVerifyEmailRegistrationAsync(_credVerifyId, verificationCode);
                    
                        Model.ShowBindConfirmation(token);

                        break;
                    case AuthIntention.Switch when Model.AuthService.RecentAccount is null or { IsGuest: false }:
                        await Model.AuthService.VerifyEmailRegistrationAsync(_credVerifyId, verificationCode);
                        
                        break;
                    case AuthIntention.Link when Model.AuthService.RecentAccount is { IsGuest: false }:
                        await Model.AuthService.VerifyEmailLinkingAsync(_credVerifyId, verificationCode);

                        Model.ShowInfo($"Successfully linked email to {Model.AuthService.RecentAccount.DisplayName}");

                        break;

                    default:
                        throw new NoctuaException(NoctuaErrorCode.Authentication, $"Invalid AuthIntention {Model.AuthIntention}");
                }

                Visible = false;

                _buttonVerify.Clear();

                View.Q<Label>("VerifyingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");                
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");

            }
            catch (Exception e)
            {
                _log.Warning($"{e.Message}\n{e.StackTrace}");

                if (e is NoctuaException noctuaEx)
                {
                    _buttonVerify.Error(noctuaEx.ErrorCode.ToString() + " : " + noctuaEx.Message);                    
                }
                else
                {
                    _buttonVerify.Error(e.Message);                    
                }
                
                _buttonVerify.ToggleLoading(false);

                View.Q<Label>("VerifyingCode").AddToClassList("hide");
                View?.Q<Label>("ResendCode")?.RemoveFromClassList("hide");                
                View?.Q<VisualElement>("DialogContent")?.RemoveFromClassList("hide");
                View?.Q<VisualElement>("DialogHeader")?.RemoveFromClassList("hide");
            }
        }

        private void HideAllErrors()
        {            
            _inputVerificationCode.Reset();
            _buttonVerify.Clear();         
        }
    }
}
