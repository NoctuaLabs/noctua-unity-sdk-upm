using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using com.noctuagames.sdk.Events;
using com.noctuagames.sdk.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    /*
    We were using Model-View-Presenter, further reading:
    - https://en.wikipedia.org/wiki/Model%E2%80%93view%E2%80%93presenter
    - https://medium.com/cr8resume/make-you-hand-dirty-with-mvp-model-view-presenter-eab5b5c16e42
    - https://www.baeldung.com/mvc-vs-mvp-pattern

    But in our case, we have unique conditions:
    1. Our model (*Services.cs) is the public facing API
    2. Our public facing API need to cover both UI and non-UI stuff.

    Thus, we have to tweak the pattern to API-Model-View-Presenter
    1. UI:
    1. Presenter: where we control the state of the UI, but not the main logics
    2. Model: main logics + public facing API
    3. API: the actual model, where we talk to either HTTP API or local storage like player prefs. (TODO)

    AuthenticationModel purposes:
    1. To allow our SDK instance (including UI) to be injected into the Scene
    3. To allow an UI presenter call another UI presenter
    2. To allow model layer (logic) to call an UI presenter
    */

    internal class AuthenticationModel
    {
        // IMPORTANT NOTES!!!
        // Your UI need to apply USS absolute property to the first VisualElement of the UI
        // before being added to the UI Document.
        // Violation of this rule will cause the UI (and the other UI too) to be unable to be displayed properly.
        private readonly UIFactory _uiFactory;
        
        private readonly AccountSelectionDialogPresenter _accountSelectionDialog;
        private readonly SwitchAccountConfirmationDialogPresenter _switchAccountConfirmationDialog;
        private readonly LoginOptionsDialogPresenter _loginOptionsDialog;
        private readonly EmailLoginDialogPresenter _emailLoginDialog;
        private readonly EmailRegisterDialogPresenter _emailRegisterDialog;
        private readonly EmailVerificationDialogPresenter _emailVerificationDialog;
        private readonly WelcomeNotificationPresenter _welcome;
        private readonly EmailResetPasswordDialogPresenter _emailResetPasswordDialog;
        private readonly EmailConfirmResetPasswordDialogPresenter _emailConfirmResetPasswordDialog;
        private readonly UserCenterPresenter _userCenter;
        private readonly AccountDeletionConfirmationDialogPresenter _accountDeletionConfirmationDialog;

        private NoctuaAuthenticationService _authService;
        private GameObject _socialAuthObject;
        private SocialAuthenticationService _socialAuth;
        
        private readonly Stack<Action> _navigationStack = new();

        public NoctuaAuthenticationService AuthService => _authService;

        internal event Action<UserBundle> OnAccountChanged;

        private AuthType _currentAuthType = AuthType.SwitchOrBindAccount;

        internal AuthenticationModel(
            UIFactory uiFactory, 
            NoctuaAuthenticationService authService, 
            GlobalConfig config,
            EventSender eventSender = null
        )
        {
            _uiFactory = uiFactory;
            
            _userCenter = _uiFactory.Create<UserCenterPresenter, AuthenticationModel>(this);
            _userCenter.EventSender = eventSender;
            _accountSelectionDialog = _uiFactory.Create<AccountSelectionDialogPresenter, AuthenticationModel>(this);
            _switchAccountConfirmationDialog = _uiFactory.Create<SwitchAccountConfirmationDialogPresenter, AuthenticationModel>(this);
            _loginOptionsDialog = _uiFactory.Create<LoginOptionsDialogPresenter, AuthenticationModel>(this);
            _emailLoginDialog = _uiFactory.Create<EmailLoginDialogPresenter, AuthenticationModel>(this);
            _emailVerificationDialog = _uiFactory.Create<EmailVerificationDialogPresenter, AuthenticationModel>(this);
            _emailRegisterDialog = _uiFactory.Create<EmailRegisterDialogPresenter, AuthenticationModel>(this);
            _emailResetPasswordDialog = _uiFactory.Create<EmailResetPasswordDialogPresenter, AuthenticationModel>(this);
            _emailResetPasswordDialog.EventSender = eventSender;
            _emailConfirmResetPasswordDialog = _uiFactory.Create<EmailConfirmResetPasswordDialogPresenter, AuthenticationModel>(this);
            _emailConfirmResetPasswordDialog.EventSender = eventSender;
            _accountDeletionConfirmationDialog = _uiFactory.Create<AccountDeletionConfirmationDialogPresenter, AuthenticationModel>(this);
            _welcome = _uiFactory.Create<WelcomeNotificationPresenter, AuthenticationModel>(this);

            _welcome.SetBehaviourWhitelabel(config);
            _emailLoginDialog.SetBehaviourWhitelabel(config);
            _emailRegisterDialog.SetBehaviourWhitelabel(config);
            _accountSelectionDialog.SetWhitelabel(config);
            _loginOptionsDialog.SetWhitelabel(config);
            _userCenter.SetWhitelabel(config);
            
            _authService = authService;
            _authService.OnAccountChanged += OnAccountChanged;
            _socialAuth = new SocialAuthenticationService(_authService, config);
        }
        
        internal void PushNavigation(Action action)
        {
            if (action == null) return;
            
            _navigationStack.Push(action);
        }
        
        internal void ClearNavigation()
        {
            _navigationStack.Clear();
        }
        
        internal void NavigateBack()
        {
            if (_navigationStack.Count > 0)
            {
                _navigationStack.Pop().Invoke();
            }
        }

        public void ShowAccountSelection()
        {
            _currentAuthType = AuthType.SwitchOrBindAccount;
            _accountSelectionDialog.Show();
        }

        public void ShowEmailRegistration(bool clearForm)
        {
            _emailRegisterDialog.Show(clearForm);
        }

        public void ShowEmailVerification(string email, string password, int verificationID)
        {
            _emailVerificationDialog.Show(email, password, verificationID);
        }

        public void ShowLoginOptions()
        {
            _loginOptionsDialog.Show();
        }

        public void ShowEmailLogin(Action<UserBundle> onLoginSuccess = null)
        {
            _emailLoginDialog.Show(onLoginSuccess);
        }

        public void ShowEmailResetPassword(bool clearForm)
        {
            _emailResetPasswordDialog.Show(clearForm);
        }

        public void ShowEmailConfirmResetPassword(int credVerifyId)
        {
            _emailConfirmResetPasswordDialog.Show(credVerifyId);
        }

        public void ShowSwitchAccountConfirmation(UserBundle recentAccount)
        {
            _switchAccountConfirmationDialog.Show(recentAccount);
        }

        public void ShowAccountDeletionConfirmation(UserBundle recentAccount)
        {
            _accountDeletionConfirmationDialog.Show(recentAccount);
        }
        
        public void ShowUserCenter()
        {
            _currentAuthType = _authService?.RecentAccount?.IsGuest == true ? AuthType.SwitchOrBindAccount : AuthType.LinkAccount;

            _userCenter.Show();
        }

        public void ShowGeneralNotification(string message, bool isNotifSuccess = false) 
        {
            _uiFactory.ShowGeneralNotification(message, isNotifSuccess);
        }

        public void ShowLoadingProgress(bool isShow)
        {
            _uiFactory.ShowLoadingProgress(isShow);
        }

        internal async UniTask<CredentialVerification> RegisterWithEmailAsync(string email, string password, Dictionary<string, string> regExtra = null)
        {
            return _currentAuthType switch
            {
                AuthType.SwitchOrBindAccount => await AuthService.RegisterWithEmailAsync(email, password, regExtra),
                AuthType.LinkAccount => await AuthService.LinkWithEmailAsync(email, password),
                _ => throw new NotImplementedException(_currentAuthType.ToString())
            };
        }

        internal async UniTask VerifyEmailRegistration(int credVerifyId, string code)
        {
            switch (_currentAuthType)
            {
                case AuthType.SwitchOrBindAccount:
                    await AuthService.VerifyEmailRegistrationAsync(credVerifyId, code);

                    break;

                case AuthType.LinkAccount:
                    await AuthService.VerifyEmailLinkingAsync(credVerifyId, code);

                    break;

                default: 
                    throw new NotImplementedException(_currentAuthType.ToString());
            }
        }

        public async UniTask<UserBundle> SocialLoginAsync(string provider)
        {
            return await _socialAuth.SocialLoginAsync(provider);
        }

        public async UniTask<Credential> SocialLinkAsync(string provider)
        {
            return await _socialAuth.SocialLinkAsync(provider);
        }
    }
    
    internal enum AuthType
    {
        SwitchOrBindAccount,
        LinkAccount,
    }
}