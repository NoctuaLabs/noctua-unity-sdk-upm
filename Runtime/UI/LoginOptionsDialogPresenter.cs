using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.UI
{
    internal class LoginOptionsDialogPresenter : Presenter<AuthenticationModel>
    {
        private readonly ILogger _log = new NoctuaLogger();

        // Flags
        private bool _ssoDisabled = false;

        private Label _tnCLabel;
        private Label _privacyLabel;
        private VisualElement _socialAccountContainer;
        private Button _loginWithEmailButton;
        private Button _registerButton;
        private Button _backButton;
        private GlobalConfig _config;

        private readonly List<SocialLoginMethod> _socialLoginMethods = new() {
            new SocialLoginMethod
            {
                Provider = "google",
                Title = "Google",
                LogoClass = "google-logo",
            },
            new SocialLoginMethod
            {
                Provider = "facebook",
                Title = "Facebook",
                LogoClass = "facebook-logo"
            },
#if UNITY_IOS
            new SocialLoginMethod
            {
                Provider = "apple",
                Title = "Apple",
                LogoClass = "apple-logo"
            },
#endif
        };

        public void Show()
        {
            if (_ssoDisabled)
            {
                Model.ShowEmailLogin(null);
            } else {
                Visible = true;
            }
        }

        protected override void Attach(){}
        protected override void Detach(){}

        private void Start()
        {
            _tnCLabel = View.Q<Label>("TnCLabel");
            _tnCLabel.RegisterCallback<PointerUpEvent>(_ => OnTnCClicked());

            _privacyLabel = View.Q<Label>("PrivacyLabel");
            _privacyLabel.RegisterCallback<PointerUpEvent>(_ => OnPrivacyClicked());

            _socialAccountContainer = View.Q<VisualElement>("SocialAccountContainer");

            foreach (var loginMethod in _socialLoginMethods)
            {
                var button = new Button();
                button.AddToClassList("social-account-button");

                var logo = new VisualElement();
                logo.AddToClassList("login-method-logo");
                logo.AddToClassList(loginMethod.LogoClass);

                var title = new Label();
                title.text = loginMethod.Title;
                title.AddToClassList("social-account-label");

                button.Add(logo);
                button.Add(title);
                button.RegisterCallback<PointerUpEvent>(_ => OnSocialLoginButtonClicked(loginMethod.Provider));

                _socialAccountContainer.Add(button);
            }
                        
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

        public void SetFlag(Dictionary<string, bool> featureFlags)
        {
            if (featureFlags.ContainsKey("ssoDisabled"))
            {
                _ssoDisabled = featureFlags["ssoDisabled"];
            }
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

        private void OnSocialLoginButtonClicked(string provider)
        {
            _log.Debug($"clicking login with {provider}");

            Visible = false;
            StartCoroutine(SocialLogin(provider).ToCoroutine());
        }

        private async UniTask SocialLogin(string provider)
        {
            try
            {
                if (Model.AuthService.RecentAccount?.IsGuest ?? false)
                {
                    var playerToken = await Model.GetSocialLoginTokenAsync(provider);
                    
                    if (playerToken.Player == null)
                    {
                        Model.ShowBindConfirmation(playerToken);
                    }
                    else
                    {
                        Model.ShowBindConflictDialog(playerToken);
                    }
                }
                else
                {
                    await Model.SocialLoginAsync(provider);
                }
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

        private class SocialLoginMethod
        {
            public string Provider;
            public string Title;
            public string LogoClass;
        }
    }
}