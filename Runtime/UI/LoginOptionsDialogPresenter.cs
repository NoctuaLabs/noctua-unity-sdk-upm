using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class LoginOptionsDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();
        private Label _tnCLabel;
        private Label _privacyLabel;
        private Button _loginWithGoogleButton;
        private Button _loginWithFacebookButton;
        private Button _loginWithEmailButton;
        private Button _registerButton;
        private Button _backButton;
        private GlobalConfig _config;
        public void Show()
        {
            Visible = true;
        }

        protected override void Attach(){}
        protected override void Detach(){}

        private void Start()
        {
            _tnCLabel = View.Q<Label>("TnCLabel");
            _tnCLabel.RegisterCallback<PointerUpEvent>(_ => OnTnCClicked());

            _privacyLabel = View.Q<Label>("PrivacyLabel");
            _privacyLabel.RegisterCallback<PointerUpEvent>(_ => OnPrivacyClicked());
            
            _loginWithGoogleButton = View.Q<Button>("LoginWithGoogleButton");
            _loginWithGoogleButton.RegisterCallback<PointerUpEvent>(_ => OnLoginWithGoogleButtonClicked());
            
            _loginWithFacebookButton = View.Q<Button>("LoginWithFacebookButton");
            _loginWithFacebookButton.RegisterCallback<PointerUpEvent>(_ => OnLoginWithFacebookButtonClicked());
            
            _loginWithEmailButton = View.Q<Button>("LoginWithEmailButton");
            _loginWithEmailButton.RegisterCallback<PointerUpEvent>(_ => OnLoginWithEmailButtonClicked());
            
            _registerButton = View.Q<Button>("RegisterButton");
            _registerButton.RegisterCallback<PointerUpEvent>(_ => OnRegisterButtonClicked());
            
            _backButton = View.Q<Button>("BackButton");
            _backButton.RegisterCallback<PointerUpEvent>(_ => OnBackButtonClicked());
        }
        
        public void SetWhitelabel(GlobalConfig config)
        {
            _config = config;
        }

        private void OnTnCClicked()
        {
            _log.Debug("clicking tnc");
            
            var tncUrl = string.IsNullOrEmpty(_config?.CoPublisher?.CompanyName) ? "https://noctua.gg/tou" : _config.CoPublisher.CompanyTermUrl;
            Application.OpenURL(tncUrl);
        }

        private void OnPrivacyClicked()
        {
            _log.Debug("clicking privacy");
            
            var privacyUrl = string.IsNullOrEmpty(_config?.CoPublisher?.CompanyName) ? "https://noctua.gg/privacy" : _config.CoPublisher.CompanyPrivacyUrl;
            Application.OpenURL(privacyUrl);
        }

        private void OnLoginWithFacebookButtonClicked()
        {
            _log.Debug("clicking login with facebook");
            
            Visible = false;
            StartCoroutine(SocialLogin("facebook").ToCoroutine());
        }

        private void OnLoginWithGoogleButtonClicked()
        {
            _log.Debug("clicking login with google");
            
            Visible = false;
            StartCoroutine(SocialLogin("google").ToCoroutine());
        }

        private async UniTask SocialLogin(string provider)
        {
            try
            {
                await Model.SocialLoginAsync(provider);
            }
            catch (Exception e)
            {
                Model.ShowGeneralNotification(e.Message);
            }
        }

        private void OnLoginWithEmailButtonClicked()
        {
            _log.Debug("clicking login with email");
            
            Visible = false;

            Model.PushNavigation(() => Model.ShowLoginOptions());
            Model.ShowEmailLogin();
        }
        
        private void OnRegisterButtonClicked()
        {
            _log.Debug("clicking register with email");
            
            Visible = false;
            
            Model.PushNavigation(() => Model.ShowLoginOptions());
            Model.ShowEmailRegistration(true);
        }

        private void OnBackButtonClicked()
        {
            _log.Debug("clicking back button");
            
            Visible = false;

            Model.NavigateBack();
        }
    }
}