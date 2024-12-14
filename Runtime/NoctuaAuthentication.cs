using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Application = UnityEngine.Device.Application;
using SystemInfo = UnityEngine.Device.SystemInfo;
using Newtonsoft.Json;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using com.noctuagames.sdk.Events;
using com.noctuagames.sdk.UI;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    

    public class NoctuaAuthentication
    {
        public readonly List<string> SsoCloseWebViewKeywords = new() { "https://developers.google.com/identity/protocols/oauth2" };

        // AccountList will be synced data from AccountContainer.Accounts
        public IReadOnlyList<UserBundle> AccountList => _service.AccountList;

        public bool IsAuthenticated => _service.IsAuthenticated;

        public UserBundle RecentAccount => _service.RecentAccount;

        public event Action<UserBundle> OnAccountChanged
        {
            add => _service.OnAccountChanged += value;
            remove => _service.OnAccountChanged -= value;
        }
        
        public event Action<Player> OnAccountDeleted
        {
            add => _service.OnAccountDeleted += value;
            remove => _service.OnAccountDeleted -= value;
        }

        private readonly ILogger _log = new NoctuaLogger();
        private bool _enabled;
        private readonly Config _config;
        private readonly PanelSettings _panelSettings;
        private readonly UIFactory _uiFactory;
        private readonly AuthenticationModel _uiModel;
        private readonly NoctuaAuthenticationService _service;
        private readonly NoctuaIAPService _iapService;
        private OauthRedirectListener _oauthOauthRedirectListener;

        internal NoctuaAuthentication(
            NoctuaAuthenticationService service, 
            NoctuaIAPService iapService,
            UIFactory uiFactory, 
            GlobalConfig config,
            EventSender eventSender = null,
            NoctuaLocale locale = null
        )
        {
            _service = service;
            _iapService = iapService;
                        
            _uiFactory = uiFactory;
            _uiModel = new AuthenticationModel(_uiFactory, _service, _iapService, config, eventSender, locale);
        }

        public void Enable()
        {
            _log.Debug("calling API");
            
            _enabled = true;
        }
        
        private void EnsureEnabled()
        {
            if (_enabled) return;

            _log.Error("Noctua Authentication is not enabled due to initialization failure.");
            throw new NoctuaException(NoctuaErrorCode.Application, "Noctua Authentication is not enabled due to initialization failure.");
        }

        public UserBundle GetRecentAccount()
        {
            return RecentAccount;
        }

        public string GetAccessToken()
        {
            return RecentAccount?.Player?.AccessToken;
        }

        /// <summary>
        /// Authenticates the user and returns the user bundle.
        /// 
        /// If the user bundle is not detected, it will show the account selection UI.
        /// 
        /// After authentication, it will show a welcome toast for the user.
        /// 
        /// Returns the authenticated user bundle.
        /// </summary>
        /// <returns>A UserBundle object representing the selected account.</returns>
        public async UniTask<UserBundle> AuthenticateAsync()
        {
            EnsureEnabled();
            
            _log.Debug("calling API");
            
            try
            {
                return await _service.AuthenticateAsync();
            }
            catch (NoctuaException noctuaEx) when (noctuaEx.ErrorCode == (int)NoctuaErrorCode.UserBanned)
            {
                bool confirmed = await _uiFactory.ShowBannedConfirmationDialog();

                if (confirmed)
                {
                    throw;
                }

                throw new OperationCanceledException("Action canceled.");
            }
        }

        public async UniTask<UserBundle> LoginAsGuest()
        {
            EnsureEnabled();

            _log.Debug("calling API");
            
            return await _service.LoginAsGuestAsync();
        }

        public void ResetAccounts() {
            EnsureEnabled();

            _log.Debug("calling API");
            
            _service.ResetAccounts();
        }

        // TODO: Add support for phone
        public async UniTask<CredentialVerification> RegisterWithEmailAsync(string email, string password, Dictionary<string, string> regExtra = null)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.RegisterWithEmailAsync(email, password, regExtra);
        }

        public async UniTask<UserBundle> VerifyEmailRegistrationAsync(int id, string code)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.VerifyEmailRegistrationAsync(id, code);
        }

        // TODO: Add support for phone
        public async UniTask<CredentialVerification> LinkWithEmailAsync(string email, string password)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.LinkWithEmailAsync(email, password);
        }
        
        public async UniTask<Credential> VerifyEmailLinkingAsync(int id, string code)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.VerifyEmailLinkingAsync(id, code);
        }

        // TODO: Add support for phone
        public async UniTask<UserBundle> LoginWithEmailAsync(string email, string password)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.LoginWithEmailAsync(email, password);
        }

        // TODO: Add support for phone
        public async UniTask<CredentialVerification> RequestResetPasswordAsync(string email)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.RequestResetPasswordAsync(email);
        }

        // TODO: Add support for phone
        public async UniTask<UserBundle> ConfirmResetPasswordAsync(int id, string code, string newPassword)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.ConfirmResetPasswordAsync(id, code, newPassword);
        }

        public void SwitchAccount(UserBundle user)
        {
            EnsureEnabled();

            _log.Debug("calling API");
            
            UniTask.Void(async () => await _service.SwitchAccountAsync(user));
        }
        
        public async UniTask<UserBundle> ExchangeToken(string accessToken)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.ExchangeTokenAsync(accessToken);
        }

        public async UniTask<string> GetSocialLoginRedirectURL(string provider)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.GetSocialAuthRedirectURLAsync(provider);
        }

        public async UniTask<UserBundle> SocialLoginAsync(string provider, SocialLoginRequest payload)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.SocialLoginAsync(provider, payload);
        }

        public async UniTask<Credential> SocialLinkAsync(string provider, SocialLinkRequest payload)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.SocialLinkAsync(provider, payload);
        }

        public async UniTask<UserBundle> SocialLoginAsync(string provider)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _uiModel.SocialLoginAsync(provider);
        }

        public async UniTask UpdatePlayerAccountAsync(PlayerAccountData playerAccountData)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            await _service.UpdatePlayerAccountAsync(playerAccountData);
        }
        
        public async UniTask<UserBundle> LogoutAsync()
        {
            EnsureEnabled();

            _log.Debug("calling API");

            return await _service.LogoutAsync();
        }

        public void ShowUserCenter()
        {
            EnsureEnabled();

            _log.Debug("calling API");

            _uiModel.ShowUserCenter();
        }

        /// <summary>
        /// Displays the account selection user interface.
        /// </summary>
        public void SwitchAccount()
        {
            EnsureEnabled();

            _log.Debug("calling API");

            _uiModel.ShowAccountSelection();
        }

        // TODO not a public facing API, need to be removed
        public void ShowRegisterDialogUI()
        {
            _uiModel.ShowEmailRegistration(true);
        }

        // TODO not a public facing API, need to be removed
        public void ShowEmailVerificationDialogUI()
        {
            _uiModel.ShowEmailVerification("foo", "bar", 123);
        }
        
        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }
}
