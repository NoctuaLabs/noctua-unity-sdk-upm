﻿using System;
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

        private readonly Config _config;
        private readonly PanelSettings _panelSettings;
        private readonly UIFactory _uiFactory;
        private readonly AuthenticationModel _uiModel;
        private readonly NoctuaAuthenticationService _service;
        private OauthRedirectListener _oauthOauthRedirectListener;

        internal NoctuaAuthentication(
            NoctuaAuthenticationService service, 
            UIFactory uiFactory, 
            GlobalConfig config,
            EventSender eventSender = null
        )
        {
            _service = service;
                        
            _uiFactory = uiFactory;
            _uiModel = new AuthenticationModel(_uiFactory, _service, config, eventSender);
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
            return await _service.AuthenticateAsync();
        }

        public async UniTask<UserBundle> LoginAsGuest()
        {
            return await _service.LoginAsGuestAsync();
        }

        public void ResetAccounts() {
            _service.ResetAccounts();
        }

        // TODO: Add support for phone
        public async UniTask<CredentialVerification> RegisterWithEmailAsync(string email, string password, Dictionary<string, string> regExtra = null)
        {
            return await _service.RegisterWithEmailAsync(email, password, regExtra);
        }

        public async UniTask<UserBundle> VerifyEmailRegistrationAsync(int id, string code)
        {
            return await _service.VerifyEmailRegistrationAsync(id, code);
        }

        // TODO: Add support for phone
        public async UniTask<CredentialVerification> LinkWithEmailAsync(string email, string password)
        {
            return await _service.LinkWithEmailAsync(email, password);
        }
        
        public async UniTask<Credential> VerifyEmailLinkingAsync(int id, string code)
        {
            return await _service.VerifyEmailLinkingAsync(id, code);
        }

        // TODO: Add support for phone
        public async UniTask<UserBundle> LoginWithEmailAsync(string email, string password)
        {
            return await _service.LoginWithEmailAsync(email, password);
        }

        // TODO: Add support for phone
        public async UniTask<CredentialVerification> RequestResetPasswordAsync(string email)
        {
            return await _service.RequestResetPasswordAsync(email);
        }

        // TODO: Add support for phone
        public async UniTask<UserBundle> ConfirmResetPasswordAsync(int id, string code, string newPassword)
        {
            return await _service.ConfirmResetPasswordAsync(id, code, newPassword);
        }

        public void SwitchAccount(UserBundle user)
        {
            UniTask.Void(async () => await _service.SwitchAccountAsync(user));
        }
        
        public async UniTask<UserBundle> ExchangeToken(string accessToken)
        {
            return await _service.ExchangeTokenAsync(accessToken);
        }

        public async UniTask<string> GetSocialLoginRedirectURL(string provider)
        {
            return await _service.GetSocialAuthRedirectURLAsync(provider);
        }

        public async UniTask<UserBundle> SocialLoginAsync(string provider, SocialLoginRequest payload)
        {
            return await _service.SocialLoginAsync(provider, payload);
        }

        public async UniTask<Credential> SocialLinkAsync(string provider, SocialLinkRequest payload)
        {
            return await _service.SocialLinkAsync(provider, payload);
        }

        public async UniTask<PlayerToken> Bind(BindRequest payload)
        {
            return await _service.Bind(payload);
        }

        public async UniTask<UserBundle> SocialLoginAsync(string provider)
        {
            return await _uiModel.SocialLoginAsync(provider);
        }

        public async UniTask UpdatePlayerAccountAsync(PlayerAccountData playerAccountData)
        {
            await _service.UpdatePlayerAccountAsync(playerAccountData);
        }
        
        public async UniTask<UserBundle> LogoutAsync()
        {
            return await _service.LogoutAsync();
        }

        public void ShowUserCenter()
        {
            _uiModel.ShowUserCenter();
        }

        /// <summary>
        /// Displays the account selection user interface.
        /// </summary>
        public void SwitchAccount()
        {
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
